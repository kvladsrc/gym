extends Control

const POINTER_TEXTURE := preload("res://assets/cursors/pointer.png")
const COLOR_DEFAULT := Color(1.0, 1.0, 1.0, 0.95)
const COLOR_DIRECTORY := Color(0.35, 0.8, 1.0, 0.95)
const COLOR_FILE := Color(1.0, 0.95, 0.45, 0.95)
const CURSOR_SCALE := 2.0
const CURSOR_HOTSPOT := Vector2(1, 1)

var cursor_color := COLOR_DEFAULT


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	add_to_group("game_cursor")
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	z_index = 1000
	set_anchors_preset(Control.PRESET_FULL_RECT)
	_update_mouse_mode()


func _process(_delta: float) -> void:
	_update_mouse_mode()
	queue_redraw()


func set_cursor_state(entry_type: String) -> void:
	if entry_type == "directory":
		cursor_color = COLOR_DIRECTORY
	elif entry_type == "file":
		cursor_color = COLOR_FILE
	else:
		cursor_color = COLOR_DEFAULT
	queue_redraw()


func _draw() -> void:
	if _is_ui_panel_open():
		return

	var center := get_viewport().get_mouse_position()
	var texture_size := Vector2(POINTER_TEXTURE.get_size()) * CURSOR_SCALE
	var cursor_rect := Rect2(center - CURSOR_HOTSPOT * CURSOR_SCALE, texture_size)
	draw_texture_rect(POINTER_TEXTURE, cursor_rect, false, cursor_color)


func _update_mouse_mode() -> void:
	var target_mode := Input.MOUSE_MODE_VISIBLE
	if not _is_ui_panel_open():
		target_mode = Input.MOUSE_MODE_HIDDEN

	if Input.get_mouse_mode() != target_mode:
		Input.set_mouse_mode(target_mode)


func _is_ui_panel_open() -> bool:
	for node in get_tree().get_nodes_in_group("ui_modal"):
		var control := node as Control
		if control != null and control.visible:
			return true
	return false
