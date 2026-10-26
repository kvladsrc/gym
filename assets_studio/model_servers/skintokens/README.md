# SkinTokens model server

The `3d-to-rig` task takes a model (GLB) and returns the same model with a
skeleton and skin weights (FBX).
[SkinTokens](https://github.com/VAST-AI-Research/SkinTokens) (VAST and Tsinghua,
2026; successor to UniRig) generates skeleton and weights in one token
sequence (Qwen3 0.6B transformer). Skeletons follow the shape: quadrupeds,
birds, trees, chests. These are not Mixamo skeletons; use `mia` for humanoids
with Mixamo animations.

```sh
just setup     # source, Python 3.11 + PyTorch 2.8/CUDA 12.9 environment;
               # prebuilt flash-attn and bpy 4.3 wheels
just download  # checkpoints (~1.6 GB) and Qwen3 config (network only)
just doctor
just serve     # http://127.0.0.1:9116
just test      # no GPU or weights needed
```

- Each job runs a child process (`rig.py`) in the source directory: bpy must
  run on the main thread, and upstream paths are relative. The model loads
  for each job.
- The upstream demo accesses bpy through a helper server that listens on all
  interfaces and unpickles request data (network code execution). It is not used.
- The skeleton is predicted from the mesh and transferred onto the original
  model (retaining its texture), followed by GLB → FBX
  (`asset_processing.blender`: named textures for Unity, welded vertices,
  smooth shading).
- The upstream README requests ≥14 GB VRAM (beam width 10); `num_beams` can
  be reduced.

License: MIT.
