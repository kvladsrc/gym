# PromptHMR-video pilot

Original RGB and a selected person track produce a preserved native SMPL-X motion
artifact (`studio.smplx-motion.v1`). Studio's `animation_tools` task
`retarget-native-motion` transfers that artifact onto the target rig separately.
No SDK contract changes are needed.

Run `just setup`, `just download`, `just doctor`, then `just serve` in this directory.
Setup pins upstream revision `3b566b7dbb28ce506c7ea972c18693f4c705ce8c`,
with separate Python 3.11 server and Python 3.10/Torch 2.3 inference environments.
Download verifies SHA256 hashes of official image and BEDLAM1+2 video weights.
Provide licensed `SMPLX_NEUTRAL.npz` through `SMPLX_MODEL_DIR`; download defaults
to reusing the existing GVHMR installation's body-model directory.

## Inputs and limits

- MP4, 30 fps, 2–300 frames, dimensions at most 1920 pixels.
- `observations`: `studio.pose-observations.v1` JSON with `frames`, `fps`, `size`
  ([width, height]), `source_sha256`, framewise `bbx_xyxy`, COCO17 `kp2d`
  ([x,y,confidence]), and `K_fullimg` (3×3 camera intrinsics).
- Single selected track; currently only an assumed static camera.
- The pilot reuses saved detector observations from the successful Human GVHMR
  run, bound to the exact normalized source video by SHA256. Automatic track
  extraction for new uploads is not yet integrated into this server.

Inference uses original RGB without generated edits or background removal,
896-pixel image crops in batches of one, and the official two-pass video fusion.
Image and video networks run sequentially to fit 8 GB VRAM. In the tested stack,
image attention uses PyTorch SDPA; all used learned checkpoint parameters load
strictly. Unused auxiliary mesh conversions are omitted. The worker does not
run upstream global camera/floor optimization: global parameters are the raw
world-head prediction, not the full official final world reconstruction.

The JSON preserves camera-space and world-space body parameters, intrinsics,
observations, image predictions, contact probabilities, timing and a BVH for
transfer. Camera intrinsics in the pilot are heuristic, not measured calibration.
The target transfer currently uses the existing 22-joint SMPL profile; fingers
and sword grip constraints are not solved.

## Controlled pilot

`workspace/prompthmr_pilot/flow_manifest.json` identifies the persisted Studio
Flow. Its three jobs run PromptHMR native inference and transfer both PromptHMR
and the saved GVHMR baseline onto the same MIA guard. `native_metrics.json`
contains projection and posture diagnostics; these have no ground-truth 3D.
This is a comparison harness, not evidence that PromptHMR is a better default.

Upstream: <https://github.com/yufu-wang/PromptHMR>.
**License: non-commercial scientific research; SMPL-X has separate terms.**
