extends RefCounted
## Reproducible real-GPU visual tour. Does not write game progress/settings.


static func run(scene) -> void:
	scene.new_game(42)
	await shot(scene, "studio", 1.0)
	scene._spin()
	await shot(scene, "dance-a", 0.1)
	await shot(scene, "dance-b", 0.3)
	scene.new_game(42)
	scene.game.spin(0)
	scene._choose_letter(scene.game.word[0])
	await shot(scene, "letter", 0.3)
	scene._help()
	await shot(scene, "rules", 0.35)
	scene._close_popup()
	scene._solve_dialog()
	await shot(scene, "answer", 0.35)
	scene._close_popup()
	scene.game.solve(scene.game.word)
	scene._refresh()
	await shot(scene, "finalists", 0.35)
	scene._close_popup()
	scene.finalist = {"score": 99999, "cash": 5000}
	scene.gifts.assign(["Чайный сервиз", "Велосипед", "Телевизор"])
	scene._shop()
	await shot(scene, "shop", 0.35)
	scene._super_offer()
	await shot(scene, "super-offer", 0.35)
	scene._quit_game()


static func shot(scene, name: String, delay: float) -> void:
	await scene.get_tree().create_timer(delay).timeout
	await RenderingServer.frame_post_draw
	var error: int = scene.get_viewport().get_texture().get_image().save_png(
		"res://tmp/%s.png" % name
	)
	assert(error == OK, "Could not save screenshot: " + name)
