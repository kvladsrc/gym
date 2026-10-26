# GVHMR: four Sieg motions and Human control

Tested stance, walk, run and jump from the same source video. Shared MIA rig,
`smpl` transfer, `stationary`, seed 20261018. Each GVHMR segment ran with its
original background and after BiRefNet. MoCapAnything received the same cleaned
video with `background_removed=true`. `workspace/human` video was rerun on the
same server.

## Differences found

Human/Sieg rigs are nearly identical: 22 bones, 24,894 vertices, neutral-position
difference <6e-7 m, normalized-weight difference <2.4e-8.

The early good Human GVHMR export lacks independent head/wrist rotations:
zero change across all 120 frames, with the head following the neck. Later
`guard_flow_gvhmr.fbx` and current full transfer preserve Head changes up to
54.98°. The early preview partly hid model predictions. Old MoCapAnything
retains up to 28.03° independent head animation; this export issue cannot
explain its result.

Human's person is about 631 pixels tall; Sieg walk/run figures are 226–332 pixels.
ViTPose head-point confidence is lower: median ~0.69–0.86 versus 0.97. Face
visibility, cape, hair, weapon and virtual camera are additional factors.
Confidence is not proof of keypoint or pose accuracy.

## Background-removal pipeline bug

For `camera=moving`, GVHMR uses visual odometry. First walk/run trials received
BiRefNet's white background and failed to find a relative camera pose; original
background clips succeeded.

The server now accepts optional `camera_video`, a synchronized original with
background. Pose uses `video`; camera uses `camera_video`. Frame counts and
sizes must match after normalization to 30 fps. No SDK/contract v1 changes:
an ordinary extra InputSpec. Both reruns with separate inputs succeeded; hashes
confirm original camera input and cleaned pose input.

The stance segment includes camera zoom. An additional raw `camera=moving`
trial also failed to find a usable camera pose: a separate failed control,
not evidence that removal alone causes VO failures. Successful white-background
processing likewise does not prove correct camera estimation from a moving
character. No automatic fallback to `static` was added.

## Pose results

Whole-clip medians. Head: model +Z-axis inclination to horizon; torso: Hips→Neck
angle from vertical. These describe reconstruction, not error against real
motion. Different actions cannot be ranked by one such number; attack/jump
lean may be intentional.

| Segment | Head: background → BiRefNet | Torso: background → BiRefNet |
| --- | ---: | ---: |
| Stance | 11.9° → 22.4° | 24.0° → 18.4° |
| Walk | 31.0° → 29.0° | 12.8° → 15.1° |
| Run | 20.2° → 26.7° | 43.4° → 44.3° |
| Jump | 3.2° → 21.0° | 32.2° → 35.9° |
| Human: axe strike | 22.4° → 23.5° | 10.6° → 11.3° |

Background removal neither eliminated GVHMR's pose tendency nor consistently
improved results. MoCapAnything stance/run look more upright, but walking is
problematic in both. Pose reconstruction from this game video remains a
limitation; do not assess head quality using an old export without its own motion.

## Artifacts and checks

Saved in `workspace/sieg/posture_batch/`:

- `index.html`: separate comparisons, native SMPL, Human and camera checks.
- `sieg_four_motions_models.mp4`: source / MoCapAnything / GVHMR, 381 frames,
  12.7 s; `_slow`: 762 frames, 25.4 s.
- `comparisons/human_old_vs_current.mp4`: historical/full transfer; original
  FBX unchanged, initial pose and XY aligned for viewing.
- `input_and_posture_summary.json`, `human_head_animation_audit.json`,
  `camera_routing_validation.json`, `validation.json` and all run snapshots.

20 successful jobs and three failed control attempts; walk/run reruns with
corrected routing succeeded. Checked 15 new FBX files: 22 bones, finite
transforms, normalized weights, expected frames. Failure history retained.
Main graph updated to version 2 with original-scene camera inputs. Full
`just check` and 15 GVHMR tests passed.
