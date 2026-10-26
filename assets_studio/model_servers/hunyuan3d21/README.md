# Hunyuan3D-2.1 Shape model server

The `image-to-3d` task takes an image and produces a shape (GLB without color).
[Hunyuan3D-2.1](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1) Shape is the
largest open Hunyuan3D shape model (June 2025): a 3.3B DiT versus 2mini's 0.6B,
using the same recipe. Color comes from `3d-paint` (its own 2.1 PBR painter
requires about 21 GB of VRAM).

```sh
just setup     # source, Python 3.11 + PyTorch 2.5.1/CUDA 12.4 environment
just download  # DiT checkpoint, VAE and image encoder (~7.4 GB)
just doctor
just serve     # http://127.0.0.1:9119
just test      # no GPU or weights needed
```

- Official requirement: about 10 GB VRAM. Here, pipeline components take turns
  on the GPU (image encoder → DiT → VAE); the rest wait in RAM. Weights are
  fp16 without quantization. Peak: about 7.1 GB; load time: 34 seconds.
- Upstream's `enable_model_cpu_offload` is copied from diffusers and does not
  work here (the pipeline has no `components`). Accelerate hooks would skip
  the VAE (called through `latents2mesh`, not `forward`), so the server wraps
  `encode_cond`, `prepare_latents` and `_export` itself.
- The same seed produces the same shape (vertex and triangle counts and
  bounds), but not a byte-identical GLB, due to nondeterministic GPU kernels.
  Run the contract check with `--allow-nondeterministic`.
- Weights live in `HY3DGEN_MODELS` (`~/.cache/assets-studio/hy3dgen`), alongside
  the 2.1 VAE used by `mia`.

License: Tencent Hunyuan 3D 2.1 Community License (excludes the EU, UK and
South Korea), as with 2.0.
