class_name LlmTaskRunner
extends Node

const MAX_FILE_BYTES := 64 * 1024
const DRAFT_DIR := "user://drafts"
const DEBUG_DIR := "user://debug"

var is_running := false
var active_debug_path := ""
var active_target_tasks: Array[Dictionary] = []
var active_target_files: Array[Dictionary] = []
var active_context_task_ids: Array[int] = []
var active_context_files: Array[Dictionary] = []
var workspace: Workspace
var task_queue: TaskQueue
var openrouter: OpenRouterAdapter


func _ready() -> void:
	add_to_group("llm_task_runner")
	workspace = get_tree().get_first_node_in_group("workspace")
	task_queue = get_tree().get_first_node_in_group("task_queue")
	openrouter = get_tree().get_first_node_in_group("openrouter_adapter")

	if openrouter != null:
		openrouter.completion_succeeded.connect(_on_completion_succeeded)
		openrouter.completion_failed.connect(_on_completion_failed)


func run_batch() -> void:
	if is_running:
		return

	if task_queue == null:
		task_queue = get_tree().get_first_node_in_group("task_queue")
	if workspace == null:
		workspace = get_tree().get_first_node_in_group("workspace")
	if openrouter == null:
		openrouter = get_tree().get_first_node_in_group("openrouter_adapter")
	if task_queue == null or workspace == null or openrouter == null:
		return

	var batch := _build_batch()
	if batch.is_empty():
		return

	var request_messages := _build_batch_messages(batch)
	if _is_debug_enabled():
		active_debug_path = _save_debug_request(batch, request_messages)

	is_running = true
	active_target_tasks = batch["target_tasks"]
	active_target_files = batch["target_files"]
	active_context_task_ids = batch["context_task_ids"]
	active_context_files = batch["context_files"]
	for target_task in active_target_tasks:
		task_queue.mark_running(target_task["id"])
	for context_task_id in active_context_task_ids:
		task_queue.mark_running(context_task_id)
	openrouter.create_completion(request_messages)


func _on_completion_succeeded(response_text: String) -> void:
	if not is_running:
		return

	_update_debug_response("succeeded", response_text, "")
	_handle_batch_completion(response_text)


func _on_completion_failed(error: String) -> void:
	if not is_running:
		return

	_update_debug_response("failed", "", error)
	_mark_active_batch_failed("Batch failed: %s" % error)


func _clear_running_batch() -> void:
	is_running = false
	active_debug_path = ""
	active_context_task_ids = []
	active_context_files = []
	active_target_tasks = []
	active_target_files = []


func _read_text_file(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {"ok": false, "error": "Task file does not exist."}

	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return {"ok": false, "error": "Cannot open task file."}

	var length := file.get_length()
	var bytes_to_read := mini(length, MAX_FILE_BYTES)
	var buffer := file.get_buffer(bytes_to_read)
	file.close()

	for byte in buffer:
		if byte == 0:
			return {"ok": false, "error": "Binary files are not supported for LLM tasks yet."}

	return {
		"ok": true,
		"content": buffer.get_string_from_utf8(),
		"truncated": length > MAX_FILE_BYTES,
	}


func _build_batch() -> Dictionary:
	var target_tasks: Array[Dictionary] = []
	var context_tasks: Array[Dictionary] = []
	for task in task_queue.get_tasks():
		if task.get("status", "") != "queued":
			continue
		if task.get("action", "") == "investigate":
			context_tasks.append(task)
		else:
			target_tasks.append(task)

	if target_tasks.is_empty():
		return {}

	var target_files: Array[Dictionary] = []
	var valid_target_tasks: Array[Dictionary] = []
	for target_task in target_tasks:
		var target_file := _read_task_file(target_task)
		if not target_file["ok"]:
			task_queue.mark_failed(target_task["id"], target_file["error"])
			continue
		target_files.append(target_file)
		valid_target_tasks.append(target_task)

	if valid_target_tasks.is_empty():
		return {}

	var context_files: Array[Dictionary] = []
	var context_task_ids: Array[int] = []
	for context_task in context_tasks:
		var context_file := _read_task_file(context_task)
		if not context_file["ok"]:
			task_queue.mark_failed(context_task["id"], context_file["error"])
			continue
		context_files.append(context_file)
		context_task_ids.append(context_task["id"])

	return {
		"target_tasks": valid_target_tasks,
		"target_files": target_files,
		"context_task_ids": context_task_ids,
		"context_files": context_files,
	}


func _read_task_file(task: Dictionary) -> Dictionary:
	var path := str(task.get("path", ""))
	if not workspace.can_access(path):
		return {"ok": false, "error": "Task file is outside the workspace root."}

	var file_payload := _read_text_file(path)
	if not file_payload["ok"]:
		return file_payload

	file_payload["path"] = path
	file_payload["relative_path"] = workspace.get_relative_path(path)
	return file_payload


func _build_batch_messages(batch: Dictionary) -> Array[Dictionary]:
	var target_tasks: Array[Dictionary] = batch["target_tasks"]
	var target_files: Array[Dictionary] = batch["target_files"]
	var context_files: Array[Dictionary] = batch["context_files"]
	var target_sections: Array[String] = []
	for index in target_tasks.size():
		var task := target_tasks[index]
		var target_file := target_files[index]
		var note := "truncated to 64 KiB" if target_file["truncated"] else "full file"
		(
			target_sections
			. append(
				(
					"Task id: %d\nAction: %s\nPath: %s (%s)\nInstruction: %s\n```text\n%s\n```"
					% [
						task["id"],
						task["action"],
						target_file["relative_path"],
						note,
						task["prompt"],
						target_file["content"],
					]
				)
			)
		)

	var context_sections: Array[String] = []
	for context_file in context_files:
		var note := "truncated to 64 KiB" if context_file["truncated"] else "full file"
		context_sections.append(
			(
				"Context path: %s (%s)\n```text\n%s\n```"
				% [context_file["relative_path"], note, context_file["content"]]
			)
		)

	var context_text := "\n\n".join(context_sections)
	if context_text == "":
		context_text = "No additional context files were selected."

	return [
		{
			"role": "system",
			"content":
			(
				"You are Cothic's batch dry-run coding assistant. Do not claim that you "
				+ "changed files. Return exactly one JSON object and no surrounding Markdown."
			),
		},
		{
			"role": "user",
			"content":
			(
				(
					"Return exactly this JSON shape:\n"
					+ "{\n"
					+ '  "results": [\n'
					+ "    {\n"
					+ '      "task_id": 0,\n'
					+ '      "kind": "inspection" | "replacement",\n'
					+ '      "summary": "...",\n'
					+ '      "replacement_text": "..."\n'
					+ "    }\n"
					+ "  ]\n"
					+ "}\n\n"
					+ 'Use kind "replacement" only for refactor tasks when replacement_text '
					+ 'contains the complete new file. Use kind "inspection" for inspect tasks. '
					+ "For inspections, replacement_text must be an empty string.\n\n"
					+ "Selected target tasks:\n\n%s\n\n"
					+ "Additional context files selected with Investigate:\n\n%s"
				)
				% ["\n\n".join(target_sections), context_text]
			),
		},
	]


func _handle_batch_completion(response_text: String) -> void:
	var parsed: Variant = JSON.parse_string(response_text)
	if typeof(parsed) != TYPE_DICTIONARY or typeof(parsed.get("results", [])) != TYPE_ARRAY:
		_mark_active_batch_failed("OpenRouter response was not a valid batch JSON object.")
		return

	var results_by_task_id := {}
	for result in parsed["results"]:
		if typeof(result) == TYPE_DICTIONARY:
			results_by_task_id[int(result.get("task_id", 0))] = result

	for index in active_target_tasks.size():
		var task := active_target_tasks[index]
		var task_id := int(task["id"])
		if not results_by_task_id.has(task_id):
			task_queue.mark_failed(task_id, "Batch response did not include this task.")
			continue

		var draft := _normalize_draft(results_by_task_id[task_id])
		if draft.is_empty():
			task_queue.mark_failed(task_id, "Batch response included invalid draft JSON.")
			continue

		var source_file := active_target_files[index]
		if draft.get("kind", "") == "replacement" and source_file["truncated"]:
			task_queue.mark_failed(
				task_id, "Replacement drafts are not accepted for truncated files."
			)
			continue

		var completed_task := task.duplicate(true)
		completed_task["status"] = "draft"
		completed_task["updated_msec"] = Time.get_ticks_msec()
		var draft_path := _save_draft(
			completed_task,
			draft,
			source_file["content"],
			source_file["truncated"],
			active_context_files
		)
		task_queue.mark_draft(task_id, draft_path, draft)

	for context_task_id in active_context_task_ids:
		task_queue.mark_context_used(context_task_id)
	_clear_running_batch()


func _mark_active_batch_failed(error: String) -> void:
	for task in active_target_tasks:
		task_queue.mark_failed(task["id"], error)
	for context_task_id in active_context_task_ids:
		task_queue.mark_failed(context_task_id, error)
	_clear_running_batch()


func _normalize_draft(draft: Dictionary) -> Dictionary:
	var kind := str(draft.get("kind", ""))
	if not ["inspection", "replacement"].has(kind):
		return {}

	if kind == "replacement" and str(draft.get("replacement_text", "")) == "":
		return {}

	draft["kind"] = kind
	draft["summary"] = str(draft.get("summary", ""))
	draft["replacement_text"] = str(draft.get("replacement_text", ""))
	return draft


func _save_draft(
	task: Dictionary,
	draft: Dictionary,
	source_text: String,
	source_truncated: bool,
	context_files: Array[Dictionary] = []
) -> String:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(DRAFT_DIR))
	var task_id := int(task.get("id", 0))
	var draft_path := "%s/task_%d.json" % [DRAFT_DIR, task_id]
	var file := FileAccess.open(draft_path, FileAccess.WRITE)
	if file == null:
		return ""

	var payload := {
		"task": task,
		"draft": draft,
		"source_text": source_text,
		"source_truncated": source_truncated,
		"context_files": context_files,
		"created_unix": Time.get_unix_time_from_system(),
	}
	file.store_string(JSON.stringify(payload, "\t"))
	file.close()
	return draft_path


func _save_debug_request(batch: Dictionary, messages: Array[Dictionary]) -> String:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(DEBUG_DIR))
	var debug_path := "%s/batch_%d.json" % [DEBUG_DIR, Time.get_ticks_msec()]
	var file := FileAccess.open(debug_path, FileAccess.WRITE)
	if file == null:
		return ""

	var payload := {
		"created_unix": Time.get_unix_time_from_system(),
		"provider": "openrouter",
		"status": "running",
		"target_tasks": _task_debug_metadata(batch["target_tasks"]),
		"context_task_ids": batch["context_task_ids"],
		"request_body":
		{
			"model": _request_model_name(),
			"messages": messages,
			"temperature": 0.2,
			"max_completion_tokens": 4096,
			"response_format": {"type": "json_object"},
		},
		"response_code": 0,
		"raw_response_body": "",
		"response_text": "",
		"error": "",
	}
	file.store_string(JSON.stringify(payload, "\t"))
	file.close()
	return debug_path


func _update_debug_response(status: String, response_text: String, error: String) -> void:
	if active_debug_path == "":
		return

	var payload := _read_debug_payload(active_debug_path)
	if payload.is_empty():
		return

	payload["updated_unix"] = Time.get_unix_time_from_system()
	payload["status"] = status
	payload["response_code"] = openrouter.get_last_response_code() if openrouter != null else 0
	payload["raw_response_body"] = (
		openrouter.get_last_raw_response_body() if openrouter != null else ""
	)
	payload["response_text"] = response_text
	payload["error"] = error
	_write_debug_payload(active_debug_path, payload)


func _read_debug_payload(path: String) -> Dictionary:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return {}

	var raw_body := file.get_as_text()
	file.close()
	var parsed: Variant = JSON.parse_string(raw_body)
	if typeof(parsed) != TYPE_DICTIONARY:
		return {}

	return parsed


func _write_debug_payload(path: String, payload: Dictionary) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return

	file.store_string(JSON.stringify(payload, "\t"))
	file.close()


func _request_model_name() -> String:
	var model := OS.get_environment("OPENROUTER_MODEL")
	if model == "":
		model = OpenRouterAdapter.DEFAULT_MODEL

	return model


func _is_debug_enabled() -> bool:
	return OS.get_environment("COTHIC_DEBUG") == "1"


func _task_debug_metadata(tasks: Array[Dictionary]) -> Array[Dictionary]:
	var metadata: Array[Dictionary] = []
	for task in tasks:
		(
			metadata
			. append(
				{
					"id": task.get("id", 0),
					"action": task.get("action", ""),
					"path": task.get("path", ""),
					"prompt": task.get("prompt", ""),
				}
			)
		)

	return metadata
