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

## FLUX 3 Image Klein (Q8): `flux3`, dropped

Dropped on November 24, 2026 after review. Almost every image had an obvious
error: a microphone nobody asked for, a figure sitting on a table, broken
faces. It was not faster than Qwen-Image in practice. The microphone came
from the upsampler: its caption for a tavern bard read “singing into a tiny
brass horn microphone”, and the image model drew the caption faithfully. The
server's code stays; restore it with `just setup` and `just download` in its
directory and a `[[servers]]` entry.

Text to image and instruction editing in 16 steps, with BFL's prompt
upsampler expanding a request in any language into a full English caption
(`model_servers/flux3`, port 9149). Tested November 23, 2026 on an RTX 3070
Laptop (8 GB).

| | |
| --- | --- |
| License | FLUX Kommunity License (non-commercial; outputs for Qualifying Users under US$5M revenue); upsampler Apache-2.0 |
| Revisions | Base `3b767ff9`, GGUF `12ef13ce`, upsampler GGUF `3dc3ad42`; diffusers 0.43.0 |
| Weights | Transformer GGUF Q8 (6.1 GB); Qwen3-VL 4B encoder bf16 (8.9 GB); upsampler Q8 (9.5 GB) |
| Loading | ~30–50 s; first offload-store write (14 GB) |
| Whole job, upsampler included | 1K 2–3 min; 1K edit 3 min; 2K 4.5 min |
| Peak VRAM | 5.4 GB; 6.9 GB in the upsampler |

Compared with Qwen-Image at 1K and seed 1 (three jobs, Studio project `test`):

- **Speed.** FLUX 3 took 126–187 s, Qwen-Image 212–259 s.
- **Prompt following.** FLUX 3 followed prompts more literally: a bard
  playing a lute on a small stage, with listeners. Qwen-Image drew a seated
  figure holding a stick.
- **Editing.** For “make the cape dark red, change nothing else”, FLUX 3
  changed only the cape. Qwen-Image also turned the blue plume red.
- **Transparency.** FLUX 3 has none: edits of transparent pictures come out on
  white. Qwen-Image keeps alpha.

Details and integration findings: [server README](../../model_servers/flux3/README.md).

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
