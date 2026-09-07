class_name Workspace
extends Node

signal current_path_changed(path: String)

const REPO_ROOT_ARG := "--repo-root"
const SETTINGS_PATH := "user://workspace.cfg"
const SETTINGS_SECTION := "workspace"
const LAST_ROOT_KEY := "last_root_path"

@export var use_default_root_on_start := false
@export_dir var default_root_path: String = "res://"

var root_path: String
var current_path: String
var visited_paths: Array[String] = []
var _saved_root_path := ""


func _enter_tree() -> void:
	add_to_group("workspace")


func _ready() -> void:
	_saved_root_path = _load_saved_root_path()
	var configured_root := _get_repo_root_from_args()
	if configured_root == "" and use_default_root_on_start:
		configured_root = default_root_path

	if configured_root != "" and not set_root(configured_root):
		push_error("Failed to configure workspace root: %s" % configured_root)


func set_root(path: String) -> bool:
	var resolved_path := _resolve_path(path)
	if resolved_path == "" or not DirAccess.dir_exists_absolute(resolved_path):
		return false

	root_path = resolved_path
	current_path = root_path
	visited_paths = [current_path]
	_save_root_path(root_path)
	current_path_changed.emit(current_path)
	return true


func can_access(path: String) -> bool:
	if root_path == "":
		return false

	var resolved_path := _resolve_path(path)
	if resolved_path == "":
		return false

	return resolved_path == root_path or resolved_path.begins_with("%s/" % root_path)


func change_directory(path: String) -> bool:
	var resolved_path := _resolve_path(path)
	if not can_access(resolved_path):
		push_warning("Refusing to leave workspace root: %s" % path)
		return false

	if not DirAccess.dir_exists_absolute(resolved_path):
		push_warning("Directory does not exist: %s" % resolved_path)
		return false

	current_path = resolved_path
	_mark_visited(current_path)
	current_path_changed.emit(current_path)
	return true


func get_visited_paths() -> Array[String]:
	return visited_paths.duplicate()


func get_saved_root_path() -> String:
	return _saved_root_path


func get_parent_path() -> String:
	if current_path == root_path:
		return ""

	var parent_path := current_path.get_base_dir().simplify_path()
	if can_access(parent_path):
		return parent_path

	return ""


func get_relative_path(path: String = current_path) -> String:
	var resolved_path := _resolve_path(path)
	if resolved_path == root_path:
		return "."

	if resolved_path.begins_with("%s/" % root_path):
		return resolved_path.substr(root_path.length() + 1)

	return resolved_path


func _get_repo_root_from_args() -> String:
	var args := OS.get_cmdline_user_args()
	for index in args.size():
		if args[index] == REPO_ROOT_ARG and index + 1 < args.size():
			return args[index + 1]

	return ""


func _load_saved_root_path() -> String:
	var config := ConfigFile.new()
	var error := config.load(SETTINGS_PATH)
	if error != OK:
		return ""

	var value := String(config.get_value(SETTINGS_SECTION, LAST_ROOT_KEY, ""))
	return _resolve_path(value)


func _save_root_path(path: String) -> void:
	_saved_root_path = path
	var config := ConfigFile.new()
	config.set_value(SETTINGS_SECTION, LAST_ROOT_KEY, path)
	var error := config.save(SETTINGS_PATH)
	if error != OK:
		push_warning("Failed to save workspace settings: %s" % error_string(error))


func _resolve_path(path: String) -> String:
	if path == "":
		return ""

	var resolved_path := path
	if resolved_path.begins_with("res://") or resolved_path.begins_with("user://"):
		resolved_path = ProjectSettings.globalize_path(resolved_path)

	return resolved_path.simplify_path().trim_suffix("/")


func _mark_visited(path: String) -> void:
	if not visited_paths.has(path):
		visited_paths.append(path)
