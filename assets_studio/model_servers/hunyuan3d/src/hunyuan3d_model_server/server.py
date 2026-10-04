"""Hunyuan3D-2mini image-to-3D server (contract v1).

A diffusion model of shape, not a reconstruction: it samples a 3D latent
conditioned on the image and decodes a surface from it, so it completes the
sides the image does not show better than TripoSR or SF3D, and the seed
gives different variants. Shape only: the mesh has no colour (Hunyuan's
texture model needs ~21 GB of VRAM); colour it from the concept elsewhere.

The full mini model (0.6B DiT, 50 steps) rather than its distilled turbo and
fast variants: quality before speed.

Licence: Tencent Hunyuan Community License. It does not apply in the EU,
the UK and South Korea.
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
    ui,
)

logger = logging.getLogger("hunyuan3d_model_server")

WEIGHTS_REPO = "tencent/Hunyuan3D-2mini"
SUBFOLDER = "hunyuan3d-dit-v2-mini"
# Git revisions, not secrets.
WEIGHTS_REVISION = "f90a0f7df7d5e6f71109cf333f6a95a0ae3194a6"  # pragma: allowlist secret
SOURCE_REVISION = "f8db63096c8282cb27354314d896feba5ba6ff8a"  # pragma: allowlist secret

# rembg's model for cutting out the background. Its default (u2net) cut away
# whole tree crowns (silver, crimson, green alike) and a sword's silver
# blade, and the 3D followed; IS-Net keeps them (~1 s an image).
BACKGROUND_MODEL = "isnet-general-use"
# Alpha below this is background noise (image models leave it over the whole
# frame). The preprocessing crops to the alpha mask, so faint noise would keep
# the whole frame.
FAINT_ALPHA = 32
# Alpha below this somewhere in the image: it already comes cut out.
TRANSPARENT_BELOW = 128


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    steps: int = Field(
        default=50,
        ge=5,
        le=100,
        description="Diffusion steps",
        json_schema_extra=ui(ru="Шаги диффузии", primary=True),
    )
    guidance_scale: float = Field(
        default=5.0,
        ge=1.0,
        le=15.0,
        description="How closely the shape follows the image",
        json_schema_extra=ui(ru="Насколько форма следует картинке"),
    )
    octree_resolution: int = Field(
        default=384,
        ge=128,
        le=512,
        description="Surface grid resolution: finer detail, more triangles",
        json_schema_extra=ui(
            ru="Разрешение сетки поверхности: мельче детали, больше треугольников"
        ),
    )
    max_faces: int = Field(
        default=0,
        ge=0,
        le=1_000_000,
        description="Reduce to at most this many triangles (0: keep all)",
        json_schema_extra=ui(ru="Сократить до стольких треугольников (0 — оставить все)"),
    )
    remove_background: bool = Field(
        default=True,
        description="Remove the background (rembg) unless the image is already transparent",
        json_schema_extra=ui(ru="Удалить фон (rembg), если картинка не прозрачная"),
    )


TASK = TaskSpec(
    KnownTask.IMAGE_TO_3D,
    Params,
    ("model/gltf-binary",),
    prompt="none",
    inputs=(InputSpec(role="image", mime=["image/png", "image/jpeg", "image/webp"]),),
    max_count=4,
)


def source_directory() -> Path:
    return Path(os.environ.get("HUNYUAN3D_SOURCE", "~/.cache/assets-studio/hunyuan3d-source"))


def clear_faint(image: Image.Image) -> Image.Image:
    """The image with alpha below FAINT_ALPHA set to 0."""
    rgba = image.convert("RGBA")
    rgba.putalpha(rgba.getchannel("A").point(lambda a: 0 if a < FAINT_ALPHA else a))
    return rgba


def is_cut_out(image: Image.Image) -> bool:
    """Whether the image already has a transparent background."""
    return (
        image.mode in ("RGBA", "LA", "PA")
        and image.getchannel("A").getextrema()[0] < TRANSPARENT_BELOW
    )


def describe(mesh: Any) -> dict[str, Any]:
    """Metadata of a trimesh in glTF axes (Y up)."""
    bounds = np.asarray(mesh.bounds, dtype=float)
    return {
        "vertices": len(mesh.vertices),
        "triangles": len(mesh.faces),
        "watertight": bool(mesh.is_watertight),
        "bounds": bounds.round(4).tolist(),
        "up_axis": "Y",
        "color": "none",
    }


class Hunyuan3DServer(ModelServer):
    model = ModelInfo(
        id="hunyuan3d",
        name="Hunyuan3D-2mini",
        revision=WEIGHTS_REVISION[:12],
        license="Tencent Hunyuan Community License (not valid in the EU, UK, South Korea)",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._pipeline: Any = None
        self._rembg_session: Any = None
        self._cleanup: list[Any] = []
        self._reducer: Any = None

    def load(self) -> None:
        import rembg
        import torch
        from huggingface_hub import snapshot_download

        source = source_directory().expanduser()
        if not (source / "hy3dgen").is_dir():
            raise FileNotFoundError(f"Hunyuan3D-2 source not found in {source}; run `just setup`")
        sys.path.insert(0, str(source))
        from hy3dgen.shapegen import (  # pyright: ignore[reportMissingImports]
            DegenerateFaceRemover,
            FaceReducer,
            FloaterRemover,
            Hunyuan3DDiTFlowMatchingPipeline,
        )

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("Hunyuan3D needs a CUDA GPU")
        weights = snapshot_download(
            WEIGHTS_REPO,
            revision=WEIGHTS_REVISION,
            allow_patterns=[f"{SUBFOLDER}/config.yaml", f"{SUBFOLDER}/model.fp16.safetensors"],
        )
        # An absolute model path: hy3dgen's loader joins it onto its own
        # cache directory, which an absolute path replaces.
        self._pipeline = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained(
            weights, subfolder=SUBFOLDER, use_safetensors=True, variant="fp16", device="cuda"
        )
        self._cleanup = [FloaterRemover(), DegenerateFaceRemover()]
        self._reducer = FaceReducer()
        self._rembg_session = rembg.new_session(BACKGROUND_MODEL)
        logger.info("Hunyuan3D-2mini ready")

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        image = self._prepare(job.inputs["image"].data, job.params)
        # One sampling per seed: output i depends on seeds[i] only.
        return [self._one(image, seed, job.params) for seed in job.seeds]

    def _one(self, image: Image.Image, seed: int, params: Params) -> Output:
        import torch

        try:
            mesh = self._pipeline(
                image=image,
                num_inference_steps=params.steps,
                guidance_scale=params.guidance_scale,
                octree_resolution=params.octree_resolution,
                generator=torch.Generator().manual_seed(seed),
                output_type="trimesh",
                enable_pbar=False,
            )[0]
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            torch.cuda.empty_cache()
            raise GenerationError(
                "out of GPU memory; free GPU memory or lower octree_resolution", retryable=True
            ) from error
        if mesh is None or not len(mesh.faces):
            raise GenerationError("no surface found: the object may be too thin or the image empty")
        for step in self._cleanup:
            mesh = step(mesh)
        if params.max_faces and len(mesh.faces) > params.max_faces:
            mesh = self._reducer(mesh, max_facenum=params.max_faces)
        return Output("model/gltf-binary", mesh.export(file_type="glb"), describe(mesh))

    def _prepare(self, data: bytes, params: Params) -> Image.Image:
        try:
            image = Image.open(io.BytesIO(data))
            image.load()
        except (UnidentifiedImageError, OSError) as error:
            raise InvalidInput(f"cannot read the image: {error}") from error
        if params.remove_background and not is_cut_out(image):
            import rembg

            image = rembg.remove(image.convert("RGB"), session=self._rembg_session)
        image = clear_faint(image)
        if image.getchannel("A").getbbox() is None:
            raise InvalidInput("no foreground object found; is the background removed?")
        return image
