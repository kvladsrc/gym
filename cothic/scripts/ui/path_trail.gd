extends Control

const HERO_ATLAS: Texture2D = preload("res://assets/sprites/knight/walk_v10.png")
const HERO_HEAD_REGION := Rect2(38.0, 4.0, 52.0, 52.0)
const STOP_WIDTH := 142.0
const CONNECTOR_WIDTH := 66.0
const TRAIL_COLOR := Color(0.92, 0.7, 0.34)
const VISITED_COLOR := Color(0.96, 0.79, 0.48)
const CURRENT_COLOR := Color(1.0, 0.94, 0.72)
const OUTLINE_COLOR := Color(0.055, 0.032, 0.014)
const OUTLINE_SIZE := 6

var workspace: Workspace

@onready var trail: HBoxContainer = %Trail


func _ready() -> void:
	workspace = get_tree().get_first_node_in_group("workspace")
	if workspace != null:
		workspace.current_path_changed.connect(_on_current_path_changed)
	_rebuild()


func _on_current_path_changed(_path: String) -> void:
	_rebuild()


func _rebuild() -> void:
	for child in trail.get_children():
		trail.remove_child(child)
		child.queue_free()

	if workspace == null or workspace.root_path == "" or workspace.current_path == "":
		visible = false
		return

	visible = true
	var paths := _current_path_chain()
	for index in paths.size():
		if index > 0:
			_add_connector()
		_add_stop(paths[index], index == paths.size() - 1)


func _current_path_chain() -> Array[String]:
	var paths: Array[String] = [workspace.root_path]
	var relative_path := workspace.get_relative_path()
	if relative_path == ".":
		return paths

	var path := workspace.root_path
	for part in relative_path.split("/", false):
		path = path.path_join(part)
		paths.append(path)
	return paths


func _add_stop(path: String, is_current: bool) -> void:
	var stop := VBoxContainer.new()
	stop.custom_minimum_size = Vector2(STOP_WIDTH, 102.0)
	stop.alignment = BoxContainer.ALIGNMENT_CENTER
	stop.add_theme_constant_override("separation", 1)

	if is_current:
		var hero := TextureRect.new()
		hero.custom_minimum_size = Vector2(60.0, 60.0)
		hero.texture = _hero_frame()
		hero.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		hero.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		hero.mouse_filter = Control.MOUSE_FILTER_IGNORE
		stop.add_child(hero)
	else:
		var marker := Button.new()
		marker.custom_minimum_size = Vector2(60.0, 60.0)
		marker.text = "●"
		marker.flat = true
		marker.focus_mode = Control.FOCUS_NONE
		marker.tooltip_text = path
		marker.add_theme_font_size_override("font_size", 34)
		marker.add_theme_color_override("font_color", VISITED_COLOR)
		marker.add_theme_color_override("font_hover_color", CURRENT_COLOR)
		marker.add_theme_color_override("font_outline_color", OUTLINE_COLOR)
		marker.add_theme_constant_override("outline_size", OUTLINE_SIZE)
		marker.pressed.connect(_on_path_pressed.bind(path))
		stop.add_child(marker)

	var label := Label.new()
	label.text = _path_label(path)
	label.tooltip_text = path
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	label.add_theme_font_size_override("font_size", 24)
	label.add_theme_color_override("font_color", CURRENT_COLOR if is_current else VISITED_COLOR)
	label.add_theme_color_override("font_outline_color", OUTLINE_COLOR)
	label.add_theme_constant_override("outline_size", OUTLINE_SIZE)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	stop.add_child(label)
	trail.add_child(stop)


func _add_connector() -> void:
	var connector := VBoxContainer.new()
	connector.custom_minimum_size = Vector2(CONNECTOR_WIDTH, 102.0)

	var top_space := Control.new()
	top_space.custom_minimum_size.y = 29.0
	connector.add_child(top_space)

	var line_outline := ColorRect.new()
	line_outline.custom_minimum_size = Vector2(CONNECTOR_WIDTH, 6.0)
	line_outline.color = OUTLINE_COLOR
	line_outline.mouse_filter = Control.MOUSE_FILTER_IGNORE

	var line := ColorRect.new()
	line.anchors_preset = Control.PRESET_FULL_RECT
	line.offset_top = 2.0
	line.offset_bottom = -2.0
	line.color = TRAIL_COLOR
	line.mouse_filter = Control.MOUSE_FILTER_IGNORE
	line_outline.add_child(line)
	connector.add_child(line_outline)
	trail.add_child(connector)


func _path_label(path: String) -> String:
	if path == workspace.root_path:
		var root_name := path.get_file()
		return root_name if root_name != "" else path
	return path.get_file()


func _hero_frame() -> AtlasTexture:
	var texture := AtlasTexture.new()
	texture.atlas = HERO_ATLAS
	texture.region = HERO_HEAD_REGION
	return texture


func _on_path_pressed(path: String) -> void:
	if workspace != null:
		workspace.change_directory(path)
