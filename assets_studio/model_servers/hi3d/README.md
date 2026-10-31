# Hi3D v1.0 model server

The `3d-refine` task takes a coarse mesh and its source image and produces a
high-resolution mesh (GLB without color).
[Hi3D v1.0](https://github.com/hi3d-official/Hi3Dv1.0) (formerly Sparc3D,
released October 23, 2026) does not generate a shape from scratch: it rebuilds
the coarse mesh surface on a sparse 512³, 1024³ or 1536³ grid, using one, two
or three passes of a 1.8B transformer, each followed by a VAE. The coarse mesh
comes from `image-to-3d` (Hunyuan3D-2.1); color is added later with `3d-paint`.

```sh
just setup     # source, Python 3.11 + PyTorch 2.5.0/CUDA 12.4 environment
just download  # pipeline artifact (~7.7 GB)
just doctor
just serve     # http://127.0.0.1:9130
just test      # no GPU or weights needed
```

- Upstream was tested only on an H20 with 96 GB. Here, pipeline components
  take turns on the GPU (image encoders → pass transformer → VAE); the rest
  wait in RAM. Weights are used as published (transformers in fp16).
  Upstream's `enable_sequential_cpu_offload` moves individual layers on every
  step; this server moves each component once per phase.
- Upstream specifies Python 3.10; the server runs on 3.11 (like the SDK),
  using the exact kernels tested with the weights: torch 2.5.0+cu124,
  flash-attn 2.7.4.post1 (prebuilt wheel), spconv-cu120 2.3.6, transformers
  4.46.3. Source is loaded from a pinned revision rather than installed.
- Hi3D normalizes its result to a unit radius; `keep_placement` (enabled by
  default) restores the coarse mesh's position and size. Hi3D itself simplifies
  the result to 1M triangles (2M at 1536³); `max_faces` lowers the target for
  that same simplification of the full surface, rather than adding a second
  pass. Before painting, set it to the painter's retained budget (Paint 2.1:
  up to 200K). The simplification (pyfqmr) leaves a few zero-area triangles;
  the server drops them (`degenerate_triangles_removed`), since TRELLIS.2
  Paint's UV unwrap would drop them and fail its triangle-count check.
- `voxel_budget_percent` selects the initial occupied-voxel budget: 100
  (default), 75 or 55. If a shape is already known to need 55, start there to
  skip two expensive OOM attempts. Retries use only lower remaining levels;
  OOM at 55 fails the job. This does not change `resolution`: a 1024 job
  still runs two passes, and each retry starts again from the coarse mesh.
  Adaptive voxelization may coarsen its internal grid to fit the budget.
  Output metadata records both the initial percentage and successful share.
- Weights and DINOv2/CLIP encoders live in `HI3D_WEIGHTS`
  (`~/.cache/assets-studio/hi3d`); encoders download on first load.

License: Hi3D Open License Agreement 1.0. Commercial use is permitted; above
200,000 monthly users or $1M monthly revenue a separate license is required.
The integration displays “Powered by Hi3D 1.0”.
