extends SceneTree
## Offline authoring: original four/eight-bar TV-band loops, no sampled recording.

const RATE := 22050
var left := PackedFloat32Array()
var right := PackedFloat32Array()
var noise := RandomNumberGenerator.new()


func _initialize() -> void:
	noise.seed = 1993
	DirAccess.make_dir_recursive_absolute("res://assets/audio")
	_compose(false)
	_compose(true)
	quit()


func _compose(spin: bool) -> void:
	var beat := 60.0 / (122.0 if spin else 84.0)
	var bars := 8 if spin else 4
	left.resize(int(beat * bars * 4 * RATE))
	right.resize(left.size())
	left.fill(0)
	right.fill(0)
	var roots := [48, 53, 57, 55]
	var melody := [12, 16, 19, 16, 14, 12, 7, 9]
	for bar in bars:
		var root_note: int = roots[bar % roots.size()]
		var third := 3 if root_note == 57 else 4
		var time := bar * beat * 4
		for chord_beat in [0.0, 1.5, 3.0]:
			for interval in [0, third, 7, 11 if third == 4 else 10]:
				_note(
					time + chord_beat * beat, beat * 1.7, root_note + interval, 0.1, "piano", -0.3
				)
		for step in 8:
			var offset: int = [0, 0, 7, 12, 0, 7, 10, 7][step]
			_note(
				time + step * beat / 2,
				beat * 0.42,
				root_note - 12 + offset,
				0.25 if spin else 0.18,
				"bass",
				0
			)
			_drum(time + step * beat / 2, "hat", 0.075 if spin else 0.027)
			if spin and step % 2 == 0:
				_note(
					time + step * beat / 2,
					beat * 0.6,
					root_note + melody[step + bar % 2],
					0.13,
					"brass",
					0.23
				)
		for step in 4:
			_drum(time + step * beat, "kick", 0.28 if spin else 0.11)
			if step % 2 == 1:
				_drum(time + step * beat, "snare", 0.12 if spin else 0.045)
	# A short stereo room tap, wrapped at the loop boundary.
	var dry_left := left.duplicate()
	var dry_right := right.duplicate()
	var delay := int(0.113 * RATE)
	var peak := 0.01
	for index in left.size():
		left[index] += dry_right[posmod(index - delay, left.size())] * 0.13
		right[index] += dry_left[posmod(index - delay, left.size())] * 0.13
		peak = maxf(peak, maxf(absf(left[index]), absf(right[index])))
	var bytes := PackedByteArray()
	bytes.resize(left.size() * 4)
	for index in left.size():
		bytes.encode_s16(index * 4, int(left[index] / peak * 26000))
		bytes.encode_s16(index * 4 + 2, int(right[index] / peak * 26000))
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.stereo = true
	stream.mix_rate = RATE
	stream.data = bytes
	var path := "res://assets/audio/%s.wav" % ("spin" if spin else "studio")
	var error := stream.save_to_wav(path)
	assert(error == OK, "Music export failed: " + path)
	print("Composed %s: %.2f seconds, stereo PCM." % [path, stream.get_length()])


func _note(
	start: float, duration: float, midi: int, gain: float, voice: String, pan: float
) -> void:
	var frequency := 440 * pow(2, (midi - 69) / 12.0)
	for index in int(duration * RATE):
		var time := float(index) / RATE
		var phase := TAU * frequency * time
		var envelope := minf(time / 0.012, 1) * minf((duration - time) / 0.06, 1)
		var value := 0.0
		match voice:
			"bass":
				value = (sin(phase) + 0.28 * sin(phase * 2)) * exp(-time * 6)
			"piano":
				value = sin(phase + 1.6 * sin(phase * 2) * exp(-time * 7)) * exp(-time * 3)
			"brass":
				for harmonic in range(1, 6):
					value += sin(phase * harmonic) * pow(0.48, harmonic - 1)
				value *= 0.55 * exp(-time * 2)
		_mix(int(start * RATE) + index, value * envelope * gain, pan)


func _drum(start: float, kind: String, gain: float) -> void:
	var duration := 0.22 if kind != "hat" else 0.06
	var previous := 0.0
	for index in int(duration * RATE):
		var time := float(index) / RATE
		var random_value := noise.randf_range(-1, 1)
		var value := 0.0
		match kind:
			"kick":
				value = sin(TAU * (48 * time + 7 * (1 - exp(-time * 30)))) * exp(-time * 22)
			"snare":
				value = (random_value * 0.7 + sin(TAU * 185 * time) * 0.3) * exp(-time * 27)
			"hat":
				value = (random_value - previous) * 0.45 * exp(-time * 65)
		previous = random_value
		value *= minf(time * 1500, 1)
		_mix(int(start * RATE) + index, value * gain, 0.3 if kind == "hat" else -0.05)


func _mix(index: int, value: float, pan: float) -> void:
	index %= left.size()
	left[index] += value * (1 - pan) * 0.7
	right[index] += value * (1 + pan) * 0.7
