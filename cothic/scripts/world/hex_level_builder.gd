class_name HexLevelBuilder
extends RefCounted

const MODEL_ROOT := "res://assets/models/hexagon-kit/"
const PORTAL_MODEL_PATH := "res://assets/models/simple_stone_portal.glb"
const HEX_SCALE := 10.0
const HEX_SPACING := 1.0 * HEX_SCALE
const HEX_ROW_HEIGHT := 0.866 * HEX_SCALE
const WALK_SURFACE_HEIGHT := 0.2
const WALK_SURFACE_TOP_Y := 0.2 * HEX_SCALE
const WALK_SURFACE_Y := WALK_SURFACE_TOP_Y - WALK_SURFACE_HEIGHT * 0.5
const OBSTACLE_RADIUS := HEX_SCALE * 0.43
const OBSTACLE_HEIGHT := 2.8
const SEA_RADIUS := 32
const DECORATION_SEED := 41
const OBJECT_Y := WALK_SURFACE_TOP_Y
const PORTAL_SCALE := 1.0
const INTERACTABLE_HEIGHT := 3.0
const INTERACTABLE_RADIUS := 3.0
const LABEL_HEIGHT := 4.2
const LABEL_PIXEL_SIZE := 0.025
const WATER_DECORATION_CHANCE := 0.015
const GRASS_MODELS: Array[String] = [
	"grass",
	"grass",
	"grass",
	"grass",
	"grass",
	"grass",
	"grass",
	"grass-forest",
	"grass-hill",
]
const PASSABLE_TERRAIN_MODELS: Array[String] = [
	"grass",
	"sand",
	"sand-desert",
	"stone",
]
const WATER_DECORATION_MODELS: Array[String] = [
	"water-rocks",
	"water-island",
]
const HEX_DIRECTIONS: Array[Vector2i] = [
	Vector2i(1, 0),
	Vector2i(1, -1),
	Vector2i(0, -1),
	Vector2i(-1, 0),
	Vector2i(-1, 1),
	Vector2i(0, 1),
]

var global_seed := 0
var _scene_cache: Dictionary = {}
var _terrain_model_by_coord: Dictionary = {}
var _beach_model_by_coord: Dictionary = {}
var _stone_model_by_coord: Dictionary = {}
var _river_tile_by_coord: Dictionary = {}
var _island_generator := IslandGenerator.new()
var _beach_generator := BeachGenerator.new()
var _river_generator := RiverGenerator.new()
var _stone_generator := StoneGenerator.new()
var _entry_placement_generator := EntryPlacementGenerator.new()


func build(parent: Node3D, entries: Array[Dictionary] = [], generation_seed: int = 0) -> void:
	_terrain_model_by_coord.clear()
	_beach_model_by_coord.clear()
	_stone_model_by_coord.clear()
	_river_tile_by_coord.clear()

	var island_cells := _generate_location_island(entries, generation_seed)
	_beach_model_by_coord = _generate_location_beaches(island_cells, generation_seed)
	_river_tile_by_coord = _generate_location_rivers(island_cells, generation_seed)
	_stone_model_by_coord = _generate_location_stones(island_cells, generation_seed)
	var placements := _generate_location_buildings(entries, island_cells, generation_seed)
	var decoration_rng := _rng_for_seed(_seed_with_offset(generation_seed, DECORATION_SEED))

	_render_location(parent, island_cells, placements, decoration_rng)


func _generate_location_island(entries: Array[Dictionary], generation_seed: int) -> Dictionary:
	return _island_generator.generate(entries, generation_seed)


func _generate_location_beaches(island_cells: Dictionary, generation_seed: int) -> Dictionary:
	return _beach_generator.generate(island_cells, generation_seed, HEX_DIRECTIONS)


func _generate_location_rivers(island_cells: Dictionary, generation_seed: int) -> Dictionary:
	return _river_generator.generate(island_cells, generation_seed, global_seed, HEX_DIRECTIONS)


func _generate_location_stones(island_cells: Dictionary, generation_seed: int) -> Dictionary:
	var blocked_coords := _beach_model_by_coord.duplicate()
	for coord in _river_tile_by_coord:
		blocked_coords[coord] = true
	return _stone_generator.generate(island_cells, generation_seed, blocked_coords, HEX_DIRECTIONS)


func _generate_location_buildings(
	entries: Array[Dictionary], island_cells: Dictionary, generation_seed: int
) -> Array[Dictionary]:
	var blocked_coords := _beach_model_by_coord.duplicate()
	for coord in _river_tile_by_coord:
		blocked_coords[coord] = true
	for coord in _stone_model_by_coord:
		blocked_coords[coord] = true
	return _entry_placement_generator.generate(
		entries, island_cells, generation_seed, blocked_coords, HEX_DIRECTIONS
	)


func _render_location(
	parent: Node3D,
	island_cells: Dictionary,
	placements: Array[Dictionary],
	decoration_rng: RandomNumberGenerator
) -> void:
	_render_tiles(parent, island_cells, placements, decoration_rng)
	_add_walk_surface(parent, island_cells)
	_add_terrain_obstacles(parent, island_cells, placements)
	_add_repository_objects(parent, placements)


func _seed_with_offset(seed: int, offset: int) -> int:
	return int((seed + offset) & 0x7FFFFFFF)


func _rng_for_seed(seed: int) -> RandomNumberGenerator:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed
	return rng


func _render_tiles(
	parent: Node3D,
	island_cells: Dictionary,
	placements: Array[Dictionary],
	rng: RandomNumberGenerator
) -> void:
	var placement_by_coord := _placement_by_coord(placements)
	for q in range(-SEA_RADIUS, SEA_RADIUS + 1):
		for r in range(-SEA_RADIUS, SEA_RADIUS + 1):
			var coord := Vector2i(q, r)
			if not _is_inside_sea(coord):
				continue

			var model_name := _tile_model(coord, island_cells, placement_by_coord, rng)
			_terrain_model_by_coord[coord] = model_name
			_add_model(parent, model_name, _hex_to_world(coord), _tile_rotation(coord))


func _tile_model(
	coord: Vector2i,
	island_cells: Dictionary,
	placement_by_coord: Dictionary,
	rng: RandomNumberGenerator
) -> String:
	if not island_cells.has(coord):
		if rng.randf() < WATER_DECORATION_CHANCE:
			return WATER_DECORATION_MODELS[rng.randi_range(0, WATER_DECORATION_MODELS.size() - 1)]
		return "water"

	if _river_tile_by_coord.has(coord):
		var river_tile: Dictionary = _river_tile_by_coord[coord]
		return String(river_tile["model"])

	if placement_by_coord.has(coord):
		var placement: Dictionary = placement_by_coord[coord]
		var model_name := String(placement["model"])
		if model_name != "":
			return model_name
		return "grass"
	return _land_terrain_model(coord, rng)


func _land_terrain_model(coord: Vector2i, rng: RandomNumberGenerator) -> String:
	if coord == Vector2i.ZERO:
		return "grass"
	if _beach_model_by_coord.has(coord):
		return _beach_model_by_coord[coord]
	if _stone_model_by_coord.has(coord):
		return _stone_model_by_coord[coord]
	return GRASS_MODELS[rng.randi_range(0, GRASS_MODELS.size() - 1)]


func _tile_rotation(coord: Vector2i) -> float:
	if not _river_tile_by_coord.has(coord):
		return 0.0

	var river_tile: Dictionary = _river_tile_by_coord[coord]
	return float(river_tile["rotation_steps"]) * TAU / float(HEX_DIRECTIONS.size())


func _add_repository_objects(parent: Node3D, placements: Array[Dictionary]) -> void:
	for placement in placements:
		var entry: Dictionary = placement["entry"]
		var coord: Vector2i = placement["coord"]
		var root := Node3D.new()
		root.name = _safe_node_name("%s_%s" % [entry["type"], entry["name"]])
		root.position = _object_position(coord)
		root.rotation.y = placement["rotation"]
		parent.add_child(root)

		if entry["type"] == "directory":
			_add_scene(root, "PortalModel", PORTAL_MODEL_PATH, Vector3.ONE * PORTAL_SCALE)
			_add_obstacle_collision(root, OBSTACLE_RADIUS, OBSTACLE_HEIGHT)

		_add_label(root, entry)
		_add_interactable(root, entry)


func _object_position(coord: Vector2i) -> Vector3:
	var position := _hex_to_world(coord)
	return Vector3(position.x, OBJECT_Y, position.z)


func _add_model(
	parent: Node, model_name: String, position: Vector3, rotation_y: float = 0.0
) -> void:
	var packed_scene := _load_scene(model_name)
	if packed_scene == null:
		return

	var instance := packed_scene.instantiate()
	if not instance is Node3D:
		instance.queue_free()
		return

	instance.name = "Hex_%s" % model_name
	instance.position = position
	instance.scale = Vector3.ONE * HEX_SCALE
	instance.rotation.y = rotation_y
	parent.add_child(instance)


func _add_scene(parent: Node3D, node_name: String, path: String, scale: Vector3) -> void:
	if not ResourceLoader.exists(path):
		push_warning("Missing model: %s" % path)
		return

	var packed_scene := load(path) as PackedScene
	var instance := packed_scene.instantiate()
	if not instance is Node3D:
		instance.queue_free()
		return

	instance.name = node_name
	instance.scale = scale
	parent.add_child(instance)


func _add_label(parent: Node3D, entry: Dictionary) -> void:
	var label := Label3D.new()
	label.name = "Label"
	label.text = "%s/" % entry["name"] if entry["type"] == "directory" else entry["name"]
	label.font_size = 50
	label.pixel_size = LABEL_PIXEL_SIZE
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.no_depth_test = true
	label.modulate = Color(0.95, 0.92, 0.82)
	label.outline_size = 10
	label.outline_modulate = Color(0.05, 0.04, 0.03)
	label.position = Vector3(0.0, LABEL_HEIGHT, 0.0)
	parent.add_child(label)


func _add_interactable(parent: Node3D, entry: Dictionary) -> void:
	var interactable := WorldInteractable.new()
	interactable.name = "Interactable"
	interactable.entry_name = entry["name"]
	interactable.entry_path = entry["path"]
	interactable.entry_type = entry["type"]
	interactable.position = Vector3.ZERO
	parent.add_child(interactable)

	var collision_shape := CollisionShape3D.new()
	collision_shape.name = "CollisionShape3D"
	var box_shape := BoxShape3D.new()
	box_shape.size = Vector3(INTERACTABLE_RADIUS, INTERACTABLE_HEIGHT, INTERACTABLE_RADIUS)
	collision_shape.shape = box_shape
	collision_shape.position.y = INTERACTABLE_HEIGHT * 0.5
	interactable.add_child(collision_shape)

	var tool_state := parent.get_tree().get_first_node_in_group("tool_state")
	if (
		tool_state != null
		and tool_state.has_method("is_marked")
		and tool_state.is_marked(entry["path"])
	):
		interactable.call_deferred("set_marked", true)


func _add_walk_surface(parent: Node, island_cells: Dictionary) -> void:
	var body := StaticBody3D.new()
	body.name = "GeneratedWalkSurface"
	parent.add_child(body)

	for coord in island_cells:
		var typed_coord: Vector2i = coord
		_add_floor_collision(body, _hex_to_world(typed_coord))


func _add_terrain_obstacles(
	parent: Node, island_cells: Dictionary, placements: Array[Dictionary]
) -> void:
	var placement_by_coord := _placement_by_coord(placements)
	var body := StaticBody3D.new()
	body.name = "GeneratedObstacles"
	parent.add_child(body)

	for coord in island_cells:
		var typed_coord: Vector2i = coord
		var model_name := String(_terrain_model_by_coord.get(typed_coord, "grass"))
		if _is_passable_terrain_model(model_name):
			continue
		if placement_by_coord.has(typed_coord):
			var placement: Dictionary = placement_by_coord[typed_coord]
			if placement["entry"]["type"] == "directory":
				continue

		var position := _hex_to_world(typed_coord)
		var collision := _obstacle_collision_shape(model_name, OBSTACLE_RADIUS, OBSTACLE_HEIGHT)
		collision.position = Vector3(
			position.x, WALK_SURFACE_TOP_Y + OBSTACLE_HEIGHT * 0.5, position.z
		)
		body.add_child(collision)


func _is_passable_terrain_model(model_name: String) -> bool:
	return PASSABLE_TERRAIN_MODELS.has(model_name) or model_name.begins_with("river-")


func _add_obstacle_collision(parent: Node3D, radius: float, height: float) -> void:
	var body := StaticBody3D.new()
	body.name = "ObstacleBody"
	parent.add_child(body)

	var collision := _obstacle_collision_shape("Obstacle", radius, height)
	collision.position.y = height * 0.5
	body.add_child(collision)


func _obstacle_collision_shape(
	model_name: String, radius: float, height: float
) -> CollisionShape3D:
	var collision := CollisionShape3D.new()
	collision.name = "Obstacle_%s" % model_name
	var shape := CapsuleShape3D.new()
	shape.radius = radius
	shape.height = maxf(height, radius * 2.0)
	collision.shape = shape
	return collision


func _add_floor_collision(parent: StaticBody3D, position: Vector3) -> void:
	var shape := BoxShape3D.new()
	shape.size = Vector3(HEX_SCALE * 1.08, WALK_SURFACE_HEIGHT, HEX_ROW_HEIGHT * 1.08)

	var collision := CollisionShape3D.new()
	collision.name = "GrassFloor"
	collision.shape = shape
	collision.position = position
	collision.position.y = WALK_SURFACE_Y
	parent.add_child(collision)


func _is_inside_sea(coord: Vector2i) -> bool:
	return _is_inside_radius(coord, SEA_RADIUS)


func _is_inside_radius(coord: Vector2i, radius: int) -> bool:
	return abs(coord.x) <= radius and abs(coord.y) <= radius and abs(coord.x + coord.y) <= radius


func _hex_to_world(coord: Vector2i) -> Vector3:
	var x := (float(coord.x) + float(coord.y) * 0.5) * HEX_SPACING
	var z := float(coord.y) * HEX_ROW_HEIGHT
	return Vector3(x, 0.0, z)


func _placement_by_coord(placements: Array[Dictionary]) -> Dictionary:
	var by_coord := {}
	for placement in placements:
		by_coord[placement["coord"]] = placement
	return by_coord


func _safe_node_name(value: String) -> String:
	var safe := value.replace(" ", "_")
	for character in ["/", "\\", ":", "*", "?", '"', "<", ">", "|", "."]:
		safe = safe.replace(character, "_")
	return safe


func _load_scene(model_name: String) -> PackedScene:
	if _scene_cache.has(model_name):
		return _scene_cache[model_name]

	var path := "%s%s.glb" % [MODEL_ROOT, model_name]
	if not ResourceLoader.exists(path):
		push_warning("Missing hexagon-kit model: %s" % path)
		return null

	var scene := load(path) as PackedScene
	_scene_cache[model_name] = scene
	return scene
