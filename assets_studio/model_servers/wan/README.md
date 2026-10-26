# Wan model server

<!-- markdownlint-disable MD013 -->

Video from a first frame (`image-to-video`, ADR-003), based on
[Wan2.2 TI2V-5B](https://huggingface.co/Wan-AI/Wan2.2-TI2V-5B-Diffusers)
(Apache-2.0): 24 fps, about 720p, up to 5 seconds, with a
[GGUF Q8 transformer](https://huggingface.co/unsloth/Wan2.2-TI2V-5B-GGUF).

```sh
just setup    # Python 3.11 + PyTorch 2.5.1/CUDA 12.4 + diffusers + gguf environment
just doctor   # GPU, ffmpeg, downloaded weights, block storage and disk space
just serve    # http://127.0.0.1:9108; first start: ~22 GB download + ~11 GB storage
just test     # no GPU or weights needed
```

From `model_servers/`: `just wan`.

## Parameters

- **Duration** (1–5 seconds) and **size** (720p is native; 480p is faster but
  the model was trained at 720p and invents extra details more often) are
  primary parameters. Dimensions follow the first frame's aspect ratio
  (no wider than 1:4); the frame is scaled and center-cropped, not stretched.
- Steps (50), guidance (5.0) and negative prompt (Wan's original standard
  Chinese negative prompt by default).
- The optional prompt describes what should happen in the frame. English
  works best.

## Fitting in 8 GB

| Component | Size | Placement |
| --- | --- | --- |
| 5B transformer, GGUF Q8 | 5.4 GB | Ordinary RAM; blocks are pinned as needed and streamed to GPU on a CUDA stream, prefetching the next while the current one computes |
| UMT5-XXL text encoder | 11.4 GB (bf16) | Disk block storage (`~/.cache/assets-studio/wan-offload`), once per request |
| VAE (fp32) | 2.8 GB | GPU layer by layer, tiled encoding and decoding |

VRAM is left for activations: 5 seconds at 720p is about 27,000 tokens
(7.8 GB peak out of 8). The server needs the entire GPU; do not run other model
servers alongside it. Use 480p if memory runs out.
**A 720p × 5-second clip takes about an hour** (3643 seconds); 1 second at 480p
is about 7 minutes.

Experiments (1-second clip, 608×608, 50 steps):

| Variant | Time | Peak VRAM |
| --- | --- | --- |
| bf16 transformer from disk, no overlap | 768 s | 6.1 GB |
| Q8 entirely on GPU | 229 s | 7.8 GB; OOM at 720p × 5 s |
| Q8 in RAM, prefetched blocks, pinned copy of entire model | 254 s | 4.8 GB; free RAM fell to 1 GB |
| Q8 pinned in place once | 374 s | Diffusers pins again anyway; free RAM fell to 4 GB |
| **Q8 in RAM, blocks pinned as needed** | **413 s** | **4.8 GB; at least ~7 GB free RAM** |

Video is encoded as canonical MP4 (H.264 High, yuv420p, tagged BT.709,
`+faststart`, byte-reproducible) through `model_server_sdk.video`. Requires
`ffmpeg` with `libx264` (system installation or SDK `video` extra).
`just doctor` reports the version and `libx264` availability.
