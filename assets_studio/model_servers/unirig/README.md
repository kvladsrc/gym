# UniRig model server

The `3d-to-rig` task takes a model (GLB) and returns the same model with a
skeleton and skin weights (FBX).
[UniRig](https://github.com/VAST-AI-Research/UniRig) (VAST and Tsinghua,
SIGGRAPH 2025) predicts the skeleton with an autoregressive transformer
(OPT-350m, 15 beams) and weights with a second model. Skeletons can have any
shape and are not Mixamo; its successor is the `skintokens` server.

```sh
just setup     # source, Python 3.11 + PyTorch 2.8/CUDA 12.9 environment;
               # prebuilt flash-attn, spconv, torch-scatter/cluster, bpy 4.2 wheels
just download  # checkpoints (~5.8 GB) and OPT-350m config (network only)
just doctor
just serve     # http://127.0.0.1:9117
just test      # no GPU or weights needed
```

- A job runs a chain of upstream scripts, each in a separate process in the
  source directory: extract mesh → skeleton → extract again → skin → transfer
  to the original model (texture retained) → FBX (`asset_processing.blender`).
  The `launch/` scripts are not called because they run `pip install`.
- Upstream queries the Hub for unpinned checkpoints on every run. Steps use
  `HF_HUB_OFFLINE=1`, so it uses pinned copies from `experiments/`.
- Upstream's merge writes FBX without textures. Instead, merging produces GLB
  and `asset_processing.blender` builds the FBX.

License: MIT (trained on Articulation-XL 2.0).
