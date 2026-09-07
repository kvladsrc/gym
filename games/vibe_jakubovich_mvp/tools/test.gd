extends SceneTree

const Rules = preload("res://scripts/round_rules.gd")
const Bot = preload("res://scripts/opponent.gd")
var checks := 0
var failures := 0


func _initialize() -> void:
	call_deferred("_run")


func expect(condition: bool, description: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("FAIL: " + description)


func sample(answer: String = "МОЛОКО"):
	var game = Rules.new()
	game.setup(
		{"answer": answer, "question": "Тест", "category": "Тест"},
		[
			{"name": "Вы", "human": true},
			{"name": "А", "human": false},
			{"name": "Б", "human": false}
		],
		42
	)
	return game


func _rules() -> void:
	var game = sample()
	expect(not game.guess("О"), "Letters require a scoring sector")
	expect(not game.spin(-1), "Negative sector rejected")
	game.spin(0)
	game.guess("о")
	expect(game.players[0].score == 300, "Three occurrences earn three stakes")
	expect(game.visible_word() == "·О·О·О", "All occurrences revealed")
	expect(game.current == 0 and game.phase == "spin", "Correct answer keeps turn")
	game.spin(0)
	expect(not game.guess("О") and game.used == "О", "Repeated letter cannot score")
	game.guess("Я")
	expect(game.current == 1 and game.streak == 0, "Missing letter passes turn")
	game.players[1].score = 600
	game.players[1].cash = 5000
	game.spin(3)
	expect(
		game.players[1].score == 0 and game.players[1].cash == 5000,
		"Bankruptcy preserves chest money"
	)
	expect(game.current == 2, "Bankruptcy passes turn")
	game.players[2].score = 900
	game.spin(6)
	expect(game.players[2].score == 900 and game.current == 0, "Zero preserves points")
	game = sample()
	game.players[0].score = 100
	game.spin(14)
	game.guess("О")
	expect(game.players[0].score == 400, "x2 with three occurrences multiplies by four")
	game = sample()
	game.spin(9)
	expect(not game.reveal(12), "Plus rejects invalid cell")
	game.reveal(1)
	expect(
		game.visible_word() == "·О·О·О" and game.players[0].score == 0,
		"Plus reveals all copies without awarding points"
	)
	game = sample()
	for letter in "МЛК":
		game.spin(0)
		game.guess(letter)
	expect(game.phase == "chest", "Three consecutive guesses offer two chests")
	game.chest(0)
	expect(game.phase == "spin" and game.streak == 0, "Chest resumes the round")
	game = sample()
	game.spin(12)
	game.prize(false)
	expect(game.phase == "letter" and game.stake == 1000, "Refused prize offers 1000")
	game = sample()
	game.spin(12)
	game.prize(true)
	expect(not game.players[0].active and game.current == 1, "Prize recipient leaves round")
	game = sample()
	game.solve("НЕВЕРНО")
	expect(not game.players[0].active, "Incorrect whole answer eliminates player")
	game.solve("НЕВЕРНО")
	game.solve("НЕВЕРНО")
	expect(game.phase == "over" and game.winner == -1, "All eliminated ends round")
	game = sample("ЁЛКА")
	game.solve("  елка ")
	expect(game.winner == 0, "Normalize case, whitespace and ё")
	game = sample("ДОМ")
	for letter in "ДОМ":
		game.spin(0)
		game.guess(letter)
	expect(game.phase == "chest" and game.winner == 0, "Last letter still grants chest")
	game.chest(1)
	expect(game.phase == "over", "Final chest resumes round victory")


func _simulations() -> void:
	var bank: Array = JSON.parse_string(FileAccess.get_file_as_string("res://data/questions.json"))
	var seen: Dictionary = {}
	for puzzle in bank:
		var answer: String = Rules.normalize(puzzle.answer)
		expect(answer.length() <= 16 and not seen.has(answer), "Unique word fits board")
		seen[answer] = true
		for letter in answer:
			expect(letter in Rules.ALPHABET, "Question uses supported alphabet")
	var rng := RandomNumberGenerator.new()
	rng.seed = 73
	for iteration in 100:
		var game = sample(bank[iteration % bank.size()].answer)
		var moves := 0
		while game.phase != "over" and moves < 3000:
			moves += 1
			match game.phase:
				"spin":
					game.spin(rng.randi_range(0, Rules.SECTORS.size() - 1))
				"letter":
					game.guess(Bot.letter(game.used, rng))
				"plus":
					game.reveal(Bot.position(game.visible_word(), rng))
				"prize":
					game.prize(false)
				"chest":
					game.chest(rng.randi_range(0, 1))
		expect(game.phase == "over" and game.winner >= 0, "AI round terminates")
		for player in game.players:
			expect(player.score >= 0, "Scores never become negative")


func click_text(parent: Node, text: String) -> bool:
	for child in parent.get_children():
		if child is Button and child.text == text and not child.disabled:
			child.pressed.emit()
			return true
		if click_text(child, text):
			return true
	return false


func check_labels(parent: Node) -> void:
	for child in parent.get_children():
		if child is Label and child.has_method("fit"):
			child.fit()
			var measured: Vector2 = child.measure(child.get_theme_font_size("font_size"))
			expect(
				measured.y <= child.bounds.y and measured.x <= child.bounds.x + 1,
				"Full text fits its sign: " + child.text.left(50)
			)
			var font_height: float = child.get_theme_font("font").get_height(
				child.get_theme_font_size("font_size")
			)
			expect(
				child.max_lines_visible * font_height + 1 >= measured.y,
				"Line limit does not hide text: " + child.text.left(50)
			)
		check_labels(child)


func _presentation(scene) -> void:
	var sheet: Image = scene.host.SHEET.get_image()
	expect(sheet.get_pixel(0, 0).a == 0, "Host sprite uses actual alpha transparency")
	scene.host.dancing = true
	scene.host.elapsed = 0
	scene.host._process(0)
	var first: int = scene.host.sprite.frame
	scene.host._process(0.3)
	expect(scene.host.sprite.frame != first, "Host changes dance pose over time")
	scene.host.reduced_motion = true
	scene.host._process(0.3)
	expect(scene.host.sprite.frame == 3, "Reduced motion uses a still host pose")
	scene.host.reduced_motion = false
	scene.host.dancing = false
	scene.effects.burst(Vector2(400, 300))
	expect(not scene.effects.particles.is_empty(), "Correct answer can emit sparks")
	scene.effects._process(3)
	expect(scene.effects.particles.is_empty(), "Particles expire without leaking nodes")
	for iteration in 20:
		scene.effects.burst(Vector2.ZERO, true)
	expect(scene.effects.particles.size() <= 300, "Repeated bursts stay bounded")
	scene.effects.particles.clear()
	for track in scene.audio.music.values():
		expect(
			track.stream.stereo and track.stream.get_length() > 10,
			"Background music has a complete stereo phrase"
		)
		expect(track.stream.loop_mode == AudioStreamWAV.LOOP_FORWARD, "Music loops continuously")
		expect(track.volume_db == -80, "Mute immediately silences background music")
	for puzzle in scene.bank:
		scene.question_label.text = puzzle.question
		check_labels(scene.question_label.get_parent())
	scene._refresh()


func _run() -> void:
	_rules()
	_simulations()
	var scene = load("res://scenes/main.tscn").instantiate()
	root.add_child(scene)
	await process_frame
	scene.set_process(false)
	# Headless tests inspect audio data without scheduling inaudible playback.
	scene.audio.muted = true
	for cue in scene.audio.sounds.values():
		expect(cue.data.size() > 0 and cue.mix_rate == 22050, "Synthesized cue has PCM data")
	expect(scene.popup != null, "Welcome screen exists")
	check_labels(scene)
	expect(click_text(scene, "Начать игру"), "Start button enters tournament")
	expect(
		scene.stage == "qualifier" and scene.game.players.size() == 3,
		"Human qualifier has three contestants"
	)
	expect(
		scene.game.players[1].name == "Нина" and scene.game.players[2].name == "Борис",
		"Player names do not include cities"
	)
	scene.new_game(42)
	_presentation(scene)
	for index in Rules.SECTORS.size():
		scene.wheel.spin_to(index, 0.001)
		await scene.wheel.landed
		var step := TAU / Rules.SECTORS.size()
		var landed_index := posmod(
			roundi(-scene.wheel.disk.rotation.y / step), Rules.SECTORS.size()
		)
		expect(landed_index == index, "Wheel angle matches result %d" % index)
	# Reset after testing the wheel signal's real controller integration.
	scene.wheel.spin_to(4, 0.01)
	scene.new_game(42)
	await create_timer(0.05).timeout
	expect(
		not scene.wheel.spinning and scene.game.phase == "spin",
		"Restart cancels the previous spin without affecting the new round"
	)
	scene._spin()
	expect(
		scene.audio.music_mode == "spin" and scene.host.dancing,
		"Wheel starts music and host dance together"
	)
	scene.new_game(42)
	expect(
		scene.audio.music_mode == "studio" and not scene.host.dancing,
		"Restart restores studio music and stops dance"
	)
	scene.game.solve(scene.game.word)
	scene._refresh()
	check_labels(scene)
	expect(click_text(scene, "Играть финал"), "Qualifier advances to final")
	expect(
		scene.stage == "final" and scene.game.players.size() == 3,
		"Two other qualifiers supply finalists"
	)
	for player in scene.game.players:
		expect(" · " not in player.name, "Finalists also use names without cities")
	scene.game.players[0].score = 5000
	scene.game.solve(scene.game.word)
	scene._refresh()
	expect(scene.stage == "shop", "Final victory opens prize selection")
	check_labels(scene)
	expect(click_text(scene, "Чайный сервиз · 500"), "Prize can be purchased")
	expect(scene.finalist.score == 4500 and scene.gifts.size() == 1, "Prize deducts points once")
	expect(not click_text(scene, "Чайный сервиз · 500"), "Duplicate prize purchase disabled")
	check_labels(scene)
	click_text(scene, "Суперигра →")
	check_labels(scene)
	click_text(scene, "Играем!")
	expect(scene.stage == "super" and scene.super_phase == "letters", "Supergame is opt-in")
	for letter in "ОАЕ":
		scene._choose_letter(letter)
	expect(scene.super_phase == "answer", "Three letters start answer timer")
	scene._solve_dialog()
	check_labels(scene)
	scene._process(61)
	expect(scene.stage == "end" and scene.gifts.is_empty(), "Answer popup cannot freeze timer")
	scene.gifts.append("Телевизор")
	scene._start_super()
	for letter in "ОАЕ":
		scene._choose_letter(letter)
	scene._help()
	scene._process(61)
	expect(scene.super_seconds == 60, "Explicit help pauses the supergame")
	scene._close_popup()
	scene._solve_dialog()
	scene.word_entry.text = scene.super_puzzle.answer
	scene._submit_word()
	check_labels(scene)
	expect(
		scene.stage == "end" and scene.gifts == ["Телевизор", "Автомобиль"],
		"Correct supergame answer retains prizes and awards a car"
	)
	scene.new_game(6)
	scene.game.solve("НЕВЕРНО")
	scene._refresh()
	expect(not scene.game.players[0].active, "Human can be eliminated without a crash")
	scene.queue_free()
	await process_frame
	print("%d checks; %d failures. Rules, AI, wheel and tournament UI tested." % [checks, failures])
	quit(1 if failures else 0)
