extends Control

const POINTER_TEXTURE := preload("res://assets/cursors/pointer.png")
const COLOR_DEFAULT := Color(1.0, 1.0, 1.0, 0.95)
const COLOR_DIRECTORY := Color(0.35, 0.8, 1.0, 0.95)
const COLOR_INSPECT := Color(1.0, 0.95, 0.45, 0.95)
const COLOR_REFACTOR := Color(1.0, 0.35, 0.25, 0.95)
const COLOR_INVESTIGATE := Color(0.7, 0.45, 1.0, 0.95)
const CURSOR_SCALE := 2.0
const CURSOR_HOTSPOT := Vector2(1, 1)

var cursor_color := COLOR_DEFAULT


func _ready() -> void:
	add_to_group("game_cursor")
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	z_index = 1000
	set_anchors_preset(Control.PRESET_FULL_RECT)
	_update_mouse_mode()


func _process(_delta: float) -> void:
	_update_mouse_mode()
	queue_redraw()


func set_cursor_state(entry_type: String, tool_id: String) -> void:
	if entry_type == "directory":
		cursor_color = COLOR_DIRECTORY
	elif tool_id == ToolState.TOOL_REFACTOR:
		cursor_color = COLOR_REFACTOR
	elif tool_id == ToolState.TOOL_INVESTIGATE:
		cursor_color = COLOR_INVESTIGATE
	elif entry_type == "file":
		cursor_color = COLOR_INSPECT
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
	return (
		_is_group_panel_open("start_menu")
		or _is_group_panel_open("file_reader")
		or _is_group_panel_open("map_panel")
		or _is_group_panel_open("task_panel")
		or _is_group_panel_open("draft_preview")
		or _is_group_panel_open("diff_preview")
	)


func _is_group_panel_open(group_name: String) -> bool:
	for panel in get_tree().get_nodes_in_group(group_name):
		if panel.has_method("is_open") and panel.is_open():
			return true
	return false
