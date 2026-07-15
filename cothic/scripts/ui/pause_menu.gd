extends PanelContainer

@onready var filter_settings: PanelContainer = %FilterSettings
@onready var resume_button: Button = %ResumeButton
@onready var options_button: Button = %OptionsButton
@onready var exit_button: Button = %ExitButton


func _ready() -> void:
	add_to_group("ui_modal")
	resume_button.pressed.connect(close)
	options_button.pressed.connect(_open_options)
	exit_button.pressed.connect(_exit_game)
	visible = false


func _unhandled_input(event: InputEvent) -> void:
	if not event.is_action_pressed("ui_cancel"):
		return

	if visible:
		close()
	elif not _is_another_modal_open():
		open()
	else:
		return
	get_viewport().set_input_as_handled()


func open() -> void:
	visible = true
	get_tree().paused = true
	get_tree().call_group("interaction_prompt", "clear_prompt")
	resume_button.grab_focus()


func close() -> void:
	visible = false
	get_tree().paused = false


func return_from_options() -> void:
	visible = true
	resume_button.grab_focus()


func _open_options() -> void:
	visible = false
	filter_settings.call("open_from_pause", self)


func _exit_game() -> void:
	get_tree().quit()


func _is_another_modal_open() -> bool:
	for node in get_tree().get_nodes_in_group("ui_modal"):
		if node != self and node is CanvasItem and (node as CanvasItem).visible:
			return true
	return false
