# DeepMesh model server

The `3d-retopo` task takes a detailed mesh and produces an artist-like
low-poly triangle mesh of the same shape (GLB, geometry only).
[DeepMesh](https://github.com/zhaorw02/DeepMesh) (Tsinghua University and
ShengShu, ICCV 2025) is an autoregressive transformer: it looks at a point
cloud of the surface and writes the mesh as a sequence of tokens, triangle
after triangle. The released 0.5B model is used. The detailed mesh comes from
`image-to-3d` and `3d-refine`; UVs and colour are baked from it afterwards
(`3d-bake-colour`, `3d-bake-normals`).

```sh
just setup     # source, Python 3.11 + PyTorch 2.5.1/CUDA 12.4, two CUDA kernels
just download  # checkpoint (~3.4 GB)
just doctor
just serve     # http://127.0.0.1:9136
just test      # no GPU or weights needed
```

- There is no target triangle count: the model decides how dense the mesh
  is. Time grows with the number of triangles, a token at a time.
  `max_tokens` (default 90,000, upstream's) stops a mesh that does not end;
  such a job fails rather than return a mesh with a part missing.
- On an 8 GB RTX 3070 Laptop GPU the model writes 38 tokens a second, about
  3 tokens per triangle: a boat of 4473 triangles (13,903 tokens) takes
  6 minutes, and the token limit is reached after 40. It keeps 3.4 GiB of GPU
  memory while loaded and peaks at 5.6 GiB, whatever the length; in RAM it
  holds 2.5 GiB (7.5 GiB while loading).
- `temperature` (default 0.5, upstream's) is the randomness of each token;
  seeds differ only in it. All seeds of a job see the same 16,384 surface
  points.
- Three seeds gave three meshes of a lopsided solid, but the contract
  checker's tetrahedron is too simple: at the default temperature every seed
  writes the same 24 tokens. Contract check:
  `just check-server http://127.0.0.1:9136 --allow-seed-independent`.
- Vertices lie on a 512³ grid over the bounding box, so details smaller than
  1/512 of the longest side are lost.
- Weights are used as published: float32 with bfloat16 autocast, as
  upstream runs them.
- The sampling loop is upstream's `sample.py` for one mesh at a time: the
  9000-token window that moves every 4500 tokens, Gumbel-max sampling and the
  final clean-up (equal corners merged, repeated triangles dropped, winding
  made consistent). Upstream's script needs `torchrun` and samples four
  meshes in one batch; the server samples the seeds of a job one after
  another, so each depends on its seed alone.
- Upstream expects its input already centred and scaled to a longest side of
  1.9; the server does that and puts the result back at the input's place
  and size. Upstream's script also exchanges the first and third coordinates
  of what it decodes, which saves a mirror image of the input; the server
  does not, and its result lies on the input (checked with a lopsided solid).
- Upstream specifies Python 3.12 and builds flash-attn from source. The
  server runs on Python 3.11 (like the SDK) with torch 2.5.1+cu124, xformers
  0.0.29 and the prebuilt flash-attn 2.7.4.post1 wheel. `just setup` builds
  the two small kernels upstream also needs, `rotary_emb` and
  `dropout_layer_norm`, from the same flash-attention tag and keeps their
  wheels in `~/.cache/assets-studio/deepmesh-native`. Source is loaded from a
  pinned revision rather than installed.
- The result is not guaranteed to be closed or manifold: the output metadata
  counts `open_edges` (rims of holes) and `non_manifold_edges`.
- Weights live in `DEEPMESH_WEIGHTS` (`~/.cache/assets-studio/deepmesh`).

License: Apache-2.0 (code), MIT (weights).
