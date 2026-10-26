# M0d: text-to-audio and audio-to-audio

## Outcome

**Stable Audio Open 1.0** (`model_servers/stable_audio`, port 9105).
Contract check: 17/17 without warnings.

| | |
| --- | --- |
| License | Stability AI Community License (gated weights) |
| Revision | `f21265c1` |
| Audio | 44.1 kHz stereo, 0.5–47 s |
| Time | ~25 s per sound at 100 steps, independent of duration |
| VRAM | ~3.5 GB between requests; peak 6.4 GB (7.6 with cache) |
| Audio-to-audio | Custom SDEdit implementation, for evolution |

## Trials (seed 1, 100 steps, guidance 7)

Spectrogram observations (user listening still needed):

- “old wooden door creaking open slowly”, 4 s: sliding creak tones.
- “sword swing whoosh followed by metal clang”, 2 s: whoosh then ringing impact.
- “cartoon coin pickup, bright chime”, 1.5 s: two clicks and a chime.
- “forest ambience, birds chirping, light wind”, 10 s: broad noise and chirping.
- “footsteps on gravel”, 5 s: regular impacts.
- Music: “128 BPM tech house drum loop”, exactly 32 beats in 15 seconds;
  village lute/flute; chiptune with square-wave harmonics.

Levels varied from −41 to 0 dBFS (the sword clipped); added peak normalization
to −1 dB.

## Audio-to-audio

Diffusers' built-in `initial_audio_waveforms` adds the input to full-strength
noise, retaining almost nothing at any setting. The custom implementation
noises the source to strength-selected σ and denoises with every step.

Source similarity: log-spectrogram correlation; two sources (door creak,
footsteps) × two seeds. Larger σ retains less source; at σ = 500 (generation
from scratch), correlations are 0.45 and 0.62, the “new sound” baseline.

| σ | Creak | Footsteps |
| --- | --- | --- |
| 0.38 | 0.93 | 0.92 |
| 1.0 | 0.88 | 0.85 |
| 2.0 | 0.80 | 0.76 |
| 4.1 | 0.69 | 0.69 |
| 9.4 | 0.52 | 0.51 |
| 23 | 0.45 | 0.55 |
| 61–500 | 0.40–0.45 | 0.62–0.63 |

Earlier strength scales:

1. Linear truncation of σ schedule 500→0.3 compressed the useful range into 0–0.4.
2. σ = 0.3·(500/0.3)^(strength^1.5), using all steps: the upper slider third
   (σ >23) was indistinguishable from 1.

Final: σ = 0.3·(40/0.3)^strength, using all steps. 0.1 → σ 0.48, 0.3 → 1.3,
0.5 → 3.5, 0.7 → 9.7, 0.9 → 24, 1 → 40: change spans the whole slider.

## VRAM

Peak `torch.cuda.max_memory_allocated` for 47 s × four variants: 5.9 GB for
text-to-audio, 6.4 GB for audio-to-audio; 7.6 GB card-wide with allocator cache.
Independent of duration because the model always computes 47-second windows.
Run the server alone; release cache after every request.

## Correction

The first report claimed a diffusers bug: noise state allegedly survived
requests. That was incorrect; diffusers 0.40 resets it in `set_timesteps`
(found by the critic). The redundant server reset was removed; a test ensures
partial schedules preserve this behavior.

## Not tested

- Stable Audio Open Small (341M): faster but lower quality; speed is not the issue.
- AudioLDM 2, MusicGen: lower effects quality; MusicGen is music only.
- Vocal music and long tracks require a separate track of work (ACE-Step, etc.).

## Review

Critic: two iterations, ACCEPT.

1. There was no diffusers noise-reset bug (see Correction).
2. The report gave idle VRAM (3.5 GB), not peak (6.4 GB), and recommended
   running beside XTTS/TripoSR. Fixed: run alone.
3. FLAC/OGG were advertised while Studio stored only WAV. Now declares WAV;
   transcoding imports are planned for M7.

Recommendations adopted: +40 dB gain cap, audio-to-audio loudness matched to
source, reading only the 47-second window, strength scale measured on multiple seeds.

## YuE2 3B — server ready

Vocal songs from lyrics and style (`model_servers/yue2`, port 9110, “Text → song”).
Stable Audio remains for effects and short instrumentals. Tested October 2,
2026 on an RTX 3070 Laptop (8 GB).

| | |
| --- | --- |
| License | CC BY-NC 4.0 + personal/content-creator permission |
| Revisions | Model `c044757a`, VAE `152733a1`; yue2-infer `18a07bb` |
| Weights | ~7.3 GB bf16 model and 0.5 GB VAE |
| 62-second song | ~62 s including model load; 93/110-second pair: ~184 s |
| Peak VRAM | 5.7–6.1 GB (authors target 24 GB) |
| Server RAM | ~9 GB, stable |

Upstream targets 24 GB VRAM; on 8 GB the AR/NAR halves take turns on GPU with
identical weights and computations. Repeated identical seeds yield bit-identical
songs. Russian lyrics with “Russian, fantasy folk ballad…” yielded 93/110 seconds,
not truncated. Loudness builds toward the chorus, and stereo is genuine
(channel correlation 0.74–0.86); listening quality has not yet been evaluated.

Details: [server README](../../model_servers/yue2/README.md).
