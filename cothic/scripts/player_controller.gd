extends CharacterBody3D

@export var walk_speed: float = 4.0
@export var sprint_speed: float = 7.0
@export var gravity: float = 24.0
@export var spawn_position: Vector3 = Vector3.ZERO
@export var spawn_yaw: float = 0.0
@export var cursor_ground_y: float = 2.0
@export var cursor_hover_radius: float = 3.2
@export var fall_respawn_y: float = -12.0
@export var camera_zoom_step: float = 1.5
@export var camera_zoom_min: float = 10.0
@export var camera_zoom_max: float = 22.0

var focused_interactable: WorldInteractable

@onready var visual_root: Node3D = $VisualRoot
@onready var spring_arm: SpringArm3D = $CameraPivot/SpringArm3D
@onready var camera: Camera3D = $CameraPivot/SpringArm3D/Camera3D


func _ready() -> void:
	add_to_group("player")

	var workspace := get_tree().get_first_node_in_group("workspace")
	if workspace != null and workspace.has_signal("current_path_changed"):
		workspace.current_path_changed.connect(_on_workspace_current_path_changed)


func _unhandled_input(event: InputEvent) -> void:
	if _is_start_menu_open():
		return

	if _is_draft_preview_open():
		return

	if _is_diff_preview_open():
		return

	if _is_task_panel_open():
		if event.is_action_pressed("open_tasks") or event.is_action_pressed("ui_cancel"):
			get_tree().call_group("task_panel", "close")
			get_viewport().set_input_as_handled()
		return

	if _is_map_open():
		if event.is_action_pressed("open_map") or event.is_action_pressed("ui_cancel"):
			get_tree().call_group("map_panel", "close")
			get_viewport().set_input_as_handled()
		return

	if _is_file_reader_open():
		return

	_handle_camera_zoom(event)

	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		_update_focused_interactable()
		if focused_interactable != null and focused_interactable.entry_type == "file":
			focused_interactable.use_tool()

	if event is InputEventKey and event.pressed and not event.echo:
		if event.physical_keycode == KEY_1:
			_set_active_tool_slot(1)
		if event.physical_keycode == KEY_2:
			_set_active_tool_slot(2)
		if event.physical_keycode == KEY_3:
			_set_active_tool_slot(3)

	if event.is_action_pressed("interact"):
		_update_focused_interactable()
	if event.is_action_pressed("interact") and focused_interactable != null:
		var interacted_type := focused_interactable.entry_type
		focused_interactable.interact()
		if interacted_type == "directory":
			_clear_interaction_focus()

	if event.is_action_pressed("open_map"):
		get_tree().call_group("map_panel", "toggle")

	if event.is_action_pressed("open_tasks"):
		get_tree().call_group("task_panel", "toggle")


func _physics_process(delta: float) -> void:
	if global_position.y < fall_respawn_y:
		_respawn_at_start()
		return

	if (
		_is_start_menu_open()
		or _is_file_reader_open()
		or _is_map_open()
		or _is_task_panel_open()
		or _is_draft_preview_open()
		or _is_diff_preview_open()
	):
		_clear_interaction_prompt()
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

	var speed := walk_speed
	if Input.is_action_pressed("sprint"):
		speed = sprint_speed

	velocity.x = direction.x * speed
	velocity.z = direction.z * speed

	if not is_on_floor():
		velocity.y -= gravity * delta
	else:
		velocity.y = -0.1

	move_and_slide()
	_face_movement_direction(direction)
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


func _set_active_tool(tool_id: String) -> void:
	var tool_state := get_tree().get_first_node_in_group("tool_state")
	if tool_state != null and tool_state.has_method("set_active_tool"):
		tool_state.set_active_tool(tool_id)
		_update_cursor_state()
		if focused_interactable != null:
			_show_interaction_prompt(focused_interactable)


func _set_active_tool_slot(slot: int) -> void:
	var tool_state := get_tree().get_first_node_in_group("tool_state")
	if tool_state != null and tool_state.has_method("set_active_tool_slot"):
		tool_state.set_active_tool_slot(slot)
		_update_cursor_state()
		if focused_interactable != null:
			_show_interaction_prompt(focused_interactable)


func _on_workspace_current_path_changed(_path: String) -> void:
	_respawn_at_start()
	_clear_interaction_focus()


func _respawn_at_start() -> void:
	global_position = spawn_position
	rotation.y = spawn_yaw
	visual_root.rotation.y = 0.0
	velocity = Vector3.ZERO


func _face_movement_direction(direction: Vector3) -> void:
	if direction.length_squared() < 0.001:
		return

	visual_root.rotation.y = atan2(direction.x, direction.z)


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
	var tool_id := ""
	var tool_state := get_tree().get_first_node_in_group("tool_state")
	if tool_state != null and tool_state.has_method("get_active_tool"):
		tool_id = tool_state.get_active_tool()

	if focused_interactable != null:
		entry_type = focused_interactable.entry_type
	get_tree().call_group("game_cursor", "set_cursor_state", entry_type, tool_id)


func _is_file_reader_open() -> bool:
	var readers := get_tree().get_nodes_in_group("file_reader")
	for reader in readers:
		if reader.has_method("is_open") and reader.is_open():
			return true

	return false


func _is_start_menu_open() -> bool:
	var panels := get_tree().get_nodes_in_group("start_menu")
	for panel in panels:
		if panel.has_method("is_open") and panel.is_open():
			return true

	return false


func _is_map_open() -> bool:
	var panels := get_tree().get_nodes_in_group("map_panel")
	for panel in panels:
		if panel.has_method("is_open") and panel.is_open():
			return true

	return false


func _is_task_panel_open() -> bool:
	var panels := get_tree().get_nodes_in_group("task_panel")
	for panel in panels:
		if panel.has_method("is_open") and panel.is_open():
			return true

	return false


func _is_draft_preview_open() -> bool:
	var panels := get_tree().get_nodes_in_group("draft_preview")
	for panel in panels:
		if panel.has_method("is_open") and panel.is_open():
			return true

	return false


func _is_diff_preview_open() -> bool:
	var panels := get_tree().get_nodes_in_group("diff_preview")
	for panel in panels:
		if panel.has_method("is_open") and panel.is_open():
			return true

	return false
