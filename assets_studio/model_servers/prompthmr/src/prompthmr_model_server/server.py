"""Original RGB + selected-track observations -> preserved native motion."""

import hashlib
import json
import math
import os
import subprocess
import tempfile
from pathlib import Path
from typing import Any, Literal

from asset_processing.native_motion import read_native_motion
from model_server_sdk.media import SMPLX_MOTION
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

REVISION = "3b566b7dbb28ce506c7ea972c18693f4c705ce8c"  # pragma: allowlist secret
WORKER = Path(__file__).resolve().parents[2] / "worker/prompthmr_worker.py"
CHECKPOINTS = (
    "data/pretrain/phmr/checkpoint.ckpt",
    "data/pretrain/phmr/config.yaml",
    "data/pretrain/phmr_vid/phmr_b1b2.ckpt",
    "data/pretrain/phmr_vid/prhmr_release_002.yaml",
    "data/body_models/smplx/SMPLX_NEUTRAL.npz",
)


def source_directory() -> Path:
    cache = Path(os.environ.get("XDG_CACHE_HOME", Path.home() / ".cache")) / "assets-studio"
    return Path(os.environ.get("PROMPTHMR_SOURCE", str(cache / "prompthmr-source"))).expanduser()


def worker_python() -> Path:
    return Path(
        os.environ.get(
            "PROMPTHMR_PYTHON", str(source_directory().parent / "venvs/prompthmr-worker/bin/python")
        )
    )


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")
    camera: Literal["static"] = Field(
        default="static",
        description="Pilot supports an explicitly assumed static camera only",
        json_schema_extra=ui(ru="Пилот: только неподвижная камера"),
    )


TASK = TaskSpec(
    "video-to-native-motion",
    Params,
    (SMPLX_MOTION,),
    prompt="none",
    inputs=(
        InputSpec(
            role="video", mime=["video/mp4"], description="Original RGB, 30 fps, 2–300 frames"
        ),
        InputSpec(
            role="observations",
            mime=["text/plain"],
            description="studio.pose-observations.v1: track, K, COCO17 keypoints and source hash",
        ),
    ),
    max_count=1,
)


def observations(data: bytes, video: bytes) -> dict[str, Any]:
    try:
        raw = json.loads(data)
        if raw["format"] != "studio.pose-observations.v1" or raw["fps"] != 30:
            raise ValueError("unsupported observations format/fps")
        frames = raw["frames"]
        if type(frames) is not int or not 2 <= frames <= 300:
            raise ValueError("observations need 2–300 frames")
        if raw["source_sha256"] != hashlib.sha256(video).hexdigest():
            raise ValueError("observations belong to a different video")
        if len(raw["size"]) != 2 or not all(0 < d <= 1920 for d in raw["size"]):
            raise ValueError("unsupported image dimensions")
        for field, rows, cols in (("K_fullimg", 3, 3), ("kp2d", 17, 3)):
            values = raw[field]
            if len(values) != frames or any(
                len(f) != rows or any(len(r) != cols for r in f) for f in values
            ):
                raise ValueError(f"invalid {field} shape")
            if not all(math.isfinite(v) for f in values for r in f for v in r):
                raise ValueError(f"nonfinite {field}")
        if len(raw["bbx_xyxy"]) != frames or any(
            len(b) != 4 or not all(math.isfinite(v) for v in b) or b[2] <= b[0] or b[3] <= b[1]
            for b in raw["bbx_xyxy"]
        ):
            raise ValueError("invalid bounding boxes")
        return raw
    except (ValueError, TypeError, KeyError, UnicodeError) as error:
        raise InvalidInput(f"invalid observations: {error}") from error


class PromptHMRServer(ModelServer):
    model = ModelInfo(
        id="prompthmr",
        name="PromptHMR-video (BEDLAM1+2, native pilot)",
        revision=REVISION[:12] + "+native.1",
        license="PromptHMR: non-commercial scientific research; SMPL-X terms",
        source="https://github.com/yufu-wang/PromptHMR",
    )
    tasks = (TASK,)

    def load(self) -> None:
        missing = [
            str(source_directory() / name)
            for name in CHECKPOINTS
            if not (source_directory() / name).is_file()
        ]
        if not worker_python().is_file():
            missing.append(str(worker_python()))
        if missing:
            raise FileNotFoundError("Run prompthmr setup/download; missing: " + ", ".join(missing))

    def generate(self, job: Job) -> list[Output]:
        video = job.inputs["video"].data
        obs = observations(job.inputs["observations"].data, video)
        with tempfile.TemporaryDirectory(prefix="prompthmr-") as directory:
            work = Path(directory)
            (work / "video.mp4").write_bytes(video)
            (work / "observations.json").write_text(json.dumps(obs))
            target = work / "motion.json"
            try:
                result = subprocess.run(  # noqa: S603 -- fixed worker argv, no shell
                    [
                        str(worker_python()),
                        str(WORKER),
                        "--source",
                        str(source_directory()),
                        "--video",
                        str(work / "video.mp4"),
                        "--observations",
                        str(work / "observations.json"),
                        "--output",
                        str(target),
                    ],
                    cwd=source_directory(),
                    capture_output=True,
                    text=True,
                    timeout=1800,
                    check=False,
                )
            except subprocess.TimeoutExpired as error:
                raise GenerationError("PromptHMR-video exceeded 1800 seconds") from error
            if result.returncode:
                raise GenerationError(
                    explain_child_failure(result.stdout + result.stderr, result.returncode)
                )
            if not target.exists():
                raise GenerationError("PromptHMR did not write native motion")
            data = target.read_bytes()
            native = read_native_motion(data)
            return [
                Output(
                    SMPLX_MOTION,
                    data,
                    {
                        "frames": native.frames,
                        "fps": 30,
                        "native_motion": True,
                        "space": "model-world-y-up",
                        "camera": "static-assumption",
                        "timing": native.source.get("timing", {}),
                    },
                )
            ]
