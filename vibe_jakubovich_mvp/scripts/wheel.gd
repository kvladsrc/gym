extends Node3D
## A real mesh wheel, shared deterministic index/angle mapping with the rules.

signal landed(index: int)
signal tick

const RULES = preload("res://scripts/round_rules.gd")
const COLORS := [Color("204888"), Color("f2d59b"), Color("9e3344"), Color("204b43")]
var disk := Node3D.new()
var spinning := false
var last_tick := -1
var ticks_enabled := true
var spin_tween: Tween


func _ready() -> void:
	add_child(disk)
	var environment := WorldEnvironment.new()
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0, 0, 0, 0)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("c3e0dd")
	env.ambient_light_energy = 0.65
	environment.environment = env
	add_child(environment)
	var camera := Camera3D.new()
	add_child(camera)
	camera.position = Vector3(0, 6.7, 8.0)
	camera.look_at(Vector3.ZERO)
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 3.9
	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-55, -25, 0)
	light.light_color = Color("ffe2aa")
	light.light_energy = 1.6
	add_child(light)
	var fill := DirectionalLight3D.new()
	fill.rotation_degrees = Vector3(-20, 140, 0)
	fill.light_color = Color("72cfce")
	fill.light_energy = 0.6
	add_child(fill)
	_cylinder(self, 2.35, 0.4, -0.4, Color("30272b"))
	_cylinder(disk, 2.6, 0.23, -0.1, Color("b99457"))
	_cylinder(disk, 2.49, 0.24, -0.06, Color("152d39"))
	for index in RULES.SECTORS.size():
		_sector(index)
	_cylinder(disk, 0.46, 0.12, 0.12, Color("d9b574"))
	_cylinder(disk, 0.32, 0.14, 0.14, Color("1b676b"))
	var pointer := MeshInstance3D.new()
	var cone := CylinderMesh.new()
	cone.top_radius = 0.0
	cone.bottom_radius = 0.17
	cone.height = 0.55
	pointer.mesh = cone
	pointer.material_override = _material(Color("ffe8b0"))
	pointer.position = Vector3(0, 0.21, 2.56)
	pointer.rotation.x = -PI / 2
	add_child(pointer)


func _material(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.metallic = 0.35
	material.roughness = 0.48
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	return material


func _cylinder(parent: Node3D, radius: float, height: float, y: float, color: Color) -> void:
	var mesh := MeshInstance3D.new()
	var cylinder := CylinderMesh.new()
	cylinder.top_radius = radius
	cylinder.bottom_radius = radius
	cylinder.height = height
	cylinder.radial_segments = 96
	mesh.mesh = cylinder
	mesh.material_override = _material(color)
	mesh.position.y = y
	parent.add_child(mesh)


func _sector(index: int) -> void:
	var step := TAU / RULES.SECTORS.size()
	var center := index * step
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for part in 8:
		var a := center - step * 0.49 + step * 0.98 * part / 8.0
		var b := center - step * 0.49 + step * 0.98 * (part + 1) / 8.0
		for point in [
			Vector3.ZERO, Vector3(sin(a), 0, cos(a)) * 2.4, Vector3(sin(b), 0, cos(b)) * 2.4
		]:
			surface.set_normal(Vector3.UP)
			surface.add_vertex(point + Vector3(0, 0.07, 0))
	var mesh := MeshInstance3D.new()
	mesh.mesh = surface.commit()
	var color: Color = COLORS[index % COLORS.size()]
	if RULES.SECTORS[index] == RULES.BANKRUPT:
		color = Color("11191f")
	mesh.material_override = _material(color)
	disk.add_child(mesh)
	var label := Label3D.new()
	label.text = RULES.sector_text(RULES.SECTORS[index])
	label.font = preload("res://assets/fonts/AlegreyaSansSC-Bold.ttf")
	label.font_size = 64
	label.pixel_size = 0.0055
	label.outline_size = 0
	label.modulate = Color("162d36") if index % 4 == 1 else Color("fff3d6")
	label.position = Vector3(sin(center) * 1.77, 0.095, cos(center) * 1.77)
	label.rotation = Vector3(-PI / 2, 0, -center)
	disk.add_child(label)
	var peg := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.05
	sphere.height = 0.1
	peg.mesh = sphere
	peg.material_override = _material(Color("f6d997"))
	peg.position = Vector3(sin(center + step / 2) * 2.49, 0.12, cos(center + step / 2) * 2.49)
	disk.add_child(peg)


func spin_to(index: int, duration: float = 3.2) -> void:
	if spinning:
		return
	spinning = true
	var step := TAU / RULES.SECTORS.size()
	var target := -index * step
	var current := disk.rotation.y
	target += ceil((current - target) / TAU) * TAU + TAU * 5
	spin_tween = create_tween()
	(
		spin_tween
		. tween_property(disk, "rotation:y", target, duration)
		. set_trans(Tween.TRANS_QUART)
		. set_ease(Tween.EASE_OUT)
	)
	spin_tween.finished.connect(_finish_spin.bind(index, target))


func _finish_spin(index: int, target: float) -> void:
	disk.rotation.y = fposmod(target, TAU)
	spinning = false
	landed.emit(index)


func cancel_spin() -> void:
	if spin_tween != null:
		spin_tween.kill()
	spinning = false


func _process(_delta: float) -> void:
	if spinning:
		var boundary := int(floor(disk.rotation.y / (TAU / RULES.SECTORS.size())))
		if boundary != last_tick:
			last_tick = boundary
			if ticks_enabled:
				tick.emit()
