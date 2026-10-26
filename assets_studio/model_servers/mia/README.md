# Make-It-Animatable model server

The `3d-to-rig` task (ADR-006), based on
[Make-It-Animatable v2](https://github.com/jasongzy/Make-It-Animatable)
(CVPR 2025), turns a character GLB into an FBX with a Mixamo skeleton and skin
weights. Mixamo animations work on it in Unity (Humanoid) without uploading
the model to Mixamo. Humanoids only.

```sh
just setup     # v2 source (with Hunyuan3D-2.1) and environment:
               # Python 3.11 + PyTorch 2.8/CUDA 12.9
just download  # weights, skeleton template, VAE (network only)
just doctor    # PyTorch, GPU, bpy and downloaded files
just serve     # http://127.0.0.1:9113
just test      # no GPU or weights needed
```

Downloads (outside Dropbox):

- Source at revision `bbd8b15` and `jasongzy/Make-It-Animatable` weights
  (`output/best/v2`): `~/.cache/assets-studio/mia-source`.
- Skeleton template `bones.fbx` from `jasongzy/Mixamo`: **gated**. Accept its
  Hugging Face terms using the account whose token is in `HF_TOKEN`.
- Hunyuan3D-2.1 VAE (the model's backbone): `~/.cache/assets-studio/hy3dgen`.

Parameters: `rest_pose` (`t-pose`: T-pose output for Mixamo; `input`: the source
model's pose), `input_pose` (`auto`, `t-pose`, `a-pose`: known input pose,
retained by the bones), and `fingers` (finger bones). The model is deterministic:
`max_count = 1`.

Steps match the upstream demo (`app_v2.py`). FBX export uses its
`app_blender.py` in a separate process: bpy data must be modified on the main
thread (v1 used the same approach).

Licenses: Make-It-Animatable — Apache-2.0; Hunyuan3D-2.1 VAE — Tencent Hunyuan
Community License (excludes the EU, UK and South Korea); skeleton template — Mixamo.
