extends CharacterBody3D

const MOVEMENT_FRAME_COUNT := 8
const MOVEMENT_ANIMATION_FPS := 8.0
const SPRINT_ANIMATION_SPEED := 1.35
const DIRECTION_COUNT := 8

@export var walk_speed: float = 4.0
@export var sprint_speed: float = 7.0
@export var gravity: float = 24.0
@export var turn_speed: float = 12.0
@export var spawn_position: Vector3 = Vector3.ZERO
@export var spawn_yaw: float = 0.0
@export var cursor_ground_y: float = 2.0
@export var cursor_hover_radius: float = 3.2
@export var fall_respawn_y: float = -12.0
@export var camera_zoom_step: float = 1.5
@export var camera_zoom_min: float = 7.0
@export var camera_zoom_max: float = 22.0
@export var use_2d_visual: bool = true

var focused_interactable: WorldInteractable
var _sprite_direction_row := 0
var _sprite_animation_position := 0.0

@onready var visual_root: Node3D = $VisualRoot
@onready var ranger_model: Node3D = $VisualRoot/Ranger
@onready var knight_sprite: Sprite3D = $VisualRoot/KnightSprite
@onready var spring_arm: SpringArm3D = $CameraPivot/SpringArm3D
@onready var camera: Camera3D = $CameraPivot/SpringArm3D/Camera3D


func _ready() -> void:
	add_to_group("player")
	_sync_player_visual()

	var workspace := get_tree().get_first_node_in_group("workspace")
	if workspace != null and workspace.has_signal("current_path_changed"):
		workspace.current_path_changed.connect(_on_workspace_current_path_changed)


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("toggle_visual"):
		use_2d_visual = not use_2d_visual
		_sync_player_visual()
		get_viewport().set_input_as_handled()
		return

	if _is_modal_open():
		return

	_handle_camera_zoom(event)

	if event.is_action_pressed("interact"):
		_update_focused_interactable()
	if event.is_action_pressed("interact") and focused_interactable != null:
		var interacted_type := focused_interactable.entry_type
		focused_interactable.interact()
		if interacted_type == "directory":
			_clear_interaction_focus()

	if event.is_action_pressed("open_map"):
		get_tree().call_group("map_panel", "toggle")


func _physics_process(delta: float) -> void:
	if global_position.y < fall_respawn_y:
		_respawn_at_start()
		return

	if _is_gameplay_blocked():
		_clear_interaction_prompt()
		_update_sprite_animation(Vector2.ZERO, false, delta)
		velocity.x = 0.0
		velocity.z = 0.0
		if not is_on_floor():
			velocity.y -= gravity * delta
		else:
			velocity.y = -0.1
		move_and_slide()
		return

	var input_dir := Vector2.ZERO

	if Input.is_action_pressed("move_forward"):
		input_dir.y -= 1.0
	if Input.is_action_pressed("move_backward"):
		input_dir.y += 1.0
	if Input.is_action_pressed("move_left"):
		input_dir.x -= 1.0
	if Input.is_action_pressed("move_right"):
		input_dir.x += 1.0

	input_dir = input_dir.normalized()

	var direction := _screen_input_to_world_direction(input_dir)

	var sprinting := Input.is_action_pressed("sprint")
	var speed := walk_speed
	if sprinting:
		speed = sprint_speed

	velocity.x = direction.x * speed
	velocity.z = direction.z * speed

	if not is_on_floor():
		velocity.y -= gravity * delta
	else:
		velocity.y = -0.1

	move_and_slide()
	_update_sprite_animation(input_dir, sprinting, delta)
	_face_movement_direction(direction, delta)
	_update_focused_interactable()


func _update_focused_interactable() -> void:
	var closest := _cursor_focused_interactable()

	if closest == focused_interactable:
		_update_cursor_state()
		return

	if focused_interactable != null and is_instance_valid(focused_interactable):
		focused_interactable.set_focused(false)

	focused_interactable = closest
	if focused_interactable != null:
		focused_interactable.set_focused(true)
		_show_interaction_prompt(focused_interactable)
	else:
		_clear_interaction_prompt()
	_update_cursor_state()


func _clear_interaction_focus() -> void:
	if focused_interactable != null and is_instance_valid(focused_interactable):
		focused_interactable.set_focused(false)

	focused_interactable = null
	_clear_interaction_prompt()
	_update_cursor_state()


func _show_interaction_prompt(interactable: WorldInteractable) -> void:
	get_tree().call_group(
		"interaction_prompt",
		"show_prompt",
		interactable.get_action_name(),
		interactable.get_display_name()
	)


func _clear_interaction_prompt() -> void:
	get_tree().call_group("interaction_prompt", "clear_prompt")


func _on_workspace_current_path_changed(_path: String) -> void:
	_respawn_at_start()
	_clear_interaction_focus()


func _respawn_at_start() -> void:
	global_position = spawn_position
	rotation.y = spawn_yaw
	visual_root.rotation.y = 0.0
	velocity = Vector3.ZERO
	_reset_sprite_animation()


func _sync_player_visual() -> void:
	knight_sprite.visible = use_2d_visual
	ranger_model.visible = not use_2d_visual
	_reset_sprite_animation()


func _update_sprite_animation(input_direction: Vector2, sprinting: bool, delta: float) -> void:
	if not use_2d_visual:
		return

	if input_direction.length_squared() < 0.001:
		_sprite_animation_position = 0.0
		_set_sprite_frame(0)
		return

	_sprite_direction_row = _direction_row_for_input(input_direction)
	var playback_speed := SPRINT_ANIMATION_SPEED if sprinting else 1.0
	_sprite_animation_position += delta * MOVEMENT_ANIMATION_FPS * playback_speed
	_set_sprite_frame(int(_sprite_animation_position) % MOVEMENT_FRAME_COUNT)


func _reset_sprite_animation() -> void:
	_sprite_animation_position = 0.0
	_set_sprite_frame(0)


func _set_sprite_frame(animation_frame: int) -> void:
	knight_sprite.frame = _sprite_direction_row * MOVEMENT_FRAME_COUNT + animation_frame


func _direction_row_for_input(input_direction: Vector2) -> int:
	var angle := atan2(-input_direction.x, input_direction.y)
	return wrapi(roundi(angle / (PI / 4.0)), 0, DIRECTION_COUNT)


func _face_movement_direction(direction: Vector3, delta: float) -> void:
	if use_2d_visual or direction.length_squared() < 0.001:
		return

	var target_yaw := atan2(direction.x, direction.z)
	var blend := 1.0 - exp(-turn_speed * delta)
	visual_root.rotation.y = lerp_angle(visual_root.rotation.y, target_yaw, blend)


func _screen_input_to_world_direction(input_dir: Vector2) -> Vector3:
	if input_dir.length_squared() < 0.001:
		return Vector3.ZERO

	var screen_right := camera.global_basis.x
	var screen_up := camera.global_basis.y
	screen_right.y = 0.0
	screen_up.y = 0.0
	screen_right = screen_right.normalized()
	screen_up = screen_up.normalized()

	return (screen_right * input_dir.x + screen_up * -input_dir.y).normalized()


func _zoom_camera(delta: float) -> void:
	spring_arm.spring_length = clampf(
		spring_arm.spring_length + delta, camera_zoom_min, camera_zoom_max
	)


func _handle_camera_zoom(event: InputEvent) -> void:
	if not event is InputEventMouseButton:
		return
	if not event.pressed:
		return

	if event.button_index == MOUSE_BUTTON_WHEEL_UP:
		_zoom_camera(-camera_zoom_step)
		get_viewport().set_input_as_handled()
	elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
		_zoom_camera(camera_zoom_step)
		get_viewport().set_input_as_handled()


func _cursor_focused_interactable() -> WorldInteractable:
	var cursor_point_value: Variant = _cursor_ground_point()
	if cursor_point_value == null:
		return null
	var cursor_point := cursor_point_value as Vector3

	var closest: WorldInteractable
	var closest_distance := cursor_hover_radius * cursor_hover_radius
	for node in get_tree().get_nodes_in_group("world_interactable"):
		var interactable := node as WorldInteractable
		if interactable == null or not is_instance_valid(interactable):
			continue

		var delta := Vector2(
			interactable.global_position.x - cursor_point.x,
			interactable.global_position.z - cursor_point.z
		)
		var distance := delta.length_squared()
		if distance < closest_distance:
			closest = interactable
			closest_distance = distance
	return closest


func _cursor_ground_point() -> Variant:
	var mouse_position := get_viewport().get_mouse_position()
	var ray_origin := camera.project_ray_origin(mouse_position)
	var ray_direction := camera.project_ray_normal(mouse_position)
	if abs(ray_direction.y) < 0.001:
		return null

	var distance := (cursor_ground_y - ray_origin.y) / ray_direction.y
	if distance < 0.0:
		return null

	return ray_origin + ray_direction * distance


func _update_cursor_state() -> void:
	var entry_type := ""
	if focused_interactable != null:
		entry_type = focused_interactable.entry_type
	get_tree().call_group("game_cursor", "set_cursor_state", entry_type)


func _is_gameplay_blocked() -> bool:
	return _is_modal_open()


func _is_modal_open() -> bool:
	for node in get_tree().get_nodes_in_group("ui_modal"):
		var control := node as Control
		if control != null and control.visible:
			return true

	return false
