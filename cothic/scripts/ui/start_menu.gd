extends PanelContainer

var workspace: Workspace

@onready var path_edit: LineEdit = %RootPathEdit
@onready var last_project_button: Button = %LastProjectButton
@onready var error_label: Label = %RootErrorLabel
@onready var file_dialog: FileDialog = %RootFileDialog


func _ready() -> void:
	add_to_group("start_menu")
	add_to_group("ui_modal")
	workspace = get_tree().get_first_node_in_group("workspace")
	error_label.text = ""
	file_dialog.dir_selected.connect(_on_directory_selected)
	_configure_last_project()

	if workspace != null and workspace.current_path != "":
		close()
	else:
		open()


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return

	if event.is_action_pressed("ui_accept"):
		_open_selected_root()
		get_viewport().set_input_as_handled()


func open() -> void:
	visible = true


func close() -> void:
	visible = false


func _on_browse_button_pressed() -> void:
	file_dialog.current_dir = path_edit.text
	file_dialog.popup_centered_ratio(0.8)


func _on_open_button_pressed() -> void:
	_open_selected_root()


func _on_last_project_button_pressed() -> void:
	_open_path(last_project_button.get_meta("root_path", ""))


func _on_directory_selected(path: String) -> void:
	path_edit.text = path
	_open_selected_root()


func _configure_last_project() -> void:
	var last_root := ""
	if workspace != null:
		last_root = workspace.get_saved_root_path()

	if last_root != "" and DirAccess.dir_exists_absolute(last_root):
		path_edit.text = last_root
		last_project_button.text = "Open last project: %s" % last_root.get_file()
		last_project_button.tooltip_text = last_root
		last_project_button.set_meta("root_path", last_root)
		last_project_button.visible = true
	else:
		path_edit.text = OS.get_system_dir(OS.SYSTEM_DIR_DOCUMENTS)
		last_project_button.visible = false


func _open_selected_root() -> void:
	if workspace == null:
		workspace = get_tree().get_first_node_in_group("workspace")

	var path := path_edit.text.strip_edges()
	_open_path(path)


func _open_path(path: String) -> void:
	if path == "":
		error_label.text = "Choose a repository directory."
		return

	if workspace == null or not workspace.set_root(path):
		error_label.text = "Cannot open directory: %s" % path
		return

	error_label.text = ""
	close()
