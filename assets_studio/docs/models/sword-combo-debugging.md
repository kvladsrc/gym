# Sword combo: capture and transfer control

Check on 2026-10-18 in `workspace/sieg`: segment 32.4–36.2 s, 114 frames at
30 fps, existing MIA guard with 22 Mixamo bones.

## Two fixes

`in-place` removes horizontal travel **and body heading**. Expected for
game-controlled locomotion, but it removed turning from the combo attack.
Added `stationary`: remove travel, retain heading, pitch, roll and vertical
movement. On the control segment, pelvis heading range rose from <0.1° to
about 433° for both models; horizontal root translation remained <1e-6 m.

MoCapAnything re-segmented RGB after BiRefNet. `background_removed=true` retains
prepared pixels on white, changing only padded cropping. Rig-reference render
segmentation is unchanged. Graph:

```text
video → BiRefNet / video-to-video → MoCapAnything / video-to-motion
                                     background_removed=true
                                     root_motion=stationary
                                 → GVHMR / video-to-motion
                                     camera=static
                                     root_motion=stationary
ready MIA rig ────────────────────→ rig input of both tasks
```

## Separating prediction from transfer errors

For diagnostic runs, `ASSETS_STUDIO_DEBUG_DIR` retains one workspace per
request. By default files are temporary/deleted. GVHMR also saves original
axis-angle, joints and prediction; MoCapAnything saves positions, rotations,
BVH and stage logs.

Checked bones over mesh, side-specific weight distributions, sample poses
and every motion frame. No left/right swaps found. Weights are normalized,
which does not prove perfect soft-tissue or armor deformation.

Original SMPL forward kinematics and imported GVHMR BVH agree within 2.8 µm
maximum joint error. BVH/final-FBX elbow/knee angles differ by <0.02° in both
models. This omitted spine, pelvis and head: elbows/knees alone cannot validate
all torso transfer. Later neutral-SMPL checks found artificial bends introduced
by transfer; see [GVHMR neutral-pose fix](gvhmr-rest-pose.md). Background removal
alone did not fix that. Hands/foot contacts need further evaluation; models
received no weapon geometry or grip points.

## Results and reproduction

`workspace/sieg/debug/index.html` contains video, FBX, bone projections and
saved-graph links. Additional files:

- `sword_combo_fixed.mp4`, `_slow.mp4`: prepared input and stationary mode.
- `sword_combo_before_after.mp4`: original and both changes.
- `sword_combo_background_only.mp4`: removal effect only, same in-place mode
  in both rows.
- `root_motion_validation.json`, `*_transfer.json`, `gvhmr_bvh_roundtrip.json`:
  numerical transfer checks, not capture-accuracy metrics against source video.
- Run snapshots, parameters, jobs and provenance in JSON and Studio/Flow.

Studio's full `just check` passed. Blender regressions cover different rest
poses, terminal wrists, old target animation, weight normalization and all
three root modes. MoCapAnything's general skeleton separately checks heading
and vertical-motion preservation in delta transfer.
