"""TripoSR image-to-3D server (contract v1).

TripoSR is a feed-forward reconstruction model: the same image always gives
the same mesh, and the seed has no effect. The server therefore produces one
candidate per request (``max_count = 1``); variants come from varying the
input image.
"""

import io
import logging
import os
import sys
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, UnidentifiedImageError
from pydantic import BaseModel, ConfigDict, Field

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
)
from triposr_model_server import mesh, surface

logger = logging.getLogger("triposr_model_server")

WEIGHTS_REPO = "stabilityai/TripoSR"
# Git revisions, not secrets.
WEIGHTS_REVISION = "5b521936b01fbe1890f6f9baed0254ab6351c04a"  # pragma: allowlist secret
SOURCE_REVISION = "107cefdc244c39106fa830359024f6a2f1c78871"  # pragma: allowlist secret
# Evaluation chunk size for surface extraction: lower uses less VRAM.
CHUNK_SIZE = 4096
# Background colour TripoSR was trained with (upstream run.py).
BACKGROUND = 0.5


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    mc_resolution: int = Field(
        default=256, ge=32, le=512, description="Разрешение сетки marching cubes"
    )
    remove_background: bool = Field(default=True, description="Удалить фон (rembg)")
    foreground_ratio: float = Field(
        default=0.85, ge=0.5, le=1.0, description="Доля кадра, занимаемая объектом"
    )


TASK = TaskSpec(
    KnownTask.IMAGE_TO_3D,
    Params,
    ("model/gltf-binary",),
    prompt="none",
    inputs=(InputSpec(role="image", mime=["image/png", "image/jpeg", "image/webp"]),),
    max_count=1,
)


def source_directory() -> Path:
    return Path(os.environ.get("TRIPOSR_SOURCE", "~/.cache/assets-studio/triposr-source"))


class TripoSRServer(ModelServer):
    model = ModelInfo(
        id="triposr",
        name="TripoSR",
        revision=WEIGHTS_REVISION[:12],
        license="MIT",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._model: Any = None
        self._rembg_session: Any = None
        self._device = "cpu"

    def load(self) -> None:
        import rembg
        import torch
        from huggingface_hub import snapshot_download

        source = source_directory().expanduser()
        if not (source / "tsr").is_dir():
            raise FileNotFoundError(f"TripoSR source not found in {source}; run `just setup`")
        surface.install()
        sys.path.insert(0, str(source))
        from tsr.system import TSR  # pyright: ignore[reportMissingImports]

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if torch.cuda.is_available():
            self._device = "cuda:0"
        else:
            logger.warning("CUDA is not available: TripoSR will run on the CPU, very slowly")
        weights = snapshot_download(
            WEIGHTS_REPO,
            revision=WEIGHTS_REVISION,
            allow_patterns=["config.yaml", "model.ckpt"],
        )
        model = TSR.from_pretrained(weights, config_name="config.yaml", weight_name="model.ckpt")
        model.renderer.set_chunk_size(CHUNK_SIZE)
        self._model = model.to(self._device)
        self._rembg_session = rembg.new_session()
        logger.info("TripoSR ready on %s", self._device)

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, Params)
        image = self._prepare(job.inputs["image"].data, job.params)
        try:
            codes = self._model([image], device=self._device)
            raw = self._model.extract_mesh(codes, True, resolution=job.params.mc_resolution)[0]
            data, meta = mesh.to_glb(raw)
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            torch.cuda.empty_cache()
            raise GenerationError(
                f"out of GPU memory at mc_resolution={job.params.mc_resolution}; "
                "free GPU memory or lower the resolution",
                retryable=True,
            ) from error
        except mesh.EmptyMesh as error:
            raise GenerationError(str(error)) from error
        # Deterministic model: every seed yields the same mesh (max_count is 1).
        return [Output("model/gltf-binary", data, meta) for _ in job.seeds]

    def _prepare(self, data: bytes, params: Params) -> Image.Image:
        """Upstream preprocessing: cut out the object, centre it, grey background."""
        from tsr.utils import remove_background  # pyright: ignore[reportMissingImports]

        try:
            image = Image.open(io.BytesIO(data))
            image.load()
        except (UnidentifiedImageError, OSError) as error:
            raise InvalidInput(f"cannot read the image: {error}") from error
        if not params.remove_background:
            # Already prepared by the user: keep the framing, but transparent
            # pixels must become the grey background the model was trained on.
            return _flatten(image.convert("RGBA"))
        image = remove_background(image.convert("RGBA"), self._rembg_session)
        return _compose_foreground(image, params.foreground_ratio)


def _compose_foreground(image: Image.Image, ratio: float) -> Image.Image:
    """Crop to the object, pad it to a square at ``ratio`` and flatten on grey.

    Same result as upstream ``resize_foreground`` plus the compositing in
    run.py, with a clear error instead of a crash when nothing is left.
    """
    rgba = np.asarray(image.convert("RGBA"))
    ys, xs = np.nonzero(rgba[..., 3])
    if not len(ys):
        raise InvalidInput("no foreground object found after background removal")
    crop = rgba[ys.min() : ys.max(), xs.min() : xs.max()]
    if not crop.size:
        raise InvalidInput("the foreground object is too small")
    height, width = crop.shape[:2]
    side = max(height, width)
    square = np.zeros((side, side, 4), dtype=np.uint8)
    top, left = (side - height) // 2, (side - width) // 2
    square[top : top + height, left : left + width] = crop
    size = int(side / ratio)
    padded = np.zeros((size, size, 4), dtype=np.uint8)
    offset = (size - side) // 2
    padded[offset : offset + side, offset : offset + side] = square
    return _flatten(Image.fromarray(padded))


def _flatten(image: Image.Image) -> Image.Image:
    """Composite an RGBA image on the grey training background."""
    pixels = np.asarray(image).astype(np.float32) / 255
    rgb = pixels[..., :3] * pixels[..., 3:4] + (1 - pixels[..., 3:4]) * BACKGROUND
    return Image.fromarray((rgb * 255).astype(np.uint8))
