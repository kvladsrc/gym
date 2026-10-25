"""SkinTokens rigging server (contract v1, task 3d-to-rig, ADR-006).

A mesh (GLB) in, the same mesh with a skeleton and skin weights out (FBX).
SkinTokens (VAST and Tsinghua, 2026; UniRig's successor) writes the
skeleton and the skin as one token sequence with a 0.6B Qwen3 transformer:
the skeleton follows the shape (a quadruped, a bird, a tree, a chest), so
it is not Mixamo's and Mixamo animations do not fit it. For humanoids that
need them use Make-It-Animatable (server ``mia``).

Each job runs in a child process (rig.py) in the source directory: upstream
reads and writes meshes with bpy, which must run on a main thread, and its
paths are relative. Upstream's demo talks to bpy through a helper server
that listens on every interface and unpickles what it receives; it is not
used. The rig is predicted on the mesh, transferred onto the input (its
texture kept), and converted to FBX (asset_processing.blender).

Weights: MIT. The model loads per job (~1.6 GB, seconds).
"""

import json
import logging
import os
import subprocess
import sys
import tempfile
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

logger = logging.getLogger("skintokens_model_server")

WEIGHTS_REPO = "VAST-AI/SkinTokens"
LLM_REPO = "Qwen/Qwen3-0.6B"  # only its config: the weights are SkinTokens'
# Git revisions, not secrets.
WEIGHTS_REVISION = "79736cad0fd84de384d5eede659b4ebd24effe33"  # pragma: allowlist secret
LLM_REVISION = "c1899de289a04d12100db370d81485cdf75e47ca"  # pragma: allowlist secret
SOURCE_REVISION = "273b691d35989d71cd17ff2895fdc735097b92d1"  # pragma: allowlist secret

CHECKPOINT = "experiments/articulation_xl_quantization_256_token_4/grpo_1400.ckpt"
VAE_CHECKPOINT = "experiments/skin_vae_2_10_32768/last.ckpt"
LLM_DIRECTORY = "models/Qwen3-0.6B"
JOB_TIMEOUT_S = 1800
# rig.py's last line of output: the result's description.
RESULT_PREFIX = "RESULT "


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    num_beams: int = Field(
        default=10,
        ge=1,
        le=20,
        description="Beam search width: more beams, better rigs, more GPU memory",
        json_schema_extra=ui(
            ru="Ширина поиска лучом: больше — лучше скелет, но больше видеопамяти",
            primary=True,
        ),
    )
    temperature: float = Field(
        default=1.0,
        ge=0.1,
        le=2.0,
        description="Sampling temperature: higher, more varied skeletons",
        json_schema_extra=ui(ru="Температура: выше — разнообразнее скелеты"),
    )
    smooth_skin: bool = Field(
        default=False,
        description="Voxel post-processing of the skin weights (upstream's use_postprocess)",
        json_schema_extra=ui(ru="Сгладить веса кожи по вокселям (use_postprocess)"),
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
        os.environ.get("SKINTOKENS_SOURCE", "~/.cache/assets-studio/skintokens-source")
    ).expanduser()


def download() -> None:
    """Checkpoints and the Qwen3 config where upstream looks for them, in the
    source directory. Network only; the server calls it on load."""
    from huggingface_hub import hf_hub_download, snapshot_download

    source = source_directory()
    for name in (CHECKPOINT, VAE_CHECKPOINT):
        hf_hub_download(WEIGHTS_REPO, name, revision=WEIGHTS_REVISION, local_dir=source)
    snapshot_download(
        LLM_REPO,
        revision=LLM_REVISION,
        local_dir=source / LLM_DIRECTORY,
        ignore_patterns=["*.bin", "*.safetensors"],
    )


def rig_command(work: Path, params: Params, seed: int) -> list[str]:
    return [
        sys.executable,
        "-m",
        "skintokens_model_server.rig",
        str(work / "input.glb"),
        str(work / "rigged.fbx"),
        params.model_dump_json(),
        str(seed),
    ]


def failure(output: str, returncode: int = 1) -> GenerationError:
    """The child's failure as the job's error: what it raised, or the signal
    that killed it (a crash in native code leaves no traceback)."""
    tail = explain(output, returncode)
    if "out of memory" in output or "OutOfMemoryError" in output:
        return GenerationError(
            f"out of GPU memory; free GPU memory or lower num_beams: {tail}", retryable=True
        )
    return GenerationError(f"rigging failed: {tail}")


def result_of(stdout: str) -> dict[str, Any]:
    for line in reversed(stdout.splitlines()):
        if line.startswith(RESULT_PREFIX):
            return json.loads(line[len(RESULT_PREFIX) :])
    raise GenerationError("the rigging process gave no result")


class SkinTokensServer(ModelServer):
    model = ModelInfo(
        id="skintokens",
        name="SkinTokens",
        revision=WEIGHTS_REVISION[:12],
        license="MIT",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
    )
    tasks = (TASK,)

    def load(self) -> None:
        source = source_directory()
        if not (source / "src/model/tokenrig.py").is_file():
            raise FileNotFoundError(f"SkinTokens source not found in {source}; run `just setup`")
        download()
        logger.info("SkinTokens ready (the model loads per job)")

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        return [self._one(job.inputs["mesh"].data, job.params, seed) for seed in job.seeds]

    def _one(self, mesh: bytes, params: Params, seed: int) -> Output:
        with tempfile.TemporaryDirectory(prefix="skintokens-") as directory:
            work = Path(directory)
            (work / "input.glb").write_bytes(mesh)
            try:
                done = subprocess.run(  # noqa: S603 -- fixed argv, paths are ours
                    rig_command(work, params, seed),
                    cwd=source_directory(),
                    capture_output=True,
                    text=True,
                    timeout=JOB_TIMEOUT_S,
                    check=False,
                )
            except subprocess.TimeoutExpired as error:
                raise GenerationError(f"rigging took over {JOB_TIMEOUT_S} s") from error
            output = work / "rigged.fbx"
            if done.returncode != 0 or not output.is_file():
                logger.error("rigging failed (%s):\n%s", done.returncode, done.stderr)
                raise failure(done.stderr or done.stdout, done.returncode)
            meta = {**result_of(done.stdout), "skeleton": "skintokens"}
            return Output("model/x-fbx", output.read_bytes(), meta)
