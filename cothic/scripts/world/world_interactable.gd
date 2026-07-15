class_name WorldInteractable
extends Area3D

@export var entry_name: String
@export var entry_path: String
@export var entry_type: String

var visual_root: Node3D
var default_scale := Vector3.ONE
var focus_tween: Tween


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

	if focus_tween != null and focus_tween.is_valid():
		focus_tween.kill()

	var target_scale := default_scale * 1.08 if is_focused else default_scale
	focus_tween = create_tween().set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	focus_tween.tween_property(visual_root, "scale", target_scale, 0.14)


func interact() -> void:
	if entry_type == "directory":
		var workspace := get_tree().get_first_node_in_group("workspace")
		if workspace != null and workspace.has_method("change_directory"):
			workspace.change_directory(entry_path)
		return

	get_tree().call_group("file_reader", "open_file", entry_path)
