class_name PixelUi
extends RefCounted

const BUTTON_TEXTURE := preload("res://assets/ui/pixel/button.png")
const BUTTON_PRESSED_TEXTURE := preload("res://assets/ui/pixel/button_round.png")
const UI_FONT_PATH := "res://assets/fonts/AlegreyaSansSC-Regular.ttf"
const UI_BOLD_FONT_PATH := "res://assets/fonts/AlegreyaSansSC-Bold.ttf"

static var ui_font: FontFile
static var ui_bold_font: FontFile


static func style_button(button: Button) -> void:
	var font := _ui_font()
	if font != null:
		button.add_theme_font_override("font", font)
	button.add_theme_font_size_override("font_size", 20)
	button.add_theme_stylebox_override("normal", _button_style(BUTTON_TEXTURE))
	button.add_theme_stylebox_override("hover", _button_style(BUTTON_PRESSED_TEXTURE))
	button.add_theme_stylebox_override("pressed", _button_style(BUTTON_PRESSED_TEXTURE))
	button.add_theme_stylebox_override("disabled", _button_style(BUTTON_TEXTURE))
	button.add_theme_color_override("font_color", Color(0.97, 0.90, 0.74))
	button.add_theme_color_override("font_hover_color", Color(1.0, 0.96, 0.82))
	button.add_theme_color_override("font_pressed_color", Color(1.0, 0.96, 0.82))
	button.add_theme_color_override("font_disabled_color", Color(0.45, 0.48, 0.50))


static func style_buttons(root: Node) -> void:
	for child in root.get_children():
		if child is Button:
			style_button(child)
		elif child is Label:
			style_label(child)
		style_buttons(child)


static func style_label(label: Label, bold: bool = false) -> void:
	var font := _ui_bold_font() if bold else _ui_font()
	if font != null:
		label.add_theme_font_override("font", font)


static func _ui_font() -> FontFile:
	if ui_font == null:
		ui_font = _load_font(UI_FONT_PATH)
	return ui_font


static func _ui_bold_font() -> FontFile:
	if ui_bold_font == null:
		ui_bold_font = _load_font(UI_BOLD_FONT_PATH)
	return ui_bold_font


static func _load_font(path: String) -> FontFile:
	var font := FontFile.new()
	var error := font.load_dynamic_font(ProjectSettings.globalize_path(path))
	if error != OK:
		push_warning("Cannot load UI font: %s" % path)
		return null
	return font


static func _button_style(texture: Texture2D) -> StyleBoxTexture:
	var style := StyleBoxTexture.new()
	style.texture = texture
	style.texture_margin_left = 5.0
	style.texture_margin_top = 5.0
	style.texture_margin_right = 5.0
	style.texture_margin_bottom = 5.0
	return style
