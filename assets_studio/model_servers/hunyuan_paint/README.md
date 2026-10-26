# Hunyuan3D-Paint model server

> Retired from the active Studio catalog. Use the 2.1 server. Local 2.0 weights
and environments were removed; this source remains for historical
reproducibility.

The `3d-paint` task takes a model (GLB) and its source image and returns the
same model with a UV texture. Based on
[Hunyuan3D-Paint v2.0 turbo](https://github.com/Tencent-Hunyuan/Hunyuan3D-2):
it removes lighting from the image, paints six views using mesh normals and
positions, and bakes them into a texture, covering unseen sides as well.
Pairs with Hunyuan3D (the same coordinate frame as its shapes).

```sh
just setup     # source, Python 3.11 + PyTorch 2.8/CUDA 12.9 environment;
               # build the CUDA rasterizer in nix develop .#assets-studio-cuda
just download  # paint and delight weights (~14 GB, network only)
just doctor
just serve     # http://127.0.0.1:9114
just test      # no GPU or weights needed
```

- PyTorch targets CUDA 12.9 rather than 12.4 as in the other servers: CUDA 12.4
  was removed from nixpkgs, and the rasterizer uses nixpkgs' nvcc (12.9),
  so the versions must match.
- Both diffusion models (~8 GB of fp16 weights) are offloaded to RAM by module
  (`enable_model_cpu_offload`); otherwise they do not fit in 8 GB.
- Images without transparency go through rembg; transparent images are used
  as supplied.

License: Tencent Hunyuan Community License (excludes the EU, UK and South Korea).
