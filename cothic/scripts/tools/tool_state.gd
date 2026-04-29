class_name ToolState
extends Node

signal active_tool_changed(tool_id: String, display_name: String)

const TOOL_INSPECT := "inspect"
const TOOL_REFACTOR := "refactor"
const TOOL_INVESTIGATE := "investigate"

var active_tool_id := TOOL_INSPECT
var tools_by_id: Dictionary = {}
var tool_order: Array[String] = []
var task_queue: TaskQueue


func _enter_tree() -> void:
	add_to_group("tool_state")


func _ready() -> void:
	_register_default_tools()
	task_queue = get_tree().get_first_node_in_group("task_queue")
	if task_queue != null:
		task_queue.tasks_changed.connect(_on_tasks_changed)
	active_tool_changed.emit(active_tool_id, get_tool_display_name(active_tool_id))


func set_active_tool(tool_id: String) -> void:
	if tool_id == active_tool_id:
		return

	if not tools_by_id.has(tool_id):
		push_warning("Unknown tool: %s" % tool_id)
		return

	active_tool_id = tool_id
	active_tool_changed.emit(active_tool_id, get_tool_display_name(active_tool_id))


func set_active_tool_slot(slot: int) -> void:
	var index := slot - 1
	if index < 0 or index >= tool_order.size():
		return

	set_active_tool(tool_order[index])


func get_active_tool() -> String:
	return active_tool_id


func get_active_tool_definition() -> ToolDefinition:
	return get_tool_definition(active_tool_id)


func get_tool_definition(tool_id: String) -> ToolDefinition:
	if not tools_by_id.has(tool_id):
		return null

	return tools_by_id[tool_id]


func get_tool_display_name(tool_id: String = active_tool_id) -> String:
	var tool := get_tool_definition(tool_id)
	if tool == null:
		return tool_id

	return tool.display_name


func get_tool_switch_hint() -> String:
	var parts: Array[String] = []
	for index in tool_order.size():
		var tool := get_tool_definition(tool_order[index])
		if tool != null:
			parts.append("[%d %s]" % [index + 1, tool.queue_label])

	return " ".join(parts)


func apply_to_file(path: String) -> void:
	var tool := get_active_tool_definition()
	if tool == null:
		return

	get_tree().call_group(
		"task_queue",
		"upsert_task",
		tool.queue_action,
		path,
		tool.prompt,
		tool.id,
		tool.queue_label,
		tool.marker_label
	)
	print("%s queued for: %s" % [tool.queue_label, path])


func is_marked(path: String) -> bool:
	return get_marker_label(path) != ""


func get_marker_label(path: String) -> String:
	if task_queue == null:
		task_queue = get_tree().get_first_node_in_group("task_queue")
	if task_queue != null and task_queue.has_method("get_queued_marker_label"):
		return task_queue.get_queued_marker_label(path)

	return ""


func _on_tasks_changed(_tasks: Array[Dictionary]) -> void:
	get_tree().call_group("area_generator", "refresh_markers")


func _register_default_tools() -> void:
	if not tools_by_id.is_empty():
		return

	_register_tool(
		ToolDefinition.new(
			TOOL_INSPECT,
			"Inspect",
			"inspect",
			"Inspect",
			(
				"Inspect this file. Summarize its responsibilities, important "
				+ "dependencies, risks, and concrete follow-up questions."
			),
			"INS"
		)
	)
	_register_tool(
		ToolDefinition.new(
			TOOL_REFACTOR,
			"Refactor Hammer",
			"refactor",
			"Refactor",
			(
				"Refactor this file for clarity and maintainability while preserving "
				+ "behavior. Explain the intended change before producing the replacement file."
			),
			"REF"
		)
	)
	_register_tool(
		ToolDefinition.new(
			TOOL_INVESTIGATE,
			"Investigate",
			"investigate",
			"Investigate",
			(
				"Use this file as supporting context for the next related code task. "
				+ "Identify facts from this file that matter to the requested change."
			),
			"INV"
		)
	)


func _register_tool(tool: ToolDefinition) -> void:
	tools_by_id[tool.id] = tool
	tool_order.append(tool.id)
