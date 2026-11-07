# Rigging and motion: working Studio pipelines

## What each model does

| Server | Input → output | Compatibility and preparation |
| --- | --- | --- |
| `mia` | Character GLB → FBX with Mixamo bones and weights | Humanoids; rest pose, fingers, texture export, welding and smooth normals configured/handled inside server |
| `skintokens` | GLB → FBX with shape-specific skeleton | Arbitrary shapes; `bone_N`, not fixed Mixamo skeleton |
| `unirig` | GLB → FBX with shape-specific skeleton | Same purpose, different skeleton/weight predictor |
| `kimodo` | Text → skeletal FBX animation | SOMA, 77 joints; compatible joints renamed to Mixamo, extra joints `soma_*`; no character mesh |
| `mocapanything` | Video + rig → animated character FBX | Uses input skeleton; automatic quadruped anatomy for `bone_N`, retains named bones |
| `gvhmr` | Human video + Mixamo rig → animated character FBX | Predicts SMPL-X, transfers 22 body bones; no fingers or weapon-contact prediction |
| `animation_tools` | Rig, or rig + motion → FBX | Blender without neural model: rig checking/preparation and humanoid transfer accounting for rest poses/rotations |

Bone names are only part of compatibility. Joint positions, bone directions,
scales, rest poses and skin bindings also matter. A shared `mixamorig:` prefix
does not make Kimodo's skeleton ready-made animation for a particular MIA mesh.

## What the jobs handle

- Rigging servers export textures and weld the mesh.
- MoCapAnything handles custom-rig conventions, reference rest animation,
  quadruped naming and rotation-lock removal. No manual preparation scripts needed.
- `retarget-motion` transfers Kimodo or Mixamo motion to a rig. It resets old
  target animation, checks bones/skin, normalizes weights, preserves textures
  and exports animated FBX.
- Shared transfer, including GVHMR/MoCapAnything, checks/normalizes weights.
  Unweighted vertices are not guessed; such input is rejected.
- Full rotation transfer preserves twist and end-joint motion lost by
  direction-only transfer.
- In older Kimodo clips, transfer prefers the real Mixamo bones over extra SOMA
  joints of the same name; Kimodo now exports its extra joints as `soma_*`.
- Shared transfer handles imported FBX frame-rate changes and range rounding.
  Export does not simplify keys.

## Building pipelines

1. **Human motion from text:** mesh → MIA; text → Kimodo; both →
   `animation_tools / retarget-motion`. Import the character/clip as Generic,
   without additional Humanoid retargeting.
2. **Human motion from video:** mesh → MIA → GVHMR `rig`; video → `video`.
   A separate preparation server is optional. `birefnet / video-to-video` can
   precede either mocap server; enable MoCapAnything `background_removed` to
   avoid repeated removal. For GVHMR `camera=moving`, pass the original
   background clip into `camera_video`, retaining scene landmarks for camera
   motion while cleaned `video` drives pose. For turning attacks use
   `root_motion=stationary`; `in-place` removes turns for game-controlled heading.
3. **Quadruped motion from video:** mesh → SkinTokens or UniRig → MoCapAnything
   `rig`; video → `video`. This does not promise automatic anatomy recognition
   for arbitrary birds, insects or objects.
4. **Check an old rig separately:** `animation_tools / prepare-rig`. The result
   is ready for later animation; preparation removes animation and restores
   the rig's own rest pose.

Save chains in [Studio Flow](../../flow/README.md). Blender regression tests
cover A/T-poses, wrist rotation, heading, in-place movement, weights and
frame-count preservation.

Weapon contact, neural joint accuracy and foot sliding are separate output
properties. Correct rigs/transfer do not guarantee contact: current inputs
lack weapon geometry/grip points. New chains need no external compatibility
fixes; artistic review and game integration remain separate stages.

GVHMR uses a separate `smpl` profile: neutral pelvis, spine, neck and head retain
MIA's bind pose while full movements/rotations transfer. SMPL joint positions
are not instructions to bend the target torso.
