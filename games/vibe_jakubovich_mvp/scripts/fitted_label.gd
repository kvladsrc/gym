extends Label
## Fixed stage signage: measure wrapped text before choosing a readable size.

var preferred_size := 24
var minimum_size := 14
var bounds := Vector2(100, 40)
var previous_text := "\u0001"


func _ready() -> void:
	clip_text = true
	autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_theme_constant_override("line_spacing", 0)
	fit()


func _process(_delta: float) -> void:
	if text != previous_text:
		fit()


func measure(font_size: int) -> Vector2:
	return get_theme_font("font").get_multiline_string_size(
		text,
		horizontal_alignment,
		bounds.x,
		font_size,
		-1,
		TextServer.BREAK_MANDATORY | TextServer.BREAK_WORD_BOUND | TextServer.BREAK_ADAPTIVE
	)


func fit() -> void:
	previous_text = text
	var font_size := preferred_size
	while font_size > minimum_size:
		var measured := measure(font_size)
		if measured.y <= bounds.y - 2 and measured.x <= bounds.x + 1:
			break
		font_size -= 1
	add_theme_font_size_override("font_size", font_size)
	# Label's implicit minimum must not enlarge a fixed physical sign.
	max_lines_visible = maxi(1, int(bounds.y / get_theme_font("font").get_height(font_size)))
	size = bounds
	tooltip_text = text
