# Stable Audio model server

Contract v1 sounds, effects and music based on
[Stable Audio Open 1.0](https://huggingface.co/stabilityai/stable-audio-open-1.0):
44.1 kHz stereo, up to 47 seconds.

```sh
just setup    # Python 3.11 + PyTorch 2.5.1/CUDA 12.4 + diffusers environment
just doctor   # PyTorch, GPU, token discovery and downloaded weights
just serve    # http://127.0.0.1:9105; downloads ~5 GB on first start
just test     # no GPU or weights needed
```

Weights are gated on Hugging Face: accept the model terms with your account
and provide its token in `HF_TOKEN` (or `HUGGINGFACE_TOKEN`), or save it with
`hf auth login`. License: Stability AI Community License, for non-commercial
use and small businesses.

## Tasks

- **text-to-audio**: sound from a description; duration 0.5–47 seconds, steps,
  guidance, negative prompt and loudness normalization.
- **audio-to-audio**: sound modification (for evolution); input WAV (mono or
  stereo, any sample rate; uses the first 47 seconds), strength and an optional
  description. Output loudness matches the source.

The entire model stays on GPU (fp16). About 25 seconds per sound at 100 steps,
regardless of duration: the model always computes a 47-second window; four
47-second variants take about 100 seconds. VRAM: ~3.5 GB between requests,
up to 6.4 GB during generation (7.6 GB including allocator cache), also
independent of duration. Prefer running this server alone.

## Additions to diffusers

- **Actual audio-to-audio.** The pipeline's `initial_audio_waveforms` adds the
  input to full-strength noise (σ = 500), leaving almost none of the source.
  Here the input is noised to the strength-selected level, and all steps
  denoise from that level (`schedule.py`).
- **Strength scale.** The source is largely retained up to σ ≈ 1, half changed
  at σ ≈ 4–9, and sounds new from σ ≈ 23 (out of 500). Strength therefore maps
  logarithmically to σ from 0.3 to 40: 0.1–0.3 is a light edit, 0.5 noticeable,
  0.7 retains half the source, and 0.9–1 produces a new sound.
- **Loudness.** Model peaks vary from −40 to 0 dBFS, sometimes clipping. By
  default the peak is normalized to −1 dB (to the source peak for audio-to-audio),
  with gain capped at +40 dB. The original peak is in `meta.peak_before_normalizing`.
- VRAM cache is released after each request; otherwise the process retains
  ~7.6 GB between requests as well.

## Music

The model was also trained on music (FMA): loops, ambient and background music
up to 47 seconds with a specified tempo (`128 BPM` in the prompt is respected).
Vocal songs and long tracks are outside its scope.
