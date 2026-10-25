"""Whole-body human motion from video with NVIDIA GEM-X, as SOMA parameters.

GEM-X (2026) tracks the main person, observes 77 SOMA keypoints (ViTPose on DINOv3), takes
Body Pose image features and predicts temporal SOMA motion in camera and world coordinates,
hands included. The result is the studio's native SOMA motion (``studio.soma-motion.v1``,
text): per-frame SOMA parameters in both spaces, the 77 joints, the rig's joint names and
parents, intrinsics, boxes and keypoint observations. Kimodo-SOMA can use it for in-betweening.

GEM-X itself runs in a child process in its own environment (worker/, PyTorch 2.10); its
code is a pinned source checkout, its weights and the SOMA v0.3.3 assets live in the cache.
"""

import hashlib
import json
import os
import subprocess
import time
from pathlib import Path
from typing import Literal

from asset_processing.workspace import job_workspace
from model_server_sdk.media import SOMA_MOTION
from model_server_sdk.video import canonical_h264_options, ffmpeg_binary
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    explain_child_failure,
    ui,
)

SOURCE_REVISION = "20555befe2fbbe5b2015e1fff7ff214dc18ea9d6"  # pragma: allowlist secret
WEIGHTS_REVISION = "4fce057963671fd95cd4154297944397e7b5a9a0"  # pragma: allowlist secret
SOMA_REVISION = "d281db2d01553f7230c56e764cec00fe27ef23f7"  # pragma: allowlist secret (v0.3.3)
FORMAT = "studio.soma-motion.v1"
FPS = 30
MAX_SECONDS = 10
TIMEOUT_S = 1800
WORKER = Path(__file__).resolve().parents[2] / "worker/gemx_worker.py"
WEIGHTS = (
    "body_s240000.ckpt",
    "vitpose.pth",
    "motion_native77_e774_s292175.ckpt",
    "motion_stats_raw.npz",
    "body_video_native77.yaml",
    "yolox_humanart.onnx",
)
SOMA_ASSETS = (
    "SOMA_neutral.npz",
    "SOMA_template_rig.usda",
    "SOMA_procedural_transforms.json",
    "correctives_model.pt",
)


def cache() -> Path:
    return Path(os.environ.get("XDG_CACHE_HOME", "~/.cache")).expanduser() / "assets-studio"


def source_directory() -> Path:
    return Path(os.environ.get("GEMX_SOURCE", str(cache() / "gemx-source"))).expanduser()


def weights_directory() -> Path:
    return Path(os.environ.get("GEMX_WEIGHTS", str(cache() / "gemx-weights"))).expanduser()


def worker_python() -> Path:
    return Path(os.environ.get("GEMX_PYTHON", str(cache() / "venvs/gemx-worker/bin/python")))


def missing_files() -> list[str]:
    weights = weights_directory()
    return [n for n in WEIGHTS if not (weights / n).is_file()] + [
        f"soma/{n}" for n in SOMA_ASSETS if not (weights / "soma" / n).is_file()
    ]


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    camera: Literal["static", "background"] = Field(
        default="static",
        description="Camera rotation: static (tripod) or estimated from the background",
        json_schema_extra=ui(ru="Камера: неподвижная или оценка поворота по фону", primary=True),
    )
    postprocess: bool = Field(
        default=True,
        description="GEM-X contact/IK correction (feet and hands)",
        json_schema_extra=ui(ru="Коррекция контактов и IK"),
    )


TASK = TaskSpec(
    "video-to-soma-motion",
    Params,
    (SOMA_MOTION,),
    prompt="none",
    inputs=(
        InputSpec(
            role="video",
            mime=["video/mp4"],
            description=f"One person, full body visible, one shot, at most {MAX_SECONDS} s",
            labels={"ru": f"Один человек в полный рост, один план, до {MAX_SECONDS} с"},
        ),
    ),
    max_count=1,
)


class PoseParams(BaseModel):
    model_config = ConfigDict(extra="forbid")


POSE_TASK = TaskSpec(
    "image-to-pose",
    PoseParams,
    (SOMA_MOTION,),
    prompt="none",
    inputs=(
        InputSpec(
            role="image",
            mime=["image/png", "image/jpeg", "image/webp"],
            description="A person, the whole body visible (a video frame saved from the studio)",
            labels={"ru": "Человек в полный рост (например, кадр видео из студии)"},
        ),
    ),
    max_count=1,
)


def run(command: list[str], *, cwd: Path | None = None, env: dict[str, str] | None = None) -> str:
    try:
        done = subprocess.run(  # noqa: S603 -- fixed argv, no shell
            command,
            cwd=cwd,
            env=env,
            capture_output=True,
            text=True,
            timeout=TIMEOUT_S,
            check=False,
        )
    except subprocess.TimeoutExpired as error:
        raise GenerationError(f"motion capture exceeded {TIMEOUT_S} s") from error
    output = done.stdout + done.stderr
    if command and str(command[0]) == str(
        worker_python()
    ):  # the worker's full log, kept for diagnosis
        logs = cache() / "gemx-logs"
        logs.mkdir(parents=True, exist_ok=True)
        (logs / f"{time.strftime('%Y%m%d-%H%M%S')}-{done.returncode}.log").write_text(output)
    if done.returncode:
        raise GenerationError(
            explain_child_failure(output, done.returncode),
            retryable="out of memory" in output.lower(),
        )
    return output


class GEMXServer(ModelServer):
    model = ModelInfo(
        id="gemx",
        name="GEM-X",
        revision=SOURCE_REVISION[:12] + "+" + WEIGHTS_REVISION[:8] + ".soma-motion.3",
        license="Code Apache-2.0; weights NVIDIA Open Model License; SOMA assets per nvidia/SOMA-X",
        source="https://github.com/NVlabs/GEM-X",
        details={
            "body model": "SOMA, 77 joints with fingers",
            "pipeline": "YOLOX person, ViTPose 77 (DINOv3, 3.4 GB), Body Pose features "
            "(3.5 GB), Motion network (46.8 M parameters)",
            "postprocessing": "contact/IK correction (optional)",
            "clip length": "up to 10 s per job",
            "PyTorch": "2.10, CUDA 12.8 (own worker environment)",
        },
    )
    tasks = (TASK, POSE_TASK)

    def load(self) -> None:
        if (
            not (source_directory() / "gem/inference/body_video.py").is_file()
            or not worker_python().is_file()
        ):
            raise FileNotFoundError("GEM-X source or worker missing; run just setup")
        missing = missing_files()
        if missing:
            raise FileNotFoundError(f"Missing GEM-X files: {', '.join(missing)}; run just download")

    def generate(self, job: Job) -> list[Output]:
        if job.task == "image-to-pose":
            return [self._pose(job)]
        assert isinstance(job.params, Params)
        with job_workspace("gemx-") as work:
            video, normalized = work / "input.mp4", work / "clip.mp4"
            video.write_bytes(job.inputs["video"].data)
            try:
                run(
                    [
                        ffmpeg_binary(),
                        "-nostdin",
                        "-y",
                        "-v",
                        "error",
                        "-i",
                        str(video),
                        "-an",
                        "-r",
                        str(FPS),
                        "-frames:v",
                        str(MAX_SECONDS * FPS + 1),
                        *canonical_h264_options(),
                        str(normalized),
                    ]
                )
            except GenerationError as error:
                raise InvalidInput(f"Cannot decode video: {error}") from error
            import imageio_ffmpeg

            reader = imageio_ffmpeg.read_frames(str(normalized))
            try:
                info = next(reader)
                frames = sum(1 for _ in reader)
            finally:
                reader.close()
            if not 2 <= frames <= MAX_SECONDS * FPS:
                raise InvalidInput(f"Video must contain 2–{MAX_SECONDS * FPS} frames at {FPS} fps")
            options = work / "options.json"
            options.write_text(
                json.dumps(
                    {
                        "weights": str(weights_directory()),
                        "soma_assets": str(weights_directory() / "soma"),
                        "camera": job.params.camera,
                        "postprocess": job.params.postprocess,
                    }
                )
            )
            env = {**os.environ, "PYTHONPATH": str(source_directory())}
            log = run(
                [str(worker_python()), str(WORKER), str(normalized), str(work), str(options)],
                cwd=source_directory(),
                env=env,
            )
            if os.environ.get("ASSETS_STUDIO_DEBUG_DIR"):
                (work / "worker.log").write_text(log)
            found = json.loads((work / "soma_motion.json").read_text())
            if found["frames"] != frames:
                raise GenerationError(f"GEM-X returned {found['frames']} frames for {frames}")
            motion = {
                "format": FORMAT,
                "fps": FPS,
                "frames": frames,
                "size": list(info["size"]),
                "model": "gemx",
                "source": {
                    "revision": SOURCE_REVISION,
                    "weights_revision": WEIGHTS_REVISION,
                    "soma_revision": SOMA_REVISION,
                    "video_sha256": hashlib.sha256(job.inputs["video"].data).hexdigest(),
                    "camera": job.params.camera,
                    "postprocess": job.params.postprocess,
                },
                "coordinate_spaces": {
                    "camera": "OpenCV camera, metres",
                    "world": "GEM-X world rollout, Y-up metres",
                },
                **{
                    k: found[k]
                    for k in (
                        "joint_names",
                        "rig",
                        "camera",
                        "world",
                        "identity_coeffs",
                        "scale_params",
                        "K_fullimg",
                        "bbx_xys",
                        "kp2d_native77",
                        "static_conf_logits",
                        "tracking",
                        "camera_diagnostics",
                    )
                },
            }
            metadata = {"frames": frames, "fps": FPS, **job.params.model_dump()}
            return [Output(SOMA_MOTION, json.dumps(motion).encode(), metadata)]

    def _pose(self, job: Job) -> Output:
        """One picture: the main person's SOMA pose (GEM-X Body Pose), a one-frame motion."""
        with job_workspace("gemx-pose-") as work:
            # the worker decodes it (OpenCV); the server keeps the bytes as they came
            suffix = {"image/jpeg": ".jpg", "image/webp": ".webp"}.get(
                job.inputs["image"].mime, ".png"
            )
            image = work / f"image{suffix}"
            image.write_bytes(job.inputs["image"].data)
            options = work / "options.json"
            options.write_text(
                json.dumps(
                    {
                        "weights": str(weights_directory()),
                        "soma_assets": str(weights_directory() / "soma"),
                        "mode": "image",
                    }
                )
            )
            env = {**os.environ, "PYTHONPATH": str(source_directory())}
            try:
                run(
                    [str(worker_python()), str(WORKER), str(image), str(work), str(options)],
                    cwd=source_directory(),
                    env=env,
                )
            except GenerationError as error:
                if "PERSON_NOT_FOUND" in str(error):
                    raise InvalidInput("no person found in the image") from error
                raise
            found = json.loads((work / "soma_motion.json").read_text())
            pose = {
                "format": FORMAT,
                "fps": FPS,
                "frames": 1,
                "model": "gemx-body-pose",
                "source": {
                    "revision": SOURCE_REVISION,
                    "weights_revision": WEIGHTS_REVISION,
                    "soma_revision": SOMA_REVISION,
                    "image_sha256": hashlib.sha256(job.inputs["image"].data).hexdigest(),
                },
                "coordinate_spaces": {
                    "camera": "OpenCV camera, metres",
                    "world": "the camera turned to Y up, the lowest joint at 0 (one image has no "
                    "gravity estimate)",
                },
                **{
                    k: found[k]
                    for k in (
                        "joint_names",
                        "rig",
                        "camera",
                        "world",
                        "K_fullimg",
                        "bbx_xys",
                        "keypoints_2d",
                    )
                },
            }
            return Output(SOMA_MOTION, json.dumps(pose).encode(), {"frames": 1})
