"""Human motion from video, retargeted onto a Mixamo rig."""

import hashlib
import json
import os
import shutil
import subprocess
from pathlib import Path
from typing import Literal

from asset_processing.blender import RETARGET_SCRIPT
from asset_processing.native_motion import FORMAT, read_native_motion
from asset_processing.workspace import job_workspace
from model_server_sdk.media import SMPLX_MOTION
from model_server_sdk.video import canonical_h264_options, ffmpeg_binary
from pydantic import BaseModel, ConfigDict, Field, field_validator

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

SOURCE_REVISION = "ee960bb6e2ea2d381aa97f08e9b71ef320b624b1"  # pragma: allowlist secret
FPS = 30
MAX_SECONDS = 10
TIMEOUT_S = 1800
WORKER = Path(__file__).resolve().parents[2] / "worker/gvhmr_worker.py"
CHECKPOINTS = (
    "gvhmr/gvhmr_siga24_release.ckpt",
    "hmr2/epoch=10-step=25000.ckpt",
    "vitpose/vitpose-h-multi-coco.pth",
    "yolo/yolov8x.pt",
    "body_models/smplx/SMPLX_NEUTRAL.npz",
)


def source_directory() -> Path:
    cache = Path(os.environ.get("XDG_CACHE_HOME", "~/.cache")).expanduser()
    return Path(
        os.environ.get("GVHMR_SOURCE", str(cache / "assets-studio/gvhmr-source"))
    ).expanduser()


def worker_python() -> Path:
    return Path(
        os.environ.get(
            "GVHMR_PYTHON", str(source_directory().parent / "venvs/gvhmr-worker/bin/python")
        )
    )


def blender() -> str | None:
    found = os.environ.get("BLENDER") or shutil.which("blender")
    if found:
        return found
    profile = Path.home() / ".nix-profile/bin/blender"
    return str(profile) if profile.is_file() else None


def missing_files() -> list[str]:
    source = source_directory()
    return [name for name in CHECKPOINTS if not (source / "inputs/checkpoints" / name).is_file()]


class NativeParams(BaseModel):
    model_config = ConfigDict(extra="forbid")
    camera: Literal["static", "moving"] = Field(
        default="static",
        description="Whether the camera stays still or moves",
        json_schema_extra=ui(ru="Камера неподвижна (static) или движется (moving)", primary=True),
    )
    ignored_person_region: str = Field(
        default="",
        description=(
            "Ignore detection centers inside an illustration rectangle: left,top,right,bottom "
            "in normalized 0–1 coordinates; empty disables it. Video and framing stay unchanged."
        ),
    )

    @field_validator("ignored_person_region")
    @classmethod
    def valid_person_region(cls, region: str) -> str:
        return person_region(region)


def person_region(region: str) -> str:
    if region.strip():
        try:
            left, top, right, bottom = map(float, region.split(","))
        except ValueError as error:
            raise ValueError("Use four comma-separated normalized coordinates") from error
        if not (0 <= left < right <= 1 and 0 <= top < bottom <= 1):
            raise ValueError(
                "Person exclusion rectangle requires 0 <= left < right <= 1 "
                "and 0 <= top < bottom <= 1"
            )
    return region.strip()


class ObservationParams(BaseModel):
    model_config = ConfigDict(extra="forbid")
    ignored_person_region: str = NativeParams.model_fields["ignored_person_region"]

    @field_validator("ignored_person_region")
    @classmethod
    def valid_person_region(cls, region: str) -> str:
        return person_region(region)


class Params(NativeParams):
    root_motion: Literal["in-place", "stationary", "world"] = Field(
        default="in-place",
        description="in-place: no path/heading; stationary: keep turns; world: keep the path",
        json_schema_extra=ui(
            ru="in-place — без пути и поворотов; stationary — на месте, с поворотами; "
            "world — ещё и с перемещением",
            primary=True,
        ),
    )


TASK = TaskSpec(
    "video-to-motion",
    Params,
    ("model/x-fbx",),
    prompt="none",
    inputs=(
        InputSpec(
            role="video",
            mime=["video/mp4"],
            description=f"One person, full body visible, one shot, at most {MAX_SECONDS} s",
            labels={"ru": f"Один человек в полный рост, один план, до {MAX_SECONDS} с"},
        ),
        InputSpec(
            role="rig",
            mime=["model/x-fbx", "model/gltf-binary"],
            description="Humanoid rig with Mixamo bone names (Make-It-Animatable)",
            labels={"ru": "Риг человека с именами костей Mixamo (Make-It-Animatable)"},
        ),
        InputSpec(
            role="camera_video",
            mime=["video/mp4"],
            required=False,
            description="Original synchronized video with background for moving-camera estimation; "
            "same frames and dimensions as video",
            labels={"ru": "Исходное видео с фоном для движущейся камеры; те же кадры и размер"},
        ),
    ),
    max_count=1,
)

NATIVE_TASK = TaskSpec(
    "video-to-native-motion",
    NativeParams,
    (SMPLX_MOTION,),
    prompt="none",
    inputs=tuple(spec for spec in TASK.inputs if spec.role != "rig"),
    max_count=1,
)


OBSERVATIONS_TASK = TaskSpec(
    "video-to-pose-observations",
    ObservationParams,
    ("text/plain",),
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


def run(command: list[str], *, cwd: Path | None = None) -> str:
    try:
        done = subprocess.run(  # noqa: S603 -- fixed argv, no shell
            command,
            cwd=cwd,
            capture_output=True,
            text=True,
            timeout=TIMEOUT_S,
            check=False,
        )
    except subprocess.TimeoutExpired as error:
        raise GenerationError(f"motion capture exceeded {TIMEOUT_S} s") from error
    output = done.stdout + done.stderr
    for line in output.splitlines():
        if line.startswith("RIG_INVALID "):
            raise InvalidInput(line.removeprefix("RIG_INVALID "))
    if done.returncode:
        raise GenerationError(
            explain_child_failure(output, done.returncode),
            retryable="out of memory" in output.lower(),
        )
    return output


class GVHMRServer(ModelServer):
    model = ModelInfo(
        id="gvhmr",
        name="GVHMR",
        revision=SOURCE_REVISION[:12] + "+tracking.2+native.1+observations.1",
        license="See upstream LICENSE and SMPL-X terms",
        source="https://github.com/zju3dv/GVHMR",
        details={
            "body model": "SMPL-X (22 body joints, no fingers)",
            "pipeline": "YOLOv8x person, ViTPose-H COCO-17, HMR2 features, GVHMR",
            "checkpoints": "~5.6 GB",
            "clip length": "up to 10 s per job",
        },
    )
    tasks = (TASK, NATIVE_TASK, OBSERVATIONS_TASK)

    def __init__(self) -> None:
        self._blender: str | None = None

    def load(self) -> None:
        if (
            not (source_directory() / "tools/demo/demo.py").is_file()
            or not worker_python().is_file()
        ):
            raise FileNotFoundError("GVHMR source or worker missing; run just setup")
        missing = missing_files()
        if missing:
            raise FileNotFoundError(
                f"Missing GVHMR checkpoints: {', '.join(missing)}; see README.md"
            )
        self._blender = blender()
        if self._blender is None:
            raise FileNotFoundError("Blender missing; set BLENDER or add it to PATH")

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, NativeParams | ObservationParams)
        assert self._blender is not None
        with job_workspace("gvhmr-") as work:
            video, normalized = work / "input.mp4", work / "clip.mp4"
            video.write_bytes(job.inputs["video"].data)
            # Decode only a bounded number of frames. Refuse long clips instead of truncating.
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
                pose_info = next(reader)
                frames = sum(1 for _ in reader)
            finally:
                reader.close()
            if not 2 <= frames <= MAX_SECONDS * FPS:
                raise InvalidInput(f"Video must contain 2–{MAX_SECONDS * FPS} frames at {FPS} fps")
            camera_reference = None
            camera = getattr(job.params, "camera", "static")
            if "camera_video" in job.inputs and camera == "moving":
                camera_input = work / "camera_input.mp4"
                camera_input.write_bytes(job.inputs["camera_video"].data)
                camera_reference = work / "camera.mp4"
                try:
                    run(
                        [
                            ffmpeg_binary(),
                            "-nostdin",
                            "-y",
                            "-v",
                            "error",
                            "-i",
                            str(camera_input),
                            "-an",
                            "-r",
                            str(FPS),
                            "-frames:v",
                            str(MAX_SECONDS * FPS + 1),
                            *canonical_h264_options(),
                            str(camera_reference),
                        ]
                    )
                    reference_reader = imageio_ffmpeg.read_frames(str(camera_reference))
                    try:
                        reference_info = next(reference_reader)
                        reference_frames = sum(1 for _ in reference_reader)
                    finally:
                        reference_reader.close()
                except (GenerationError, RuntimeError) as error:
                    raise InvalidInput(f"Cannot decode camera reference: {error}") from error
                if reference_frames != frames or reference_info["size"] != pose_info["size"]:
                    raise InvalidInput(
                        "camera_video must have the same frame count and dimensions as video"
                    )
            rig = work / "rig.fbx"
            if job.task == "video-to-motion":
                rig = work / ("rig.fbx" if job.inputs["rig"].mime == "model/x-fbx" else "rig.glb")
                rig.write_bytes(job.inputs["rig"].data)
            if job.task == "video-to-motion" and rig.suffix == ".glb":
                converted = work / "rig.fbx"
                run(
                    [
                        self._blender,
                        "-b",
                        "--factory-startup",
                        "-P",
                        str(Path(__file__).with_name("convert_rig.py")),
                        "--",
                        str(rig),
                        str(converted),
                        str(RETARGET_SCRIPT.parent.parent),
                    ]
                )
                if not converted.is_file():
                    raise InvalidInput("Cannot convert GLB rig to FBX")
                rig = converted
            command = [
                str(worker_python()),
                str(WORKER),
                str(normalized),
                str(work),
                "observations" if job.task == "video-to-pose-observations" else camera,
            ]
            if camera_reference is not None:
                command.append(str(camera_reference))
            (work / "person_selection.json").write_text(
                json.dumps(
                    {
                        "ignored_person_regions": [
                            list(map(float, job.params.ignored_person_region.split(",")))
                        ]
                        if job.params.ignored_person_region
                        else []
                    }
                )
            )
            worker_log = run(
                command,
                cwd=source_directory(),
            )
            if os.environ.get("ASSETS_STUDIO_DEBUG_DIR"):
                (work / "worker.log").write_text(worker_log)
            tracking_path = work / "clip/preprocess/person_tracking.json"
            tracking = json.loads(tracking_path.read_text()) if tracking_path.exists() else None
            metadata = {
                "frames": frames,
                "fps": FPS,
                **job.params.model_dump(exclude={"ignored_person_region"}),
            }
            if job.params.ignored_person_region:
                metadata["ignored_person_region"] = job.params.ignored_person_region
            if tracking is not None:
                metadata["tracking"] = tracking
            if job.task == "video-to-pose-observations":
                found = json.loads((work / "observations.json").read_text())
                if found["frames"] != frames:
                    raise GenerationError("observations disagree with the video's frame count")
                observations = {
                    "format": "studio.pose-observations.v1",
                    "fps": FPS,
                    "frames": frames,
                    "size": found["size"],
                    # The same bytes reach PromptHMR or other consumers as their video input.
                    "source_sha256": hashlib.sha256(job.inputs["video"].data).hexdigest(),
                    "bbx_xyxy": found["bbx_xyxy"],
                    "kp2d": found["kp2d"],
                    "K_fullimg": found["K_fullimg"],
                    "intrinsics_source": "GVHMR estimate_K: focal length from the image size",
                    "detector": "YOLO dominant person + ViTPose-H COCO-17 (GVHMR preprocessing)",
                    "tracking": tracking,
                    "revision": SOURCE_REVISION,
                }
                return [Output("text/plain", json.dumps(observations).encode(), metadata)]
            if job.task == "video-to-native-motion":
                package = {
                    "format": FORMAT,
                    "coordinate_space": "model-world-y-up",
                    "model": "gvhmr",
                    "fps": FPS,
                    "frames": frames,
                    "retarget_bvh": (work / "motion.bvh").read_text(),
                    "source": {
                        "camera": camera,
                        "revision": SOURCE_REVISION,
                        "tracking": tracking,
                        "video_sha256": hashlib.sha256(job.inputs["video"].data).hexdigest(),
                    },
                }
                native_path = work / "smplx.json"
                if native_path.exists():
                    package["smplx"] = json.loads(native_path.read_text())
                    metadata["has_smplx_parameters"] = True
                data = json.dumps(package).encode()
                read_native_motion(data)
                return [Output(SMPLX_MOTION, data, metadata)]
            assert isinstance(job.params, Params)
            target = work / "animated.fbx"
            transfer_log = run(
                [
                    self._blender,
                    "-b",
                    "--factory-startup",
                    "-P",
                    str(RETARGET_SCRIPT),
                    "--",
                    str(rig),
                    str(work / "motion.bvh"),
                    str(target),
                    "sides",
                    "auto",
                    "auto",
                    job.params.root_motion,
                    "smpl",
                ]
            )
            if os.environ.get("ASSETS_STUDIO_DEBUG_DIR"):
                (work / "retarget.log").write_text(transfer_log)
            if not target.is_file():
                raise GenerationError("Blender did not write the animated FBX")
            return [
                Output(
                    "model/x-fbx",
                    target.read_bytes(),
                    metadata,
                )
            ]
