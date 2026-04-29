class_name TaskQueue
extends Node

signal tasks_changed(tasks: Array[Dictionary])

const QUEUED_STATUS := "queued"

var tasks: Array[Dictionary] = []
var next_task_id := 1


func _enter_tree() -> void:
	add_to_group("task_queue")


func add_task(action: String, path: String) -> Dictionary:
	return upsert_task(action, path)


func upsert_task(
	action: String,
	path: String,
	prompt: String = "",
	tool_id: String = "",
	queue_label: String = "",
	marker_label: String = ""
) -> Dictionary:
	var existing := _find_queued_task(path)
	if not existing.is_empty():
		existing["action"] = action
		existing["prompt"] = prompt
		existing["tool_id"] = tool_id
		existing["queue_label"] = queue_label
		existing["marker_label"] = marker_label
		existing["status"] = QUEUED_STATUS
		existing.erase("draft_path")
		existing.erase("draft")
		existing.erase("error")
		existing["updated_msec"] = Time.get_ticks_msec()
		tasks_changed.emit(get_tasks())
		return existing

	var task := {
		"id": next_task_id,
		"action": action,
		"path": path,
		"prompt": prompt,
		"tool_id": tool_id,
		"queue_label": queue_label,
		"marker_label": marker_label,
		"status": QUEUED_STATUS,
		"created_msec": Time.get_ticks_msec(),
		"updated_msec": Time.get_ticks_msec(),
	}
	next_task_id += 1
	tasks.append(task)
	tasks_changed.emit(get_tasks())
	return task


func remove_task(task_id: int) -> void:
	for index in tasks.size():
		if tasks[index]["id"] == task_id:
			tasks.remove_at(index)
			tasks_changed.emit(get_tasks())
			return


func get_task(task_id: int) -> Dictionary:
	for task in tasks:
		if task["id"] == task_id:
			return task.duplicate(true)

	return {}


func get_queued_marker_label(path: String) -> String:
	var task := _find_queued_task(path)
	if task.is_empty():
		return ""

	if str(task.get("marker_label", "")) != "":
		return task["marker_label"]

	match task.get("action", ""):
		"inspect":
			return "INS"
		"refactor":
			return "REF"
		"investigate":
			return "INV"
		_:
			return ""


func mark_running(task_id: int) -> void:
	_update_task(
		task_id,
		{
			"status": "running",
			"error": "",
			"updated_msec": Time.get_ticks_msec(),
		}
	)


func mark_draft(task_id: int, draft_path: String, draft: Dictionary) -> void:
	_update_task(
		task_id,
		{
			"status": "draft",
			"draft_path": draft_path,
			"draft": draft,
			"error": "",
			"updated_msec": Time.get_ticks_msec(),
		}
	)


func mark_failed(task_id: int, error: String) -> void:
	_update_task(
		task_id,
		{
			"status": "failed",
			"error": error,
			"updated_msec": Time.get_ticks_msec(),
		}
	)


func mark_applied(task_id: int) -> void:
	_update_task(
		task_id,
		{
			"status": "applied",
			"error": "",
			"applied_msec": Time.get_ticks_msec(),
			"updated_msec": Time.get_ticks_msec(),
		}
	)


func mark_rejected(task_id: int) -> void:
	_update_task(
		task_id,
		{
			"status": "rejected",
			"error": "",
			"updated_msec": Time.get_ticks_msec(),
		}
	)


func mark_context_used(task_id: int) -> void:
	_update_task(
		task_id,
		{
			"status": "context",
			"error": "",
			"updated_msec": Time.get_ticks_msec(),
		}
	)


func get_tasks() -> Array[Dictionary]:
	return tasks.duplicate(true)


func _find_queued_task(path: String) -> Dictionary:
	for task in tasks:
		if task.get("path", "") == path and task.get("status", "") == QUEUED_STATUS:
			return task

	return {}


func _update_task(task_id: int, values: Dictionary) -> void:
	for task in tasks:
		if task["id"] != task_id:
			continue

		for key in values:
			task[key] = values[key]
		tasks_changed.emit(get_tasks())
		return
