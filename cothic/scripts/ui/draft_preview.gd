extends PanelContainer

var workspace: Workspace

@onready var title_label: Label = %DraftTitleLabel
@onready var meta_label: Label = %DraftMetaLabel
@onready var content_edit: CodeEdit = %DraftContentEdit


func _ready() -> void:
	add_to_group("draft_preview")
	workspace = get_tree().get_first_node_in_group("workspace")
	CodeSurface.configure(content_edit, false)
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

	title_label.text = "Draft: %s" % display_path
	meta_label.text = _meta_text(task)
	content_edit.text = _draft_text(task)
	visible = true
	get_tree().call_group("interaction_prompt", "clear_prompt")


func close() -> void:
	visible = false


func is_open() -> bool:
	return visible


func _meta_text(task: Dictionary) -> String:
	var draft_path := str(task.get("draft_path", ""))
	if draft_path == "":
		return "Draft response"

	return "Saved at %s" % draft_path


func _draft_text(task: Dictionary) -> String:
	var draft := _draft(task)
	if draft.is_empty():
		return "No structured draft is available."

	var lines: Array[String] = [
		"Kind: %s" % str(draft.get("kind", "")),
		"",
		str(draft.get("summary", "")),
	]

	var replacement_text := str(draft.get("replacement_text", ""))
	if replacement_text != "":
		lines.append("")
		lines.append("Replacement text:")
		lines.append(replacement_text)

	return "\n".join(lines)


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
