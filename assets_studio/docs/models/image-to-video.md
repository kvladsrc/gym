# Image to video (video from a first frame)

<!-- markdownlint-disable MD013 -->

## Outcome

**Wan2.2 TI2V-5B** (`model_servers/wan`, port 9108): Apache-2.0,
24 fps, about 720p, up to 5 seconds; GGUF Q8 transformer.

Retired on 2026-11-09: an hour per five-second clip is too slow. The server's
code stays; the studio configuration and the launcher no longer list it.

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

## Integration findings

- **Color.** Clips are encoded as tagged BT.709: browsers read untagged HD as
  BT.709, and untagged BT.601 shifted reds and greens in every clip (red Y/U/V
  81/90/240 instead of 63/102/240). The SDK and the studio share these
  options. The studio transcodes uploaded video without BT.709 tags, reading
  untagged sources as BT.709 above 576 lines and BT.601 below, as players do.
- The first frame is center-cropped rather than stretched; the aspect ratio is
  at most 1:4 (100:1 would yield 9472×64); the frame count is the nearest
  4k+1; the original Chinese negative prompt is kept.
- ffmpeg logs to a file (a full pipe could hang encoding), and a fixed x264
  thread count gives identical bytes across machines. `doctor` reports
  ffmpeg and libx264.
