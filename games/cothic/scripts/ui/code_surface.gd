class_name CodeSurface
extends RefCounted

const MONO_FONT: FontFile = preload("res://assets/fonts/MononokiNerdFontMono-Regular.ttf")
const FONT_SIZE := 22


static func configure(surface: TextEdit, show_line_numbers: bool = true) -> void:
	surface.editable = false
	surface.wrap_mode = TextEdit.LINE_WRAPPING_NONE
	surface.scroll_fit_content_height = false
	surface.add_theme_font_override("font", MONO_FONT)
	surface.add_theme_font_size_override("font_size", FONT_SIZE)
	surface.add_theme_color_override("font_color", Color(0.88, 0.90, 0.87))
	surface.add_theme_color_override("font_readonly_color", Color(0.88, 0.90, 0.87))
	surface.add_theme_color_override("selection_color", Color(0.24, 0.36, 0.42, 0.85))
	surface.add_theme_color_override("current_line_color", Color(0.18, 0.20, 0.20, 0.8))

	_set_property_if_present(surface, "draw_line_numbers", show_line_numbers)
	_set_property_if_present(surface, "gutters_draw_line_numbers", show_line_numbers)
	_set_property_if_present(surface, "highlight_current_line", true)


static func _set_property_if_present(
	surface: TextEdit, property_name: String, value: Variant
) -> void:
	for property in surface.get_property_list():
		if property.get("name", "") == property_name:
			surface.set(property_name, value)
			return
