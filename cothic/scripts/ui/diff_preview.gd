extends PanelContainer

const DIFF_DIR := "user://diff"

var workspace: Workspace

@onready var title_label: Label = %DiffTitleLabel
@onready var meta_label: Label = %DiffMetaLabel
@onready var content_edit: CodeEdit = %DiffContentEdit


func _ready() -> void:
	add_to_group("diff_preview")
	workspace = get_tree().get_first_node_in_group("workspace")
	CodeSurface.configure(content_edit)
	PixelUi.style_buttons(self)
	close()


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return

	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("open_tasks"):
		close()
		get_viewport().set_input_as_handled()


func open_task(task: Dictionary) -> void:
	if workspace == null:
		workspace = get_tree().get_first_node_in_group("workspace")

	var path := str(task.get("path", ""))
	var display_path := path
	if workspace != null:
		display_path = workspace.get_relative_path(path)

	title_label.text = "Diff: %s" % display_path
	meta_label.text = "Read-only preview. No files are changed."
	content_edit.text = _build_diff_for_task(task)
	visible = true
	get_tree().call_group("interaction_prompt", "clear_prompt")


func close() -> void:
	visible = false


func is_open() -> bool:
	return visible


func _build_diff_for_task(task: Dictionary) -> String:
	var path := str(task.get("path", ""))
	if workspace == null or not workspace.can_access(path):
		return "Cannot build diff: file is outside the workspace root."

	if not FileAccess.file_exists(path):
		return "Cannot build diff: source file does not exist."

	var source_file := FileAccess.open(path, FileAccess.READ)
	if source_file == null:
		return "Cannot build diff: source file cannot be opened."

	var original_text := source_file.get_as_text()
	source_file.close()

	var replacement_text := str(_draft(task).get("replacement_text", ""))
	if replacement_text == "":
		return (
			"No replacement file was found in this draft.\n\n"
			+ "The draft kind is not replacement, or replacement_text is empty."
		)

	return _make_diff(task, path, original_text, replacement_text)


func _draft(task: Dictionary) -> Dictionary:
	var draft: Dictionary = task.get("draft", {})
	if not draft.is_empty():
		return draft

	var draft_path := str(task.get("draft_path", ""))
	if draft_path == "":
		return {}

	var file := FileAccess.open(draft_path, FileAccess.READ)
	if file == null:
		return {}

	var raw_body := file.get_as_text()
	file.close()

	var parsed: Variant = JSON.parse_string(raw_body)
	if typeof(parsed) != TYPE_DICTIONARY:
		return {}

	return parsed.get("draft", {})


func _make_diff(
	task: Dictionary, path: String, original_text: String, replacement_text: String
) -> String:
	var temp_paths := _write_temp_files(task, original_text, replacement_text)
	if temp_paths.is_empty():
		return "Cannot build diff: temporary files cannot be written."

	var display_path := workspace.get_relative_path(path)

	var output: Array = []
	var exit_code := (
		OS
		. execute(
			"diff",
			[
				"-u",
				"--label",
				display_path,
				"--label",
				"draft:%s" % display_path,
				"--",
				temp_paths["original"],
				temp_paths["replacement"],
			],
			output,
			true
		)
	)
	if exit_code == 0:
		return "No text changes."
	if exit_code == 1:
		return "".join(output)

	return "Cannot build diff: diff exited with code %d.\n\n%s" % [exit_code, "".join(output)]


func _write_temp_files(
	task: Dictionary, original_text: String, replacement_text: String
) -> Dictionary:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(DIFF_DIR))
	var task_id := int(task.get("id", 0))
	var original_path := "%s/task_%d_original.txt" % [DIFF_DIR, task_id]
	var replacement_path := "%s/task_%d_replacement.txt" % [DIFF_DIR, task_id]

	if not _write_text_file(original_path, original_text):
		return {}
	if not _write_text_file(replacement_path, replacement_text):
		return {}

	return {
		"original": ProjectSettings.globalize_path(original_path),
		"replacement": ProjectSettings.globalize_path(replacement_path),
	}


func _write_text_file(path: String, text: String) -> bool:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false

	file.store_string(text)
	file.close()
	return true
