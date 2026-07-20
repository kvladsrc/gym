class_name HexLevelBuilder
extends RefCounted

const MODEL_ROOT := "res://assets/models/hexagon-kit/"
const MODEL_PALETTE_PATH := "res://assets/models/hexagon-kit/Textures/colormap.png"
const PORTAL_TEXTURE_PATH := "res://assets/sprites/world/archive_gateway.png"
const NATURE_ATLAS_PATH := "res://assets/sprites/world/nature_atlas.png"
const STONE_DECOR_ATLAS_PATH := "res://assets/sprites/world/stone_decor_atlas.png"
const WILDLIFE_TEXTURE_PATHS: Array[String] = [
	"res://assets/sprites/wildlife/fox_run.png",
	"res://assets/sprites/wildlife/boar_run.png",
	"res://assets/sprites/wildlife/deer_run.png",
]
const TERRAIN_SHADER_PATH := "res://resources/cozy_terrain.gdshader"
const HEX_SCALE := 10.0
const HEX_SPACING := 1.0 * HEX_SCALE
const HEX_ROW_HEIGHT := 0.866 * HEX_SCALE
const WALK_SURFACE_HEIGHT := 0.2
const WALK_SURFACE_TOP_Y := 0.2 * HEX_SCALE
const WALK_SURFACE_Y := WALK_SURFACE_TOP_Y - WALK_SURFACE_HEIGHT * 0.5
const OBSTACLE_RADIUS := HEX_SCALE * 0.43
const OBSTACLE_HEIGHT := 2.8
const SEA_MARGIN := 6
const TERRAIN_NOISE_SEED := 127
const TERRAIN_NOISE_FREQUENCY := 0.16
const OBJECT_Y := WALK_SURFACE_TOP_Y
const PORTAL_PIXEL_SIZE := 0.003
const PORTAL_GROUND_OFFSET_PIXELS := 513.0
const PORTAL_COLLISION_RADIUS := 0.8
const NATURE_DECORATION_SEED := 739
const WILDLIFE_SEED := 947
const NATURE_DENSITY := 1.0
const NATURE_SECONDARY_DENSITY := 0.9
const LOW_GRASS_INDEX := 3
const LOW_GRASS_DENSITY := 0.95
const LOW_GRASS_SECONDARY_DENSITY := 0.45
const STONE_DECOR_DENSITY := 0.9
const STONE_DECOR_SECONDARY_DENSITY := 0.35
const NATURE_OFFSET_X := 2.8
const NATURE_OFFSET_Z := 2.4
const INTERACTABLE_HEIGHT := 3.0
const INTERACTABLE_RADIUS := 3.0
const FILE_LABEL_HEIGHT := 4.2
const DIRECTORY_LABEL_HEIGHT := 3.2
const FILE_LABEL_DISTANCE := 26.0
const DIRECTORY_LABEL_DISTANCE := 55.0
const NATURE_REGIONS: Array[Rect2] = [
	Rect2(0, 0, 512, 512),
	Rect2(512, 0, 512, 512),
	Rect2(1024, 0, 512, 512),
	Rect2(0, 512, 512, 512),
	Rect2(512, 512, 512, 512),
	Rect2(1024, 512, 512, 512),
]
const NATURE_PIXEL_SIZES: Array[float] = [0.008, 0.0075, 0.007, 0.0055, 0.0055, 0.006]
const NATURE_GROUND_OFFSETS: Array[float] = [244.0, 244.0, 244.0, 162.0, 162.0, 162.0]
const NATURE_CASTS_SHADOW: Array[bool] = [true, true, true, false, false, false]
const NATURE_SWAY_AMPLITUDES: Array[float] = [1.8, 2.2, 1.0, 0.0, 0.0, 0.0]
const NATURE_SWAY_SPEEDS: Array[float] = [0.62, 0.72, 0.54, 0.0, 0.0, 0.0]
const NATURE_WEIGHTED_INDICES: Array[int] = [
	0,
	0,
	1,
	1,
	1,
	2,
	2,
	2,
	2,
	3,
	3,
	3,
	3,
	3,
	4,
	4,
	4,
	4,
	5,
	5,
	5,
]
const NATURE_SECONDARY_INDICES: Array[int] = [2, 3, 3, 3, 4, 4, 4, 5, 5]
const STONE_DECOR_REGIONS: Array[Rect2] = [
	Rect2(0, 0, 512, 1024),
	Rect2(512, 0, 512, 1024),
	Rect2(1024, 0, 512, 1024),
]
const STONE_DECOR_PIXEL_SIZES: Array[float] = [0.006, 0.006, 0.0055]
const STONE_DECOR_GROUND_OFFSETS: Array[float] = [112.0, 106.0, 104.0]
const WILDLIFE_FRAME_SIZE := 627
const WILDLIFE_TARGET_DENSITY := 1.0 / 18.0
const WILDLIFE_MIN_COUNT := 1
const WILDLIFE_MAX_COUNT := 16
const WILDLIFE_PIXEL_SIZES: Array[float] = [0.0045, 0.0047, 0.0044]
const WILDLIFE_GROUND_OFFSETS: Array[float] = [148.0, 145.0, 142.0]
const WILDLIFE_RUN_SPEEDS: Array[float] = [4.2, 3.4, 4.8]
const TEXTURED_STONE_BUILDING_MODELS: Array[String] = [
	"building-castle",
	"building-tower",
	"building-wall",
	"building-walls",
	"building-wizard-tower",
]
const SHADOWLESS_TERRAIN_MODELS := {
	"grass": true,
	"sand": true,
	"sand-desert": true,
	"stone": true,
	"water": true,
}
const PASSABLE_TERRAIN_MODELS: Array[String] = [
	"grass",
	"sand",
	"sand-desert",
	"stone",
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
var _terrain_noise := FastNoiseLite.new()
var _terrain_material: ShaderMaterial
var _building_material: ShaderMaterial
var _textured_stone_building_material: ShaderMaterial
var _nature_textures: Array[AtlasTexture] = []
var _stone_decor_textures: Array[AtlasTexture] = []
var _wildlife_frames: Array[SpriteFrames] = []
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
	_configure_terrain_noise(generation_seed)

	var island_cells := _generate_location_island(entries, generation_seed)
	_beach_model_by_coord = _generate_location_beaches(island_cells, generation_seed)
	_river_tile_by_coord = _generate_location_rivers(island_cells, generation_seed)
	_stone_model_by_coord = _generate_location_stones(island_cells, generation_seed)
	var placements := _generate_location_buildings(entries, island_cells, generation_seed)

	_render_location(parent, island_cells, placements, generation_seed)


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
	parent: Node3D, island_cells: Dictionary, placements: Array[Dictionary], generation_seed: int
) -> void:
	_render_tiles(parent, island_cells, placements)
	_add_walk_surface(parent, island_cells)
	_add_terrain_obstacles(parent, island_cells, placements)
	_add_nature_details(parent, island_cells, placements, generation_seed)
	_add_wildlife(parent, island_cells, placements, generation_seed)
	_add_repository_objects(parent, placements)


func _seed_with_offset(seed: int, offset: int) -> int:
	return int((seed + offset) & 0x7FFFFFFF)


func _render_tiles(parent: Node3D, island_cells: Dictionary, placements: Array[Dictionary]) -> void:
	var placement_by_coord := _placement_by_coord(placements)
	var sea_radius := _sea_radius(island_cells)
	for q in range(-sea_radius, sea_radius + 1):
		for r in range(-sea_radius, sea_radius + 1):
			var coord := Vector2i(q, r)
			if not _is_inside_radius(coord, sea_radius):
				continue

			var model_name := _tile_model(coord, island_cells, placement_by_coord)
			_terrain_model_by_coord[coord] = model_name
			_add_model(parent, model_name, _hex_to_world(coord), _tile_rotation(coord))


func _tile_model(
	coord: Vector2i, island_cells: Dictionary, placement_by_coord: Dictionary
) -> String:
	if not island_cells.has(coord):
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
	return _land_terrain_model(coord)


func _land_terrain_model(coord: Vector2i) -> String:
	if coord == Vector2i.ZERO:
		return "grass"
	if _beach_model_by_coord.has(coord):
		return _beach_model_by_coord[coord]
	if _stone_model_by_coord.has(coord):
		return _stone_model_by_coord[coord]

	var terrain_noise := _terrain_noise.get_noise_2d(float(coord.x), float(coord.y))
	var model_name := "grass"
	if terrain_noise < -0.36:
		model_name = "grass-hill"
	elif terrain_noise > 0.42:
		model_name = "grass-forest"
	elif terrain_noise > 0.28:
		model_name = "grass-hill"
	return model_name


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
		if entry["type"] != "directory":
			root.rotation.y = placement["rotation"]
		parent.add_child(root)

		if entry["type"] == "directory":
			_add_portal_sprite(root)
			_add_obstacle_collision(root, PORTAL_COLLISION_RADIUS, OBSTACLE_HEIGHT)

		_add_label(root, entry)
		_add_interactable(root, entry)


func _add_nature_details(
	parent: Node3D, island_cells: Dictionary, placements: Array[Dictionary], generation_seed: int
) -> void:
	var placement_by_coord := _placement_by_coord(placements)
	var rng := RandomNumberGenerator.new()
	rng.seed = _seed_with_offset(generation_seed, NATURE_DECORATION_SEED)

	for coord_value in island_cells:
		var coord: Vector2i = coord_value
		var model_name := String(_terrain_model_by_coord.get(coord, ""))
		if placement_by_coord.has(coord):
			if rng.randf() <= LOW_GRASS_DENSITY:
				_add_nature_sprite(parent, coord, LOW_GRASS_INDEX, rng)
			continue

		if model_name == "grass":
			if coord != Vector2i.ZERO:
				_add_general_nature_decorations(parent, coord, rng)
			_add_low_grass_decorations(parent, coord, rng)
		elif model_name.begins_with("stone"):
			_add_stone_decorations(parent, coord, rng)


func _add_general_nature_decorations(
	parent: Node3D, coord: Vector2i, rng: RandomNumberGenerator
) -> void:
	if rng.randf() <= NATURE_DENSITY:
		var index := NATURE_WEIGHTED_INDICES[rng.randi_range(0, NATURE_WEIGHTED_INDICES.size() - 1)]
		_add_nature_sprite(parent, coord, index, rng)
		if rng.randf() <= NATURE_SECONDARY_DENSITY:
			index = NATURE_SECONDARY_INDICES[rng.randi_range(
				0, NATURE_SECONDARY_INDICES.size() - 1
			)]
			_add_nature_sprite(parent, coord, index, rng)


func _add_low_grass_decorations(
	parent: Node3D, coord: Vector2i, rng: RandomNumberGenerator
) -> void:
	if rng.randf() <= LOW_GRASS_DENSITY:
		_add_nature_sprite(parent, coord, LOW_GRASS_INDEX, rng)
		if rng.randf() <= LOW_GRASS_SECONDARY_DENSITY:
			_add_nature_sprite(parent, coord, LOW_GRASS_INDEX, rng)


func _add_stone_decorations(parent: Node3D, coord: Vector2i, rng: RandomNumberGenerator) -> void:
	if rng.randf() > STONE_DECOR_DENSITY:
		return

	_add_stone_decor_sprite(parent, coord, rng.randi_range(0, 2), rng)
	if rng.randf() <= STONE_DECOR_SECONDARY_DENSITY:
		_add_stone_decor_sprite(parent, coord, rng.randi_range(0, 2), rng)


func _add_nature_sprite(
	parent: Node3D, coord: Vector2i, index: int, rng: RandomNumberGenerator
) -> void:
	var texture := _nature_texture(index)
	if texture == null:
		return

	var position := _hex_to_world(coord)
	var sprite: Sprite3D
	if NATURE_SWAY_AMPLITUDES[index] > 0.0:
		var swaying_sprite := SwayingSprite3D.new()
		swaying_sprite.configure_sway(
			rng.randf_range(0.0, TAU),
			NATURE_SWAY_AMPLITUDES[index] * rng.randf_range(0.86, 1.14),
			NATURE_SWAY_SPEEDS[index] * rng.randf_range(0.9, 1.1)
		)
		sprite = swaying_sprite
	else:
		sprite = Sprite3D.new()
	sprite.name = "Nature_%d_%d_%d" % [coord.x, coord.y, index]
	sprite.position = Vector3(
		position.x + rng.randf_range(-NATURE_OFFSET_X, NATURE_OFFSET_X),
		OBJECT_Y + 0.01,
		position.z + rng.randf_range(-NATURE_OFFSET_Z, NATURE_OFFSET_Z)
	)
	sprite.texture = texture
	sprite.offset.y = NATURE_GROUND_OFFSETS[index]
	sprite.pixel_size = NATURE_PIXEL_SIZES[index] * rng.randf_range(0.9, 1.08)
	sprite.flip_h = rng.randi_range(0, 1) == 1
	sprite.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	sprite.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR
	sprite.alpha_cut = SpriteBase3D.ALPHA_CUT_OPAQUE_PREPASS
	if not NATURE_CASTS_SHADOW[index]:
		sprite.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(sprite)


func _add_stone_decor_sprite(
	parent: Node3D, coord: Vector2i, index: int, rng: RandomNumberGenerator
) -> void:
	var texture := _stone_decor_texture(index)
	if texture == null:
		return

	var position := _hex_to_world(coord)
	var sprite := Sprite3D.new()
	sprite.name = "StoneDecor_%d_%d_%d" % [coord.x, coord.y, index]
	sprite.position = Vector3(
		position.x + rng.randf_range(-NATURE_OFFSET_X, NATURE_OFFSET_X),
		OBJECT_Y + 0.015,
		position.z + rng.randf_range(-NATURE_OFFSET_Z, NATURE_OFFSET_Z)
	)
	sprite.texture = texture
	sprite.offset.y = STONE_DECOR_GROUND_OFFSETS[index]
	sprite.pixel_size = STONE_DECOR_PIXEL_SIZES[index] * rng.randf_range(0.86, 1.1)
	sprite.flip_h = rng.randi_range(0, 1) == 1
	sprite.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	sprite.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR
	sprite.alpha_cut = SpriteBase3D.ALPHA_CUT_OPAQUE_PREPASS
	parent.add_child(sprite)


func _add_wildlife(
	parent: Node3D, island_cells: Dictionary, placements: Array[Dictionary], generation_seed: int
) -> void:
	var placement_by_coord := _placement_by_coord(placements)
	var candidates: Array[Vector2i] = []
	var traversable_coords := {}
	for coord_value in island_cells:
		var coord: Vector2i = coord_value
		if (
			coord != Vector2i.ZERO
			and not placement_by_coord.has(coord)
			and _terrain_model_by_coord.get(coord, "") == "grass"
		):
			candidates.append(coord)
			traversable_coords[coord] = true

	if candidates.is_empty():
		return

	var rng := RandomNumberGenerator.new()
	rng.seed = _seed_with_offset(generation_seed, WILDLIFE_SEED)
	_shuffle_coords(candidates, rng)
	var target_count := clampi(
		int(round(float(candidates.size()) * WILDLIFE_TARGET_DENSITY)),
		min(WILDLIFE_MIN_COUNT, candidates.size()),
		min(WILDLIFE_MAX_COUNT, candidates.size())
	)
	for index in range(target_count):
		_add_wild_animal(
			parent,
			candidates[index],
			index % WILDLIFE_TEXTURE_PATHS.size(),
			candidates,
			traversable_coords,
			rng
		)


func _add_wild_animal(
	parent: Node3D,
	coord: Vector2i,
	species_index: int,
	candidates: Array[Vector2i],
	traversable_coords: Dictionary,
	rng: RandomNumberGenerator
) -> void:
	var frames := _wild_animal_frames(species_index)
	if frames == null:
		return

	var route := _wildlife_route(coord, candidates, traversable_coords, rng)

	var animal := WildAnimal3D.new()
	animal.name = "WildAnimal_%d_%d_%d" % [coord.x, coord.y, species_index]
	animal.pixel_size = WILDLIFE_PIXEL_SIZES[species_index] * rng.randf_range(0.92, 1.08)
	animal.offset.y = WILDLIFE_GROUND_OFFSETS[species_index]
	animal.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	animal.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR
	animal.alpha_cut = SpriteBase3D.ALPHA_CUT_OPAQUE_PREPASS
	animal.configure_run(
		frames, route, WILDLIFE_RUN_SPEEDS[species_index] * rng.randf_range(0.9, 1.1), rng.randf()
	)
	parent.add_child(animal)


func _wildlife_route(
	start_coord: Vector2i,
	candidates: Array[Vector2i],
	traversable_coords: Dictionary,
	rng: RandomNumberGenerator
) -> PackedVector3Array:
	var route_coords: Array[Vector2i] = [start_coord]
	var current_coord := start_coord
	for _segment in range(4):
		var segment: Array[Vector2i] = []
		for _attempt in range(10):
			var destination := candidates[rng.randi_range(0, candidates.size() - 1)]
			if _hex_distance_between(current_coord, destination) < 5:
				continue
			segment = _grass_path(current_coord, destination, traversable_coords)
			if not segment.is_empty():
				break
		if segment.is_empty():
			continue
		for index in range(1, segment.size()):
			route_coords.append(segment[index])
		current_coord = segment.back()

	if route_coords.size() == 1:
		for direction in HEX_DIRECTIONS:
			var neighbor := start_coord + direction
			if traversable_coords.has(neighbor):
				route_coords.append(neighbor)
				break

	var route := PackedVector3Array()
	for route_coord in route_coords:
		var point := _hex_to_world(route_coord)
		point.x += rng.randf_range(-1.2, 1.2)
		point.z += rng.randf_range(-1.0, 1.0)
		point.y = OBJECT_Y + 0.04
		route.append(point)
	if route.size() == 1:
		route.append(route[0] + Vector3(3.0, 0.0, 0.0))
	return route


func _grass_path(
	start_coord: Vector2i, destination: Vector2i, traversable_coords: Dictionary
) -> Array[Vector2i]:
	var frontier: Array[Vector2i] = [start_coord]
	var came_from := {start_coord: start_coord}
	var frontier_index := 0
	while frontier_index < frontier.size():
		var current := frontier[frontier_index]
		frontier_index += 1
		if current == destination:
			break
		for direction in HEX_DIRECTIONS:
			var neighbor := current + direction
			if traversable_coords.has(neighbor) and not came_from.has(neighbor):
				came_from[neighbor] = current
				frontier.append(neighbor)

	if not came_from.has(destination):
		return []

	var path: Array[Vector2i] = []
	var current := destination
	while current != start_coord:
		path.push_front(current)
		current = came_from[current]
	path.push_front(start_coord)
	return path


func _hex_distance_between(first: Vector2i, second: Vector2i) -> int:
	var delta := first - second
	return maxi(abs(delta.x), maxi(abs(delta.y), abs(delta.x + delta.y)))


func _wild_animal_frames(species_index: int) -> SpriteFrames:
	while _wildlife_frames.size() <= species_index:
		_wildlife_frames.append(null)
	if _wildlife_frames[species_index] != null:
		return _wildlife_frames[species_index]

	var texture_path := WILDLIFE_TEXTURE_PATHS[species_index]
	if not ResourceLoader.exists(texture_path):
		push_warning("Missing wildlife sprite sheet: %s" % texture_path)
		return null

	var atlas := load(texture_path) as Texture2D
	var frames := SpriteFrames.new()
	frames.add_animation("run")
	frames.set_animation_loop("run", true)
	frames.set_animation_speed("run", 8.0)
	for row in range(2):
		for column in range(2):
			var frame := AtlasTexture.new()
			frame.atlas = atlas
			frame.region = Rect2(
				column * WILDLIFE_FRAME_SIZE,
				row * WILDLIFE_FRAME_SIZE,
				WILDLIFE_FRAME_SIZE,
				WILDLIFE_FRAME_SIZE
			)
			frames.add_frame("run", frame)
	_wildlife_frames[species_index] = frames
	return frames


func _shuffle_coords(coords: Array[Vector2i], rng: RandomNumberGenerator) -> void:
	for index in range(coords.size() - 1, 0, -1):
		var other := rng.randi_range(0, index)
		var value := coords[index]
		coords[index] = coords[other]
		coords[other] = value


func _nature_texture(index: int) -> Texture2D:
	if _nature_textures.is_empty():
		if not ResourceLoader.exists(NATURE_ATLAS_PATH):
			push_warning("Missing nature atlas: %s" % NATURE_ATLAS_PATH)
			return null

		var atlas := load(NATURE_ATLAS_PATH) as Texture2D
		for region in NATURE_REGIONS:
			var texture := AtlasTexture.new()
			texture.atlas = atlas
			texture.region = region
			_nature_textures.append(texture)
	return _nature_textures[index]


func _stone_decor_texture(index: int) -> Texture2D:
	if _stone_decor_textures.is_empty():
		if not ResourceLoader.exists(STONE_DECOR_ATLAS_PATH):
			push_warning("Missing stone decor atlas: %s" % STONE_DECOR_ATLAS_PATH)
			return null

		var atlas := load(STONE_DECOR_ATLAS_PATH) as Texture2D
		for region in STONE_DECOR_REGIONS:
			var texture := AtlasTexture.new()
			texture.atlas = atlas
			texture.region = region
			_stone_decor_textures.append(texture)
	return _stone_decor_textures[index]


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
	var material := _get_terrain_material()
	if _uses_roof_texture(model_name):
		material = _get_building_material()
	if _uses_textured_stone(model_name):
		material = _get_textured_stone_building_material()
	_apply_material(instance, material)
	if SHADOWLESS_TERRAIN_MODELS.has(model_name) or model_name.begins_with("river-"):
		_disable_shadow_casting(instance)
	parent.add_child(instance)


func _apply_material(root: Node, material: Material) -> void:
	if root is GeometryInstance3D:
		(root as GeometryInstance3D).material_override = material
	for node in root.find_children("*", "GeometryInstance3D", true, false):
		var geometry := node as GeometryInstance3D
		if geometry != null:
			geometry.material_override = material


func _get_terrain_material() -> ShaderMaterial:
	if _terrain_material == null:
		_terrain_material = ShaderMaterial.new()
		_terrain_material.shader = load(TERRAIN_SHADER_PATH) as Shader
		_terrain_material.set_shader_parameter(
			"palette_texture", load(MODEL_PALETTE_PATH) as Texture2D
		)
	return _terrain_material


func _get_building_material() -> ShaderMaterial:
	if _building_material == null:
		_building_material = _get_terrain_material().duplicate() as ShaderMaterial
		_building_material.set_shader_parameter("roof_enabled", 1.0)
	return _building_material


func _get_textured_stone_building_material() -> ShaderMaterial:
	if _textured_stone_building_material == null:
		_textured_stone_building_material = _get_building_material().duplicate() as ShaderMaterial
		_textured_stone_building_material.set_shader_parameter("building_enabled", 1.0)
	return _textured_stone_building_material


func _uses_roof_texture(model_name: String) -> bool:
	return model_name.begins_with("building-")


func _uses_textured_stone(model_name: String) -> bool:
	return model_name in TEXTURED_STONE_BUILDING_MODELS


func _disable_shadow_casting(root: Node) -> void:
	if root is GeometryInstance3D:
		(root as GeometryInstance3D).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	for node in root.find_children("*", "GeometryInstance3D", true, false):
		var geometry := node as GeometryInstance3D
		if geometry != null:
			geometry.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _add_portal_sprite(parent: Node3D) -> void:
	if not ResourceLoader.exists(PORTAL_TEXTURE_PATH):
		push_warning("Missing portal texture: %s" % PORTAL_TEXTURE_PATH)
		return

	var sprite := Sprite3D.new()
	sprite.name = "PortalSprite"
	sprite.texture = load(PORTAL_TEXTURE_PATH) as Texture2D
	sprite.offset.y = PORTAL_GROUND_OFFSET_PIXELS
	sprite.pixel_size = PORTAL_PIXEL_SIZE
	sprite.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	sprite.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR
	sprite.alpha_cut = SpriteBase3D.ALPHA_CUT_OPAQUE_PREPASS
	parent.add_child(sprite)


func _add_label(parent: Node3D, entry: Dictionary) -> void:
	var is_directory: bool = entry["type"] == "directory"
	var marker := Marker3D.new()
	marker.name = "LabelMarker"
	marker.position.y = DIRECTORY_LABEL_HEIGHT if is_directory else FILE_LABEL_HEIGHT
	parent.add_child(marker)

	var label_layer := parent.get_tree().get_first_node_in_group("world_label_layer") as Control
	if label_layer == null:
		push_warning("Missing world label UI layer")
		return

	var label := ScreenWorldLabel.new()
	label.name = "Label"
	label.configure(
		marker,
		"%s/" % entry["name"] if is_directory else entry["name"],
		is_directory,
		DIRECTORY_LABEL_DISTANCE if is_directory else FILE_LABEL_DISTANCE
	)
	label_layer.add_child(label)


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


func _configure_terrain_noise(generation_seed: int) -> void:
	_terrain_noise.seed = _seed_with_offset(generation_seed, TERRAIN_NOISE_SEED)
	_terrain_noise.frequency = TERRAIN_NOISE_FREQUENCY


func _sea_radius(island_cells: Dictionary) -> int:
	var island_radius := 0
	for coord in island_cells:
		var typed_coord := coord as Vector2i
		island_radius = maxi(island_radius, _hex_distance(typed_coord))
	return island_radius + SEA_MARGIN


func _hex_distance(coord: Vector2i) -> int:
	return maxi(abs(coord.x), maxi(abs(coord.y), abs(coord.x + coord.y)))


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
