class_name ToolDefinition
extends RefCounted

var id: String
var display_name: String
var queue_action: String
var queue_label: String
var prompt: String
var marker_label: String


func _init(
	tool_id: String,
	tool_display_name: String,
	tool_queue_action: String,
	tool_queue_label: String,
	tool_prompt: String,
	tool_marker_label: String = ""
) -> void:
	id = tool_id
	display_name = tool_display_name
	queue_action = tool_queue_action
	queue_label = tool_queue_label
	prompt = tool_prompt
	marker_label = tool_marker_label
