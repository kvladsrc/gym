class_name StoneGenerator
extends RefCounted

const STONE_SEED := 89
const MIN_STONE_CLUSTER_TILE_COUNT := 2
const MAX_STONE_CLUSTER_TILE_COUNT := 9
const STONE_CLUSTER_TILE_DIVISOR := 8.0
const STONE_MIN_ISLAND_TILE_COUNT := 22
const STONE_MODELS: Array[String] = [
	"stone",
	"stone",
	"stone-rocks",
	"stone-hill",
	"stone-mountain",
]

var _directions: Array[Vector2i] = []


func generate(
	island_cells: Dictionary,
	generation_seed: int,
	blocked_coords: Dictionary,
	directions: Array[Vector2i]
) -> Dictionary:
	_directions = directions
	return _generate_stones(
		island_cells, blocked_coords, _seed_with_offset(generation_seed, STONE_SEED)
	)


func _generate_stones(
	island_cells: Dictionary, blocked_coords: Dictionary, seed: int
) -> Dictionary:
	if island_cells.size() < STONE_MIN_ISLAND_TILE_COUNT:
		return {}

	var rng := _rng_for_seed(seed)
	var candidates := _stone_seed_cells(island_cells, blocked_coords)
	if candidates.is_empty():
		return {}

	var stone_models := {}
	var seed_coord = _pop_stone_seed(candidates)
	if seed_coord == null:
		return {}

	var typed_seed := seed_coord as Vector2i
	var target_size := _stone_cluster_tile_count(island_cells.size(), rng)
	var cluster := _grow_stone_cluster(
		typed_seed, target_size, island_cells, blocked_coords, stone_models, rng
	)
	for coord in cluster:
		stone_models[coord] = _random_stone_model(rng)

	return stone_models


func _stone_cluster_tile_count(island_tile_count: int, rng: RandomNumberGenerator) -> int:
	var base_count := int(round(float(island_tile_count) / STONE_CLUSTER_TILE_DIVISOR))
	var varied_count := base_count + rng.randi_range(-1, 1)
	return mini(MAX_STONE_CLUSTER_TILE_COUNT, maxi(MIN_STONE_CLUSTER_TILE_COUNT, varied_count))


func _stone_seed_cells(island_cells: Dictionary, blocked_coords: Dictionary) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord in island_cells:
		var typed_coord: Vector2i = coord
		if _can_be_stone_cell(typed_coord, island_cells, blocked_coords):
			coords.append(typed_coord)
	coords.sort_custom(
		func(left: Vector2i, right: Vector2i) -> bool:
			return _center_distance(left) > _center_distance(right)
	)
	return coords


func _pop_stone_seed(candidates: Array[Vector2i]):
	while not candidates.is_empty():
		var coord: Vector2i = candidates.pop_back()
		return coord
	return null


func _grow_stone_cluster(
	seed_coord: Vector2i,
	target_size: int,
	island_cells: Dictionary,
	blocked_coords: Dictionary,
	stone_models: Dictionary,
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
			if cluster.has(neighbor) or stone_models.has(neighbor):
				continue
			if not _can_be_stone_cell(neighbor, island_cells, blocked_coords):
				continue
			cluster.append(neighbor)
			frontier.append(neighbor)
	return cluster


func _can_be_stone_cell(
	coord: Vector2i, island_cells: Dictionary, blocked_coords: Dictionary
) -> bool:
	if coord == Vector2i.ZERO:
		return false
	if blocked_coords.has(coord):
		return false
	if not island_cells.has(coord):
		return false
	if _is_coast_cell(coord, island_cells):
		return false
	return true


func _is_coast_cell(coord: Vector2i, island_cells: Dictionary) -> bool:
	for direction in _directions:
		if not island_cells.has(coord + direction):
			return true
	return false


func _center_distance(coord: Vector2i) -> int:
	return maxi(abs(coord.x), maxi(abs(coord.y), abs(coord.x + coord.y)))


func _random_stone_model(rng: RandomNumberGenerator) -> String:
	return STONE_MODELS[rng.randi_range(0, STONE_MODELS.size() - 1)]


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
