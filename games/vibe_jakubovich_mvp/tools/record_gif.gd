extends SceneTree
## Deterministic offscreen render: compositor/window size cannot distort the GIF.


func _initialize() -> void:
	call_deferred("_record")


func _record() -> void:
	DirAccess.make_dir_recursive_absolute("res://tmp/wheel-gif")
	var viewport := SubViewport.new()
	viewport.size = Vector2i(800, 450)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var scene = load("res://scenes/main.tscn").instantiate()
	viewport.add_child(scene)
	scene.audio.muted = true
	scene.set_process(false)
	scene.reduced_motion = false
	scene.host.reduced_motion = false
	scene.stage_lights.reduced_motion = false
	scene.new_game(42)
	await process_frame
	for frame in 88:
		if frame == 8:
			scene._spin()
		await RenderingServer.frame_post_draw
		var error := viewport.get_texture().get_image().save_png(
			"res://tmp/wheel-gif/frame%03d.png" % frame
		)
		if error != OK:
			printerr("Could not record GIF frame: ", frame)
			quit(1)
			return
	scene.audio.shutdown()
	await create_timer(0.15).timeout
	viewport.queue_free()
	await process_frame
	print("Recorded 88 frames at 20 fps, 800 x 450.")
	quit()
