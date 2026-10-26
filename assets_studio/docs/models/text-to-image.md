# M0a. Text-to-image and image-to-image

<!-- markdownlint-disable MD013 -->

## SDXL base 1.0 — server removed (October 2026)

Removed with TripoSR and Stable Fast 3D: Qwen-Image and FLUX give better images.
Like FLUX, SDXL ignores prohibitions (capes, held weapons on rig-ready characters;
see [guide](../guides/3d.md)). Measurements retained for history.

Tested September 30, 2026 on an RTX 3070 Laptop (8 GB).

| | |
| --- | --- |
| License | CreativeML Open RAIL++-M; sdxl-vae-fp16-fix VAE: MIT |
| Revisions | Base `46216598`, VAE `207b116d` |
| Weights | ~7 GB (fp16 only) |
| First load | ~6 min including download; cached afterward |
| text-to-image 1024², 30 steps | ~23 s per variant |
| image-to-image 1024², strength 0.55 (default 0.5) | ~14 s per variant |
| Four text-to-image variants | ~92 s |
| Peak VRAM | ~5.4 GB; models on CPU between calls (~0.5 GB retained) |
| Contract check | 17 checks, 0 errors, 0 warnings (bit-reproducible variants) |

Good game-asset quality: stylized props with painted texture. Image-to-image
at ~0.5 strength keeps shape/composition and changes details, suitable for evolution.

### Integration findings

- `StableDiffusionXLImg2ImgPipeline.from_pipe()` rebuilds CPU offload so text
  encoders remain on GPU while UNet loads, exceeding 8 GB. Build the second
  pipeline from first-pipeline components (`Pipeline(**pipeline.components)`)
  to share weights/hooks: 5.4 GB peak.
- Attention slicing and tiled VAE were unnecessary.
- Prefer English prompts: SDXL CLIP encoders handle Russian poorly. Prompt
  translation (for example with Gemma from `local_llm`) is a possible improvement.

### Review

Three critic findings fixed with tests: zero-denoising-step image-to-image
(`steps × strength < 1`) now rejects clearly rather than failing internally;
palette/grayscale PNG transparency no longer becomes black; very narrow inputs
no longer expand to several MP, with aspect ratio capped at 1:4. GPU-tested.

Second iteration: OOM exceptions retained failed call frames/activations,
preventing cache release. Store only error text. Live test with another process
using 4.5 GB: request returns a retryable failure and server retains ~200 MB.
Third iteration: **ACCEPT**.

## FLUX.1-schnell (Q8) — server ready

Second image server (`model_servers/flux`, port 9104). SDXL retained at this stage.

| | |
| --- | --- |
| License | Apache-2.0 (gated weights: token and terms acceptance required) |
| Revisions | Base `741f7c3c`, GGUF `f495746e` |
| Transformer | GGUF Q8 (12.7 GB), T5 bf16 (9.5 GB) |
| 1024² image, four steps | 55–70 s including T5; load ~35 s (first ~2 min) |
| Peak VRAM | ~5.3 GB process-wide (blocks alternate, CLIP/VAE stay loaded) |
| RAM | Blocks on disk (~21 GB); ≥18 GB free during generation, ≥10 GB loading |
| First start | ~22 GB download + ~21 GB block storage |
| Contract check | 17/17 without warnings |

### Same-prompt/seed comparison

Prompt: “a mossy stone lantern, stylized game asset, soft hand-painted texture,
centered, plain light background”.

- **SDXL:** added unrequested forest and rocks.
- **FLUX Q3:** exactly the description, centered lantern on light background,
  glowing glass.
- **FLUX Q8:** same, with cleaner finial metalwork, moss and base.

Chose Q8: quality over speed.

### Configuration experiments

| Variant | Outcome |
| --- | --- |
| T5 on CPU (bf16) | 545 s per prompt; Ryzen 5900HX lacks hardware bf16 |
| T5 GPU blocks | 9 s, 0.94 GB peak |
| Q3 + ordinary CPU offload | 90 s, 5.6 GB peak |
| Q4 + ordinary CPU offload | Does not fit (7.66 GB) |
| Q8 + accelerate layer offload | Unsupported with GGUF |
| Q8 + group offload, RAM blocks | Separate processes: 100 s, 2.55 GB peak; together with T5: ~22 GB RAM, kernel kills server |
| **Q8 + group offload, disk blocks** | **70 s, ≥18 GB free RAM; bit-identical image** |

## Qwen-Image 2.1 (Q8) — server ready

Third image server (`model_servers/qwen_image`, port 9109): transparency,
lettering and instruction editing. SDXL/FLUX retained at this stage. Tested
October 2, 2026 on an RTX 3070 Laptop (8 GB).

| | |
| --- | --- |
| License | Qwen Research License (non-commercial) |
| Revisions | Base `d26bb612`, GGUF `2c31ccd3`; diffusers main `578c9b2` |
| Transformer | GGUF Q8 (7.6 GB); Qwen3-VL 8B encoder bf16 (~16 GB) |
| Loading | ~100 s; first offload-storage write (~23 GB): ~2 min |
| Text-to-image, 40 steps | 1024² ~3.5 min (5 s/step); 2048² ~12 min (17 s/step) |
| Editing, 40 steps | 1K ~4 min; 2K ~27 min (no key/value cache) |
| Peak VRAM | 5.2–5.3 GB; editing 7.5 GB |
| Free RAM during generation | ≥15.5 GB |

Best quality of the three image servers. Plain-background props are clean
painted game assets. “THE MOSSY LANTERN” signage has no errors. Transparent
potion: 49% transparent, 50% opaque pixels, <1% translucent edge. Editing
(“emerald crystal, a little moss on the lid”) retains shape/composition. 2K
contains real detail: individual stones/leaves at full-resolution crops.
Default 2K prioritizes quality; 1K is 3–6× faster.

Details and integration findings: [server README](../../model_servers/qwen_image/README.md).

## Not tested

## FLUX review

Critic: two iterations, ACCEPT.

1. Disk blocks were reused without validation: interrupted initial starts
   reported ready but every generation failed; revised weights/libraries
   could silently produce wrong images from stale blocks. Now uses a version
   fingerprint directory, `complete` marker after `sync`, locking and disk-space
   checks (shared SDK `model_server_sdk.offload`).
2. Image-to-image used SDXL step rounding: defaults reported two instead of
   three steps and rejected some valid requests. Now uses its own formula,
   tested against diffusers.
