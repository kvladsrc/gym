class_name BeachGenerator
extends RefCounted

const BEACH_SEED := 73
const MIN_BEACH_COUNT := 1
const MAX_BEACH_COUNT := 2
const MIN_BEACH_TILE_COUNT := 2
const MAX_BEACH_TILE_COUNT := 4
const SAND_MODELS: Array[String] = [
	"sand",
	"sand",
	"sand-desert",
]

var _directions: Array[Vector2i] = []


func generate(
	island_cells: Dictionary,
	generation_seed: int,
	directions: Array[Vector2i],
	blocked_coords: Dictionary = {}
) -> Dictionary:
	_directions = directions
	return _generate_beaches(
		island_cells, blocked_coords, _seed_with_offset(generation_seed, BEACH_SEED)
	)


func _generate_beaches(
	island_cells: Dictionary, blocked_coords: Dictionary, seed: int
) -> Dictionary:
	var rng := _rng_for_seed(seed)
	var candidates := _beach_seed_cells(island_cells, blocked_coords)
	_shuffle(candidates, rng)

	var beach_models := {}
	var beach_count := mini(rng.randi_range(MIN_BEACH_COUNT, MAX_BEACH_COUNT), candidates.size())
	for _beach_index in range(beach_count):
		var seed_coord = _pop_beach_seed(candidates, beach_models)
		if seed_coord == null:
			break
		var typed_seed := seed_coord as Vector2i
		var target_size := rng.randi_range(MIN_BEACH_TILE_COUNT, MAX_BEACH_TILE_COUNT)
		var cluster := _grow_beach_cluster(
			typed_seed, target_size, island_cells, blocked_coords, beach_models, rng
		)
		for coord in cluster:
			beach_models[coord] = _random_sand_model(rng)

	return beach_models


func _beach_seed_cells(island_cells: Dictionary, blocked_coords: Dictionary) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord in island_cells:
		var typed_coord: Vector2i = coord
		if _can_be_beach_cell(typed_coord, island_cells, blocked_coords):
			coords.append(typed_coord)
	return coords


func _pop_beach_seed(candidates: Array[Vector2i], beach_models: Dictionary):
	while not candidates.is_empty():
		var coord: Vector2i = candidates.pop_back()
		if beach_models.has(coord):
			continue
		if _touches_existing_beach(coord, beach_models):
			continue
		return coord
	return null


func _grow_beach_cluster(
	seed_coord: Vector2i,
	target_size: int,
	island_cells: Dictionary,
	blocked_coords: Dictionary,
	beach_models: Dictionary,
	rng: RandomNumberGenerator
) -> Array[Vector2i]:
	var cluster: Array[Vector2i] = [seed_coord]
	var frontier: Array[Vector2i] = [seed_coord]
	while cluster.size() < target_size and not frontier.is_empty():
		var current: Vector2i = frontier.pop_front()
		var neighbors: Array[Vector2i] = _directions.duplicate()
		_shuffle(neighbors, rng)
		for direction in neighbors:
			if cluster.size() >= target_size:
				break
			var neighbor: Vector2i = current + direction
			if cluster.has(neighbor) or beach_models.has(neighbor):
				continue
			if not _can_be_beach_cell(neighbor, island_cells, blocked_coords):
				continue
			cluster.append(neighbor)
			frontier.append(neighbor)
	return cluster


func _can_be_beach_cell(
	coord: Vector2i, island_cells: Dictionary, blocked_coords: Dictionary
) -> bool:
	if coord == Vector2i.ZERO:
		return false
	if blocked_coords.has(coord):
		return false
	if not island_cells.has(coord):
		return false
	return _is_coast_cell(coord, island_cells)


func _is_coast_cell(coord: Vector2i, island_cells: Dictionary) -> bool:
	for direction in _directions:
		if not island_cells.has(coord + direction):
			return true
	return false


func _touches_existing_beach(coord: Vector2i, beach_models: Dictionary) -> bool:
	for direction in _directions:
		if beach_models.has(coord + direction):
			return true
	return false


func _random_sand_model(rng: RandomNumberGenerator) -> String:
	return SAND_MODELS[rng.randi_range(0, SAND_MODELS.size() - 1)]


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
