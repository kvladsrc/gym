# Qwen-Image model server

Contract v1 text-to-image and instruction-based image editing (image-to-image),
based on [Qwen-Image 2.1](https://huggingface.co/Qwen/Qwen-Image-2.1), with a
[GGUF Q8 transformer](https://huggingface.co/unsloth/Qwen-Image-2.1-GGUF).

```sh
just setup    # Python 3.11 + PyTorch 2.9.1/CUDA 12.8 + diffusers (main) environment
just doctor   # PyTorch, GPU, downloaded weights and offload storage state
just serve    # http://127.0.0.1:9109; first start ~4 min, ~50 GB on disk
just test     # declarations, prompts, transparency; no GPU or weights needed
```

Weight license: Qwen Research License, for research and other non-commercial use.

## Capabilities

- Images up to native 2K (default 2048²), and around 1 MP.
- Transparent backgrounds: the model's VAE decodes alpha. “Transparent
  background” enables the prompt format from the model card. PNG alpha is
  retained only if requested (or present in the edited image) and the result
  contains transparent pixels.
- Image text: lettering is rendered correctly.
- Instruction-based editing (“make the crystal green”): preserves the source
  image's shape and composition, at its aspect ratio and around 1 or 4 MP.

## Fitting in 8 GB

The 7B transformer and 8B Qwen3-VL encoder total about 31 GB in bf16.

- Transformer: GGUF Q8 (7.6 GB); encoder: bf16 without the unused output head
  (~16 GB).
- Both models stream GPU blocks from disk (`~/.cache/assets-studio/qwen_image-offload`,
  ~23 GB), as with FLUX. The VAE stays on GPU and decodes 768 px tiles with
  128 px overlap.
- The encoder runs once per request, including once for all editing variants.
- At 1K, editing caches text and source-image keys and values (~2.6 GB) across
  steps. At 2K the cache does not fit, so they are recomputed.

| | 1K | 2K |
| --- | --- | --- |
| Text-to-image, 40 steps | ~3.5 min | ~12 min |
| Editing, 40 steps | ~4 min | ~27 min |
| Peak VRAM | 5.2 GB (editing 7.5) | 5.3 GB (editing 7.5) |

Run alone: it does not fit alongside other heavy servers.

## Integration findings

- Diffusers supports the model only on main; commit `578c9b2` is pinned.
  Requires torch 2.6+.
- The checkpoint converter splits fused projections with `chunk()`. The halves
  share storage, preventing safetensors from writing offload storage; each
  receives its own copy.
- Text projection RMSNorm is stored as BF16 in GGUF. Diffusers decodes GGUF
  only inside its linear layers, so normalization saw raw bytes (8192 “values”
  for 4096 channels). These parameters are decoded at load time.
- The encoder's token embedding runs before the language model. It needs a
  separate offload group; otherwise it stays on CPU.
- VAE alpha is noisy on opaque images (a few percent of pixels at 203–254).
  Only alpha below 128 counts as transparency.
