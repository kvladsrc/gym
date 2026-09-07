extends MarginContainer

@onready var prompt_label: Label = %PromptLabel


func _ready() -> void:
	add_to_group("interaction_prompt")
	clear_prompt()


func show_prompt(action: String, target_name: String) -> void:
	prompt_label.text = "E    %s  %s" % [action, target_name]
	visible = true


func clear_prompt() -> void:
	prompt_label.text = ""
	visible = false
