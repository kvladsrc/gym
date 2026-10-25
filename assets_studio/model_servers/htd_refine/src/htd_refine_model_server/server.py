"""HTD-Refine on synchronized native SMPL-X windows; no rigging or SDK extensions."""

import hashlib
import json
import math
import os
import shutil
import subprocess
from pathlib import Path
from typing import Any

import imageio_ffmpeg
from asset_processing.native_motion import read_native_motion
from asset_processing.workspace import job_workspace
from model_server_sdk.media import SMPLX_MOTION
from pydantic import BaseModel, ConfigDict

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
    is_out_of_memory,
)

REVISION = "2fcd6ddef3c4eb75a636062245f80a0136c09b7e"  # pragma: allowlist secret
WORKER = Path(__file__).resolve().parents[2] / "worker/refine.py"


def cache() -> Path:
    return Path(os.environ.get("XDG_CACHE_HOME", "~/.cache")).expanduser() / "assets-studio"


def source() -> Path:
    return Path(os.environ.get("HTD_REFINE_SOURCE", str(cache() / "htd-refine-source")))


def worker_python() -> Path:
    return Path(
        os.environ.get("HTD_REFINE_PYTHON", str(cache() / "venvs/htd-refine-worker/bin/python"))
    )


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")


def validate_package(data: bytes) -> dict[str, Any]:
    try:
        motion = read_native_motion(data)
        p = motion.source
        if not 4 <= motion.frames <= 300:
            raise ValueError("HTD needs 4–300 frames; refine raw windows before joining")
        if p.get("source", {}).get("camera") != "static":
            raise ValueError("Only raw static-camera captures are supported")
        raw = p.get("smplx", {})
        if raw.get("format") != "studio.smplx-parameters.v1" or raw.get("fps") != 30:
            raise ValueError("Full SMPL-X parameters required; generate a new GVHMR native capture")

        def tensor(value: Any, shape: tuple[int, ...]) -> None:
            if not shape:
                if type(value) not in (int, float) or not math.isfinite(value):
                    raise ValueError("Non-finite native parameter")
            else:
                if not isinstance(value, list) or len(value) != shape[0]:
                    raise ValueError("Native parameter dimensions disagree with frame count")
                for item in value:
                    tensor(item, shape[1:])

        for space in ("smpl_params_global", "smpl_params_incam"):
            for key, width in (
                ("global_orient", 3),
                ("body_pose", 63),
                ("betas", 10),
                ("transl", 3),
            ):
                tensor(raw[space][key], (motion.frames, width))
        tensor(raw["K_fullimg"], (motion.frames, 3, 3))
        tensor(raw["bbx_xyxy"], (motion.frames, 4))
        if any(b[2] <= b[0] or b[3] <= b[1] for b in raw["bbx_xyxy"]):
            raise ValueError("Invalid synchronized person box")
        return p
    except (KeyError, TypeError, ValueError) as error:
        raise InvalidInput(str(error)) from error


def run(command: list[str], *, cwd: Path, env: dict[str, str]) -> None:
    try:
        subprocess.run(  # noqa: S603 -- fixed local worker commands, no shell
            command, cwd=cwd, env=env, capture_output=True, text=True, timeout=1800, check=True
        )
    except subprocess.CalledProcessError as error:
        message = explain_child_failure(
            (error.stdout or "") + (error.stderr or ""), error.returncode
        )
        raise GenerationError(message, retryable=is_out_of_memory(RuntimeError(message))) from error
    except subprocess.TimeoutExpired as error:
        raise GenerationError("HTD stage exceeded 1800 seconds") from error


class HTDRefineServer(ModelServer):
    model = ModelInfo(
        id="htd-refine",
        name="HTD-Refine",
        revision=REVISION[:12] + "+studio.1",
        license="AGPL-3.0",
        source="https://github.com/ant-research/HTD-Refine",
    )
    tasks = (
        TaskSpec(
            "refine-native-motion",
            Params,
            (SMPLX_MOTION,),
            prompt="none",
            max_count=1,
            inputs=(
                InputSpec(
                    role="motion",
                    mime=[SMPLX_MOTION],
                    required=True,
                    description=("Raw GVHMR with full SMPL-X; before joining or retargeting"),
                    labels={"ru": "Исходный GVHMR с полным SMPL-X, до соединения окон и переноса"},
                ),
                InputSpec(
                    role="video",
                    mime=["video/mp4"],
                    required=True,
                    description="Exact source clip, static camera, 4–300 frames at 30 fps",
                    labels={"ru": "То же исходное видео: статичная камера, 4–300 кадров, 30 fps"},
                ),
            ),
        ),
    )

    def load(self) -> None:
        for path in (
            worker_python(),
            source() / "scripts/demo_optimization.py",
            source() / "inputs/checkpoints/pvanet.pt",
            source() / "inputs/checkpoints/body_models/smplx/SMPLX_NEUTRAL.npz",
        ):
            if not path.is_file():
                raise FileNotFoundError(f"Missing HTD dependency: {path}; see htd_refine/README.md")
        revision = subprocess.check_output(  # noqa: S603 -- local checkout identity
            [shutil.which("git") or "/usr/bin/git", "-C", str(source()), "rev-parse", "HEAD"],
            text=True,
        ).strip()
        if revision != REVISION:
            raise RuntimeError(f"HTD source revision mismatch: expected {REVISION}, got {revision}")

    def generate(self, job: Job) -> list[Output]:
        package = validate_package(job.inputs["motion"].data)
        video = job.inputs["video"].data
        expected = package["source"].get("video_sha256")
        fingerprint = hashlib.sha256(video).hexdigest()
        if expected is not None and expected != fingerprint:
            raise InvalidInput(
                "Video differs from the native capture's source; use the exact source asset"
            )
        with job_workspace("htd-refine-") as work:
            (work / "source.mp4").write_bytes(video)
            reader = imageio_ffmpeg.read_frames(str(work / "source.mp4"))
            try:
                info = next(reader)
                frames = sum(1 for _ in reader)
            except (RuntimeError, StopIteration) as error:
                raise InvalidInput(f"Cannot decode source video: {error}") from error
            finally:
                reader.close()
            if frames != package["frames"] or abs(info["fps"] - 30) > 0.01:
                raise InvalidInput("Source video must match native frames exactly at 30 fps")
            (work / "input.json").write_text(json.dumps(package))
            env = dict(os.environ, PYTHONPATH=str(source()))
            common = [str(worker_python()), str(WORKER)]
            run([*common, "prepare", str(work)], cwd=source(), env=env)
            run([*common, "pva", str(work)], cwd=source(), env=env)
            run(
                [
                    str(worker_python()),
                    str(source() / "scripts/demo_optimization.py"),
                    "--video",
                    str(work / "source.mp4"),
                    "--motion",
                    str(work / "prediction.pt"),
                    "--pva_result",
                    str(work / "pva.pt"),
                    "--camera_intrinsics",
                    str(work / "K.pt"),
                    "--static_cam",
                    "--source",
                    "gvhmr",
                    "--run_optimization",
                    "--output_root",
                    str(work / "optimization"),
                ],
                cwd=source(),
                env=env,
            )
            run([*common, "export", str(work)], cwd=source(), env=env)
            data = (work / "output.json").read_bytes()
            output = read_native_motion(data)
            if output.frames != frames:
                raise GenerationError("HTD output changed the source frame clock")
            meta = json.loads((work / "report.json").read_text())
            meta.update(
                frames=frames, fps=30, camera="static", video_identity_verified=expected is not None
            )
            return [Output(SMPLX_MOTION, data, meta)]
