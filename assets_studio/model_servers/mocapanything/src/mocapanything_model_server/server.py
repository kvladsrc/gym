"""MoCapAnything V2 motion capture server (contract v1, task video-to-motion).

A video of an animal and a rig of that kind of animal (FBX or GLB with a
skeleton, e.g. from SkinTokens) in; the rig moving as the animal in the
video does out (FBX). MoCapAnything V2 (Gong et al., 2026) predicts the
joints of an arbitrary skeleton from a monocular video end to end, video to
positions to rotations, conditioned on a reference frame of the rig (DINOv2
features, joint names through T5). The code is a clean reimplementation by
phongdaot, not the authors' research code; the weights are the authors'.

Each job runs upstream's custom-rig pipeline in child processes and puts
the predicted BVH on the input rig (pipeline.py says what was needed for
rigs that are not Truebones Zoo ones). The frames' background is removed by
upstream's RMBG-1.4. Rigs whose bones say nothing (bone_N: SkinTokens,
UniRig) are read as quadrupeds; others (Mixamo's, from Make-It-Animatable)
keep their names, which upstream cleans for T5.

Licences: MIT (code and V2 weights); RMBG-1.4: BRIA's own licence
(non-commercial); DINOv2 and T5: Apache-2.0.
"""

import logging
import os
import shutil
from pathlib import Path
from typing import Literal

from asset_processing.workspace import job_workspace
from pydantic import BaseModel, ConfigDict, Field

from mocapanything_model_server.naming import NamingError
from mocapanything_model_server.pipeline import (
    FPS,
    NO_ARMATURE,
    Job,
    Models,
    StageError,
    frame_count,
)
from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    ui,
)
from model_server_sdk import Job as SdkJob
from model_server_sdk import explain_child_failure as explain

logger = logging.getLogger("mocapanything_model_server")

# Git revisions, not secrets.
SOURCE_REVISION = "c99fb22c2f6001958c73c197065322b98ca0b93b"  # pragma: allowlist secret
WEIGHTS_REPO = "kehong/MoCapAnythingV2-weights"
WEIGHTS_REVISION = "4b8c28e69aea9e39610264818d83153d59219bd6"  # pragma: allowlist secret
WEIGHTS_FILE = "video2pos2rot_epoch60.pt"
# Upstream fetches these at runtime; pinned here.
RMBG_REPO = "briaai/RMBG-1.4"
RMBG_REVISION = "2ceba5a5efaec153162aedea169f76caf9b46cf8"  # pragma: allowlist secret
DINO_REPO = "facebook/dinov2-large"
DINO_REVISION = "47b73eefe95e8d44ec3623f8890bd894b6ea2d6c"  # pragma: allowlist secret
T5_REPO = "google-t5/t5-base"
T5_REVISION = "a9723ea7f1b39c1eae772870f3b547bf6ef7e6c1"  # pragma: allowlist secret
# Only the PyTorch weights: the repositories also hold ONNX, TF and Flax ones.
PYTORCH_FILES = ["*.json", "*.safetensors", "*.model"]

# Longer clips are refused rather than cut. A 6 s clip peaked at 7.6 GB of
# the laptop's 8 (upstream encodes the frames with DINOv2 in batches of 512).
MAX_SECONDS = 10
JOB_TIMEOUT_S = 3600


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    background_removed: bool = Field(
        default=False,
        description="The video is already matted onto white (BiRefNet); skip a second cut-out",
        json_schema_extra=ui(ru="Фон уже удалён на белый (BiRefNet): не вырезать повторно"),
    )
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
            description=f"The animal moving, one continuous shot, at most {MAX_SECONDS} s",
            labels={"ru": f"Животное в движении, один непрерывный план, не дольше {MAX_SECONDS} с"},
        ),
        InputSpec(
            role="rig",
            mime=["model/x-fbx", "model/gltf-binary"],
            description="A rigged character: a quadruped (SkinTokens, UniRig) or one with "
            "named Left/Right bones (Make-It-Animatable)",
            labels={
                "ru": "Персонаж со скелетом: четвероногое (SkinTokens, UniRig) или с костями "
                "Left/Right (Make-It-Animatable)"
            },
        ),
    ),
    max_count=1,
)


def source_directory() -> Path:
    return Path(
        os.environ.get("MOCAPANYTHING_SOURCE", "~/.cache/assets-studio/mocapanything-source")
    ).expanduser()


def blender() -> str | None:
    return os.environ.get("BLENDER") or shutil.which("blender")


def download() -> Models:
    """The weights, the encoders and the demo data (network only on the first call)."""
    from huggingface_hub import hf_hub_download, snapshot_download

    checkpoint = Path(hf_hub_download(WEIGHTS_REPO, WEIGHTS_FILE, revision=WEIGHTS_REVISION))
    return Models(
        weights=checkpoint.parent,
        rmbg=Path(
            snapshot_download(RMBG_REPO, revision=RMBG_REVISION, allow_patterns=PYTORCH_FILES)
        ),
        dinov2=Path(
            snapshot_download(DINO_REPO, revision=DINO_REVISION, allow_patterns=PYTORCH_FILES)
        ),
        t5=Path(snapshot_download(T5_REPO, revision=T5_REVISION, allow_patterns=PYTORCH_FILES)),
        checkpoint=WEIGHTS_FILE,
    )


def failure(error: StageError) -> GenerationError:
    tail = explain(error.output, error.returncode)
    if "out of memory" in error.output or "OutOfMemoryError" in error.output:
        return GenerationError(f"out of GPU memory in {error.stage}: {tail}", retryable=True)
    return GenerationError(f"{error.stage} failed: {tail}")


class MoCapAnythingServer(ModelServer):
    model = ModelInfo(
        id="mocapanything",
        name="MoCapAnything V2",
        revision=WEIGHTS_REVISION[:12],
        license="MIT; RMBG-1.4: BRIA licence (non-commercial)",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._models: Models | None = None
        self._blender: str | None = None

    def load(self) -> None:
        source = source_directory()
        if not (source / "inference/video2pose2rot.py").is_file():
            raise FileNotFoundError(f"MoCapAnything source not found in {source}; run `just setup`")
        self._blender = blender()
        if self._blender is None:
            raise FileNotFoundError("Blender not found: put it on PATH or set BLENDER")
        self._models = download()
        logger.info("MoCapAnything ready (the models load per job)")

    def generate(self, job: SdkJob) -> list[Output]:
        assert isinstance(job.params, Params)
        assert self._models is not None
        assert self._blender is not None
        rig_input = job.inputs["rig"]
        suffix = ".fbx" if rig_input.mime == "model/x-fbx" else ".glb"
        with job_workspace("mocapanything-") as work:
            rig = work / f"input{suffix}"
            rig.write_bytes(rig_input.data)
            video = work / "input.mp4"
            video.write_bytes(job.inputs["video"].data)
            run = Job(work, source_directory(), self._blender, self._models, JOB_TIMEOUT_S)
            try:
                reading = run.prepare(rig, video)
                frames = frame_count(work / "videos" / "Rig#clip.mp4")
                if frames > MAX_SECONDS * FPS:
                    raise InvalidInput(
                        f"the video is {frames / FPS:.1f} s; at most {MAX_SECONDS} s"
                    )
                run.dataset()
                motion = run.infer(background_removed=job.params.background_removed)
                agreement = run.consistency()
                animated, summary = run.apply(rig, motion, reading.facing, job.params.root_motion)
            except NamingError as error:
                raise InvalidInput(
                    "cannot read the rig: bones named bone_N must make a quadruped, "
                    f"others need Left…/Right… names ({error})"
                ) from error
            except StageError as error:
                invalid = next(
                    (
                        line.removeprefix("RIG_INVALID ")
                        for line in error.output.splitlines()
                        if line.startswith("RIG_INVALID ")
                    ),
                    None,
                )
                if invalid:
                    raise InvalidInput(invalid) from error
                if error.stage == "prepare_rig" and NO_ARMATURE in error.output:
                    raise InvalidInput(
                        "the rig input has no skeleton; rig it first (3D → rig)"
                    ) from error
                logger.error("%s failed:\n%s", error.stage, "\n".join(run.log)[-20000:])
                raise failure(error) from error
            finally:
                if os.environ.get("ASSETS_STUDIO_DEBUG_DIR"):
                    (work / "pipeline.log").write_text("\n".join(run.log))
            meta = {
                "frames": int(summary["frames"]),
                "fps": FPS,
                "bones": summary["bones"],
                "root_motion": job.params.root_motion,
                "background_removed": job.params.background_removed,
                "worst_joint_m": float(summary["worst_joint_m"]),
                "largest_vertical_travel_m": float(summary["largest_vertical_travel_m"]),
                # The model's positions against its rotations, as a share of the
                # body: 0.04 on upstream's Truebones horse (consistency.py).
                "consistency": agreement,
                # Ours for bone_N rigs; else the rig's own, cleaned upstream.
                "joint_names": reading.names or "the rig's own",
                "facing": list(reading.facing),
            }
            return [Output("model/x-fbx", animated.read_bytes(), meta) for _ in job.seeds]
