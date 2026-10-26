# FLUX model server

> Retired from the active Studio catalog at the user’s request; weights,
> offload cache and local environment removed. Use Qwen-Image instead.
> Source is retained for historical jobs; it is not an active launcher recipe.

Contract v1 text-to-image and image-to-image based on
[FLUX.1-schnell](https://huggingface.co/black-forest-labs/FLUX.1-schnell)
(Apache-2.0), with a
[GGUF Q8 transformer](https://huggingface.co/city96/FLUX.1-schnell-gguf).

```sh
just setup    # Python 3.11 + PyTorch 2.5.1/CUDA 12.4 + diffusers + gguf environment
just doctor   # PyTorch, GPU, token configuration and downloaded weights
just serve    # http://127.0.0.1:9104; first start ~2 min, ~43 GB on disk
just test     # declarations; no GPU or weights needed
```

FLUX weights are gated on Hugging Face: accept the model terms with your
account and set its token in `HF_TOKEN` (or `HUGGINGFACE_TOKEN`, which recipes
pass as `HF_TOKEN`).

## Fitting in 8 GB

The 12B transformer and 4.7B T5 text encoder total about 34 GB in bf16.

- The transformer is quantized to GGUF Q8 with practically no quality loss
  (Q3 loses noticeable detail; Q4 does not fit entirely on GPU).
- Transformer and T5 blocks are moved to the GPU using group offload. CLIP
  and VAE stay on GPU. Peak process VRAM is about 5.3 GB (VAE decoding at 1024²).
- Offloaded blocks live **on disk**, not in RAM. Keeping both models in RAM
  (~22 GB) alongside the desktop exhausted memory and the kernel killed the
  server. Generation now leaves about 18 GB RAM free (at least ~10 GB while loading).
- Ordinary CPU offload (`enable_model_cpu_offload` /
  `enable_sequential_cpu_offload`) does not work with GGUF: accelerate cannot
  move GGUF parameters.

The tradeoff is speed: 55–70 seconds per 1024² image including T5 (the prompt
is encoded once per request for all variants; faster with blocks in the OS
page cache). Loading takes about 35 seconds, or ~2 minutes initially. Quality
was deliberately prioritized over speed.

## Block storage

`~/.cache/assets-studio/flux-offload/<fingerprint>` (~21 GB). Diffusers reuses
any existing block file without checking it, so the server:

- Names the directory using weight revisions and diffusers, transformers and
  torch versions; changes rebuild storage and remove the old copy.
- Treats storage as ready only with a `complete` marker; interrupted initial
  writes are removed and retried.
- Checks for ≥23 GB free before writing.
- Holds a lock: a second FLUX server cannot start while the first is running.

You can remove it manually while the server is stopped (`rm -rf
~/.cache/assets-studio/flux-offload`). `just doctor` reports its state and
free disk space.

## Limitations

- schnell was trained for 4 steps; guidance and negative prompts are not
  supported (the distilled model does not use them).
- Prompts have a 256-token T5 limit; English works best.
- Image-to-image with strength s runs `steps − ⌊steps × (1 − s)⌋` steps,
  always at least one (4 steps at 0.6 runs three). The prompt is optional:
  without one, it produces variations of the input image.
- After an OOM during generation, the block on GPU (a few hundred MB) stays
  there until the next FLUX request.
- A CUDA GPU is required; the server does not run on CPU.
