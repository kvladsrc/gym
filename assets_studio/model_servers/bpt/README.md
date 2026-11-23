# BPT model server

The `3d-retopo` task takes a detailed mesh and produces an artist-like
low-poly triangle mesh of the same shape (GLB, geometry only).
[BPT](https://github.com/Tencent-Hunyuan/bpt) (Tencent Hunyuan, CVPR 2025) is
an autoregressive transformer: it looks at a point cloud of the surface and
writes the mesh as a sequence of tokens. Its Blocked and Patchified
Tokenization names a vertex by its block of the grid and its place in the
block, and writes a fan of triangles around a vertex as one patch. The
released "lite" 0.5B model is used, the only one published; it reads a point
cloud and nothing else (inference from an image is on upstream's plan, not
released). The detailed mesh comes from `image-to-3d` and `3d-refine`; UVs
and colour are baked from it afterwards (`3d-bake-colour`,
`3d-bake-normals`).

```sh
just setup     # source, Python 3.11 + PyTorch 2.5.1/CUDA 12.4 environment
just download  # checkpoint (~1.6 GB)
just doctor
just serve     # http://127.0.0.1:9137
just test      # no GPU or weights needed
```

- There is no target triangle count: the model decides how dense the mesh
  is.
- A mesh is at most 10,000 tokens long, the positions the model has. A
  triangle takes 2.0–2.3 tokens (counted with upstream's tokeniser on meshes
  of 2000–7000 triangles), so the limit is about 4500–4900 triangles. A mesh
  that does not end by then fails rather than come back with a part missing.
  Of five detailed meshes tried, a boat, a boar and a castle on a rock ended
  (1323–2802 triangles); a guard with a spear and a shield and a bell tower
  did not.
- On an 8 GB RTX 3070 Laptop GPU the model writes 23 tokens a second, one
  CPU core being the limit: a boat of 1473 triangles (3464 tokens) takes
  2.5 minutes, a boar of 2802 (6076 tokens) 4.5, and a mesh that reaches
  the limit about 7 (9.5 in a run on a busy machine). It keeps 1.3 GiB of
  GPU memory while loaded and peaked at 2.0 and 2.5 GiB on those two; in
  RAM it holds 2.0 GiB (4.5 GiB while loading). Upstream states about 12 GB
  of GPU memory.
- `temperature` (default 0.5, upstream's) is the randomness of each token
  among the 50 likeliest ones whose probabilities add up to 0.95, as
  upstream's script samples; seeds differ only in it. All seeds of a job see
  the same 4096 surface points.
- Three seeds gave three meshes of a lopsided solid, but the contract
  checker's tetrahedron is too simple: every seed writes the same 16 tokens,
  at temperature 1.5 too. Contract check:
  `just check-server http://127.0.0.1:9137 --allow-seed-independent`.
- Vertices lie on a 128³ grid over the bounding box, so details smaller than
  1/128 of the longest side are lost.
- The weights are used as published: bfloat16, run in float16 as upstream
  runs them.
- The checkpoint is a whole training state. Upstream unpickles all of it,
  which needs DeepSpeed installed for the classes of the training run it
  names. The server reads the model's tensors with PyTorch's weights-only
  loader and puts empty stand-ins in the place of those three classes; all
  802 tensors load into upstream's model with no key missing or left over.
- Sampling is upstream's `MeshTransformer.generate` with the settings of its
  `main.py`, and so is the clean-up of the result (equal corners merged,
  repeated triangles dropped, winding made consistent). Upstream's script
  takes its surface points at random; the server takes the same ones every
  time, so a seed reproduces its mesh.
- Upstream scales its input to a longest side of 1.9 around the centre of
  the bounding box; the server does the same and puts the result back at the
  input's place and size. The result lies on the input, not mirrored
  (checked with a lopsided solid).
- Upstream tests on Python 3.9 with torch 2.1.2 (CUDA 12.1). The server runs
  on Python 3.11 (like the SDK) with torch 2.5.1 (CUDA 12.4), the stack of
  the `deepmesh` server, with upstream's versions of x-transformers (1.26.6)
  and pytorch-custom-utils (0.0.21). Of upstream's requirements only what
  inference imports is installed. Source is loaded from a pinned revision
  rather than installed.
- The result is not guaranteed to be closed or manifold: the output metadata
  counts `open_edges` (rims of holes) and `non_manifold_edges`.
- Weights live in `BPT_WEIGHTS` (`~/.cache/assets-studio/bpt`).

License: the code's repository carries the Tencent Hunyuan 3D 2.0 Community
License since July 2025, which is not granted in the European Union, the
United Kingdom and South Korea and forbids using the results there too; the
weights' page on Hugging Face says MIT. The point-cloud encoder in the
source (Michelangelo) carries GPL-3.0. Check which terms apply to you
before using the results.
