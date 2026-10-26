# M7b: image-to-video (video from a first frame)

<!-- markdownlint-disable MD013 -->

## Outcome

**Wan2.2 TI2V-5B** (`model_servers/wan`, `just wan`, port 9108): Apache-2.0,
24 fps, about 720p, up to 5 seconds; GGUF Q8 transformer.

## Candidates (as of 2026-10-01)

| Model | Selection rationale |
| --- | --- |
| **Wan2.2 TI2V-5B** | Selected: 5B, diffusers, Apache-2.0, Q8 is 5.4 GB |
| Wan2.2 I2V-A14B | Two 14B experts: estimated 30–60 min per clip even in Q8; fallback if 5B is insufficient |
| LTX-2.5 (July 2026) | 22B transformer (42 GB bf16) + Gemma 4 12B encoder (26 GB), does not fit |
| HunyuanVideo-1.5, FramePack | Tencent license excludes the EU |
| CogVideoX 1.5 | Outdated (2024) |

## Fitting in 8 GB (1-second clip, 608×608, 50 steps)

| Variant | Time | Peak VRAM | Issue |
| --- | --- | --- | --- |
| bf16 transformer from disk | 768 s | 6.1 GB | GPU waits for 10 GB per each of ~100 passes |
| Q8 entirely on GPU | 229 s | 7.8 GB | 720p × 5 s runs out of VRAM (+1.8 GB activations) |
| Q8 in pinned RAM, prefetched blocks | 254 s | 4.8 GB | Pinned copy of entire model; free RAM fell to 1 GB |
| Q8 pinned in place once | 374 s | 4.8 GB | Diffusers pins again; free RAM fell to 4 GB |
| **Same, pinning as needed** | **413 s** | **4.8 GB** | Slower, but at least ~7 GB free RAM |

The UMT5 embedding table (2.1 GB) also failed to fit beside the transformer.
Prefetching avoids keeping the full transformer on GPU, resolving this.
Pinned memory is released after each request (`torch._C._host_emptyCache`).

## Quality

- 480p (608×608 from a square frame), 1 second: first frame preserved, but an
  unrelated object entered the frame; the model was trained at 720p.
- **720p (928×928), 5 seconds: 3012 s (50 min)**: stationary lantern with gently
  changing glow and no extras (measured before switching to on-demand pinning).
- Identical seeds/parameters give byte-identical MP4s (three runs).

## Contract check

`just check-server http://127.0.0.1:9108 --timeout 3600 --param
seconds=1.0 --param size=480p`: **10/10 without warnings** (two clips in 813 s).
Defaults (720p × 5 s) would take several hours to check, motivating `--param`.

**720p × 5 s with final configuration: 3643 s (61 min)**, at least 5 GB free RAM,
7.8 GB peak VRAM (mostly activations). Tagged BT.709 color.

## Review

Critic, iteration 1: REVISE.

1. **Color.** ffmpeg used BT.601 RGB → YUV without tags; browsers interpret
   untagged HD as BT.709, shifting reds/greens in every clip. The SDK bug would
   affect every video server. Fixed with tagged BT.709 (red Y/U/V 63/102/240
   instead of 81/90/240), shared SDK/Studio options; Studio transcodes uploaded
   video lacking BT.709 tags.
2. README/docstring memory descriptions lagged behind final configuration.

Recommendations implemented: center-crop first frame rather than stretching
(16:9 at 480p stretched 4.4%); aspect ratio at most 1:4 (100:1 would yield
9472×64); nearest 4k+1 frame count (1.1 seconds previously gave 29 instead of
25 frames); original Chinese negative prompt; ffmpeg logs to file (a full pipe
could hang encoding); fixed x264 thread count (identical bytes across machines);
`doctor` reports ffmpeg/libx264; removed unused bf16 weight cache (19 GB).

Iteration 2: REVISE. The first fix transcoded every untagged video but let
ffmpeg assume BT.601. HD (usually actually BT.709) shifted colors the other way
(red 58/105/229). Untagged sources are now BT.709 above 576 lines, otherwise
BT.601, matching players. Tests cover red 720p and 240p inputs.

Iteration 3: ACCEPT.
