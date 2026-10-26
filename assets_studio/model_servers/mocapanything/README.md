# MoCapAnything model server

The `video-to-motion` task takes a human or animal video and its rig (FBX or
skeletal GLB, for example from `skintokens`), returning the same rig moving as
in the video (FBX). [MoCapAnything V2](https://animotionlab.github.io/MoCapAnythingV2/)
(Gong et al., 2026) predicts arbitrary-skeleton joints end to end from monocular
video: video → positions → rotations, using a rig reference frame (DINOv2
features, joint names through T5). Source:
[phongdaot/MocapAnything](https://github.com/phongdaot/MocapAnything), the repository
linked by the official V2 project, with the authors' weights.

```sh
just setup     # source, Python 3.11 + PyTorch 2.8/CUDA 12.9 environment
just download  # weights (464 MB), RMBG-1.4, DINOv2-large, T5-base (network only)
just doctor    # also checks Blender, required in PATH (or BLENDER)
just serve     # http://127.0.0.1:9122
just test      # no GPU, weights or Blender needed
```

Each job runs the authors' custom-rig path (`examples/custom_rig`) in child
processes, then transfers the predicted BVH onto the input rig. Additions needed
for SkinTokens rigs (found on a horse by comparison with the demo's Truebones
horse, where the same steps work):

- **Units and axes** (`prepare_rig.py`): the pipeline targets Truebones Zoo
  from 3ds Max, with bones in centimeters and Y up. A rig in meters shrank
  100-fold, putting the reference-render camera inside the mesh.
- **Rig motion**: the reference frame comes from rig animation. A fresh rig
  has none, so an animation is created from its rest pose.
- **Static joints**: the pipeline marks joints that do not rotate in rig
  animations. In a rest pose that means all joints, freezing the mesh. The
  rotation list is cleared.
- **Joint names** (`naming.py`): stripping digits from `bone_0…` would give T5
  the same word, “bone”. Names resembling training data (“quadruped leg left
  01”) are assigned from skeleton shape; quadrupeds only for now.
- **Facing direction**: the pipeline's guess turned the horse almost backward.
  Direction is derived from pelvis to head instead.
- **`fix_fbx` is skipped**: it removes unweighted 3ds Max Biped end bones and
  requires animation on the base FBX.
- **Per-job shadow source tree**: two stages read `zoo/` from the source root
  regardless of `ZOO_ROOT`.
- **PYTHONPATH**: Nix Blender exports Python 3.13 packages, and upstream scripts
  pass this environment to their own Python.

Transfer (`model_server_sdk/retarget.py`, shared with `gvhmr`) checks itself:
it prints the maximum difference between output joints and BVH (2 µm on the horse).

`root_motion`: `in-place` keeps locomotion in place, leaving heading and travel
to the game; `stationary` stays in place but retains body turns (for attacks
and turns); `world` preserves model-predicted turns and travel (a horse running
an arc turns in a panning video).

For video from `birefnet / video-to-video`, enable `background_removed`:
white-background frames are cropped with padding without repeating RMBG.
This preserves prepared pixels, including light details inside the silhouette.
The default is off, with RMBG removing the original background. Rig reference
renders are processed separately; their preparation is unchanged.

`ASSETS_STUDIO_DEBUG_DIR=/path` retains a separate workspace per request:
inputs, reference render, predicted positions/rotations, BVH, `pipeline.log`
and final FBX. Without it, temporary files are deleted. This is a server
diagnostic mode, not mandatory asset modification.

Measurement (RTX 3070 Laptop, 31-bone SkinTokens rig): 6-second clip (180 frames),
214 seconds per job, peak VRAM 7.6 GB out of 8; hence the 10-second limit.

The authors' RMBG-1.4 removes frame backgrounds. PNG frames with alpha are used
as supplied; BiRefNet or SAM 2 can be connected if RMBG includes unwanted
subjects (another horse or people).

The sample contract check does not pass generation: the sample mesh is a
tetrahedron without a skeleton, so the server correctly returns `invalid_request`.

Licenses: MIT (V2 code and weights); RMBG-1.4 — BRIA non-commercial license;
DINOv2 and T5 — Apache-2.0.
