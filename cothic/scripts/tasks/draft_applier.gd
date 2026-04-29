class_name DraftApplier
extends Node

var workspace: Workspace
var task_queue: TaskQueue


func _ready() -> void:
	add_to_group("draft_applier")
	workspace = get_tree().get_first_node_in_group("workspace")
	task_queue = get_tree().get_first_node_in_group("task_queue")


func apply_task(task_id: int) -> void:
	if task_queue == null:
		task_queue = get_tree().get_first_node_in_group("task_queue")
	if workspace == null:
		workspace = get_tree().get_first_node_in_group("workspace")

	if task_queue == null or workspace == null:
		return

	var task := task_queue.get_task(task_id)
	if task.is_empty():
		return

	var result := _apply_task(task)
	if result["ok"]:
		task_queue.mark_applied(task_id)
		_refresh_area()
	else:
		task_queue.mark_failed(task_id, result["error"])


func _apply_task(task: Dictionary) -> Dictionary:
	var path := str(task.get("path", ""))
	var draft_file: Dictionary = {}
	var draft: Dictionary = {}
	var replacement_text := ""
	var source_text := ""
	var current_text := ""
	var error := ""

	if not workspace.can_access(path):
		error = "Cannot apply: file is outside the workspace root."

	if error == "":
		draft_file = _read_draft_file(str(task.get("draft_path", "")))
		if draft_file.is_empty():
			error = "Cannot apply: draft file is missing or invalid."

	if error == "" and bool(draft_file.get("source_truncated", false)):
		error = "Cannot apply: draft was created from truncated source."

	if error == "":
		draft = draft_file.get("draft", {})
		if draft.get("kind", "") != "replacement":
			error = "Cannot apply: draft is not a replacement."

	if error == "":
		replacement_text = str(draft.get("replacement_text", ""))
		if replacement_text == "":
			error = "Cannot apply: replacement text is empty."

	if error == "":
		source_text = str(draft_file.get("source_text", ""))
		var current_source := _read_source_text(path)
		if not current_source["ok"]:
			error = "Cannot apply: source file does not exist or cannot be opened."
		else:
			current_text = current_source["text"]

	if error == "" and current_text != source_text:
		error = "Cannot apply: source file changed since the draft was created."

	if error == "":
		error = _write_replacement(path, replacement_text)

	return {"ok": error == "", "error": error}


func _read_source_text(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {"ok": false, "text": ""}

	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return {"ok": false, "text": ""}

	var text := file.get_as_text()
	file.close()
	return {"ok": true, "text": text}


func _write_replacement(path: String, replacement_text: String) -> String:
	var output_file := FileAccess.open(path, FileAccess.WRITE)
	if output_file == null:
		return "Cannot apply: source file cannot be written."

	output_file.store_string(replacement_text)
	output_file.close()
	return ""


func _read_draft_file(path: String) -> Dictionary:
	if path == "" or not FileAccess.file_exists(path):
		return {}

	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return {}

	var raw_body := file.get_as_text()
	file.close()

	var parsed: Variant = JSON.parse_string(raw_body)
	if typeof(parsed) != TYPE_DICTIONARY:
		return {}

	return parsed


func _refresh_area() -> void:
	if workspace.current_path == "":
		return

	get_tree().call_group("area_generator", "generate", workspace.current_path)
