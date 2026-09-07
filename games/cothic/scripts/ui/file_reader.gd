extends PanelContainer

const MAX_TEXT_BYTES := 64 * 1024

var workspace: Workspace

@onready var title_label: Label = %TitleLabel
@onready var meta_label: Label = %MetaLabel
@onready var content_edit: CodeEdit = %ContentEdit


func _ready() -> void:
	add_to_group("file_reader")
	add_to_group("ui_modal")
	workspace = get_tree().get_first_node_in_group("workspace")
	CodeSurface.configure(content_edit)
	close()


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return

	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("interact"):
		close()
		get_viewport().set_input_as_handled()


func open_file(path: String) -> void:
	if workspace == null:
		workspace = get_tree().get_first_node_in_group("workspace")

	if workspace == null or not workspace.can_access(path):
		_show_error(path, "File is outside the workspace root.")
		return

	if not FileAccess.file_exists(path):
		_show_error(path, "File does not exist.")
		return

	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		_show_error(path, "Cannot open file.")
		return

	var length := file.get_length()
	var bytes_to_read := mini(length, MAX_TEXT_BYTES)
	var buffer := file.get_buffer(bytes_to_read)
	file.close()

	if _looks_binary(buffer):
		title_label.text = workspace.get_relative_path(path)
		meta_label.text = "%s bytes - binary or non-text file" % length
		content_edit.text = "This file is not displayed as text."
		visible = true
		get_tree().call_group("interaction_prompt", "clear_prompt")
		return

	var content := buffer.get_string_from_utf8()

	title_label.text = workspace.get_relative_path(path)
	meta_label.text = (
		"%s bytes%s"
		% [
			length,
			" - truncated" if length > MAX_TEXT_BYTES else "",
		]
	)
	content_edit.text = content
	visible = true
	get_tree().call_group("interaction_prompt", "clear_prompt")


func close() -> void:
	visible = false


func _show_error(path: String, message: String) -> void:
	title_label.text = path
	meta_label.text = "Error"
	content_edit.text = message
	visible = true


func _looks_binary(buffer: PackedByteArray) -> bool:
	for byte in buffer:
		if byte == 0:
			return true

	return false
