# Text to image and image editing

<!-- markdownlint-disable MD013 -->

## Qwen-Image 2.1 (Q8): `qwen_image`

Text to image with transparency and lettering, and instruction editing
(`model_servers/qwen_image`, port 9109). Tested October 2, 2026 on an RTX 3070
Laptop (8 GB).

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

The best quality of the three image models tried (below). Plain-background
props are clean painted game assets. “THE MOSSY LANTERN” signage has no errors.
Transparent potion: 49% transparent, 50% opaque pixels, <1% translucent edge.
Editing (“emerald crystal, a little moss on the lid”) retains shape/composition.
2K contains real detail: individual stones/leaves at full-resolution crops.
Default 2K prioritizes quality; 1K is 3–6× faster.

Details and integration findings: [server README](../../model_servers/qwen_image/README.md).

## Replaced models

- **FLUX.1-schnell, GGUF Q8** (`flux`, retired; its code stays,
  [README](../../model_servers/flux/README.md)): 55–70 s per 1024² image at
  ~5.3 GB VRAM, with offloaded blocks kept on disk. For “a mossy stone lantern,
  stylized game asset, soft hand-painted texture, centered, plain light
  background” it drew exactly the description, Q8 with cleaner metalwork, moss
  and base than Q3.
- **SDXL base 1.0** (removed in October 2026): ~23 s per 1024² image at
  ~5.4 GB VRAM; stylized props with painted texture, and image-to-image at
  strength ~0.5 kept shape and composition. For the same prompt it added an
  unrequested forest and rocks; its CLIP encoders handle Russian poorly.

Both ignore prohibitions such as capes and held weapons on rig-ready
characters ([3D guide](../guides/3d.md)); Qwen-Image follows them.
