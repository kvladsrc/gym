class_name SwayingSprite3D
extends Sprite3D

var _amplitude := 0.0
var _elapsed := 0.0
var _phase := 0.0
var _speed := 0.0


## Configures a gentle wind sway around the sprite's grounded origin.
func configure_sway(phase: float, amplitude_degrees: float, speed: float) -> void:
	_phase = phase
	_amplitude = deg_to_rad(amplitude_degrees)
	_speed = speed


func _process(delta: float) -> void:
	_elapsed += delta
	var primary := sin(_elapsed * _speed + _phase)
	var secondary := sin(_elapsed * _speed * 0.47 + _phase * 1.7) * 0.22
	rotation.z = (primary + secondary) * _amplitude
