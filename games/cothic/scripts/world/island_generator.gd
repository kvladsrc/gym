class_name IslandGenerator
extends RefCounted

const ISLAND_RADIUS := 28
const MIN_ISLAND_TILE_COUNT := 6
const ISLAND_TILES_PER_ENTRY := 3
const ISLAND_SEED := 19


func generate(entries: Array[Dictionary], generation_seed: int) -> Dictionary:
	return _generate_island(
		_island_tile_count(entries), ISLAND_RADIUS, _seed_with_offset(generation_seed, ISLAND_SEED)
	)


func _island_tile_count(entries: Array[Dictionary]) -> int:
	var disk_tile_count := _disk_cells(ISLAND_RADIUS).size()
	var target := maxi(MIN_ISLAND_TILE_COUNT, entries.size() * ISLAND_TILES_PER_ENTRY)
	return mini(disk_tile_count, target)


func _generate_island(target_tile_count: int, radius: int, seed: int) -> Dictionary:
	var island_cells := {}
	var rng := _rng_for_seed(seed)

	var candidates := _disk_cells(radius)
	var score_by_coord := {}
	for coord in candidates:
		score_by_coord[coord] = _compactness_score(coord, rng)
	candidates.sort_custom(
		func(left: Vector2i, right: Vector2i) -> bool:
			var left_score: float = score_by_coord[left]
			var right_score: float = score_by_coord[right]
			return left_score < right_score
	)

	var count := mini(target_tile_count, candidates.size())
	for index in range(count):
		island_cells[candidates[index]] = true

	island_cells[Vector2i.ZERO] = true
	return island_cells


func _compactness_score(coord: Vector2i, rng: RandomNumberGenerator) -> float:
	var hex_distance := maxi(abs(coord.x), maxi(abs(coord.y), abs(coord.x + coord.y)))
	var center_bias := Vector2(coord).length() * 0.12
	var noise := rng.randf_range(-0.35, 0.35)
	return float(hex_distance) + center_bias + noise


func _disk_cells(radius: int) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	for q in range(-radius, radius + 1):
		for r in range(-radius, radius + 1):
			var coord := Vector2i(q, r)
			if _is_inside_radius(coord, radius):
				cells.append(coord)
	return cells


func _is_inside_radius(coord: Vector2i, radius: int) -> bool:
	return abs(coord.x) <= radius and abs(coord.y) <= radius and abs(coord.x + coord.y) <= radius


func _seed_with_offset(seed: int, offset: int) -> int:
	return int((seed + offset) & 0x7FFFFFFF)


func _rng_for_seed(seed: int) -> RandomNumberGenerator:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed
	return rng
