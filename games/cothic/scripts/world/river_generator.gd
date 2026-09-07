class_name RiverGenerator
extends RefCounted

const RIVER_SEED := 97
const SLOPE_SEED := 131
const RIVER_SPLIT_CHANCE := 0.10
const RIVER_STRAIGHT_WEIGHT := 1.35
const RIVER_TURN_WEIGHT := 1.0
const RIVER_SHARP_TURN_WEIGHT := 0.35
const SIDE_COUNT := 6
# Side order matches HexLevelBuilder.HEX_DIRECTIONS.
# A value of 1 means the river connects through that side.
const TILE_MASKS: Dictionary = {
	"bridge": [1, 0, 0, 1, 0, 0],
	"building-watermill": [1, 0, 0, 1, 0, 0],
	"river-corner": [0, 1, 0, 1, 0, 0],
	"river-corner-sharp": [0, 0, 1, 1, 0, 0],
	"river-crossing": [1, 1, 1, 1, 1, 1],
	"river-end": [0, 0, 0, 1, 0, 0],
	"river-intersectionA": [0, 1, 1, 1, 0, 0],
	"river-intersectionB": [1, 1, 0, 1, 0, 0],
	"river-intersectionC": [1, 0, 0, 1, 0, 1],
	"river-intersectionD": [1, 1, 0, 1, 0, 1],
	"river-intersectionE": [1, 0, 1, 1, 0, 1],
	"river-intersectionF": [0, 1, 0, 1, 0, 1],
	"river-intersectionG": [1, 1, 1, 1, 0, 1],
	"river-intersectionH": [1, 1, 1, 1, 0, 0],
	"river-start": [0, 0, 0, 1, 0, 0],
	"river-straight": [1, 0, 0, 1, 0, 0],
}

var _directions: Array[Vector2i] = []


func generate(
	island_cells: Dictionary, generation_seed: int, global_seed: int, directions: Array[Vector2i]
) -> Dictionary:
	_directions = directions
	var river_masks := _generate_river_masks(
		island_cells,
		_seed_with_offset(generation_seed, RIVER_SEED),
		_slope_direction_index(global_seed)
	)
	return _river_tiles_from_masks(river_masks)


func _generate_river_masks(island_cells: Dictionary, seed: int, slope_direction: int) -> Dictionary:
	var rng := _rng_for_seed(seed)
	var coast_cells := _river_start_candidates(island_cells)
	if coast_cells.is_empty():
		return {}
	_shuffle(coast_cells, rng)

	var start := _pick_river_start(coast_cells, island_cells, slope_direction, rng)
	if start.is_empty():
		return {}

	var river_masks := {}
	var start_coord: Vector2i = start["coord"]
	var start_side: int = start["side"]
	_add_river_side(river_masks, start_coord, start_side)

	var queue: Array[Vector2i] = [start_coord]
	var processed := {}
	while not queue.is_empty():
		var coord: Vector2i = queue.pop_front()
		if processed.has(coord):
			continue
		processed[coord] = true

		if not island_cells.has(coord):
			continue

		var output_sides := _river_output_sides(
			coord, island_cells, river_masks, slope_direction, rng
		)
		for side in output_sides:
			_add_river_side(river_masks, coord, side)
			var neighbor := coord + _directions[side]
			if not island_cells.has(neighbor):
				continue
			_add_river_side(river_masks, neighbor, _opposite_side(side))
			if not processed.has(neighbor):
				queue.append(neighbor)
	return river_masks


func _river_start_candidates(island_cells: Dictionary) -> Array[Vector2i]:
	var candidates: Array[Vector2i] = []
	for coord in island_cells:
		var typed_coord: Vector2i = coord
		if _water_side_indices(typed_coord, island_cells).is_empty():
			continue
		candidates.append(typed_coord)
	return candidates


func _pick_river_start(
	coast_cells: Array[Vector2i],
	island_cells: Dictionary,
	slope_direction: int,
	rng: RandomNumberGenerator
) -> Dictionary:
	for coord in coast_cells:
		var water_sides := _uphill_water_side_indices(coord, island_cells, slope_direction)
		_shuffle_ints(water_sides, rng)
		for side in water_sides:
			var test_masks := {}
			_add_river_side(test_masks, coord, side)
			if _has_land_output_side(coord, island_cells, test_masks, slope_direction):
				return {
					"coord": coord,
					"side": side,
				}
	return {}


func _uphill_water_side_indices(
	coord: Vector2i, island_cells: Dictionary, slope_direction: int
) -> Array[int]:
	var sides: Array[int] = []
	var uphill_sides := _uphill_side_indices(slope_direction)
	for side in _water_side_indices(coord, island_cells):
		if uphill_sides.has(side):
			sides.append(side)
	return sides


func _has_land_output_side(
	coord: Vector2i, island_cells: Dictionary, river_masks: Dictionary, slope_direction: int
) -> bool:
	for side in _allowed_river_output_sides(coord, island_cells, river_masks, slope_direction):
		if island_cells.has(coord + _directions[side]):
			return true
	return false


func _water_side_indices(coord: Vector2i, island_cells: Dictionary) -> Array[int]:
	var sides: Array[int] = []
	for side in range(_directions.size()):
		if not island_cells.has(coord + _directions[side]):
			sides.append(side)
	return sides


func _river_output_sides(
	coord: Vector2i,
	island_cells: Dictionary,
	river_masks: Dictionary,
	slope_direction: int,
	rng: RandomNumberGenerator
) -> Array[int]:
	var candidates := _allowed_river_output_sides(coord, island_cells, river_masks, slope_direction)

	var output_count := 1
	if _can_split_river(coord, island_cells, candidates, rng):
		output_count = 2

	var output_sides: Array[int] = []
	while output_sides.size() < output_count and not candidates.is_empty():
		var side := _take_weighted_river_output_side(candidates, river_masks[coord], rng)
		output_sides.append(side)
		candidates.erase(side)
	return output_sides


func _can_split_river(
	coord: Vector2i, island_cells: Dictionary, candidates: Array[int], rng: RandomNumberGenerator
) -> bool:
	if candidates.size() < 2:
		return false
	if rng.randf() >= RIVER_SPLIT_CHANCE:
		return false

	for side in candidates:
		if not island_cells.has(coord + _directions[side]):
			return false
	return true


func _take_weighted_river_output_side(
	candidates: Array[int], current_mask: Array[int], rng: RandomNumberGenerator
) -> int:
	var total_weight := 0.0
	var weights: Array[float] = []
	for side in candidates:
		var weight := _river_output_side_weight(side, current_mask)
		weights.append(weight)
		total_weight += weight

	var target := rng.randf() * total_weight
	var cursor := 0.0
	for index in range(candidates.size()):
		cursor += weights[index]
		if target <= cursor:
			return candidates[index]
	return candidates.back()


func _river_output_side_weight(side: int, current_mask: Array[int]) -> float:
	var connected_sides := _river_connected_sides(current_mask)
	if connected_sides.is_empty():
		return RIVER_TURN_WEIGHT

	var total_weight := 0.0
	for input_side in connected_sides:
		var straight_side := _opposite_side(input_side)
		var turn_distance := _side_distance(side, straight_side)
		if turn_distance == 0:
			total_weight += RIVER_STRAIGHT_WEIGHT
		elif turn_distance == 1:
			total_weight += RIVER_TURN_WEIGHT
		else:
			total_weight += RIVER_SHARP_TURN_WEIGHT
	return total_weight / float(connected_sides.size())


func _river_connected_sides(mask: Array[int]) -> Array[int]:
	var sides: Array[int] = []
	for side in range(mask.size()):
		if mask[side] == 1:
			sides.append(side)
	return sides


func _allowed_river_output_sides(
	coord: Vector2i, island_cells: Dictionary, river_masks: Dictionary, slope_direction: int
) -> Array[int]:
	var sides: Array[int] = []
	var current_mask: Array[int] = river_masks[coord]
	for side in _downhill_side_indices(slope_direction):
		if current_mask[side] == 1:
			continue

		var neighbor := coord + _directions[side]
		if not island_cells.has(neighbor):
			sides.append(side)
			continue

		if _river_mask_has_side(river_masks, neighbor, _opposite_side(side)):
			continue
		sides.append(side)
	return sides


func _downhill_side_indices(slope_direction: int) -> Array[int]:
	return [
		_wrap_side(slope_direction - 1),
		_wrap_side(slope_direction),
		_wrap_side(slope_direction + 1),
	]


func _uphill_side_indices(slope_direction: int) -> Array[int]:
	return [
		_wrap_side(slope_direction + 2),
		_wrap_side(slope_direction + 3),
		_wrap_side(slope_direction + 4),
	]


func _river_tiles_from_masks(river_masks: Dictionary) -> Dictionary:
	var river_tiles := {}
	for coord in river_masks:
		var typed_coord: Vector2i = coord
		var mask: Array[int] = river_masks[typed_coord]
		var tile := _find_tile_for_mask(mask)
		if tile.is_empty():
			push_warning("Missing river tile for mask %s at %s" % [mask, typed_coord])
			continue
		river_tiles[typed_coord] = tile
	return river_tiles


func _find_tile_for_mask(target_mask: Array[int]) -> Dictionary:
	for model_name in _river_model_names():
		var model_mask := _mask_for(model_name)
		for rotation in range(SIDE_COUNT):
			if _rotated_mask(model_mask, rotation) == target_mask:
				return {
					"model": model_name,
					"rotation_steps": rotation,
				}
	return {}


func _river_model_names() -> Array[String]:
	var names: Array[String] = []
	for model_name in TILE_MASKS:
		if String(model_name).begins_with("river-"):
			names.append(model_name)
	names.sort()
	return names


func _mask_for(model_name: String) -> Array[int]:
	if not TILE_MASKS.has(model_name):
		return []

	var mask: Array[int] = []
	for value in TILE_MASKS[model_name]:
		mask.append(int(value))
	return mask


func _rotated_mask(mask: Array[int], rotation: int) -> Array[int]:
	var rotated: Array[int] = []
	rotated.resize(SIDE_COUNT)
	for side in range(SIDE_COUNT):
		rotated[(side + rotation) % SIDE_COUNT] = mask[side]
	return rotated


func _empty_river_mask() -> Array[int]:
	var mask: Array[int] = []
	for _side in range(_directions.size()):
		mask.append(0)
	return mask


func _add_river_side(river_masks: Dictionary, coord: Vector2i, side: int) -> void:
	if not river_masks.has(coord):
		river_masks[coord] = _empty_river_mask()
	var mask: Array[int] = river_masks[coord]
	mask[side] = 1


func _river_mask_has_side(river_masks: Dictionary, coord: Vector2i, side: int) -> bool:
	if not river_masks.has(coord):
		return false
	var mask: Array[int] = river_masks[coord]
	return mask[side] == 1


func _slope_direction_index(global_seed: int) -> int:
	var rng := _rng_for_seed(_seed_with_offset(global_seed, SLOPE_SEED))
	return rng.randi_range(0, _directions.size() - 1)


func _side_distance(left: int, right: int) -> int:
	var clockwise := _wrap_side(left - right)
	var counter_clockwise := _wrap_side(right - left)
	return mini(clockwise, counter_clockwise)


func _opposite_side(side: int) -> int:
	return _wrap_side(side + 3)


func _wrap_side(side: int) -> int:
	return (side + _directions.size()) % _directions.size()


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


func _shuffle_ints(values: Array[int], rng: RandomNumberGenerator) -> void:
	for index in range(values.size() - 1, 0, -1):
		var swap_index := rng.randi_range(0, index)
		var value := values[index]
		values[index] = values[swap_index]
		values[swap_index] = value
