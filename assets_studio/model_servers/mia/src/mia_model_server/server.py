"""Make-It-Animatable v2 rigging server (contract v1, task 3d-to-rig, ADR-006).

A character mesh (GLB) in, the same character with a Mixamo skeleton and
skin weights out (FBX), ready for Mixamo animations in Unity (Humanoid).
The model predicts joints, skin weights and the pose of the input; the
result can be put into the T-pose. Humanoids only: the skeleton is Mixamo's.

The steps are upstream's demo (app_v2.py): prepare_input → preprocess →
infer → vis on the model thread, then upstream's Blender export
(app_blender.py) in a child process: bpy must not modify data from a
non-main thread (upstream does the same in v1).

Weights: Make-It-Animatable (Apache-2.0) on the Hunyuan3D-2.1 VAE (Tencent
Hunyuan Community License: not valid in the EU, UK, South Korea); the
skeleton template is Mixamo's (gated dataset, automatic approval).
"""

import logging
import os
import struct
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any, Literal

import numpy as np
from pydantic import BaseModel, ConfigDict, Field

from mia_model_server import glb
from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    KnownTask,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
    ui,
)

logger = logging.getLogger("mia_model_server")

WEIGHTS_REPO = "jasongzy/Make-It-Animatable"
TEMPLATE_REPO = "jasongzy/Mixamo"  # a dataset
VAE_REPO = "tencent/Hunyuan3D-2.1"
# Git revisions, not secrets.
WEIGHTS_REVISION = "ca0daf6cb164f939e77bf32667513fc7558d5f98"  # pragma: allowlist secret
TEMPLATE_REVISION = "b1c7f4975ea3261d3d0aa2379f6e24754ccde9d8"  # pragma: allowlist secret
VAE_REVISION = "0b94677654c57bb9a6b6845cd7b704ccf551d327"  # pragma: allowlist secret
SOURCE_REVISION = "bbd8b158d88879c310ad130f9b25056935d221e9"  # pragma: allowlist secret

# Gradio components the demo's steps use as keys of their return values;
# the server ignores those values.
_UI_NAMES = (
    "state",
    "output_joints_coarse",
    "output_normed_input",
    "output_sample",
    "output_joints",
    "output_bw",
    "output_rest_vis",
    "output_rest_lbs",
    "output_anim_vis",
    "output_anim",
)
BLENDER_TIMEOUT_S = 600


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    rest_pose: Literal["t-pose", "input"] = Field(
        default="t-pose",
        description="Rest pose of the result: the T-pose (for Mixamo) or the pose of the input",
        json_schema_extra=ui(
            ru="Поза покоя: T-поза (для Mixamo) или поза исходной модели", primary=True
        ),
    )
    input_pose: Literal["auto", "t-pose", "a-pose"] = Field(
        default="auto",
        description="Pose of the input if known: those bones then keep it instead of a prediction",
        json_schema_extra=ui(ru="Поза исходной модели, если известна: кости сохранят её"),
    )
    fingers: bool = Field(
        default=True,
        description="Finger bones (off: the hands bend as a whole)",
        json_schema_extra=ui(ru="Кости пальцев (без них кисть гнётся целиком)"),
    )


TASK = TaskSpec(
    KnownTask.RIG_3D,
    Params,
    ("model/x-fbx",),
    prompt="none",
    inputs=(InputSpec(role="mesh", mime=["model/gltf-binary"]),),
    max_count=1,
)

_INPUT_POSES = {"auto": None, "t-pose": "T-pose", "a-pose": "A-pose"}


def source_directory() -> Path:
    return Path(os.environ.get("MIA_SOURCE", "~/.cache/assets-studio/mia-source")).expanduser()


def models_directory() -> Path:
    return Path(os.environ.get("HY3DGEN_MODELS", "~/.cache/assets-studio/hy3dgen")).expanduser()


def blender_command(params: Params, data: Path, output: Path, template: Path) -> list[str]:
    """Upstream's export CLI: the rig in ``data`` (npz) to an FBX, through
    blender_export.py (its textures named)."""
    command = [
        sys.executable,
        str(Path(__file__).with_name("blender_export.py")),
        "--input_path",
        str(data),
        "--output_path",
        str(output),
        "--template_path",
        str(template),
    ]
    if params.rest_pose == "t-pose":
        command.append("--reset_to_rest")
    if not params.fingers:
        command.append("--remove_fingers")
    return command


def describe(bones: int, params: Params) -> dict[str, Any]:
    return {
        "skeleton": "mixamo",
        "bones": bones,
        "fingers": params.fingers,
        "rest_pose": params.rest_pose,
    }


def download() -> None:
    """The weights, the skeleton template and the VAE, where upstream expects
    them: weights in output/, the template in data/Mixamo/, the VAE under
    HY3DGEN_MODELS. Network only; the server calls it on load."""
    from huggingface_hub import snapshot_download

    source = source_directory()
    snapshot_download(
        WEIGHTS_REPO,
        revision=WEIGHTS_REVISION,
        allow_patterns=["output/best/v2/*"],
        local_dir=source,
    )
    os.environ.setdefault("HY3DGEN_MODELS", str(models_directory()))
    snapshot_download(
        VAE_REPO,
        revision=VAE_REVISION,
        allow_patterns=["hunyuan3d-vae-v2-1/*"],
        local_dir=models_directory() / VAE_REPO,
    )
    # Last: a gated dataset, the step that needs the licence accepted.
    snapshot_download(
        TEMPLATE_REPO,
        repo_type="dataset",
        revision=TEMPLATE_REVISION,
        allow_patterns=["bones.fbx"],
        local_dir=source / "data/Mixamo",
    )


class MiaServer(ModelServer):
    model = ModelInfo(
        id="mia",
        name="Make-It-Animatable v2",
        revision=WEIGHTS_REVISION[:12],
        license=(
            "Apache-2.0; Hunyuan3D-2.1 VAE: Tencent Hunyuan Community License"
            " (not EU, UK, South Korea)"
        ),
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._app: Any = None

    def load(self) -> None:
        import torch

        source = source_directory()
        if (
            not (source / "app_v2.py").is_file()
            or not (source / "util/Hunyuan3D_21/hy3dshape").is_dir()
        ):
            raise FileNotFoundError(
                f"Make-It-Animatable source not found in {source}; run `just setup`"
            )
        if not torch.cuda.is_available():
            raise RuntimeError("Make-It-Animatable needs a CUDA GPU")
        download()
        sys.path.insert(0, str(source))
        import app_v2  # pyright: ignore[reportMissingImports]

        for name in _UI_NAMES:
            setattr(app_v2, name, object())
        app_v2.init_models()  # also changes into the source directory
        self._app = app_v2
        logger.info("Make-It-Animatable v2 ready")

    def generate(self, job: Job) -> list[Output]:
        import gradio as gr
        import torch

        assert isinstance(job.params, Params)
        params = job.params
        app = self._app
        with tempfile.TemporaryDirectory(prefix="mia-") as work:
            mesh_path = Path(work) / "input.glb"
            try:
                # trimesh (upstream's loader) reads only 8-bit vertex colours.
                mesh_path.write_bytes(glb.byte_colours(job.inputs["mesh"].data))
            except (ValueError, KeyError, struct.error) as error:
                raise InvalidInput(f"cannot read the GLB: {error}") from error
            db = app.DB()
            try:
                app.prepare_input(str(mesh_path), db=db)
            except gr.Error as error:
                raise InvalidInput(f"cannot read the mesh: {error.message}") from error
            try:
                app.preprocess(db)
                app.infer(True, db)
                app.vis(True, "LeftArm", not params.fingers, False, db)
            except gr.Error as error:
                raise GenerationError(str(error.message)) from error
            except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
                if not is_out_of_memory(error):
                    raise
                torch.cuda.empty_cache()
                raise GenerationError(
                    "out of GPU memory; free GPU memory", retryable=True
                ) from error
            data = Path(work) / "rig.npz"
            output = Path(work) / "rigged.fbx"
            bones = self._save_rig(db, params, data)
            self._export(params, data, output)
            return [
                Output("model/x-fbx", output.read_bytes(), describe(bones, params))
                for _ in job.seeds
            ]

    def _save_rig(self, db: Any, params: Params, path: Path) -> int:
        """The rig as upstream's vis_blender hands it to Blender."""
        app = self._app
        bones = dict(app.BONES_IDX_DICT)
        rig = {
            "mesh": db.mesh,
            "gs": None,
            "joints": db.joints,
            "joints_tail": db.joints_tail,
            "bw": db.bw,
            "pose": db.pose,
            "bones_idx_dict": bones,
            "pose_ignore_list": app.get_pose_ignore_list(_INPUT_POSES[params.input_pose], []),
        }
        np.savez(path, **rig)  # pyright: ignore[reportArgumentType]
        fingers = sum(
            1
            for name in bones
            if any(f in name for f in ("Thumb", "Index", "Middle", "Ring", "Pinky"))
        )
        return len(bones) - (0 if params.fingers else fingers)

    def _export(self, params: Params, data: Path, output: Path) -> None:
        source = source_directory()
        command = blender_command(params, data, output, source / "data/Mixamo/bones.fbx")
        try:
            done = subprocess.run(  # noqa: S603 -- fixed argv, paths are ours
                command,
                cwd=source,
                capture_output=True,
                text=True,
                timeout=BLENDER_TIMEOUT_S,
                check=False,
            )
        except subprocess.TimeoutExpired as error:
            raise GenerationError(f"the Blender export took over {BLENDER_TIMEOUT_S} s") from error
        if done.returncode != 0 or not output.is_file():
            tail = "\n".join((done.stderr or done.stdout).strip().splitlines()[-5:])
            logger.error("Blender export failed (%s):\n%s", done.returncode, done.stderr)
            raise GenerationError(f"the Blender export failed: {tail}")
