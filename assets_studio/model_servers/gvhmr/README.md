# GVHMR

Human motion from a monocular video onto a humanoid rig with Mixamo bone
names (e.g. Make-It-Animatable). Inputs: MP4 and rigged FBX/GLB; output:
animated FBX. One person, full body visible, one continuous shot, up to 10 s.
Video is resampled to 30 fps. Fingers and facial motion are not predicted.

`video-to-native-motion` needs only the video and returns one JSON motion asset.
Alongside the retarget BVH, new captures preserve an optional `smplx` block
(`studio.smplx-parameters.v1`): global and camera-space body parameters, estimated
intrinsics and synchronized person boxes. This supports experiments such as
HTD-Refine without relying on temporary debug files or changing the SDK.
Existing BVH-only assets remain valid. Joining BVHs does not merge these full
parameters or infer a camera trajectory; refiners should use the raw captures.

```sh
cd assets_studio/model_servers/gvhmr
just setup
just download                    # pinned HF mirror, SHA256 verified (~5.6 GB)
# Install the registered SMPL-X body model as described below.
just doctor
just serve                       # 127.0.0.1:9123
just test                        # no GPU or weights
```

`camera=static` skips visual odometry; choose `moving` for a moving camera.
For an embedded human illustration or picture-in-picture, set
`ignored_person_region="0,0,0.35,0.55"` to exclude detection centers in that
normalized left/top/right/bottom rectangle. The default empty string excludes
nothing. This filters person selection only: the video, camera intrinsics and
frame count remain unchanged. Keep the live actor outside the excluded region;
multiple people elsewhere still cause an error. The tracking report records the
region and excluded detection count. This is a GVHMR task parameter, with no SDK
changes.

`root_motion=in-place` removes horizontal travel and heading for locomotion;
`stationary` keeps heading/spins while removing horizontal travel, suitable for
attacks; `world` also retains the predicted path. Blender transfers full bone
rotations (including wrist twist), aligned to
the rest bone directions, to
the input rig; differing proportions and bone rest poses affect the result.

Source, weights and environments live under `~/.cache/assets-studio`, outside
Dropbox (`XDG_CACHE_HOME` supported). `GVHMR_SOURCE`, `GVHMR_PYTHON` and
`BLENDER` override the source checkout, worker interpreter and Blender binary.
The server uses Python 3.11; the worker uses Python 3.10, torch 2.3/CUDA 12.1.

`just download` uses the community mirror [camenduru/GVHMR](https://huggingface.co/camenduru/GVHMR),
pinned by revision and SHA256. `just download-drive` uses upstream's original
Google Drive folder (may hit its download quota). DPVO and training datasets
are not needed.

Upstream's downloadable checkpoints do not include the registration-gated
SMPL-X model. Register at <https://smpl-x.is.tue.mpg.de/>, accept its terms,
and place the neutral NPZ body model at:

```text
~/.cache/assets-studio/gvhmr-source/inputs/checkpoints/body_models/smplx/SMPLX_NEUTRAL.npz
```

GVHMR's upstream LICENSE permits educational, research and non-profit use;
SMPL-X has separate terms. See the pinned source's LICENSE for full conditions.

Validation: server unit tests and the SDK/launcher tests pass. All four
checkpoint downloads were SHA256 verified; HMR2 and ViTPose each completed
a one-frame CUDA inference on the laptop (about 2.7 GiB peak allocated
VRAM per process). A synthetic
30-frame motion was written by the worker's BVH exporter, retargeted with
Blender 5.1.1 and reimported: the FBX preserves a 1 m root path and arm
motion. The server's full file-processing path was checked with real video
encoding, GLB conversion and Blender in both root-motion modes, substituting
a synthetic BVH for the neural prediction.

Full neural capture validated on 2026-10-18 with the registered SMPL-X model:
`workspace/human/human_0m04-0m08.mp4` (4 s), retargeted onto
`guard_kimodo_7a531b.fbx`. Static camera + world motion: 54 s; moving camera
(SimpleVO) + in-place motion: 59 s. Each output has 120 frames and 22 bones.
Blender reimport found finite joint positions in every frame and preserved
the textured mesh. Unity 6000.6.0f1 imported the world-motion FBX as Generic
with a 120-frame clip; sampling the clip changed 16 bones by more than 1°.
This checks import and animation sampling, not gameplay or foot-contact quality.

Results: `workspace/human/guard_gvhmr_world.fbx`,
`guard_gvhmr_in_place.fbx`, and `guard_gvhmr_preview.png`.
The preview shows the static-camera world-motion result.

For operator diagnostics, set `ASSETS_STUDIO_DEBUG_DIR=/path` on the server.
Each request retains its inputs, preprocessing, `body_motion.npz`, raw
`prediction.pt`, BVH, logs and final FBX in a unique directory. Ordinary jobs
still remove their temporary files. The retained predictions let us compare
SMPL forward kinematics, BVH and skinned output without rerunning inference.

Retargeting uses the `smpl` profile from `asset_processing`: it preserves the
target rig's neutral spine, neck and head shape and transfers rotations relative
to it. The pelvis receives the original SMPL rotation rather than a tilt derived
from joint positions. This does not straighten leans already predicted by the model.

If background removal precedes GVHMR and the camera moves, connect two videos:
`video`, the BiRefNet result for pose estimation, and `camera_video`, the same
segment with its original background for camera-motion estimation. They must
have identical frames and dimensions. With `camera=static`, the separate camera
video is not used. Without `camera_video`, the previous single-video behavior remains.

A white background has no scene landmarks. Visual odometry may fail or mistake
character motion for camera motion. If no usable camera pose is found, the job
fails with an explanation; the server does not silently switch to a static camera.

`video-to-native-motion` accepts the same video/camera inputs without a rig and
returns one `studio.smplx-motion.v1` JSON (`text/plain`). It contains the native
world-space BVH before heading/path removal or target-rig transfer. The same
10-second input limit applies. No SDK changes are needed.
