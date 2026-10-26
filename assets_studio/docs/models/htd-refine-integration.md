# HTD-Refine after GVHMR: compatibility check

**Current status (October 20, 2026):** HTD is integrated as server `htd_refine`,
task `refine-native-motion`, port 9127, automatically started by the launcher.
It appears under Animations and in Flow. Real 60/300-frame jobs passed. Server
and worker have separate environments; SDK and GVHMR environment are unchanged.
Instructions: `model_servers/htd_refine/README.md`. Initial checks and pilot
findings are retained below.

Inputs are the exact source video and unmerged native GVHMR with full SMPL-X.
Currently supports static cameras, 4–300 frames at 30 fps. After HTD, rejoin
windows into the source segment before transferring to MIA. Window-level
raw/refined parameters must not be presented as whole merged-clip parameters.

Tested revision `2fcd6ddef3c4eb75a636062245f80a0136c09b7e` of the
[official repository](https://github.com/ant-research/HTD-Refine). Ran a real
PVA-Net/optimization pilot on static Human (120 frames, 30 fps). Results and
comparison: `workspace/sword_prof/htd_refine/`.

## Position in the pipeline

```text
30 fps video + selected person
              ↓
       GVHMR: original SMPL-X ──────────────┐
              ↓                            │
 HTD-Refine: video + SMPL-X + K + camera    │ control branch
              ↓                            │
       refined SMPL-X parameters           │
              ↓                            ↓
         identical MIA transfer → two comparable FBX files
```

Refinement runs **before target-character transfer** and before `stationary` /
`in-place` travel removal. A finished guard FBX lacks original SMPL-X shape
parameters and camera calibration. Arbitrary MoCapAnything FBX is therefore
not ready-made HTD input; separate SMPL recovery/fitting and quality measurement
would be needed.

## Checks on our data

Loaded official
[`utils/gvhmr.py`](https://github.com/ant-research/HTD-Refine/blob/2fcd6ddef3c4eb75a636062245f80a0136c09b7e/utils/gvhmr.py)
without SDK/environment changes. Its loader accepted saved Human (120 frames)
and Sieg walking (90 frames) predictions, already understanding our field names.

Both provide:

- Global and camera-space SMPL-X parameters.
- Root rotation, 63 body-pose components, 10 shape coefficients, translation.
- Per-frame camera intrinsics.

Tensor shapes, hashes and source paths:
`workspace/sword_prof/htd_refine/input_compatibility.json`. This checks format
only; the full Human pilot is below.

Checked packages lack explicit `T_w2c`. For moving cameras, official
[`demo_optimization.py`](https://github.com/ant-research/HTD-Refine/blob/2fcd6ddef3c4eb75a636062245f80a0136c09b7e/scripts/demo_optimization.py)
requires a synchronized external trajectory. Do not implicitly recover it from
two human poses or substitute a static camera. Human or a static BOHEMA segment
is suitable for the first pilot.

PVA and optimization require identical frames, person and `K`. GVHMR calibration
is estimated, not measured. HTD demos use 30 fps; our mocap already normalizes
to that rate.

## Expected effects and evaluation

PVA-Net estimates temporal motion properties; optimization then refines SMPL.
Separately assess jitter, foot placement, lunge retention, peak hand speed and
torso/head pose. Smoother motion alone does not prove improved fencing.

The [authors' table](https://zju3dv.github.io/htd-refine/) reports GVHMR EMDB-2
jitter 17.2 → 7.2, but FS 4.0 → 5.7. On RICH, jitter 13.0 → 3.6 and FS remains
3.3. Do not promise simultaneous improvement in every measure. Constant head
orientation errors may persist; fixing them is our hypothesis, not an established
fencing result.

## Studio integration without SDK changes

Proposed boundary: a separate HTD server/worker environment. Inputs: video,
native SMPL-X motion and explicit camera when required; output: refined SMPL-X
motion. MIA transfer is a separate final node.

Retain compatible GVHMR `video-to-motion`. Native motion needs separate export;
do not add a second output per seed without checking output-count rules.
A suitable option is a separate task returning one versioned motion JSON in
supported `text/plain`, interpreted only by model servers. The SDK should not
know SMPL structure or refinement algorithms. Finalize format after a real pilot.

Ordinary jobs delete temporary predictions after FBX. Diagnostic Human/Sieg
files allow the pilot without rerunning capture. For new chains, native packages
must become normal library artifacts, not debug-directory dependencies.

Our existing GVHMR worker uses Python 3.10, torch 2.3/CUDA 12.1 and PyTorch3D,
close to HTD's stack. Do not extend it in place; isolation preserves working
GVHMR reproducibility. HTD's `pyproject.toml` has empty dependencies; versions
are in `requirements.txt`. The pilot used the existing compatible interpreter
without installing into GVHMR's environment; `video-reader-rs==0.2.1` was added
in a separate overlay. Official PVA-Net weights were downloaded into HTD cache.

Next practical test: one static clip, original SMPL-X → HTD, then identical
transfer profile/preview camera for both branches. Compare quality/memory before
expanding the set or moving cameras.

## Human pilot result (October 19, 2026)

Official PVA-Net/optimizer ran all 120 frames with original person boxes and
K, static camera and default optimization. ViTPose processes independent images
in batches of four; PVA temporal window stays 120 frames, overlap 30. PVA took
13.85 seconds, peak Torch-allocated CUDA memory 5.01 GiB (not full process memory).
Both branches transferred to one MIA guard with `smpl`, `stationary`, no extra
pose edits.

Custom diagnostics, **without 3D ground truth**, not paper metrics: mean pelvis-relative
joint third-difference norm fell 457.10 → 153.35 m/s³ (−66.5%). Peak pelvis-relative
wrist speed fell 10.61 → 7.45 m/s (−29.8%). SMPL +Z head-down proxy median rose
22.41° → 33.00°; torso lean 10.60° → 13.09°. This indicates smoothing, not pose
correction; stronger default HTD across all motions is unwarranted.

Artifacts: `human_pva.pt`, `human_optimization/clip/opt_results.pt`, `before.fbx`,
`after.fbx`, `comparison_metrics.json`, `human_htd_comparison.mp4`. Production
still requires standard native SMPL-X export, separate server/environment and
Flow nodes. This pilot alone does not mean HTD is integrated. SDK unchanged.

## Longsword experiment (October 20, 2026)

Three synchronized 120-frame clips: stance 172.4–176.4 s, dynamic sequence
57.7–61.7 s, abrupt pose change 647.7–651.7 s. No GVHMR rerun: reused saved
window predictions that passed person-box checks. HTD received identical frames,
boxes, K, static camera and default settings. Control parameters exactly match
the originals (`input_preservation.json`).

Pelvis-relative jitter proxy fell 67.1%, 68.4%, 77.1%. Peak wrist speed changed
−1.9%, −0.7%, −73.2%. In the third case, peak fell 22.0 → 5.9 m/s, but p95 speed
rose 79.4%: a lower peak does not prove improvement. P95 wrist displacement is
10–14 cm; pose/path also change. These are no-ground-truth diagnostics, not
accuracy or strike-preservation evidence.

Both branches transferred to KPop MIA using `bone-directions`, `world`, identical
two-handed swords, shared camera/scale and initial control-branch alignment.
All 12 transfer/equip jobs in three saved Studio Flows succeeded.

Artifacts: `workspace/longsword_htd_experiment/index.html`, `comparison_reel.mp4`,
`htd_comparisons.zip` (12 FBX), `metrics.json`, `verification.json`. Previews were
added to Studio with `experiment:longsword-htd`. Original library retained.
HTD ran as a separate offline experiment; at that point no HTD server/node
existed in Studio. Launcher stayed running; SDK/installed dependencies unchanged.
