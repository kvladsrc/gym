extends Node3D

var current_path: String
var workspace: Workspace
var hex_level_builder := HexLevelBuilder.new()
var session_seed := 0


func _ready() -> void:
	add_to_group("area_generator")
	session_seed = _random_session_seed()
	workspace = get_tree().get_first_node_in_group("workspace")
	if workspace != null:
		workspace.current_path_changed.connect(generate)
		if workspace.current_path != "":
			generate(workspace.current_path)
	else:
		push_error("AreaGenerator requires a Workspace node.")


func generate(path: String) -> void:
	current_path = path
	_clear_generated_children()
	hex_level_builder.global_seed = session_seed

	var entries := RepositoryScanner.scan_directory(path)
	var parent_path := workspace.get_parent_path() if workspace != null else ""
	if parent_path != "":
		(
			entries
			. push_front(
				{
					"name": "..",
					"path": parent_path,
					"type": "directory",
				}
			)
		)
	hex_level_builder.build(self, entries, _area_seed(path))


func refresh_markers() -> void:
	pass


func _clear_generated_children() -> void:
	for child in get_children():
		child.queue_free()


func _random_session_seed() -> int:
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	return rng.randi()


func _area_seed(path: String) -> int:
	return int((session_seed + path.hash()) & 0x7FFFFFFF)
