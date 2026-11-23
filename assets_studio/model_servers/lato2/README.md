# LATO.2 model server

The `3d-retopo` task takes a detailed mesh and produces a low-poly triangle
mesh of the same shape with about as many vertices as asked (GLB, geometry
only). [LATO.2](https://github.com/LoHhhha/LATO.2) (Meshy AI, July 2026) is
not autoregressive: one flow model places the vertices on the 64³ voxels the
surface occupies, guided by a rendered view of it, and a second flow model
connects them. A mesh takes a fixed number of steps, however many triangles it
has. The detailed mesh comes from `image-to-3d` and `3d-refine`; UVs and
colour are baked from it afterwards (`3d-bake-colour`, `3d-bake-normals`).

```sh
just setup     # sources, Python 3.11 + PyTorch 2.8.0/CUDA 12.9, native wheels
just download  # six checkpoints (~3.3 GB)
just doctor
just serve     # http://127.0.0.1:9135
just test      # no GPU or weights needed
```

- `vertices` (200–5000, default 2000) is the target the vertex model is
  conditioned on; the result has about that many (1194, 2268 and 4913 for
  1000, 2000 and 5000 on one character) and 2.4–2.7 triangles per vertex. The
  model was trained for this range.
- On an 8 GB RTX 3070 Laptop GPU a 617,000-triangle character takes 22 s at
  1000 vertices, 30 s at 2000 and 58 s at 5000; 7–9 s of that is preparing
  the input and moving the models. GPU memory peaks at 2.6, 2.8 and 3.2 GiB;
  between jobs the server keeps 0.25 GiB of it. In RAM it holds 6.2 GiB once
  loaded and up to 8.6 GiB after jobs.
- Upstream reports about 8 GB with every model on the GPU. Here the three
  groups take turns on it (the DINOv2 image encoder → the vertex flow, its
  VAE and the offset head → the voxel encoder, the topology flow and its
  VAE); the rest wait in RAM. Weights are used as published.
- Upstream specifies Python 3.10 and torch 2.6.0+cu124. The server runs on
  Python 3.11 (like the SDK) and torch 2.8.0+cu129, the stack of
  `trellis2_paint`: LATO.2 voxelises its input with TRELLIS.2's o-voxel, so
  both servers share the same four native wheels (o-voxel, CuMesh, FlexGEMM,
  nvdiffrast), built once. flash-attn 2.8.3 comes as a prebuilt wheel; spconv
  and torch-scatter as their published wheels. Source is loaded from a pinned
  revision rather than installed.
- The model outputs vertices and edges. As upstream, a triangle is made of
  every three vertices connected to each other, and `fill_quad_rings`
  (upstream's default) adds two more in every ring of four edges that has no
  diagonal. For one seed the server returns the vertices and triangles of
  upstream's `scripts/e2e_inference.py`.
- Expect a faulty mesh. Upstream warns that results "may still contain holes
  and incorrect connectivity", and neither rule above knows which rings are
  faces of the surface: the ring around a vertex with four neighbours, or
  around a thin part, is filled too, so triangles overlap and cross the
  inside. The character above at 2000 vertices has 269 open and 2630
  non-manifold edges out of 7489; without `fill_quad_rings`, 674 and 1215.
  The output metadata counts `open_edges` (rims of holes) and
  `non_manifold_edges`.
- The triangles face no particular way, so the server winds each like the
  nearest point of the input surface. Thin two-sided parts can get a
  triangle of the wrong side.
- LATO.2 works in a unit box. The server puts the result back at the input's
  place and size, and adds the half voxel that upstream's export leaves out.
- The conditioning view is rendered with Open3D offscreen (EGL); the server
  sets `EGL_PLATFORM=surfaceless` as upstream does. As in upstream's script,
  each input is voxelised and rendered in a process started for it: Open3D's
  renderer adds a different noise to every frame it draws, and only the
  first frame of a new renderer is the same for the same mesh, which a seed
  needs to reproduce its result.
- Weights live in `LATO2_WEIGHTS` (`~/.cache/assets-studio/lato2`); DINOv2's
  source is pinned there by `just setup`, its weights download on first load.

License: MIT (code and weights).
