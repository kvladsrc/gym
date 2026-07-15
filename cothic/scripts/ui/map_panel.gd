extends PanelContainer

const DIRECTORIES_PER_FRAME := 24

var workspace: Workspace
var _scan_generation := 0

@onready var path_list: VBoxContainer = %PathList


func _ready() -> void:
	add_to_group("map_panel")
	add_to_group("ui_modal")
	workspace = get_tree().get_first_node_in_group("workspace")
	close()


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return

	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("open_map"):
		close()
		get_viewport().set_input_as_handled()


func toggle() -> void:
	if visible:
		close()
	else:
		open()


func open() -> void:
	if workspace == null:
		workspace = get_tree().get_first_node_in_group("workspace")

	visible = true
	get_tree().call_group("interaction_prompt", "clear_prompt")
	_rebuild_path_list()


func close() -> void:
	_scan_generation += 1
	visible = false


func _rebuild_path_list() -> void:
	_scan_generation += 1
	var generation := _scan_generation

	for child in path_list.get_children():
		child.queue_free()

	if workspace == null:
		return

	if workspace.root_path == "":
		return

	_add_status_row("Scanning repository directories...")
	_scan_directory_tree_incrementally(generation)


func _scan_directory_tree_incrementally(generation: int) -> void:
	await get_tree().process_frame

	var root_path := workspace.root_path
	var stack: Array[Dictionary] = [
		{
			"path": root_path,
			"depth": 0,
		}
	]
	var added_count := 0

	for child in path_list.get_children():
		child.queue_free()

	while not stack.is_empty():
		if generation != _scan_generation or not visible:
			return

		var processed_this_frame := 0
		while processed_this_frame < DIRECTORIES_PER_FRAME and not stack.is_empty():
			var item: Dictionary = stack.pop_back()
			var path := String(item["path"])
			var depth := int(item["depth"])
			_add_path_row(path, depth)
			added_count += 1
			processed_this_frame += 1

			var child_dirs := _child_directories(path, depth + 1)
			for index in range(child_dirs.size() - 1, -1, -1):
				stack.append(child_dirs[index])

		await get_tree().process_frame

	if added_count == 0:
		_add_status_row("No directories found.")


func _child_directories(path: String, depth: int) -> Array[Dictionary]:
	var children: Array[Dictionary] = []
	for entry in RepositoryScanner.scan_directory(path):
		if entry["type"] != "directory":
			continue
		(
			children
			. append(
				{
					"path": entry["path"],
					"depth": depth,
				}
			)
		)
	return children


func _add_path_row(path: String, depth: int) -> void:
	var button := Button.new()
	var path_name := path.get_file()
	if path == workspace.root_path:
		path_name = path_name if path_name != "" else path
	button.text = (
		"%s%s%s" % [_indent(depth), "◆  " if path == workspace.current_path else "", path_name]
	)
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.disabled = path == workspace.current_path
	button.pressed.connect(_on_path_button_pressed.bind(path))
	path_list.add_child(button)


func _add_status_row(text: String) -> void:
	var label := Label.new()
	label.text = text
	path_list.add_child(label)


func _indent(depth: int) -> String:
	var value := ""
	for index in range(depth):
		value += "  "
	return value


func _on_path_button_pressed(path: String) -> void:
	if workspace != null and workspace.change_directory(path):
		close()
