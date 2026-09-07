class_name Opponent
extends RefCounted
## Receives public information only; never receives the hidden answer.

const FREQUENCY := "ОЕАИНТСРВЛКМДПУЯЫЬГЗБЧЙХЖШЮЦЩЭФЪ"


static func letter(used: String, rng: RandomNumberGenerator) -> String:
	var available := ""
	for item in FREQUENCY:
		if item not in used:
			available += item
	if available.is_empty():
		return ""
	return available[rng.randi_range(0, mini(4, available.length() - 1))]


static func position(mask: String, rng: RandomNumberGenerator) -> int:
	var closed: Array[int] = []
	for index in mask.length():
		if mask[index] == "·":
			closed.append(index)
	return closed[rng.randi_range(0, closed.size() - 1)] if not closed.is_empty() else -1
