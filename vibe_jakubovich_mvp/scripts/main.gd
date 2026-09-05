extends Control
## Studio presentation and tournament orchestration. Rules live in RoundRules.

const Rules = preload("res://scripts/round_rules.gd")
const Bot = preload("res://scripts/opponent.gd")
const Wheel = preload("res://scripts/wheel.gd")
const Sound = preload("res://scripts/sound.gd")
const FittedLabel = preload("res://scripts/fitted_label.gd")
const Host = preload("res://scripts/host.gd")
const StageEffects = preload("res://scripts/stage_effects.gd")
const REGULAR = preload("res://assets/fonts/AlegreyaSansSC-Regular.ttf")
const BOLD = preload("res://assets/fonts/AlegreyaSansSC-Bold.ttf")
const INK := Color("11142c")
const CREAM := Color("f6e7c9")
const GOLD := Color("d7b476")
const TEAL := Color("197e80")
const MUTED := Color("a8bab6")
const SHOP := [
	{"name": "Чайный сервиз", "cost": 500},
	{"name": "Велосипед", "cost": 1500},
	{"name": "Телевизор", "cost": 3000}
]

var game = Rules.new()
var rng := RandomNumberGenerator.new()
var bank: Array = []
var episode: Array = []
var stage := "qualifier"
var busy := false
var started := false
var ai_clock := 0.0
var popup: Control
var canvas: Control
var board: Control
var keys: Control
var actions: Control
var contestants: Control
var dialogue: Label
var instruction: Label
var round_label: Label
var question_label: Label
var category_label: Label
var wheel
var audio
var word_entry: LineEdit
var letter_buttons: Dictionary = {}
var gifts: Array[String] = []
var super_used := ""
var super_phase := ""
var super_seconds := 60.0
var super_puzzle: Dictionary = {}
var finalist: Dictionary = {}
var fast_ai := false
var sound_button: Button
var speed_button: Button
var last_mask := ""
var record := 0
var host
var stage_lights
var effects
var reduced_motion := false
var popup_body
var displayed_scores: Dictionary = {}
var quitting := false


func _ready() -> void:
	get_tree().auto_accept_quit = false
	rng.randomize()
	bank = JSON.parse_string(FileAccess.get_file_as_string("res://data/questions.json"))
	audio = Sound.new()
	add_child(audio)
	_load_settings()
	_build_studio()
	audio.set_music("studio")
	if "--capture" in OS.get_cmdline_user_args():
		await load("res://tools/capture.gd").run(self)
	else:
		_welcome()
		if "--smoke" in OS.get_cmdline_user_args():
			await get_tree().create_timer(2.0).timeout
			_quit_game()


func _box(rect: Rect2, parent: Node, color: Color = INK, border: Color = GOLD) -> Panel:
	var panel := Panel.new()
	panel.position = rect.position
	panel.size = rect.size
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.border_color = Color(border, 0.85)
	style.set_border_width_all(2)
	style.border_width_bottom = 5
	style.set_corner_radius_all(3)
	style.shadow_color = Color(0, 0, 0, 0.55)
	style.shadow_size = 5
	style.shadow_offset = Vector2(0, 4)
	panel.add_theme_stylebox_override("panel", style)
	parent.add_child(panel)
	return panel


func _label(
	text: String,
	rect: Rect2,
	font_size: int,
	parent: Node,
	color: Color = CREAM,
	bold: bool = false
) -> Label:
	var label := FittedLabel.new()
	label.text = text
	label.position = rect.position
	label.size = rect.size
	label.bounds = rect.size
	label.preferred_size = font_size
	label.minimum_size = mini(14, font_size)
	label.add_theme_font_override("font", BOLD if bold else REGULAR)
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(label)
	return label


func _button(
	text: String, rect: Rect2, parent: Node, callback: Callable, primary: bool = false
) -> Button:
	var button := Button.new()
	button.text = text
	button.clip_text = true
	button.position = rect.position
	button.size = rect.size
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.add_theme_font_override("font", BOLD)
	var font_size := 22
	while (
		font_size > 14
		and (
			BOLD.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
			> rect.size.x - 18
		)
	):
		font_size -= 1
	button.add_theme_font_size_override("font_size", font_size)
	for state in ["normal", "hover", "pressed", "disabled", "focus"]:
		var style := StyleBoxFlat.new()
		style.set_corner_radius_all(3)
		style.set_border_width_all(2)
		style.border_width_bottom = 4 if state != "pressed" else 1
		style.content_margin_left = 7
		style.content_margin_right = 7
		style.border_color = GOLD if state == "focus" else Color(GOLD, 0.35)
		style.bg_color = GOLD if primary else Color("282b52")
		if state == "hover":
			style.bg_color = Color("ffe6ae") if primary else Color("515782")
		if state == "disabled":
			style.bg_color = Color("1c2037")
		button.add_theme_stylebox_override(state, style)
	button.add_theme_color_override("font_color", INK if primary else CREAM)
	button.add_theme_color_override("font_hover_color", INK if primary else CREAM)
	button.add_theme_color_override("font_pressed_color", INK if primary else CREAM)
	button.add_theme_color_override("font_disabled_color", Color("52686b"))
	button.mouse_entered.connect(
		func():
			if not button.disabled:
				button.create_tween().tween_property(
					button, "modulate", Color(1.12, 1.12, 1.12), 0.12
				)
	)
	button.mouse_exited.connect(
		func(): button.create_tween().tween_property(button, "modulate", Color.WHITE, 0.12)
	)
	button.pressed.connect(
		func():
			audio.play("button")
			callback.call()
	)
	parent.add_child(button)
	return button


func _build_studio() -> void:
	canvas = Control.new()
	canvas.size = Vector2(1600, 900)
	add_child(canvas)
	resized.connect(_resize_canvas)
	_resize_canvas()
	var art := TextureRect.new()
	art.texture = preload("res://assets/studio-90s.png")
	art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	art.size = Vector2(1600, 900)
	art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	canvas.add_child(art)
	stage_lights = StageEffects.new()
	stage_lights.reduced_motion = reduced_motion
	canvas.add_child(stage_lights)
	var sign := _box(Rect2(110, 40, 510, 82), canvas, Color("25152b"))
	var title := _label("ПОЛЕ ЧУДЕС", Rect2(12, 0, 486, 61), 54, sign, GOLD, true)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var tagline := _label("К А П И Т А Л - Ш О У", Rect2(12, 58, 486, 20), 14, sign, CREAM)
	tagline.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	round_label = _label("КЛАССИЧЕСКАЯ ИГРА", Rect2(658, 80, 390, 34), 23, canvas, GOLD)
	_button("Правила", Rect2(1124, 23, 124, 40), canvas, _help)
	sound_button = _button("Звук", Rect2(1260, 23, 130, 40), canvas, _toggle_sound)
	speed_button = _button("Темп ×1", Rect2(1402, 23, 142, 40), canvas, _toggle_speed)
	sound_button.text = "Без звука" if audio.muted else "Звук вкл."
	var board_panel := _box(Rect2(116, 163, 882, 248), canvas, Color("111b38"))
	category_label = _label("", Rect2(26, 15, 830, 25), 17, board_panel, GOLD)
	question_label = _label("", Rect2(26, 46, 830, 78), 25, board_panel)
	board = Control.new()
	board.position = Vector2(20, 147)
	board.size = Vector2(842, 74)
	board_panel.add_child(board)
	host = Host.new()
	host.position = Vector2(1335, 697)
	host.reduced_motion = reduced_motion
	canvas.add_child(host)
	var wheel_container := SubViewportContainer.new()
	wheel_container.position = Vector2(30, 435)
	wheel_container.size = Vector2(670, 375)
	wheel_container.stretch = true
	wheel_container.mouse_filter = Control.MOUSE_FILTER_IGNORE
	canvas.add_child(wheel_container)
	var viewport := SubViewport.new()
	viewport.size = Vector2i(670, 375)
	viewport.transparent_bg = true
	viewport.own_world_3d = true
	viewport.msaa_3d = Viewport.MSAA_4X
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	wheel_container.add_child(viewport)
	wheel = Wheel.new()
	viewport.add_child(wheel)
	wheel.landed.connect(_landed)
	wheel.tick.connect(func(): audio.play("tick"))
	var controls := _box(Rect2(710, 471, 410, 329), canvas, Color("252039"))
	instruction = _label("", Rect2(22, 12, 366, 46), 23, controls, GOLD, true)
	keys = Control.new()
	keys.position = Vector2(24, 68)
	controls.add_child(keys)
	for index in Rules.ALPHABET.length():
		var letter: String = Rules.ALPHABET[index]
		var button := _button(
			letter,
			Rect2((index % 8) * 46, (index / 8) * 42, 40, 37),
			keys,
			func(): _choose_letter(letter)
		)
		letter_buttons[letter] = button
	actions = Control.new()
	actions.position = Vector2(38, 259)
	controls.add_child(actions)
	var host_panel := _box(Rect2(1142, 726, 412, 138), canvas, Color("211830"))
	_label("ЛЕОНИД ЯКУБОВИЧ", Rect2(19, 10, 374, 22), 17, host_panel, GOLD, true)
	dialogue = _label("", Rect2(19, 38, 374, 87), 23, host_panel)
	contestants = Control.new()
	contestants.position = Vector2(58, 817)
	canvas.add_child(contestants)
	_label(
		"Пробел — барабан  ·  Esc — пауза  ·  F11 — полный экран  ·  F8 — спокойный свет",
		Rect2(66, 883, 1460, 17),
		13,
		canvas,
		MUTED
	)
	effects = StageEffects.new()
	effects.foreground = true
	effects.reduced_motion = reduced_motion
	canvas.add_child(effects)


func _resize_canvas() -> void:
	if not is_instance_valid(canvas):
		return
	var factor := minf(size.x / 1600.0, size.y / 900.0)
	canvas.scale = Vector2.ONE * factor
	canvas.position = (size - Vector2(1600, 900) * factor) / 2


func _clear(node: Node) -> void:
	for child in node.get_children():
		node.remove_child(child)
		child.queue_free()


func new_game(seed_value: int = -1) -> void:
	_close_popup()
	wheel.cancel_spin()
	audio.set_music("studio")
	host.dancing = false
	host.celebration = 0
	stage_lights.spinning = false
	effects.particles.clear()
	displayed_scores.clear()
	if seed_value >= 0:
		rng.seed = seed_value
	episode.clear()
	var theme_index := rng.randi_range(0, 1)
	var choices: Array = []
	for item in bank:
		if item.category == ["Вещи с историей", "Удивительная природа"][theme_index]:
			choices.append(item)
	while episode.size() < 5:
		var index := rng.randi_range(0, choices.size() - 1)
		episode.append(choices.pop_at(index))
	stage = "qualifier"
	gifts.clear()
	super_phase = ""
	started = true
	busy = false
	ai_clock = 0
	game.setup(
		episode[0],
		[
			{"name": "Вы", "human": true},
			{"name": "Нина", "human": false},
			{"name": "Борис", "human": false}
		],
		rng.randi()
	)
	_refresh()


func _refresh() -> void:
	if not started:
		return
	var is_super := stage == "super"
	var puzzle: Dictionary = (
		super_puzzle if is_super else {"question": game.question, "category": game.category}
	)
	category_label.text = "ТЕМА ВЫПУСКА  /  " + puzzle.category.to_upper()
	question_label.text = puzzle.question
	round_label.text = (
		{
			"qualifier": "01 / ТРОЙКА ИГРОКОВ",
			"final": "02 / ФИНАЛ",
			"super": "03 / СУПЕРИГРА",
			"shop": "ВАШИ ПРИЗЫ",
			"end": "ДО НОВОЙ ВСТРЕЧИ"
		}
		. get(stage, "")
	)
	_render_board()
	_render_players()
	_render_actions()
	for letter in letter_buttons:
		var allowed: bool = (
			(is_super and super_phase == "letters")
			or (not is_super and _human_turn() and game.phase == "letter" and not busy)
		)
		letter_buttons[letter].disabled = (
			not allowed or letter in (super_used if is_super else game.used)
		)
	if not is_super:
		dialogue.text = game.message
	if busy:
		instruction.text = "Барабан вращается…"
	elif is_super:
		instruction.text = (
			"Выберите ещё %d буквы" % (3 - super_used.length())
			if super_phase == "letters"
			else "Назовите слово!"
		)
	elif not _human_turn() and game.phase != "over":
		instruction.text = "Играет %s" % game.players[game.current].name
	else:
		instruction.text = (
			{
				"spin": "Ваш ход. Крутите барабан!",
				"letter": "Назовите букву",
				"plus": "Выберите клетку на табло",
				"prize": "Приз или игра?",
				"chest": "В какой шкатулке деньги?",
				"over": "Раунд завершён"
			}
			. get(game.phase, "")
		)
	if not is_super and game.phase == "over" and popup == null:
		_round_over()


func _render_board() -> void:
	_clear(board)
	var mask: String = _super_mask() if stage == "super" else game.visible_word()
	var width := minf(56, (820.0 - mask.length() * 6) / maxf(mask.length(), 1))
	var x := (842 - mask.length() * (width + 6) + 6) / 2
	for index in mask.length():
		var item := mask[index]
		var cell := _button(
			item if item != "·" else "",
			Rect2(x + index * (width + 6), 0, width, 62),
			board,
			func(): _choose_position(index),
			item != "·"
		)
		cell.add_theme_font_size_override("font_size", 32)
		var face := StyleBoxFlat.new()
		face.bg_color = Color("fff2cb") if item != "·" else Color("164939")
		face.border_color = Color("a3a39b")
		face.set_border_width_all(3)
		face.border_width_bottom = 6
		for state in ["normal", "hover", "pressed", "focus"]:
			cell.add_theme_stylebox_override(state, face)
		cell.pivot_offset = cell.size / 2
		if item != "·" and last_mask.length() == mask.length() and last_mask[index] == "·":
			if not reduced_motion:
				cell.scale.x = 0.02
				var reveal := cell.create_tween()
				reveal.tween_interval(index * 0.045)
				reveal.tween_property(cell, "scale:x", 1.0, 0.32).set_trans(Tween.TRANS_BACK)
		cell.mouse_filter = (
			Control.MOUSE_FILTER_STOP
			if item == "·" and game.phase == "plus" and _human_turn() and stage != "super"
			else Control.MOUSE_FILTER_IGNORE
		)
		if item == "·":
			var ornament := _label("◆", Rect2(0, 9, width, 43), 16, cell, Color("3d7b7c"))
			ornament.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	last_mask = mask


func _render_players() -> void:
	_clear(contestants)
	for index in game.players.size():
		var player: Dictionary = game.players[index]
		var active: bool = (
			index == game.current and game.phase != "over" and stage not in ["end", "shop", "super"]
		)
		var panel := _box(
			Rect2(index * 355, 0, 340, 60),
			contestants,
			Color("654729") if active else Color("241e31")
		)
		_label(
			("●  " if active else "") + player.name, Rect2(14, 4, 215, 28), 21, panel, CREAM, true
		)
		_label(
			"В игре" if player.active else "Выбыл из раунда",
			Rect2(14, 34, 134, 18),
			15,
			panel,
			MUTED
		)
		var score := _label(str(player.score), Rect2(227, 4, 98, 30), 29, panel, GOLD, true)
		score.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		var previous_score: int = displayed_scores.get(player.name, player.score)
		displayed_scores[player.name] = player.score
		if previous_score != player.score and not reduced_motion:
			score.create_tween().tween_method(
				func(value: float): score.text = str(roundi(value)),
				float(previous_score),
				float(player.score),
				0.5
			)
		var cash := _label(
			"%d ₽ в шкатулках" % player.cash, Rect2(147, 36, 178, 18), 13, panel, MUTED
		)
		cash.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT


func _render_actions() -> void:
	_clear(actions)
	if stage == "super":
		if super_phase == "answer":
			_button("Ответить", Rect2(0, 0, 332, 52), actions, _solve_dialog, true)
		return
	if busy or not _human_turn() or stage not in ["qualifier", "final"]:
		return
	match game.phase:
		"spin", "letter", "plus":
			var spin := _button("Крутить", Rect2(0, 0, 170, 52), actions, _spin, true)
			spin.disabled = game.phase != "spin"
			_button("Слово целиком", Rect2(180, 0, 152, 52), actions, _solve_dialog)
		"prize":
			_button("Беру приз", Rect2(0, 0, 158, 52), actions, func(): _prize(true), true)
			_button("Играю дальше", Rect2(169, 0, 163, 52), actions, func(): _prize(false))
		"chest":
			_button("Шкатулка I", Rect2(0, 0, 158, 52), actions, func(): _chest(0), true)
			_button("Шкатулка II", Rect2(169, 0, 163, 52), actions, func(): _chest(1), true)


func _human_turn() -> bool:
	return not game.players.is_empty() and game.players[game.current].human


func _spin() -> void:
	if busy or game.phase != "spin" or popup != null:
		return
	busy = true
	audio.set_music("spin")
	host.elapsed = 0
	host.dancing = true
	stage_lights.spinning = true
	var sector := rng.randi_range(0, Rules.SECTORS.size() - 1)
	_refresh()
	wheel.spin_to(sector, 1.1 if fast_ai and not _human_turn() else 3.2)


func _landed(sector: int) -> void:
	busy = false
	audio.set_music("studio")
	host.dancing = false
	stage_lights.spinning = false
	game.spin(sector)
	ai_clock = 0
	_refresh()


func _choose_letter(letter: String) -> void:
	if popup != null or busy:
		return
	if stage == "super" and super_phase == "letters":
		if letter not in super_used:
			super_used += letter
			audio.play("letter")
			if super_used.length() == 3:
				super_phase = "answer"
				dialogue.text = "Буквы открыты. У вас одна минута и одна попытка назвать слово!"
			_refresh()
		return
	if _human_turn() and game.guess(letter):
		audio.play("letter" if letter in game.word else "wrong")
		_refresh()
		if letter in game.word:
			_celebrate()


func _choose_position(index: int) -> void:
	if popup == null and not busy and stage != "super" and _human_turn() and game.reveal(index):
		audio.play("letter")
		_refresh()
		_celebrate()


func _celebrate(big: bool = false) -> void:
	host.cheer()
	effects.burst(Vector2(340, 343), big)
	effects.burst(Vector2(785, 343), big)


func _prize(accept: bool) -> void:
	game.prize(accept)
	_refresh()


func _chest(choice: int) -> void:
	game.chest(choice)
	audio.play("letter")
	_refresh()


func _process(delta: float) -> void:
	if not started or busy:
		return
	if stage == "super" and super_phase == "answer":
		# The answer dialog does not stop the clock; the explicit pause menu does.
		if popup == null or popup.has_meta("answer"):
			super_seconds = maxf(0, super_seconds - delta)
			round_label.text = (
				"СУПЕРИГРА  /  %02d:%02d" % [int(super_seconds) / 60, int(super_seconds) % 60]
			)
			if super_seconds <= 0:
				_finish_super(false)
		return
	if (
		popup != null
		or stage not in ["qualifier", "final"]
		or _human_turn()
		or game.phase == "over"
	):
		return
	ai_clock += delta
	if ai_clock < (0.4 if fast_ai else 1.35):
		return
	ai_clock = 0
	if game.phase == "spin":
		_spin()
	else:
		var previous_mask: String = game.visible_word()
		_bot_action(game)
		_refresh()
		if game.visible_word() != previous_mask:
			audio.play("letter")
			_celebrate()


func _bot_action(round_game) -> void:
	match round_game.phase:
		"spin":
			round_game.spin(rng.randi_range(0, Rules.SECTORS.size() - 1))
		"letter":
			round_game.guess(Bot.letter(round_game.used, rng))
		"plus":
			round_game.reveal(Bot.position(round_game.visible_word(), rng))
		"prize":
			round_game.prize(false)
		"chest":
			round_game.chest(rng.randi_range(0, 1))


func _round_over() -> void:
	audio.play("win")
	if game.winner < 0 or not game.players[game.winner].human:
		stage = "end"
		var panel := _modal(
			"Спасибо за игру!",
			game.message + "\nМожно попробовать ещё раз — вопросы и соперники будут ждать."
		)
		_button("Новый выпуск", Rect2(40, 246, 580, 56), panel, new_game, true)
		return
	finalist = game.players[game.winner].duplicate(true)
	_celebrate(true)
	if stage == "qualifier":
		var finalists: Array = [finalist]
		var summary := "Вы выиграли свою тройку!\n\n"
		var names := [["Ольга", "Лев", "Вера"], ["Павел", "Анна", "Игорь"]]
		for index in 2:
			var other = Rules.new()
			var lineup: Array = []
			for person in names[index]:
				lineup.append({"name": person, "human": false})
			other.setup(episode[index + 1], lineup, rng.randi())
			for attempt in 3000:
				if other.phase == "over":
					break
				_bot_action(other)
			assert(other.winner >= 0, "Simulated qualifier must finish")
			finalists.append(other.players[other.winner].duplicate(true))
			summary += "Тройка %d: %s\n" % [index + 2, finalists[-1].name]
		var panel := _modal("Встречаем финалистов", summary)
		_button(
			"Играть финал",
			Rect2(40, 246, 580, 56),
			panel,
			func():
				_close_popup()
				stage = "final"
				game.setup(episode[3], finalists, rng.randi())
				_refresh(),
			true
		)
	else:
		stage = "shop"
		_shop()


func _shop() -> void:
	var panel := _modal(
		"Вы победитель!",
		(
			"Обменяйте очки на призы. Осталось: %d\nВыбрано: %s"
			% [finalist.score, ", ".join(gifts) if not gifts.is_empty() else "пока ничего"]
		)
	)
	popup_body.bounds.y = 77
	popup_body.fit()
	for index in SHOP.size():
		var item: Dictionary = SHOP[index]
		var button := _button(
			"%s · %d" % [item.name, item.cost],
			Rect2(40 + index * 198, 183, 184, 52),
			panel,
			func():
				finalist.score -= item.cost
				gifts.append(item.name)
				_shop()
		)
		button.add_theme_font_size_override("font_size", 18)
		button.disabled = finalist.score < item.cost or item.name in gifts
	_button("Забрать призы", Rect2(40, 260, 278, 54), panel, func(): _finish_show(false))
	_button("Суперигра →", Rect2(335, 260, 285, 54), panel, _super_offer, true)


func _super_offer() -> void:
	var panel := _modal(
		"Рискнёте сыграть?",
		(
			"Одно слово, три буквы на выбор и 60 секунд.\n\n"
			+ "Победа принесёт автомобиль. При проигрыше призы за очки сгорают; "
			+ "деньги из шкатулок остаются."
		)
	)
	_button("Сохранить призы", Rect2(40, 260, 278, 54), panel, func(): _finish_show(false))
	_button("Играем!", Rect2(335, 260, 285, 54), panel, _start_super, true)


func _start_super() -> void:
	_close_popup()
	stage = "super"
	super_puzzle = episode[4]
	super_used = ""
	super_phase = "letters"
	super_seconds = 60
	dialogue.text = "Выберите три разные буквы. Я открою все их вхождения на табло."
	_refresh()


func _super_mask() -> String:
	var mask := ""
	for letter in Rules.normalize(super_puzzle.get("answer", "")):
		mask += letter if letter in super_used else "·"
	return mask


func _finish_super(won: bool) -> void:
	super_phase = "done"
	if won:
		gifts.append("Автомобиль")
		_celebrate(true)
	else:
		gifts.clear()
	_finish_show(true, won)


func _finish_show(played: bool, won: bool = false) -> void:
	stage = "end"
	record = maxi(record, game.players[game.winner].score)
	_save_settings()
	var text := (
		"Ваши призы: %s\nДеньги из шкатулок: %d ₽\nРекорд: %d очков"
		% [", ".join(gifts) if not gifts.is_empty() else "памятный подарок", finalist.cash, record]
	)
	if played:
		text = (
			("Суперигра выиграна!" if won else "Суперигра не выиграна.")
			+ " Ответ: %s.\n\n" % super_puzzle.answer
			+ text
		)
	var panel := _modal("До новой встречи!", text)
	_button("Ещё один выпуск", Rect2(40, 265, 580, 54), panel, new_game, true)
	audio.play("win" if won or not played else "wrong")


func _modal(title: String, body: String) -> Panel:
	_close_popup()
	popup = Control.new()
	popup.size = Vector2(1600, 900)
	canvas.add_child(popup)
	var shade := ColorRect.new()
	shade.color = Color(0.01, 0.02, 0.025, 0.78)
	shade.size = Vector2(1600, 900)
	popup.add_child(shade)
	var panel := _box(Rect2(470, 266, 660, 370), popup, Color("241b36"))
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	panel.clip_contents = true
	_label(title, Rect2(40, 27, 580, 58), 37, panel, GOLD, true)
	popup_body = _label(body, Rect2(40, 95, 580, 148), 24, panel)
	if not reduced_motion:
		panel.pivot_offset = panel.size / 2
		panel.scale = Vector2.ONE * 0.94
		panel.create_tween().tween_property(panel, "scale", Vector2.ONE, 0.2).set_trans(
			Tween.TRANS_CUBIC
		)
	return panel


func _close_popup() -> void:
	if is_instance_valid(popup):
		canvas.remove_child(popup)
		popup.queue_free()
	popup = null
	popup_body = null
	word_entry = null


func _welcome() -> void:
	var panel := _modal(
		"Добро пожаловать в студию",
		(
			"Один вечер. Девять участников. Один победитель.\n\n"
			+ "Крутите барабан, угадывайте буквы и доберитесь до суперигры. "
			+ "Рекорд: %d очков." % record
		)
	)
	_button("Начать игру", Rect2(40, 263, 580, 56), panel, new_game, true)


func _solve_dialog() -> void:
	if busy:
		return
	var panel := _modal(
		"Назовите слово", "Одна попытка. Неверный ответ завершает ваше участие в раунде."
	)
	popup_body.bounds.y = 55
	popup_body.fit()
	popup.set_meta("answer", true)
	word_entry = LineEdit.new()
	word_entry.position = Vector2(40, 164)
	word_entry.size = Vector2(580, 54)
	word_entry.placeholder_text = "Введите слово по-русски"
	word_entry.max_length = 30
	word_entry.add_theme_font_override("font", REGULAR)
	word_entry.add_theme_font_size_override("font_size", 27)
	panel.add_child(word_entry)
	word_entry.text_submitted.connect(func(_text): _submit_word())
	_button("Подумать ещё", Rect2(40, 263, 278, 54), panel, _close_popup)
	_button("Это мой ответ", Rect2(335, 263, 285, 54), panel, _submit_word, true)
	word_entry.grab_focus()


func _submit_word() -> void:
	if not is_instance_valid(word_entry):
		return
	var answer := Rules.normalize(word_entry.text)
	if answer.is_empty():
		return
	_close_popup()
	if stage == "super":
		_finish_super(answer == Rules.normalize(super_puzzle.answer))
	else:
		game.solve(answer)
		_refresh()


func _help() -> void:
	if popup != null:
		return
	var panel := _modal(
		"Правила классической игры",
		(
			"Буква есть — очки за каждое вхождение и ещё один ход.\n"
			+ "«0» — пропуск, «Б» — потеря очков, «+» — открыть букву.\n"
			+ "«×2» — счёт × (число букв + 1). «П» — приз или игра.\n"
			+ "Три буквы подряд — две шкатулки. Ё считается Е."
		)
	)
	_button("Продолжить", Rect2(40, 263, 278, 54), panel, _close_popup, true)
	popup.set_meta("help", true)
	_button("Новый выпуск", Rect2(335, 263, 285, 54), panel, _confirm_restart)
	_button("Выйти из игры", Rect2(224, 326, 212, 32), panel, _quit_game)


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		_quit_game()


func _quit_game() -> void:
	if quitting:
		return
	quitting = true
	audio.shutdown()
	# Let the audio server retire its playing streams before destroying resources.
	await get_tree().create_timer(0.15).timeout
	get_tree().quit()


func _confirm_restart() -> void:
	var panel := _modal(
		"Начать заново?", "Текущий выпуск будет завершён. Рекорд и настройки сохранятся."
	)
	_button("Вернуться", Rect2(40, 263, 278, 54), panel, _close_popup)
	_button("Новая игра", Rect2(335, 263, 285, 54), panel, new_game, true)


func _toggle_sound() -> void:
	audio.muted = not audio.muted
	sound_button.text = "Без звука" if audio.muted else "Звук вкл."
	_save_settings()


func _toggle_speed() -> void:
	fast_ai = not fast_ai
	speed_button.text = "Темп ×3" if fast_ai else "Темп ×1"


func _toggle_motion() -> void:
	reduced_motion = not reduced_motion
	host.reduced_motion = reduced_motion
	stage_lights.reduced_motion = reduced_motion
	effects.reduced_motion = reduced_motion
	effects.particles.clear()
	_save_settings()


func _load_settings() -> void:
	var settings := ConfigFile.new()
	if settings.load("user://settings.cfg") == OK:
		audio.muted = settings.get_value("game", "muted", false)
		record = settings.get_value("game", "record", 0)
		reduced_motion = settings.get_value("game", "reduced_motion", false)


func _save_settings() -> void:
	var settings := ConfigFile.new()
	settings.set_value("game", "muted", audio.muted)
	settings.set_value("game", "record", record)
	settings.set_value("game", "reduced_motion", reduced_motion)
	settings.save("user://settings.cfg")


func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	if event.keycode == KEY_F8:
		_toggle_motion()
		return
	if event.keycode == KEY_F11:
		var mode := DisplayServer.window_get_mode()
		DisplayServer.window_set_mode(
			(
				DisplayServer.WINDOW_MODE_WINDOWED
				if mode == DisplayServer.WINDOW_MODE_FULLSCREEN
				else DisplayServer.WINDOW_MODE_FULLSCREEN
			)
		)
		return
	if event.keycode == KEY_ESCAPE:
		if popup != null and (popup.has_meta("answer") or popup.has_meta("help")):
			_close_popup()
		elif popup == null:
			_help()
		return
	if popup != null or not started:
		return
	if event.keycode == KEY_SPACE and _human_turn() and stage in ["qualifier", "final"]:
		_spin()
	elif event.unicode > 0:
		var letter := Rules.normalize(String.chr(event.unicode))
		if letter in Rules.ALPHABET:
			_choose_letter(letter)
