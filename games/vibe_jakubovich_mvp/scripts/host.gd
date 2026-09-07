extends Node2D
## Four painted poses with beat-synchronised weight shifts; no video/API.

const SHEET = preload("res://assets/host-dance-alpha.png")
const DANCE := [0, 1, 2, 1]
const ALIGN := [-63.0, -8.0, -37.0, 18.0]
var sprite := Sprite2D.new()
var dancing := false
var celebration := 0.0
var elapsed := 0.0
var reduced_motion := false


func _ready() -> void:
	sprite.texture = SHEET
	sprite.hframes = 4
	sprite.frame = 3
	sprite.scale = Vector2.ONE * 0.61
	add_child(sprite)
	_process(0)


func cheer() -> void:
	celebration = 4.0


func _process(delta: float) -> void:
	elapsed += delta
	celebration = maxf(0, celebration - delta)
	var moving := (dancing or celebration > 0) and not reduced_motion
	var beat := elapsed * 122.0 / 60.0 * TAU
	sprite.frame = DANCE[int(elapsed * 122.0 / 60.0 * 2) % 4] if moving else 3
	var sway := sin(beat * 0.5) * 5.0 if moving else 0.0
	var bounce := -absf(sin(beat)) * 9.0 if moving else sin(elapsed * 1.8) * 1.4
	if reduced_motion:
		bounce = 0
	sprite.position = Vector2(ALIGN[sprite.frame] * 0.61 + sway, -249 + bounce)
	sprite.rotation = sin(beat * 0.5) * 0.022 if moving else 0.0
	queue_redraw()


func _draw() -> void:
	draw_set_transform(Vector2(0, 0), 0, Vector2(1, 0.22))
	draw_circle(Vector2.ZERO, 108, Color(0.015, 0.005, 0.03, 0.5))
