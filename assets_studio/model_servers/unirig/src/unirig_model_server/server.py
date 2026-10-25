"""UniRig rigging server (contract v1, task 3d-to-rig, ADR-006).

A mesh (GLB) in, the same mesh with a skeleton and skin weights out (FBX).
UniRig (VAST and Tsinghua, SIGGRAPH 2025) predicts the skeleton with an
autoregressive transformer (OPT-350m, 15 beams) and then the skin weights
with a second model: the skeleton follows the shape (a quadruped, a bird, a
tree), so it is not Mixamo's and Mixamo animations do not fit it. For
humanoids that need them use Make-It-Animatable (server ``mia``); UniRig's
successor SkinTokens is the server ``skintokens``.

A job is upstream's command-line chain, each step a child process in the
source directory (upstream's paths are relative, and its bpy steps need a
main thread): extract the mesh → skeleton → extract again → skin → merge
the rig onto the input (its texture kept) → FBX (asset_processing.blender).
Upstream asks the Hub for its checkpoints on every run, without a revision;
the children run offline, so it falls back to the pinned copies in its
experiments/ directory.

Weights: MIT (trained on Articulation-XL 2.0). Each step loads its model
(~1.4 GB skeleton, ~4.4 GB skin).
"""

import logging
import os
import re
import subprocess
import sys
import tempfile
import time
from pathlib import Path
from typing import Any

from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
    InputSpec,
    Job,
    KnownTask,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    ui,
)
from model_server_sdk import (
    explain_child_failure as explain,
)

logger = logging.getLogger("unirig_model_server")

WEIGHTS_REPO = "VAST-AI/UniRig"
LLM_REPO = "facebook/opt-350m"  # only its config: the weights are UniRig's
# Git revisions, not secrets.
WEIGHTS_REVISION = "36842e2b5947e9e60f89275b83208c8e74071c63"  # pragma: allowlist secret
LLM_REVISION = "08ab08cc4b72ff5593870b5d527cf4230323703c"  # pragma: allowlist secret
SOURCE_REVISION = "6793c6640ff01c8fb389f3993434124bb43d2933"  # pragma: allowlist secret

CHECKPOINTS = (
    "skeleton/articulation-xl_quantization_256/model.ckpt",
    "skin/articulation-xl/model.ckpt",
)
SKELETON_TASK = "configs/task/quick_inference_skeleton_articulationxl_ar_256.yaml"
SKIN_TASK = "configs/task/quick_inference_unirig_skin.yaml"
STEP_TIMEOUT_S = 1200


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    faces: int = Field(
        default=50000,
        ge=5000,
        le=200000,
        description="Triangles of the copy the rig is predicted on (the result keeps the input's)",
        json_schema_extra=ui(
            ru="Треугольников в копии, по которой строится скелет (результат — исходная сетка)",
            primary=True,
        ),
    )


TASK = TaskSpec(
    KnownTask.RIG_3D,
    Params,
    ("model/x-fbx",),
    prompt="none",
    inputs=(InputSpec(role="mesh", mime=["model/gltf-binary"]),),
    max_count=2,
)


def source_directory() -> Path:
    return Path(
        os.environ.get("UNIRIG_SOURCE", "~/.cache/assets-studio/unirig-source")
    ).expanduser()


def download() -> None:
    """The checkpoints where upstream falls back to them (experiments/) and
    OPT-350m's config in the Hub cache, where transformers looks offline.
    Network only; the server calls it on load."""
    from huggingface_hub import hf_hub_download

    for name in CHECKPOINTS:
        hf_hub_download(
            WEIGHTS_REPO,
            name,
            revision=WEIGHTS_REVISION,
            local_dir=source_directory() / "experiments",
        )
    # By the branch name: offline, transformers finds files through refs/main.
    hf_hub_download(LLM_REPO, "config.json", revision="main")


def steps(work: Path, params: Params, seed: int) -> list[tuple[str, list[str]]]:
    """Upstream's launch/inference scripts, unrolled (they pip install)."""
    python = sys.executable

    def extract(source: str, into: str) -> list[str]:
        return [
            python,
            "-m",
            "src.data.extract",
            "--config=configs/data/quick_inference.yaml",
            f"--require_suffix={Path(source).suffix[1:]}",
            "--force_override=true",
            "--num_runs=1",
            "--id=0",
            "--time=job",
            f"--faces_target_count={params.faces}",
            f"--input={work / source}",
            f"--output_dir={work / into}",
        ]

    return [
        ("extract", extract("input.glb", "mesh")),
        (
            "skeleton",
            [
                python,
                "run.py",
                f"--task={SKELETON_TASK}",
                f"--seed={seed}",
                f"--input={work / 'input.glb'}",
                f"--output={work / 'skeleton.fbx'}",
                f"--npz_dir={work / 'mesh'}",
            ],
        ),
        ("extract skeleton", extract("skeleton.fbx", "skeleton")),
        (
            "skin",
            [
                python,
                "run.py",
                f"--task={SKIN_TASK}",
                f"--seed={seed}",
                f"--input={work / 'skeleton.fbx'}",
                f"--output={work / 'skin.fbx'}",
                f"--npz_dir={work / 'skeleton'}",
                "--data_name=raw_data.npz",
            ],
        ),
        (
            "merge",
            [
                python,
                "-m",
                "src.inference.merge",
                "--require_suffix=obj,fbx,FBX,dae,glb,gltf,vrm",
                "--num_runs=1",
                "--id=0",
                f"--source={work / 'skin.fbx'}",
                f"--target={work / 'input.glb'}",
                f"--output={work / 'rigged.glb'}",
            ],
        ),
        (
            "fbx",
            [
                python,
                "-m",
                "asset_processing.blender",
                str(work / "rigged.glb"),
                str(work / "rigged.fbx"),
            ],
        ),
    ]


def failure(step: str, output: str, returncode: int = 1) -> GenerationError:
    """A step's failure as the job's error: what it raised, or the signal
    that killed it (a crash in native code leaves no traceback)."""
    tail = explain(output, returncode)
    if "out of memory" in output or "OutOfMemoryError" in output:
        return GenerationError(f"{step}: out of GPU memory: {tail}", retryable=True)
    return GenerationError(f"{step} failed: {tail}")


def counts_of(stdout: str) -> dict[str, int]:
    """The bones and triangles asset_processing.blender printed."""
    found = re.search(r"\{'bones': (\d+), 'triangles': (\d+)\}", stdout)
    return {"bones": int(found[1]), "triangles": int(found[2])} if found else {}


class UniRigServer(ModelServer):
    model = ModelInfo(
        id="unirig",
        name="UniRig",
        revision=WEIGHTS_REVISION[:12],
        license="MIT",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
    )
    tasks = (TASK,)

    def load(self) -> None:
        source = source_directory()
        if not (source / "run.py").is_file():
            raise FileNotFoundError(f"UniRig source not found in {source}; run `just setup`")
        download()
        logger.info("UniRig ready (the models load per step)")

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        return [self._one(job.inputs["mesh"].data, job.params, seed) for seed in job.seeds]

    def _one(self, mesh: bytes, params: Params, seed: int) -> Output:
        env = {
            **os.environ,
            "HF_HUB_OFFLINE": "1",
            # Upstream's Lightning checkpoints predate torch.load's weights_only
            # default (PyTorch 2.6) and need full unpickling: acceptable for
            # these, pinned from VAST's repository, and only for them.
            "TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD": "1",
            # pyrender (the skin step's depth maps) without a display.
            "PYOPENGL_PLATFORM": "egl",
        }
        timings: dict[str, float] = {}
        with tempfile.TemporaryDirectory(prefix="unirig-") as directory:
            work = Path(directory)
            (work / "input.glb").write_bytes(mesh)
            stdout = ""
            for name, command in steps(work, params, seed):
                started = time.monotonic()
                try:
                    done = subprocess.run(  # noqa: S603 -- fixed argv, paths are ours
                        command,
                        cwd=source_directory(),
                        env=env,
                        capture_output=True,
                        text=True,
                        timeout=STEP_TIMEOUT_S,
                        check=False,
                    )
                except subprocess.TimeoutExpired as error:
                    raise GenerationError(f"{name} took over {STEP_TIMEOUT_S} s") from error
                timings[name] = round(time.monotonic() - started, 1)
                if done.returncode != 0:
                    logger.error("%s failed (%s):\n%s", name, done.returncode, done.stderr)
                    raise failure(name, done.stderr or done.stdout, done.returncode)
                stdout = done.stdout
            output = work / "rigged.fbx"
            if not output.is_file():
                raise GenerationError("the rig was not written")
            meta: dict[str, Any] = {**counts_of(stdout), "skeleton": "unirig", "steps_s": timings}
            return Output("model/x-fbx", output.read_bytes(), meta)
