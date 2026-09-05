extends Node
## Small original synthesized studio cues; no samples or network dependencies.

var muted := false:
	set(value):
		muted = value
		for player in voices:
			if muted:
				player.stop()
		for player in music.values():
			player.volume_db = -80 if muted else _music_volume(player.name)
var voices: Array[AudioStreamPlayer] = []
var sounds: Dictionary = {}
var voice_index := 0
var music: Dictionary = {}
var music_mode := "studio"


func _ready() -> void:
	for index in 6:
		var player := AudioStreamPlayer.new()
		player.volume_db = -14
		add_child(player)
		voices.append(player)
	sounds["tick"] = _tone([1800.0], 0.025)
	sounds["letter"] = _tone([523.25, 659.25, 783.99], 0.33)
	sounds["wrong"] = _tone([196.0, 185.0], 0.28)
	sounds["win"] = _tone([523.25, 659.25, 783.99, 1046.5], 0.8)
	sounds["button"] = _tone([660.0], 0.055)
	for track in ["studio", "spin"]:
		var player := AudioStreamPlayer.new()
		player.name = track
		var stream: AudioStreamWAV = load("res://assets/audio/%s.wav" % track)
		stream.loop_mode = AudioStreamWAV.LOOP_FORWARD
		stream.loop_begin = 0
		stream.loop_end = int(stream.get_length() * stream.mix_rate)
		player.stream = stream
		player.volume_db = -80
		add_child(player)
		music[track] = player
		if DisplayServer.get_name() != "headless":
			player.play()


func set_music(track: String) -> void:
	if track == music_mode or not music.has(track):
		return
	music_mode = track
	if track == "spin" and DisplayServer.get_name() != "headless":
		music[track].play(0)


func _music_volume(track: String) -> float:
	return (-23.0 if track == "studio" else -15.0) if track == music_mode else -80.0


func _process(delta: float) -> void:
	for track in music:
		var target := -80.0 if muted else _music_volume(track)
		music[track].volume_db = move_toward(music[track].volume_db, target, delta * 95)


func play(cue: String) -> void:
	if muted or not sounds.has(cue):
		return
	var player: AudioStreamPlayer = voices[voice_index % voices.size()]
	voice_index += 1
	player.stream = sounds[cue]
	player.play()


func shutdown() -> void:
	set_process(false)
	for player in voices:
		player.stop()
		player.stream = null
	for player in music.values():
		player.stop()
		player.stream = null


func _exit_tree() -> void:
	shutdown()


func _tone(frequencies: Array, duration: float) -> AudioStreamWAV:
	var rate := 22050
	var count := int(duration * rate)
	var data := PackedByteArray()
	data.resize(count * 2)
	for index in count:
		var time := float(index) / rate
		var envelope := minf(time * 120, 1.0) * pow(1.0 - time / duration, 2)
		var value := 0.0
		for frequency in frequencies:
			value += sin(TAU * frequency * time) / frequencies.size()
		data.encode_s16(index * 2, int(value * envelope * 14000))
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = rate
	stream.data = data
	return stream
