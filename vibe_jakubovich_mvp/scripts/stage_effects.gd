extends Node2D
## Soft theatrical lighting and bounded, non-strobing spark/confetti bursts.

const GOLD := Color("ffe0a0")
var foreground := false
var elapsed := 0.0
var spinning := false
var reduced_motion := false
var particles: Array[Dictionary] = []
var rng := RandomNumberGenerator.new()


func _ready() -> void:
	rng.randomize()


func burst(origin: Vector2, big: bool = false) -> void:
	if reduced_motion:
		return
	for index in 90 if big else 32:
		if particles.size() >= 300:
			break
		var angle := rng.randf_range(-PI, 0)
		particles.append(
			{
				"p": origin,
				"v": Vector2(cos(angle), sin(angle)) * rng.randf_range(70, 290),
				"life": rng.randf_range(0.9, 1.8),
				"color": [GOLD, Color("87e5ff"), Color("ffb5c8")][index % 3],
				"length": rng.randf_range(3, 7)
			}
		)


func _process(delta: float) -> void:
	if not reduced_motion:
		elapsed += delta
	for index in range(particles.size() - 1, -1, -1):
		var item: Dictionary = particles[index]
		item.life -= delta
		item.v.y += 210 * delta
		item.p += item.v * delta
		if item.life <= 0:
			particles.remove_at(index)
	queue_redraw()


func _draw() -> void:
	if foreground:
		for item in particles:
			var color: Color = item.color
			color.a = minf(1, item.life * 2)
			draw_line(item.p, item.p - item.v.normalized() * item.length, color, 2, true)
		return
	for index in 3:
		var start := Vector2(230 + index * 555, 30)
		var end := Vector2(320 + index * 455 + sin(elapsed * 0.37 + index * 2) * 185, 835)
		draw_polygon(
			PackedVector2Array([start, end - Vector2(120, 0), end + Vector2(120, 0)]),
			PackedColorArray(
				[Color(0.75, 0.8, 1, 0.09), Color(0.7, 0.75, 1, 0.005), Color(0.7, 0.75, 1, 0.005)]
			)
		)
	marquee(Rect2(105, 152, 904, 270), 28)
	marquee(Rect2(100, 30, 530, 102), 24)
	for index in 14:
		var location := Vector2(60 + index * 115, 894)
		draw_circle(location, 4, GOLD)
		draw_circle(location, 10, Color(GOLD, 0.06))


func marquee(rect: Rect2, spacing: int) -> void:
	var points: Array[Vector2] = []
	for x in range(int(rect.position.x), int(rect.end.x), spacing):
		points.append(Vector2(x, rect.position.y))
		points.append(Vector2(x, rect.end.y))
	for y in range(int(rect.position.y) + spacing, int(rect.end.y), spacing):
		points.append(Vector2(rect.position.x, y))
		points.append(Vector2(rect.end.x, y))
	for index in points.size():
		var level := 0.68 + sin(elapsed * (3 if spinning else 1.4) - index * 0.4) * 0.25
		draw_circle(points[index], 9, Color(GOLD, 0.06 * level))
		draw_circle(points[index], 3.6, Color(GOLD, level))
