extends Label


func _ready() -> void:
	add_to_group("interaction_prompt")
	clear_prompt()


func show_prompt(action: String, target_name: String) -> void:
	text = "%s: %s" % [action, target_name]
	visible = true


func clear_prompt() -> void:
	text = ""
	visible = false
