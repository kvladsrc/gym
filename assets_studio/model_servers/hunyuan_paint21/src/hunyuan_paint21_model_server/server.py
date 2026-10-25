"""Hunyuan3D-Paint 2.1 server (contract v1, task 3d-paint).

A mesh (GLB) and the image it was made from in, the mesh with PBR materials
out (GLB: base colour, metallic and roughness textures). Hunyuan3D-Paint 2.1
(June 2025) draws the object's views as albedo and metal-roughness pairs
with a multiview diffusion model conditioned on the mesh's normals and
positions and on the image (DINOv2-giant features), upscales them
(RealESRGAN x4) and bakes them into UV textures; unseen parts are inpainted.
Against Paint 2.0 (server `hunyuan_paint`): PBR instead of colour only, and
better scores on its authors' benchmarks.

Officially ~21 GB of VRAM. Here the diffusion pipeline sits on the GPU in
fp16 (~3.5 GB), DINOv2-giant (2.2 GB) waits in RAM and visits the GPU for
its single call, the VAE decodes view by view; nothing quantised.

Each job runs in a child process (paint.py) in the source directory:
upstream's paths are relative, its OBJ to GLB export uses bpy (a main
thread, and a crash at exit), and RAM is given back after every job.

Licence: Tencent Hunyuan 3D 2.1 Community License (not valid in the EU, the
UK and South Korea); DINOv2, Apache-2.0; RealESRGAN, BSD-3-Clause.
"""

import json
import logging
import os
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any, Literal

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

logger = logging.getLogger("hunyuan_paint21_model_server")

WEIGHTS_REPO = "tencent/Hunyuan3D-2.1"
PAINT = "hunyuan3d-paintpbr-v2-1"
DINO_REPO = "facebook/dinov2-giant"
REALESRGAN_URL = (
    "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth"
)
REALESRGAN_SIZE = 67040989
# Git revisions, not secrets.
WEIGHTS_REVISION = "0b94677654c57bb9a6b6845cd7b704ccf551d327"  # pragma: allowlist secret
DINO_REVISION = "611a9d42f2335e0f921f1e313ad3c1b7178d206d"  # pragma: allowlist secret
SOURCE_REVISION = "82920d643c0dc2f7bfd7255f45f62d386edfe60c"  # pragma: allowlist secret

JOB_TIMEOUT_S = 3600
RESULT_PREFIX = "RESULT "


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    views: int = Field(
        default=6,
        ge=6,
        le=9,
        description="Views drawn and baked (more: fewer inpainted gaps, more memory and time)",
        json_schema_extra=ui(
            ru="Сколько видов рисовать (больше — меньше дорисованных пятен, но дольше)",
            primary=True,
        ),
    )
    view_size: Literal[512, 768] = Field(
        default=512,
        description="Size of each drawn view, pixels (768: finer, much more memory)",
        json_schema_extra=ui(ru="Размер каждого вида, пикселей (768 — детальнее и тяжелее)"),
    )
    texture_size: Literal[2048, 4096] = Field(
        default=2048,
        description="Texture size of the result, pixels (baked at twice that, as upstream)",
        json_schema_extra=ui(ru="Размер текстуры в результате, пикселей (запекается вдвое больше)"),
    )
    max_faces: int = Field(
        default=40000,
        ge=1000,
        le=200000,
        description="Reduce the mesh to this many triangles before painting (upstream: 40k)",
        json_schema_extra=ui(ru="Упростить модель до стольких треугольников перед покраской"),
    )
    remove_background: bool = Field(
        default=True,
        description="Remove the image's background (rembg) unless it is already transparent",
        json_schema_extra=ui(ru="Удалить фон картинки (rembg), если она не прозрачная"),
    )
    min_roughness: float = Field(
        default=0.6,
        ge=0.0,
        le=1.0,
        description=(
            "Raise the painted roughness to at least this (0: as painted). Paint 2.1 reads"
            " gloss off the image: wet skin, bone and highlights came out mirror-like"
        ),
        json_schema_extra=ui(
            ru="Поднять шероховатость хотя бы до этой (0 — как нарисовано): иначе блики"
            " с картинки дают зеркальные отражения"
        ),
    )
    max_metallic: float = Field(
        default=1.0,
        ge=0.0,
        le=1.0,
        description="Lower the painted metalness to at most this (1: as painted; 0 with"
        " min_roughness 1: fully matte)",
        json_schema_extra=ui(
            ru="Снизить металличность хотя бы до этой (1 — как нарисовано; 0 вместе с"
            " min_roughness 1 — полностью матовая)"
        ),
    )


TASK = TaskSpec(
    KnownTask.PAINT_3D,
    Params,
    ("model/gltf-binary",),
    prompt="none",
    inputs=(
        InputSpec(
            role="mesh",
            mime=["model/gltf-binary"],
            description="The mesh to paint",
            labels={"ru": "Модель для покраски"},
        ),
        InputSpec(
            role="image",
            mime=["image/png", "image/jpeg", "image/webp"],
            description="The image the mesh was made from",
            labels={"ru": "Картинка, из которой сделана модель"},
        ),
    ),
    max_count=1,
)


def source_directory() -> Path:
    return Path(
        os.environ.get("HUNYUAN3D21_SOURCE", "~/.cache/assets-studio/hunyuan3d21-source")
    ).expanduser()


def models_directory() -> Path:
    return Path(os.environ.get("HY3DGEN_MODELS", "~/.cache/assets-studio/hy3dgen")).expanduser()


def paint_directory() -> Path:
    return models_directory() / WEIGHTS_REPO / PAINT


def dino_directory() -> Path:
    return models_directory() / DINO_REPO


def realesrgan_path() -> Path:
    return models_directory() / "RealESRGAN_x4plus.pth"


def download() -> None:
    """The paint model, DINOv2-giant and RealESRGAN x4plus, pinned, under
    HY3DGEN_MODELS. Network only; the server calls it on load."""
    import urllib.request

    from huggingface_hub import snapshot_download

    snapshot_download(
        WEIGHTS_REPO,
        revision=WEIGHTS_REVISION,
        allow_patterns=[f"{PAINT}/*"],
        local_dir=models_directory() / WEIGHTS_REPO,
    )
    snapshot_download(
        DINO_REPO,
        revision=DINO_REVISION,
        allow_patterns=["*.json", "model.safetensors"],
        local_dir=dino_directory(),
    )
    target = realesrgan_path()
    if not target.is_file() or target.stat().st_size != REALESRGAN_SIZE:
        partial = target.with_suffix(".part")
        urllib.request.urlretrieve(REALESRGAN_URL, partial)
        if partial.stat().st_size != REALESRGAN_SIZE:
            raise RuntimeError(f"RealESRGAN download is {partial.stat().st_size} bytes")
        partial.rename(target)


def paint_command(work: Path, params: Params) -> list[str]:
    return [
        sys.executable,
        "-m",
        "hunyuan_paint21_model_server.paint",
        str(work),
        params.model_dump_json(),
    ]


def failure(output: str, returncode: int = 1) -> GenerationError:
    """The child's failure as the job's error."""
    tail = explain(output, returncode)
    if "out of memory" in output or "OutOfMemoryError" in output:
        return GenerationError(
            f"out of GPU memory; free GPU memory, use fewer views or 512: {tail}", retryable=True
        )
    return GenerationError(f"painting failed: {tail}")


def result_of(stdout: str) -> dict[str, Any]:
    for line in reversed(stdout.splitlines()):
        if line.startswith(RESULT_PREFIX):
            return json.loads(line[len(RESULT_PREFIX) :])
    raise GenerationError("the painting process gave no result")


class HunyuanPaint21Server(ModelServer):
    model = ModelInfo(
        id="hunyuan_paint21",
        name="Hunyuan3D-Paint 2.1 (PBR)",
        revision=WEIGHTS_REVISION[:12] + ".plain-pbr.1.roughness-floor.2",
        license="Tencent Hunyuan 3D 2.1 Community License (not valid in the EU, UK, South Korea)",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
        details={
            "precision": "fp16, nothing quantised",
            "offloading": "diffusion pipeline on the GPU (~3.5 GB); DINOv2-giant (2.2 GB) in "
            "RAM, on the GPU for its one call; VAE decodes view by view",
            "VRAM": "officially ~21 GB; fits 8 GB with the offload",
            "materials": "PBR: base colour, metallic, roughness",
        },
    )
    tasks = (TASK,)

    def load(self) -> None:
        source = source_directory()
        if not (source / "hy3dpaint/textureGenPipeline.py").is_file():
            raise FileNotFoundError(f"Hunyuan3D-2.1 source not found in {source}; run `just setup`")
        download()
        logger.info("Hunyuan3D-Paint 2.1 ready (the models load per job)")

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        with tempfile.TemporaryDirectory(prefix="paint21-") as directory:
            work = Path(directory)
            (work / "input.glb").write_bytes(job.inputs["mesh"].data)
            (work / "image").write_bytes(job.inputs["image"].data)
            try:
                done = subprocess.run(  # noqa: S603 -- fixed argv, paths are ours
                    paint_command(work, job.params),
                    cwd=source_directory(),
                    capture_output=True,
                    text=True,
                    timeout=JOB_TIMEOUT_S,
                    check=False,
                )
            except subprocess.TimeoutExpired as error:
                raise GenerationError(f"painting took over {JOB_TIMEOUT_S} s") from error
            output = work / "textured.glb"
            if done.returncode != 0 or not output.is_file():
                logger.error("painting failed (%s):\n%s", done.returncode, done.stderr)
                raise failure(done.stderr or done.stdout, done.returncode)
            meta = result_of(done.stdout)
            return [Output("model/gltf-binary", output.read_bytes(), meta) for _ in job.seeds]
