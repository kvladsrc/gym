class_name RepositoryScanner
extends RefCounted

const DEFAULT_EXCLUDED_NAMES := {
	".git": true,
	".jj": true,
	".godot": true,
	".cache": true,
	"node_modules": true,
	"target": true,
	"tmp": true,
}

const DEFAULT_EXCLUDED_EXTENSIONS := {
	"import": true,
	"uid": true,
}


static func scan_directory(
	path: String, excluded_names: Dictionary = DEFAULT_EXCLUDED_NAMES
) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	var dir := DirAccess.open(path)
	if dir == null:
		push_error("Cannot open directory: %s" % path)
		return entries

	dir.list_dir_begin()
	var entry_name := dir.get_next()
	while entry_name != "":
		if not _should_exclude(entry_name, dir, excluded_names):
			var entry_path := path.path_join(entry_name)
			var entry_type := "directory" if dir.current_is_dir() else "file"
			(
				entries
				. append(
					{
						"name": entry_name,
						"path": entry_path,
						"type": entry_type,
					}
				)
			)

		entry_name = dir.get_next()
	dir.list_dir_end()

	entries.sort_custom(_sort_entries)
	return entries


static func scan_directory_tree(
	root_path: String, excluded_names: Dictionary = DEFAULT_EXCLUDED_NAMES
) -> Array[Dictionary]:
	var directories: Array[Dictionary] = []
	_scan_directory_tree(root_path, root_path, directories, excluded_names)
	return directories


static func _should_exclude(entry_name: String, dir: DirAccess, excluded_names: Dictionary) -> bool:
	if excluded_names.has(entry_name):
		return true

	if dir.is_link(entry_name):
		return true

	var extension := entry_name.get_extension()
	if DEFAULT_EXCLUDED_EXTENSIONS.has(extension):
		return true

	return false


static func _sort_entries(left: Dictionary, right: Dictionary) -> bool:
	if left["type"] != right["type"]:
		return left["type"] == "directory"

	return String(left["name"]).naturalnocasecmp_to(String(right["name"])) < 0


static func _scan_directory_tree(
	root_path: String,
	path: String,
	directories: Array[Dictionary],
	excluded_names: Dictionary,
	depth: int = 0
) -> void:
	var name := "." if path == root_path else path.get_file()
	(
		directories
		. append(
			{
				"name": name,
				"path": path,
				"type": "directory",
				"depth": depth,
			}
		)
	)

	var entries := scan_directory(path, excluded_names)
	for entry in entries:
		if entry["type"] == "directory":
			_scan_directory_tree(root_path, entry["path"], directories, excluded_names, depth + 1)
