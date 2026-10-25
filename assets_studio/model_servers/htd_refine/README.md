# HTD-Refine

Native SMPL-X refinement before joining model windows or retargeting a character.
Task `refine-native-motion` takes the exact source MP4 and a fresh GVHMR native
JSON with full `studio.smplx-parameters.v1` data. One native JSON output, no SDK
extensions. Shown in Studio Animations and available in Flow.

Supported: one person, static camera,4–300frames at30fps. PVA uses120-frame
windows with30-frame overlap, independent image encoder batches4. Optimization
uses the full input window and default upstream settings. No strength controls
are exposed without validated semantics. Moving-camera trajectories, arbitrary
FBX and already joined BVH-only motion are not accepted as refinement inputs.

Full graph: video -> GVHMR native -> HTD -> join overlapping native windows ->
one MIA retarget -> weapon. Keep the control branch on the same raw captures.
Gallery/delivery should expose whole source segments, not each neural window.

```sh
just setup       # isolated Python3.11 server / Python3.10 worker; UV cache reused
just test        # no GPU or weights
just serve       # 127.0.0.1:9127; ordinary launcher recipe: htd_refine
```

The existing source/checkpoints from the earlier pilot are reused. Source must
be the official ant-research/HTD-Refine checkout at revision
2fcd6ddef3c4eb75a636062245f80a0136c09b7e, under
~/.cache/assets-studio/htd-refine-source. Official serialized checkpoint:
inputs/checkpoints/pvanet.pt. Licensed SMPL-X model files must be present under
inputs/checkpoints/body_models/smplx; the current installation links the existing
GVHMR body-model cache. Setup does not silently fetch model weights or licenses.
HTD_REFINE_SOURCE and HTD_REFINE_PYTHON override the source and worker interpreter.
Worker dependencies use the tested torch2.3/CUDA12.1 compatibility stack in a
separate environment; no packages are installed into the GVHMR environment.

The server validates dimensions, finite parameters, static camera, frame count,
30fps and source byte identity when a GVHMR fingerprint is available. The worker
also checks that BVH rotations/root path agree with the raw SMPL-X. Body shape
must remain fixed for native export; output does not retain stale input camera
parameters. Refined global parameters are retained separately as refined_smplx;
joining removes window-specific raw/refined parameter blocks. Unknown old source
fingerprints are reported as video_identity_verified=false, not assumed verified.

Diagnostics in asset metadata include jerk, wrist speed, wrist displacement and
PVA runtime/memory. They are not calibrated accuracy scores. Smoothing can alter
pose, travel and real strikes. Output frames/timing are preserved. Refinement is
an explicit optional node, never applied to all library animations by default.
