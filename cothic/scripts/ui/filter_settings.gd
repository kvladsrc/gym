extends PanelContainer

const CONFIG_PATH := "user://filter_settings.cfg"
const CONFIG_SECTION := "filters"
const FILTER_PATH := ^"../../PostProcess/CozyFilter"
const SETTINGS: Array[Dictionary] = [
	{
		"key": "pixel_size",
		"label": "Pixel size",
		"minimum": 1.0,
		"maximum": 3.0,
		"step": 0.25,
		"suffix": "x",
	},
	{
		"key": "color_levels",
		"label": "Color levels",
		"minimum": 4.0,
		"maximum": 24.0,
		"step": 1.0,
		"suffix": "",
	},
	{
		"key": "dither_strength",
		"label": "Dithering",
		"minimum": 0.0,
		"maximum": 0.3,
		"step": 0.01,
		"suffix": "",
	},
	{
		"key": "saturation",
		"label": "Saturation",
		"minimum": 0.5,
		"maximum": 1.4,
		"step": 0.01,
		"suffix": "",
	},
	{
		"key": "contrast",
		"label": "Contrast",
		"minimum": 0.7,
		"maximum": 1.3,
		"step": 0.01,
		"suffix": "",
	},
	{
		"key": "tilt_shift_strength",
		"label": "Tilt-shift",
		"minimum": 0.0,
		"maximum": 1.0,
		"step": 0.01,
		"suffix": "",
	},
	{
		"key": "tilt_shift_blur_radius",
		"label": "Tilt blur",
		"minimum": 0.0,
		"maximum": 4.0,
		"step": 0.05,
		"suffix": "",
	},
	{
		"key": "tilt_shift_focus_center",
		"label": "Focus height",
		"minimum": 0.0,
		"maximum": 1.0,
		"step": 0.01,
		"suffix": "",
	},
	{
		"key": "tilt_shift_focus_width",
		"label": "Focus width",
		"minimum": 0.05,
		"maximum": 0.5,
		"step": 0.01,
		"suffix": "",
	},
]

var _filter: ColorRect
var _material: ShaderMaterial
var _defaults := {}
var _sliders := {}
var _save_timer: Timer
var _return_menu: Control

@onready var enabled_check: CheckButton = %EnabledCheck
@onready var rows: VBoxContainer = %Rows
@onready var reset_button: Button = %ResetButton
@onready var close_button: Button = %CloseButton


func _ready() -> void:
	add_to_group("ui_modal")
	_filter = get_node_or_null(FILTER_PATH) as ColorRect
	if _filter == null or not _filter.material is ShaderMaterial:
		push_error("Filter settings require the CozyFilter shader material.")
		return

	_material = _filter.material as ShaderMaterial
	_save_timer = Timer.new()
	_save_timer.one_shot = true
	_save_timer.wait_time = 0.25
	_save_timer.timeout.connect(_save_settings)
	add_child(_save_timer)

	_capture_defaults()
	_build_rows()
	_load_settings()
	enabled_check.toggled.connect(_on_enabled_toggled)
	reset_button.pressed.connect(_reset_settings)
	close_button.pressed.connect(close)
	close()


func _unhandled_input(event: InputEvent) -> void:
	if visible and event.is_action_pressed("ui_cancel"):
		close()
		get_viewport().set_input_as_handled()


func open_from_pause(return_menu: Control) -> void:
	_return_menu = return_menu
	visible = true
	get_tree().call_group("interaction_prompt", "clear_prompt")


func close() -> void:
	visible = false
	if _return_menu != null:
		var return_menu := _return_menu
		_return_menu = null
		return_menu.call("return_from_options")


func _capture_defaults() -> void:
	_defaults["enabled"] = _filter.visible
	for setting in SETTINGS:
		var key := StringName(setting["key"])
		_defaults[key] = float(_material.get_shader_parameter(key))


func _build_rows() -> void:
	for setting in SETTINGS:
		var key := StringName(setting["key"])
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 12)

		var label := Label.new()
		label.custom_minimum_size.x = 165.0
		label.text = String(setting["label"])
		row.add_child(label)

		var slider := HSlider.new()
		slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		slider.min_value = float(setting["minimum"])
		slider.max_value = float(setting["maximum"])
		slider.step = float(setting["step"])
		slider.value = float(_defaults[key])
		row.add_child(slider)

		var value_label := Label.new()
		value_label.custom_minimum_size.x = 72.0
		value_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		_update_value_label(value_label, slider.value, String(setting["suffix"]), slider.step)
		row.add_child(value_label)

		slider.value_changed.connect(
			_on_slider_changed.bind(key, value_label, String(setting["suffix"]), slider.step)
		)
		_sliders[key] = slider
		rows.add_child(row)


func _load_settings() -> void:
	var config := ConfigFile.new()
	if config.load(CONFIG_PATH) != OK:
		enabled_check.set_pressed_no_signal(bool(_defaults["enabled"]))
		return

	var enabled := bool(config.get_value(CONFIG_SECTION, "enabled", _defaults["enabled"]))
	enabled_check.set_pressed_no_signal(enabled)
	_filter.visible = enabled
	for setting in SETTINGS:
		var key := StringName(setting["key"])
		var slider := _sliders[key] as HSlider
		slider.value = float(config.get_value(CONFIG_SECTION, key, _defaults[key]))


func _on_enabled_toggled(enabled: bool) -> void:
	_filter.visible = enabled
	_queue_save()


func _on_slider_changed(
	value: float, key: StringName, value_label: Label, suffix: String, step: float
) -> void:
	_material.set_shader_parameter(key, value)
	_update_value_label(value_label, value, suffix, step)
	_queue_save()


func _update_value_label(label: Label, value: float, suffix: String, step: float) -> void:
	var digits := 0 if step >= 1.0 else 2
	label.text = ("%.*f%s" % [digits, value, suffix])


func _reset_settings() -> void:
	enabled_check.button_pressed = bool(_defaults["enabled"])
	for setting in SETTINGS:
		var key := StringName(setting["key"])
		var slider := _sliders[key] as HSlider
		slider.value = float(_defaults[key])
	_queue_save()


func _queue_save() -> void:
	_save_timer.start()


func _save_settings() -> void:
	var config := ConfigFile.new()
	config.set_value(CONFIG_SECTION, "enabled", enabled_check.button_pressed)
	for key in _sliders:
		var slider := _sliders[key] as HSlider
		config.set_value(CONFIG_SECTION, key, slider.value)
	var error := config.save(CONFIG_PATH)
	if error != OK:
		push_warning("Cannot save filter settings: %s" % error_string(error))
