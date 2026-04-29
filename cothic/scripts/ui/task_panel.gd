extends PanelContainer

const DETAIL_MAX_LENGTH := 260

var task_queue: TaskQueue
var workspace: Workspace
var llm_task_runner: LlmTaskRunner
var draft_applier: DraftApplier

@onready var task_list: VBoxContainer = %TaskList


func _ready() -> void:
	add_to_group("task_panel")
	task_queue = get_tree().get_first_node_in_group("task_queue")
	workspace = get_tree().get_first_node_in_group("workspace")
	llm_task_runner = get_tree().get_first_node_in_group("llm_task_runner")
	draft_applier = get_tree().get_first_node_in_group("draft_applier")
	if task_queue != null:
		task_queue.tasks_changed.connect(_on_tasks_changed)
	PixelUi.style_buttons(self)
	close()


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return

	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("open_tasks"):
		close()
		get_viewport().set_input_as_handled()


func toggle() -> void:
	if visible:
		close()
	else:
		open()


func open() -> void:
	_rebuild_task_list()
	visible = true
	get_tree().call_group("interaction_prompt", "clear_prompt")


func close() -> void:
	visible = false


func is_open() -> bool:
	return visible


func _on_tasks_changed(_tasks: Array[Dictionary]) -> void:
	if visible:
		_rebuild_task_list()


func _rebuild_task_list() -> void:
	for child in task_list.get_children():
		child.queue_free()

	if task_queue == null:
		return

	var tasks := task_queue.get_tasks()
	var queued_tasks: Array[Dictionary] = []
	var history_tasks: Array[Dictionary] = []
	for task in tasks:
		if task.get("status", "") == "queued":
			queued_tasks.append(task)
		else:
			history_tasks.append(task)

	_add_section_header("Queue")

	var batch_button := Button.new()
	batch_button.text = "Run Queue"
	batch_button.disabled = not _can_run_queue(queued_tasks)
	PixelUi.style_button(batch_button)
	batch_button.pressed.connect(_on_run_batch_pressed)
	task_list.add_child(batch_button)

	if queued_tasks.is_empty():
		_add_empty_label("No queued tasks")
	else:
		for task in queued_tasks:
			_add_task_item(task)

	var separator := HSeparator.new()
	task_list.add_child(separator)

	_add_section_header("History")
	if history_tasks.is_empty():
		_add_empty_label("No history")
	else:
		history_tasks.reverse()
		for task in history_tasks:
			_add_task_item(task)


func _add_section_header(text: String) -> void:
	var label := Label.new()
	PixelUi.style_label(label, true)
	label.add_theme_font_size_override("font_size", 20)
	label.text = text
	task_list.add_child(label)


func _add_empty_label(text: String) -> void:
	var label := Label.new()
	PixelUi.style_label(label)
	label.add_theme_font_size_override("font_size", 15)
	label.text = text
	task_list.add_child(label)


func _add_task_item(task: Dictionary) -> void:
	var item := VBoxContainer.new()
	item.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	task_list.add_child(item)

	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	item.add_child(row)

	var label := Label.new()
	PixelUi.style_label(label)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.text = (
		"%s: %s  [%s]"
		% [_action_label(task), _display_path(task["path"]), str(task.get("status", "queued"))]
	)
	label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	row.add_child(label)

	if task.get("status", "") == "draft":
		var preview_button := Button.new()
		preview_button.text = "Preview"
		PixelUi.style_button(preview_button)
		preview_button.pressed.connect(_on_preview_task_pressed.bind(task["id"]))
		row.add_child(preview_button)

		var diff_button := Button.new()
		diff_button.text = "Diff"
		PixelUi.style_button(diff_button)
		diff_button.pressed.connect(_on_diff_task_pressed.bind(task["id"]))
		row.add_child(diff_button)

		if _can_apply(task):
			var apply_button := Button.new()
			apply_button.text = "Apply"
			PixelUi.style_button(apply_button)
			apply_button.pressed.connect(_on_apply_task_pressed.bind(task["id"]))
			row.add_child(apply_button)

		var reject_button := Button.new()
		reject_button.text = "Reject"
		PixelUi.style_button(reject_button)
		reject_button.pressed.connect(_on_reject_task_pressed.bind(task["id"]))
		row.add_child(reject_button)

	var delete_button := Button.new()
	delete_button.text = "Delete"
	PixelUi.style_button(delete_button)
	delete_button.pressed.connect(_on_delete_task_pressed.bind(task["id"]))
	row.add_child(delete_button)

	var detail_text := _task_detail(task)
	if detail_text != "":
		var detail_label := Label.new()
		PixelUi.style_label(detail_label)
		detail_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		detail_label.add_theme_font_size_override("font_size", 15)
		detail_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		detail_label.text = detail_text
		item.add_child(detail_label)


func _on_run_batch_pressed() -> void:
	if llm_task_runner == null:
		llm_task_runner = get_tree().get_first_node_in_group("llm_task_runner")
	if llm_task_runner != null:
		llm_task_runner.run_batch()


func _on_preview_task_pressed(task_id: int) -> void:
	if task_queue == null:
		return

	var task := task_queue.get_task(task_id)
	if task.is_empty():
		return

	close()
	get_tree().call_group("draft_preview", "open_task", task)


func _on_diff_task_pressed(task_id: int) -> void:
	if task_queue == null:
		return

	var task := task_queue.get_task(task_id)
	if task.is_empty():
		return

	close()
	get_tree().call_group("diff_preview", "open_task", task)


func _on_apply_task_pressed(task_id: int) -> void:
	if draft_applier == null:
		draft_applier = get_tree().get_first_node_in_group("draft_applier")
	if draft_applier != null:
		draft_applier.apply_task(task_id)


func _on_reject_task_pressed(task_id: int) -> void:
	if task_queue != null:
		task_queue.mark_rejected(task_id)


func _on_delete_task_pressed(task_id: int) -> void:
	if task_queue != null:
		task_queue.remove_task(task_id)


func _display_path(path: String) -> String:
	if workspace != null:
		return workspace.get_relative_path(path)

	return path


func _action_label(task: Dictionary) -> String:
	if task.get("queue_label", "") != "":
		return task["queue_label"]

	match task.get("action", ""):
		"inspect":
			return "Inspect"
		"refactor":
			return "Refactor"
		_:
			return str(task.get("action", "")).capitalize()


func _task_detail(task: Dictionary) -> String:
	var status := str(task.get("status", ""))
	var detail := ""
	if status == "running":
		detail = "Running"
	elif status == "failed":
		detail = "Error: %s" % _ellipsize(str(task.get("error", "")), DETAIL_MAX_LENGTH)
	elif status == "applied":
		detail = "Applied"
	elif status == "context":
		detail = "Used as batch context"
	elif status == "rejected":
		detail = "Rejected"
	if status == "draft":
		var draft_path := str(task.get("draft_path", ""))
		var draft: Dictionary = task.get("draft", {})
		var summary := _ellipsize(str(draft.get("summary", "")).strip_edges(), DETAIL_MAX_LENGTH)
		detail = "Draft saved: %s\n%s" % [draft_path, summary]

	return detail


func _ellipsize(text: String, max_length: int) -> String:
	if text.length() <= max_length:
		return text

	return "%s..." % text.substr(0, max_length)


func _can_apply(task: Dictionary) -> bool:
	var draft: Dictionary = task.get("draft", {})
	return draft.get("kind", "") == "replacement" and str(draft.get("replacement_text", "")) != ""


func _can_run_queue(tasks: Array[Dictionary]) -> bool:
	var has_primary := false
	for task in tasks:
		if task.get("status", "") != "queued":
			continue
		if task.get("action", "") == "investigate":
			continue
		else:
			has_primary = true

	return has_primary
