# Kimodo model server

The `text-to-motion` task takes an English motion description and produces a
humanoid animation (FBX: T-pose skeleton and motion at 30 fps).
[Kimodo](https://github.com/nv-tlabs/kimodo) (NVIDIA, 2026) is a motion diffusion
model trained on about 700 hours of motion capture. Period-separated sentences
produce sequential movements with transitions.

```sh
just setup     # source, Python 3.11 + PyTorch 2.8/CUDA 12.9 environment;
               # build Kimodo with MotionCorrection (C++) in nix develop .#assets-studio-cuda
just download  # Kimodo (~1.1 GB), LLM2Vec adapters, Llama-3-8B (~16 GB);
               # requires HF_TOKEN
just doctor
just serve     # http://127.0.0.1:9118
just test      # no GPU or weights needed
```

- Kimodo uses SOMA (77 joints); bones are renamed to Mixamo names
  (`mixamorig:Hips`…, `to_fbx.py`). Unity builds a Humanoid avatar automatically,
  and the clip plays on any humanoid, including `mia` rigs.
- The text encoder is LLM2Vec on Llama-3-8B-Instruct, on CPU (~16 GB RAM,
  one pass per prompt). Only Kimodo is on GPU (<3 GB). Llama 3 is gated:
  Meta approves Hugging Face access.
- MotionCorrection (prevents foot sliding) builds with `-include stdlib.h`:
  its `Scalar.h` calls `abs()` without the header.

Licenses: Kimodo — NVIDIA Open Model License (commercial use permitted;
outputs do not belong to NVIDIA); LLM2Vec adapters — MIT; Llama 3 — Meta Llama 3
Community License.

For a finished character FBX, pass Kimodo's output and the MIA rig to
[animation_tools](../animation_tools/README.md), task `retarget-motion`. It
accounts for rest poses and proportions, transfers rotations and returns the
character with a clip. Save the chain in [Studio Flow](../../flow/README.md).
No external Blender script invocation is needed.

## Sparse pose / hand / foot constraints

`text-to-motion` optionally accepts a `constraints` text/plain JSON input using
Kimodo's official list format (`root2d`, `fullbody`, hand/foot, `end-effector`).
With constraints, use a single motion description. Frame indices are 0-based
inside the generated clip; values are finite SOMA 30/77-joint axis-angle poses
and root positions in Y-up metres, not raw Mixamo/MIA bone Euler rotations.
The input is validated before inference; model revision includes `.constraints.1`.
Metadata records constraint groups, frame indices and a canonical JSON hash.
Existing text-only generation is unchanged; no SDK modifications.

Keyframe constraints require a correct target-to-SOMA pose conversion. Correct
reference stances first, then supply selected good phases as intermediate keys.
Kimodo has no independent sword object constraint. Target rig contact, wrist
rotation and weapon orientation need separate validation after retargeting.
