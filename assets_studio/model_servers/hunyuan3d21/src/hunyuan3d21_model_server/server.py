"""Hunyuan3D-2.1 Shape image-to-3D server (contract v1).

The largest open Hunyuan3D shape model (June 2025): a 3.3B DiT against the
0.6B of 2mini, the same flow-matching recipe (a 3D latent sampled from the
image, a surface decoded from it, the seed gives variants). Shape only;
colour it with `3d-paint` (2.1's own PBR painter needs ~21 GB of VRAM).

Officially ~10 GB of VRAM. On 8 GB the pipeline's parts take turns on the
GPU (enable_model_cpu_offload: image encoder, DiT, VAE), the rest waits in
RAM; the weights stay fp16, nothing is quantised.

Licence: Tencent Hunyuan 3D 2.1 Community License. It does not apply in the
EU, the UK and South Korea.
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

logger = logging.getLogger("hunyuan3d21_model_server")

WEIGHTS_REPO = "tencent/Hunyuan3D-2.1"
SUBFOLDER = "hunyuan3d-dit-v2-1"
# Git revisions, not secrets.
WEIGHTS_REVISION = "0b94677654c57bb9a6b6845cd7b704ccf551d327"  # pragma: allowlist secret
SOURCE_REVISION = "82920d643c0dc2f7bfd7255f45f62d386edfe60c"  # pragma: allowlist secret

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
    return Path(os.environ.get("HUNYUAN3D21_SOURCE", "~/.cache/assets-studio/hunyuan3d21-source"))


def models_directory() -> Path:
    return Path(os.environ.get("HY3DGEN_MODELS", "~/.cache/assets-studio/hy3dgen")).expanduser()


def download() -> None:
    """The DiT checkpoint (with its VAE and image encoder) where the pipeline
    looks for it: HY3DGEN_MODELS/tencent/Hunyuan3D-2.1. Network only."""
    from huggingface_hub import snapshot_download

    snapshot_download(
        WEIGHTS_REPO,
        revision=WEIGHTS_REVISION,
        allow_patterns=[f"{SUBFOLDER}/config.yaml", f"{SUBFOLDER}/model.fp16.ckpt"],
        local_dir=models_directory() / WEIGHTS_REPO,
    )


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


def take_turns_on_gpu(pipeline: Any) -> None:
    """Each part of the pipeline on the GPU only for its phase: the image
    encoder for encode_cond, the DiT for the denoising loop (from
    prepare_latents on), the VAE for _export. Upstream's own
    enable_model_cpu_offload is diffusers' and does not run here (the
    pipeline has no `components`), and accelerate's hooks would miss the
    VAE, which _export calls through latents2mesh, not forward."""
    import torch

    gpu = torch.device("cuda")
    pipeline.device = gpu  # where __call__ makes its tensors

    def only(*on_gpu: Any) -> None:
        for part in (pipeline.conditioner, pipeline.model, pipeline.vae):
            part.to(gpu if part in on_gpu else "cpu")
        torch.cuda.empty_cache()

    def phase(method: Any, *parts: Any) -> Any:
        def run(*args: Any, **kwargs: Any) -> Any:
            only(*parts)
            return method(*args, **kwargs)

        return run

    pipeline.encode_cond = phase(pipeline.encode_cond, pipeline.conditioner)
    pipeline.prepare_latents = phase(pipeline.prepare_latents, pipeline.model)
    pipeline._export = phase(pipeline._export, pipeline.vae)
    only()


class Hunyuan3D21Server(ModelServer):
    model = ModelInfo(
        id="hunyuan3d21",
        name="Hunyuan3D-2.1 Shape",
        revision=WEIGHTS_REVISION[:12],
        license="Tencent Hunyuan 3D 2.1 Community License (not valid in the EU, UK, South Korea)",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
        details={
            "precision": "fp16, nothing quantised",
            "offloading": "model CPU offload: image encoder, DiT and VAE take turns on the GPU",
            "VRAM": "officially ~10 GB; fits 8 GB with the offload",
        },
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

        source = source_directory().expanduser()
        if not (source / "hy3dshape/hy3dshape").is_dir():
            raise FileNotFoundError(f"Hunyuan3D-2.1 source not found in {source}; run `just setup`")
        sys.path.insert(0, str(source / "hy3dshape"))
        from hy3dshape.pipelines import (  # pyright: ignore[reportMissingImports]
            Hunyuan3DDiTFlowMatchingPipeline,
        )
        from hy3dshape.postprocessors import (  # pyright: ignore[reportMissingImports]
            DegenerateFaceRemover,
            FaceReducer,
            FloaterRemover,
        )

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("Hunyuan3D-2.1 needs a CUDA GPU")
        download()
        os.environ["HY3DGEN_MODELS"] = str(models_directory())
        # Loaded to RAM; each part moves to the GPU only while it runs.
        self._pipeline = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained(
            WEIGHTS_REPO, subfolder=SUBFOLDER, device="cpu"
        )
        take_turns_on_gpu(self._pipeline)
        self._cleanup = [FloaterRemover(), DegenerateFaceRemover()]
        self._reducer = FaceReducer()
        self._rembg_session = rembg.new_session(BACKGROUND_MODEL)
        logger.info("Hunyuan3D-2.1 Shape ready (offloaded)")

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
