class_name ScreenWorldLabel
extends Label

const DIRECTORY_COLOR := Color(1.0, 0.91, 0.65)
const FILE_COLOR := Color(1.0, 0.95, 0.8)
const OUTLINE_COLOR := Color(0.045, 0.025, 0.01)

var _max_distance := 0.0
var _target: Node3D


## Binds a crisp UI label to a marker in the filtered 3D world.
func configure(target: Node3D, label_text: String, is_directory: bool, max_distance: float) -> void:
	_target = target
	_max_distance = max_distance
	text = label_text
	horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_theme_font_size_override("font_size", 29 if is_directory else 24)
	add_theme_color_override("font_color", DIRECTORY_COLOR if is_directory else FILE_COLOR)
	add_theme_color_override("font_outline_color", OUTLINE_COLOR)
	add_theme_color_override("font_shadow_color", OUTLINE_COLOR)
	add_theme_constant_override("outline_size", 5)
	add_theme_constant_override("shadow_offset_x", 2)
	add_theme_constant_override("shadow_offset_y", 2)
	reset_size()


func _process(_delta: float) -> void:
	if not is_instance_valid(_target):
		queue_free()
		return

	var camera := get_viewport().get_camera_3d()
	if camera == null:
		visible = false
		return

	var target_position := _target.global_position
	var distance := camera.global_position.distance_to(target_position)
	if camera.is_position_behind(target_position) or distance >= _max_distance:
		visible = false
		return

	visible = true
	modulate.a = 1.0
	position = camera.unproject_position(target_position) - size * 0.5
