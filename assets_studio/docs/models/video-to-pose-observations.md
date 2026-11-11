# Video to 2D joints (pose observations)

<!-- markdownlint-disable MD013 -->

## Outcome

**ViTPose++-H** (`model_servers/vitpose`, port 9134): Apache-2.0, the 17 COCO
joints of one character per frame, about 3.5 GB of VRAM, 0.15 s per frame. The
server has no person detector: the caller names the character's region, and the
region then follows the torso. Its task and file are the existing ones,
`video-to-pose-observations` and `studio.pose-observations.v1`, so the result is
filed under Animations and has the fields PromptHMR's `observations` input reads
(the two have not been run together).

## Test clip (2026-11-11)

A phone screen recording of a mobile game: 480×1068, 91 frames at 24.13 fps. A
drawn, large-headed swordsman about 65 px tall runs in place in three-quarter view
among interface elements and other creatures; the clip holds 5.6 run cycles.

## Why not the existing video models

`prompthmr` and `gvhmr` recover a 3D body (SMPL-X) and begin with a person
detector and tracker. On this clip that first stage fails, so nothing reaches the
pose model:

| Input | Result of `gvhmr / video-to-pose-observations` |
| --- | --- |
| The recording | `No foreground person detected` |
| The character's region enlarged 6× | `Person tracking missing for 19 frames; refusing stale crop` |

The 2D pose model inside both, ViTPose-H, reads the character well once it is
given a box by hand. The detector is the weak part for drawn and small characters,
and 2D joints need neither a detector trained on photographs nor a 3D body.

## Candidates

| Model | Time, 91 frames | Peak VRAM | Unsure joints (score < 0.3) | Outcome |
| --- | --- | --- | --- | --- |
| **ViTPose++-H**, `usyd-community/vitpose-plus-huge`, Transformers | 14.0 s | 3.47 GB | 9 of 1547 | Selected |
| ViTPose-H, the checkpoint bundled with GVHMR | not measured | not measured | 3 of 1547 | Reference: same family, a pickled checkpoint with GVHMR's own model code |
| SDPose-Body (Stable Diffusion backbone) | 211.6 s | 5.47 GB | 24 of 1547 | Rejected |

All three were given the same fixed box and the same protocol where it applies
(flip test, sub-pixel heatmap decoding).

- **ViTPose++-H against ViTPose-H.** Frame by frame the two differ by 0.5–1.5 px
  in the head, arms and hips and by 1.9–2.7 px in knees and ankles. ViTPose++
  loads pinned safetensors through Transformers and needs no code from another
  project.
- **SDPose.** Fifteen times slower, two gigabytes more memory (one frame at a
  time; two do not fit in 8 GB), and its leg joints wander: the leg scores are
  lower (0.54–0.68 on average against 0.64–0.76) and the run's period could not be
  measured from its joints.

## What the joints are not

The server returns what the model answers, frame by frame. Measured on the clip,
for whoever turns the joints into an animation:

| Property | Evidence |
| --- | --- |
| Like limbs are named by position, not identity | The legs change names in 44 of 91 frames |
| A foot passing behind the other is found on the visible one | Both ankles on one foot, 12–13 px off, twice per run cycle |
| Trembling | The same moment of the cycle differs between its 5–6 repetitions; a joint's frame-to-frame second difference is 9–15 px in knees and ankles of a 65 px character, the run and the noise together |
| Sensitivity to the encoding | The studio's re-encoded copy of the clip moves the joints by 0.1–0.6 px (median) and puts leg joints on the other leg in 7 of 91 frames |

## Limits found

- Which leg is the left one in a side view is not in the picture: the model's
  "left" and "right" are positions there, not identities.
- "Ankles" sit at the boot tops of a character in boots; feet and hands have no
  joints in the COCO set.
- The clip's character is in three-quarter view, so the thighs cross at the hips in
  half of the cycle. That is what the picture shows, not an error of the model.
