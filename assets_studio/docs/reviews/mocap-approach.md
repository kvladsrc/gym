# Review: motion capture and visual inspection (2026-10-19)

The delivered fencing animations have not met the user's quality requirement.
Successful jobs, valid FBX and a saved graph demonstrate operability and
reproducibility; they do not demonstrate usable motion. This review uses code,
logs and saved numeric reports, without viewing images or video.

## Findings

- We escalated from short pilots to five clips and ten outputs before agreeing
  what a successful movement looks like. Iteration cost increased before the
  principal failure was isolated.
- Earlier conclusions that input quality explained the issue were too strong.
  Better inputs can help, but the available outputs and Human HTD measurements
  do not establish a single cause. Source pose, camera calibration, retargeting
  and skinning must be checked independently.
- Known-good MIA + Mixamo animation validates that particular rig/animation path.
  It does not validate SMPL-to-MIA rest-axis conversion, but it argues against
  globally changing the character to compensate for one mocap model.
- Head/spine problems cannot be diagnosed from the rendered target alone.
  Compare native source-joint orientations, source mesh, target rest axes,
  transferred rotations and skin deformation, holding each other stage fixed.
  Numeric joint proxies are diagnostics, not anatomically calibrated ground truth.
- Our character previews normalize initial heading and pelvis and use a shared
  display camera, while the source panel retains the original camera. Direct
  silhouette/head-angle differences between these panels can be view differences.
  Reproject native camera-space motion with the original intrinsics for source
  correspondence; compare native and target bodies from one identical viewpoint
  for retargeting. A local VLM cannot compensate for a missing camera match.
- White background removal changes the input distribution and removes camera
  cues. Keep an untreated control. For a moving camera retain synchronized
  original imagery and explicit camera estimation; never substitute a static
  camera merely to make a job succeed.
- Qwen-Image editing is slow and generative. It can alter anatomy, limbs and
  equipment. It should not repaint every mocap frame. Our SAM reference scheme
  preserves original pixels, but subject selection still needs validation.
- SAM can lose occluded limbs, switch subject or clip the body; nonempty masks
  and full-body edge checks are useful but do not prove temporal correctness.
  Try crop/box/point selection first when it can avoid generative references.
- GVHMR and MoCapAnything infer motion from ambiguous video. Masks, two actors,
  two-handed grips, fast limbs and occlusions make fencing difficult. Neither
  model is a source of known-correct sword technique or weapon/finger animation.
- The Human HTD pilot reduced root-relative joint third differences by 66.5%,
  while head-down proxy increased 22.4° → 33.0° and peak wrist speed fell 29.8%.
  That is evidence of smoothing with pose/speed tradeoffs, not a quality fix.
  Keep refinement optional and before retargeting; do not enable it globally.
- Repeated provider-side viewing of contact sheets is expensive and informal.
  A local VLM provides reusable text observations but cannot establish 3D
  correctness or replace temporal measurements and the user's acceptance.

## Responsibilities of the tools

| Component | Keep it responsible for | Do not infer from its success |
| --- | --- | --- |
| Studio / launcher | Assets, jobs, provenance; one heavy worker at a time | Animation quality |
| Flow | Saved recipes and comparable branches | Correctness of the recipe |
| Qwen3.8 local VLM | Bounded, timestamped text observations | Ground truth, reliable 3D anatomy or continuous motion |
| SAM / background removal | Subject selection, original pixel preservation | Correct motion in hidden body parts |
| GVHMR / MoCapAnything | Native inferred motion and confidence where available | Correct rigging or professional technique |
| Retarget / Blender | Deterministic transfer, valid transforms and weights | Correct source pose |
| HTD | Optional native SMPL dynamics refinement | Head/spine correction or preserved impact speed |

## Change the experiment before scaling it

1. Define acceptance on one simple visible full-body action and one sword action:
   stable identity, complete framing, plausible torso/head, consistent limbs,
   preserved timing/turns/impact, acceptable feet. Ask the user to judge the
   concrete pilot before scaling a dataset; do independent preparation first.
2. Save original frame range, frame mapping, crop, subject choice, camera mode,
   native model output and model revision as ordinary library artifacts.
3. Compare untreated vs minimal crop/segmentation with the same model and seed.
   Change one stage per experiment; generation of a cleaner-looking image is
   not itself evidence that mocap will improve.
4. Evaluate native motion before applying a target rig. Use an unchanged known
   animation to validate retargeting separately, including head and spine axes.
5. Retarget accepted native motion to one fixed rig; validate frame counts,
   transforms, weights and temporal motion. Measure smoothing separately from
   speed loss and posture drift. Do not straighten poses manually in secret.
6. Save the comparison, numeric report and local VLM observations together.
   Mark unresolved results as experiments. A successful queue status must not
   be described as user acceptance or production readiness.

The next motion experiment should follow these gates, not add another automatic
post-processing stage. This task installs the local inspection path and shared
agent rules; it does not claim to have fixed the fencing animations.

## First local VLM review

Six bounded video requests cover the five fencing comparisons and Human HTD,
with two sampled timestamps each. They are stored in
`workspace/qwen_video_review/`, with model revision, prompt, timestamp, job ID
and text-asset ID. Observations are provisional: the four-panel fencing layout
reduces detail, there can be multiple actors in the original, and the source
and standardized 3D previews have different cameras. The prompt asks about
visible pose, not a score or a rigging diagnosis. These reports do not replace
matched-camera source/target validation.

Detailed follow-up: [animation graph audit](animation-graphs.md), including
all 16 saved graphs, camera-space diagnostics and replacement candidates.
