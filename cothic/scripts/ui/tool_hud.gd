extends Label

var tool_state: ToolState


func _ready() -> void:
	tool_state = get_tree().get_first_node_in_group("tool_state")
	if tool_state != null:
		tool_state.active_tool_changed.connect(_on_active_tool_changed)
		_on_active_tool_changed(tool_state.get_active_tool(), tool_state.get_tool_display_name())


func _on_active_tool_changed(_tool_id: String, display_name: String) -> void:
	var switch_hint := "[1 Inspect] [2 Refactor]"
	if tool_state != null and tool_state.has_method("get_tool_switch_hint"):
		switch_hint = tool_state.get_tool_switch_hint()

	text = "Tool: %s  %s  LMB Use" % [display_name, switch_hint]
