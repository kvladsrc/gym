class_name WildAnimal3D
extends AnimatedSprite3D

var _route := PackedVector3Array()
var _route_direction := 1
var _target_index := 1
var _target_position := Vector3.ZERO
var _run_speed := 3.0


## Configures a looping run along a route through the location.
func configure_run(
	frames: SpriteFrames, route: PackedVector3Array, run_speed: float, animation_offset: float
) -> void:
	assert(route.size() >= 2)
	sprite_frames = frames
	_route = route
	_target_index = 1
	_target_position = route[_target_index]
	_run_speed = run_speed
	position = route[0]
	play("run")
	set_frame_and_progress(
		int(animation_offset * float(sprite_frames.get_frame_count("run"))), animation_offset
	)


func _process(delta: float) -> void:
	var direction := position.direction_to(_target_position)
	position = position.move_toward(_target_position, _run_speed * delta)
	_update_facing(direction)

	if position.distance_squared_to(_target_position) < 0.01:
		_advance_route()


func _advance_route() -> void:
	if _target_index == _route.size() - 1:
		_route_direction = -1
	elif _target_index == 0:
		_route_direction = 1
	_target_index += _route_direction
	_target_position = _route[_target_index]


func _update_facing(direction: Vector3) -> void:
	var camera := get_viewport().get_camera_3d()
	if camera == null:
		return
	flip_h = direction.dot(camera.global_basis.x) < 0.0
