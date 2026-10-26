# GVHMR: neutral body shape and rig rotations

## Bug

Mixamo names in GVHMR BVH denote SMPL-X joints. Their neutral offsets describe
body shape; directions between them are not commands to bend target bones.

The previous `rotations` profile aligned these directions for every bone.
Pelvis orientation also came from a pelvis/hips/spine triangle rather than
original pelvis rotation; terminal heads received neck-direction correction.
Thus even zero-rotation BVH bent MIA's neutral spine, with 33–37° local changes.

Prior elbow/knee checks did not exclude this bug. Valid weights/hierarchy also
do not prove correct axial-chain transfer.

## Fix

GVHMR uses `asset_processing`'s internal `smpl` profile:

- Retains target neutral pelvis, spine, neck and head shape.
- Transfers full source rotations relative to it, including nods and turns.
- Uses source pelvis rotation, not neutral-joint-derived tilt.
- Retains limb rest alignment, wrist twist, translation scaling and `world`,
  `stationary`, `in-place` modes.

Kimodo/MoCapAnything do not switch to `smpl`. HTTP contract, GVHMR parameter
schema and SDK remain unchanged.

## sword_combo control

Both variants reuse one saved GVHMR prediction from BiRefNet-cleaned video and
the same MIA rig, avoiding neural rerun differences: `stationary`, 114 frames,
30 fps.

| Check | Before | After |
| --- | ---: | ---: |
| Maximum pelvis rotation error | 17.09° | <0.04° |
| Spine | 6.37° | <0.04° |
| Spine1 | 30.00° | <0.04° |
| Spine2 | 15.43° | <0.04° |
| Neck/head | 14.81° | <0.04° |
| Horizontal root range | <1e-6 m | <1e-6 m |
| Root heading range | 433.15° | 433.54° |
| Vertical root range | 0.3683 m | 0.3683 m |

Rotation errors compare original BVH rotations in aligned world axes relative
to each skeleton's own neutral pose, not actor ground truth. Different bodies'
neutral joint positions intentionally need not match.

For visual review, original `prediction.pt` was also rendered as an actual
SMPL-X mesh, without MIA or retargeting. Lowered head and some torso lean are
already present there. Transfer fixes should not arbitrarily straighten source
motion. GVHMR pose quality is not fully solved.

Artifacts: `workspace/gvhmr_posture/`, including
`sword_combo_gvhmr_diagnostic.mp4` (SMPL-X / before / after), `_slow`, two-column
comparison, fixed FBX and `sword_transfer_validation.json`.

The Blender regression uses different neutral spine/head offsets and checks
neutral pose, later rotations of all six axial bones, head/wrist movement and
all three root modes.

## Differences in the older good Human example

Checked actual FBX files from `workspace/human`, not previews alone:

| File | Head's own rotation change relative to first frame |
| --- | ---: |
| `guard_gvhmr_axe_comparison.fbx` | 0° |
| `guard_gvhmr_world.fbx`, `guard_gvhmr_in_place.fbx` | 0° |
| `guard_flow_gvhmr.fbx` | 54.98° |
| New GVHMR, same video/prepared rig | 54.98° |
| `guard_mocap_axe.fbx` | 28.03° |

Early GVHMR exports also lack independent wrist motion. This matches old
position-based transfer: terminal bones follow parents but lose predicted
rotations. Later full transfer preserves them. The old good preview therefore
does not validate GVHMR's predicted head orientation; visible head motion with
the neck could still occur. This does not apply to old MoCapAnything, whose
head is animated.

The old Human rig and prepared Sieg rig share 22 bones, hierarchy and 24,894
vertices. Maximum neutral-coordinate difference <6e-7 m; normalized-weight
difference <2.4e-8. Character replacement or another skeleton does not explain
the examples' differences.

Human input shows a large real person and nearly static camera. Sieg shows a
small game character, cape, hair, weapon, effects and camera movement. These
are factors to test, not proof of each pose error's cause.

Detailed comparisons/audit: `workspace/sieg/posture_batch/`, especially
`comparisons/human_old_vs_current.mp4`, `human_head_animation_audit.json`,
`human_vs_sieg_rig.json`.

Next experiment: [four Sieg motions and Human control](sieg-posture-comparison.md).
