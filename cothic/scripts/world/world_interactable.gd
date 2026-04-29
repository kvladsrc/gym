class_name WorldInteractable
extends Area3D

@export var entry_name: String
@export var entry_path: String
@export var entry_type: String

var visual_root: Node3D
var default_scale := Vector3.ONE
var marked := false
var mark_label: Label3D


func _enter_tree() -> void:
	add_to_group("world_interactable")


func _ready() -> void:
	if get_parent() is Node3D:
		visual_root = get_parent()
		default_scale = visual_root.scale


func get_display_name() -> String:
	if entry_type == "directory":
		return "%s/" % entry_name

	return entry_name


func get_action_name() -> String:
	if entry_type == "directory":
		return "Open"

	return "Read"


func set_focused(is_focused: bool) -> void:
	if visual_root == null:
		return

	visual_root.scale = default_scale * 1.08 if is_focused else default_scale


func interact() -> void:
	if entry_type == "directory":
		var workspace := get_tree().get_first_node_in_group("workspace")
		if workspace != null and workspace.has_method("change_directory"):
			workspace.change_directory(entry_path)
		return

	get_tree().call_group("file_reader", "open_file", entry_path)


func use_tool() -> void:
	if entry_type != "file":
		return

	var tool_state := get_tree().get_first_node_in_group("tool_state")
	if tool_state != null and tool_state.has_method("apply_to_file"):
		tool_state.apply_to_file(entry_path)
		if tool_state.has_method("is_marked") and tool_state.is_marked(entry_path):
			set_marked(true)


func set_marked(is_marked: bool) -> void:
	if visual_root == null:
		return

	var marker_text := _get_marker_text()
	marked = is_marked and marker_text != ""

	if marked and mark_label == null:
		mark_label = Label3D.new()
		mark_label.name = "MarkLabel"
		mark_label.text = marker_text
		mark_label.font_size = 50
		mark_label.pixel_size = 0.004
		mark_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		mark_label.no_depth_test = true
		mark_label.modulate = Color(1.0, 0.55, 0.25)
		mark_label.outline_size = 10
		mark_label.outline_modulate = Color(0.05, 0.03, 0.02)
		mark_label.position = Vector3(0.0, 1.9, 0.0)
		visual_root.add_child(mark_label)
	elif marked and mark_label != null:
		mark_label.text = marker_text
	elif not marked and mark_label != null:
		mark_label.queue_free()
		mark_label = null


func _get_marker_text() -> String:
	var tool_state := get_tree().get_first_node_in_group("tool_state")
	if tool_state != null and tool_state.has_method("get_marker_label"):
		var label: String = tool_state.get_marker_label(entry_path)
		if label != "":
			return label

	return "MARK"
