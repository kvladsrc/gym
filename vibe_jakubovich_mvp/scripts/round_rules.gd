class_name RoundRules
extends RefCounted
## Presentation-independent rules for a classic three-player round.

const ALPHABET := "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ"
const SECTORS := [100, 200, 300, -1, 400, 500, -2, 600, 100, -3, 700, 200, -4, 300, -5, 1000]
const BANKRUPT := -1
const ZERO := -2
const PLUS := -3
const PRIZE := -4
const MULTIPLY := -5

var players: Array = []
var word := ""
var question := ""
var category := ""
var used := ""
var current := 0
var winner := -1
var phase := "spin"
var stake := 0
var streak := 0
var message := "Крутите барабан!"
var chest_resume := "spin"
var rng := RandomNumberGenerator.new()


static func normalize(text: String) -> String:
	return text.strip_edges().to_upper().replace("Ё", "Е")


static func sector_text(value: int) -> String:
	match value:
		BANKRUPT:
			return "Б"
		ZERO:
			return "0"
		PLUS:
			return "+"
		PRIZE:
			return "П"
		MULTIPLY:
			return "×2"
	return str(value)


func setup(puzzle: Dictionary, contestants: Array, seed_value: int = -1) -> void:
	rng.randomize()
	if seed_value >= 0:
		rng.seed = seed_value
	word = normalize(puzzle.answer)
	question = puzzle.question
	category = puzzle.category
	players = contestants.duplicate(true)
	for player in players:
		player["active"] = true
		if not player.has("score"):
			player["score"] = 0
		if not player.has("cash"):
			player["cash"] = 0
	used = ""
	current = 0
	winner = -1
	phase = "spin"
	streak = 0
	message = "Вопрос на табло. %s, крутите барабан!" % players[current].name


func visible_word() -> String:
	var result := ""
	for letter in word:
		result += letter if letter in used or letter not in ALPHABET else "·"
	return result


func spin(sector: int) -> bool:
	if phase != "spin" or sector < 0 or sector >= SECTORS.size():
		return false
	stake = SECTORS[sector]
	match stake:
		BANKRUPT:
			players[current].score = 0
			message = "Банкрот! Очки сгорают. Деньги из шкатулок остаются."
			next_player()
		ZERO:
			message = "Ноль. Очки сохраняются, ход переходит дальше."
			next_player()
		PLUS:
			phase = "plus"
			message = "Сектор «Плюс»! Выберите закрытую клетку на табло."
		PRIZE:
			phase = "prize"
			message = "Сектор «Приз»! Заберёте подарок или продолжите игру?"
		_:
			phase = "letter"
			message = "Сектор %s. Назовите букву!" % sector_text(stake)
	return true


func guess(letter: String) -> bool:
	letter = normalize(letter)
	if phase != "letter" or letter.length() != 1 or letter not in ALPHABET or letter in used:
		return false
	used += letter
	var count := word.count(letter)
	if count == 0:
		message = "Буквы «%s» нет. Ход переходит дальше." % letter
		next_player()
		return true
	if stake == MULTIPLY:
		players[current].score *= count + 1
	else:
		players[current].score += stake * count
	streak += 1
	message = "Есть такая буква! «%s» — %d. Продолжайте!" % [letter, count]
	phase = "spin"
	if "·" not in visible_word():
		finish()
	if streak == 3:
		streak = 0
		chest_resume = phase
		phase = "chest"
		message = "Три буквы подряд! Выберите одну из двух шкатулок."
	return true


func reveal(position: int) -> bool:
	if phase != "plus" or position < 0 or position >= word.length():
		return false
	var letter := word[position]
	if letter in used or letter not in ALPHABET:
		return false
	used += letter
	streak = 0
	phase = "spin"
	message = "Открываем все буквы «%s». Крутите барабан!" % letter
	if "·" not in visible_word():
		finish()
	return true


func solve(answer: String) -> bool:
	if phase not in ["spin", "letter", "plus"] or normalize(answer).is_empty():
		return false
	if normalize(answer) == word:
		used = ALPHABET
		finish()
	else:
		players[current].active = false
		message = "Неверное слово. %s выбывает из этого раунда." % players[current].name
		next_player()
	return true


func prize(accept: bool) -> bool:
	if phase != "prize":
		return false
	if accept:
		players[current]["gift"] = "Подарок из чёрного ящика"
		players[current].active = false
		message = "%s забирает приз и покидает раунд." % players[current].name
		next_player()
	else:
		stake = 1000
		phase = "letter"
		message = "Играем дальше! Назовите букву за 1000 очков."
	return true


func chest(choice: int) -> bool:
	if phase != "chest" or choice not in [0, 1]:
		return false
	var success := choice == rng.randi_range(0, 1)
	if success:
		players[current].cash += 5000
	phase = chest_resume
	message = "В шкатулке 5000 ₽!" if success else "Эта шкатулка пуста. Продолжаем!"
	return true


func next_player() -> void:
	streak = 0
	for offset in range(1, players.size() + 1):
		var index := (current + offset) % players.size()
		if players[index].active:
			current = index
			phase = "spin"
			return
	phase = "over"
	winner = -1
	message = "Все участники выбыли. Ответ: %s." % word


func finish() -> void:
	winner = current
	phase = "over"
	message = "Верно! «%s». Побеждает %s!" % [word, players[current].name]
