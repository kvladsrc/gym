class_name EntryPlacementGenerator
extends RefCounted

const PLACEMENT_SEED := 41
const LARGE_BUILDING_MIN_FILE_COUNT := 6
const SMALL_BUILDING_RATIO := 0.5
const MEDIUM_BUILDING_RATIO := 2.0 / 6.0
const SMALL_BUILDING_MODELS: Array[String] = [
	"building-farm",
	"building-house",
	"building-sheep",
]
const MEDIUM_BUILDING_MODELS: Array[String] = [
	"building-archery",
	"building-cabin",
	"building-dock",
	"building-market",
	"building-mill",
	"building-mine",
	"building-port",
	"building-smelter",
	"building-tower",
	"building-village",
]
const LARGE_BUILDING_MODELS: Array[String] = [
	"building-castle",
	"building-wall",
	"building-walls",
	"building-wizard-tower",
]

var _directions: Array[Vector2i] = []


func generate(
	entries: Array[Dictionary],
	island_cells: Dictionary,
	generation_seed: int,
	blocked_coords: Dictionary,
	directions: Array[Vector2i]
) -> Array[Dictionary]:
	_directions = directions
	var placement_rng := _rng_for_seed(_seed_with_offset(generation_seed, PLACEMENT_SEED))
	var file_category_by_path := _file_building_category_by_path(entries)
	return _place_entries(
		entries, island_cells, placement_rng, blocked_coords, file_category_by_path
	)


func _place_entries(
	entries: Array[Dictionary],
	island_cells: Dictionary,
	rng: RandomNumberGenerator,
	blocked_coords: Dictionary = {},
	file_category_by_path: Dictionary = {}
) -> Array[Dictionary]:
	var preferred_coords := _preferred_placement_cells(island_cells, blocked_coords)
	var general_coords := _general_placement_cells(island_cells, blocked_coords)
	_shuffle(preferred_coords, rng)
	_shuffle(general_coords, rng)

	var placements: Array[Dictionary] = []
	var used_coords := {}
	for entry in entries:
		var coord: Variant = _take_placement_coord(preferred_coords, general_coords, used_coords)
		if coord == null:
			continue
		var typed_coord := coord as Vector2i
		var placement := {
			"coord": typed_coord,
			"entry": entry,
			"model": _entry_model(entry, file_category_by_path, rng),
			"rotation": rng.randf_range(0.0, TAU),
		}
		placements.append(placement)
	return placements


func _preferred_placement_cells(
	island_cells: Dictionary, blocked_coords: Dictionary = {}
) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord in island_cells:
		var typed_coord: Vector2i = coord
		if typed_coord == Vector2i.ZERO or _is_coast_cell(typed_coord, island_cells):
			continue
		if blocked_coords.has(typed_coord):
			continue
		if _has_land_neighbor_count(typed_coord, island_cells, 5):
			coords.append(typed_coord)
	return coords


func _general_placement_cells(
	island_cells: Dictionary, blocked_coords: Dictionary = {}
) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord in island_cells:
		var typed_coord: Vector2i = coord
		if typed_coord == Vector2i.ZERO:
			continue
		if blocked_coords.has(typed_coord):
			continue
		coords.append(typed_coord)
	return coords


func _take_placement_coord(
	preferred_coords: Array[Vector2i], general_coords: Array[Vector2i], used_coords: Dictionary
):
	var coord = _pop_unused_coord(preferred_coords, used_coords)
	if coord != null:
		return coord
	return _pop_unused_coord(general_coords, used_coords)


func _pop_unused_coord(coords: Array[Vector2i], used_coords: Dictionary):
	while not coords.is_empty():
		var coord: Vector2i = coords.pop_back()
		if used_coords.has(coord):
			continue
		used_coords[coord] = true
		return coord
	return null


func _is_coast_cell(coord: Vector2i, island_cells: Dictionary) -> bool:
	for direction in _directions:
		if not island_cells.has(coord + direction):
			return true
	return false


func _has_land_neighbor_count(
	coord: Vector2i, island_cells: Dictionary, minimum_count: int
) -> bool:
	var count := 0
	for direction in _directions:
		if island_cells.has(coord + direction):
			count += 1
	return count >= minimum_count


func _file_building_category_by_path(entries: Array[Dictionary]) -> Dictionary:
	var file_entries := _file_entries_by_size(entries)
	var category_by_path := {}
	if file_entries.is_empty():
		return category_by_path

	var counts := _building_category_counts(file_entries.size())
	for index in range(file_entries.size()):
		var entry: Dictionary = file_entries[index]
		var path := String(entry["path"])
		if index < counts["small"]:
			category_by_path[path] = "small"
		elif index < counts["small"] + counts["medium"]:
			category_by_path[path] = "medium"
		else:
			category_by_path[path] = "large"
	return category_by_path


func _file_entries_by_size(entries: Array[Dictionary]) -> Array[Dictionary]:
	var file_entries: Array[Dictionary] = []
	for entry in entries:
		if entry["type"] != "file":
			continue
		var sized_entry := entry.duplicate()
		sized_entry["size"] = _file_size(String(entry["path"]))
		file_entries.append(sized_entry)
	file_entries.sort_custom(
		func(left: Dictionary, right: Dictionary) -> bool:
			var left_size := int(left["size"])
			var right_size := int(right["size"])
			if left_size != right_size:
				return left_size < right_size
			return String(left["name"]).naturalnocasecmp_to(String(right["name"])) < 0
	)
	return file_entries


func _building_category_counts(file_count: int) -> Dictionary:
	if file_count <= 2:
		return {
			"small": file_count,
			"medium": 0,
			"large": 0,
		}

	var large_count := 0
	if file_count >= LARGE_BUILDING_MIN_FILE_COUNT:
		large_count = maxi(
			1, int(round(float(file_count) * (1.0 - SMALL_BUILDING_RATIO - MEDIUM_BUILDING_RATIO)))
		)

	var medium_count := int(round(float(file_count) * MEDIUM_BUILDING_RATIO))
	medium_count = mini(medium_count, file_count - large_count)
	var small_count := file_count - medium_count - large_count
	return {
		"small": small_count,
		"medium": medium_count,
		"large": large_count,
	}


func _file_size(path: String) -> int:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return 0
	return int(file.get_length())


func _entry_model(
	entry: Dictionary, file_category_by_path: Dictionary, rng: RandomNumberGenerator
) -> String:
	if entry["type"] == "file":
		var category := String(file_category_by_path.get(String(entry["path"]), "medium"))
		return _random_building_model(category, rng)
	return ""


func _random_building_model(category: String, rng: RandomNumberGenerator) -> String:
	var models := _building_models_for_category(category)
	return models[rng.randi_range(0, models.size() - 1)]


func _building_models_for_category(category: String) -> Array[String]:
	if category == "small":
		return SMALL_BUILDING_MODELS
	if category == "large":
		return LARGE_BUILDING_MODELS
	return MEDIUM_BUILDING_MODELS


func _seed_with_offset(seed: int, offset: int) -> int:
	return int((seed + offset) & 0x7FFFFFFF)


func _rng_for_seed(seed: int) -> RandomNumberGenerator:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed
	return rng


func _shuffle(coords: Array[Vector2i], rng: RandomNumberGenerator) -> void:
	for index in range(coords.size() - 1, 0, -1):
		var swap_index := rng.randi_range(0, index)
		var value := coords[index]
		coords[index] = coords[swap_index]
		coords[swap_index] = value
