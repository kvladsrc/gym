"""TripoSG image-to-3D server (contract v1).

VAST's open shape model (March 2025): a 1.5B rectified-flow transformer
samples 2048 latent tokens conditioned on the image (DINOv2), and the VAE
decodes them to a surface on a 512³ grid. Like Hunyuan3D a diffusion model,
so the seed gives different variants. Shape only: the mesh has no colour;
paint it with `3d-paint`.

The weights are fp32 on Hugging Face (~8 GB); the server keeps them in fp16
(~4 GB). The surface is extracted by diso, whose CUDA buffers live outside
PyTorch's allocator: for a large surface they did not fit beside the
transformer, and diso, which ignores its allocation errors, then broke the
process's CUDA context. So the transformer and the image encoder wait in RAM
while the surface is extracted (a few seconds a generation).

Licence: MIT (code and weights).
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

logger = logging.getLogger("triposg_model_server")

WEIGHTS_REPO = "VAST-AI/TripoSG"
# Git revisions, not secrets.
WEIGHTS_REVISION = "2c1c516d22d58db486a058d98d31bb6177344e06"  # pragma: allowlist secret
SOURCE_REVISION = "fc5c40990181e2a756c4e0b1c2f4d6b5202faf8c"  # pragma: allowlist secret

# The same cut-out as Hunyuan3D (see there): IS-Net keeps thin and pale parts
# that u2net cuts away. TripoSG's own scripts use BRIA RMBG-1.4, whose
# licence forbids commercial use.
BACKGROUND_MODEL = "isnet-general-use"
# Alpha below this is background noise (image models leave it over the whole
# frame); it would keep the whole frame in the crop.
FAINT_ALPHA = 32
# Alpha below this somewhere in the image: it already comes cut out.
TRANSPARENT_BELOW = 128
# As TripoSG was trained: the object on white, in a square with this margin
# around its longer side.
PADDING = 0.1


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
        default=7.0,
        ge=1.0,
        le=15.0,
        description="How closely the shape follows the image",
        json_schema_extra=ui(ru="Насколько форма следует картинке"),
    )
    octree_depth: int = Field(
        default=9,
        ge=7,
        le=10,
        description="Surface grid: 2^depth cells a side (9: 512³); finer detail, more triangles",
        json_schema_extra=ui(
            ru="Сетка поверхности: 2^depth ячеек по стороне (9 — 512³); мельче детали, "
            "больше треугольников"
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
    return Path(os.environ.get("TRIPOSG_SOURCE", "~/.cache/assets-studio/triposg-source"))


def download() -> str:
    from huggingface_hub import snapshot_download

    return snapshot_download(WEIGHTS_REPO, revision=WEIGHTS_REVISION)


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


def frame_on_white(image: Image.Image) -> Image.Image:
    """A cut-out object cropped, centred in a padded square, on white.

    TripoSG's own preprocessing (scripts/image_process.py): the model saw
    its training objects like this.
    """
    rgba = image.convert("RGBA")
    box = rgba.getchannel("A").getbbox()
    if box is None:
        raise InvalidInput("no foreground object found; is the background removed?")
    crop = rgba.crop(box)
    side = round(max(crop.size) * (1 + 2 * PADDING))
    framed = Image.new("RGB", (side, side), "white")
    framed.paste(crop, ((side - crop.width) // 2, (side - crop.height) // 2), crop)
    return framed


def reduce_faces(mesh: Any, max_faces: int) -> Any:
    """The mesh decimated to at most max_faces triangles (0: unchanged)."""
    if not max_faces or len(mesh.faces) <= max_faces:
        return mesh
    import pymeshlab
    import trimesh

    meshes = pymeshlab.MeshSet()
    meshes.add_mesh(pymeshlab.Mesh(vertex_matrix=mesh.vertices, face_matrix=mesh.faces))
    meshes.meshing_merge_close_vertices()
    meshes.meshing_decimation_quadric_edge_collapse(targetfacenum=max_faces)
    reduced = meshes.current_mesh()
    return trimesh.Trimesh(reduced.vertex_matrix(), reduced.face_matrix())


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


class TripoSGServer(ModelServer):
    model = ModelInfo(
        id="triposg",
        name="TripoSG",
        revision=WEIGHTS_REVISION[:12],
        license="MIT",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._pipeline: Any = None
        self._rembg_session: Any = None

    def load(self) -> None:
        import rembg
        import torch

        source = source_directory().expanduser()
        if not (source / "triposg").is_dir():
            raise FileNotFoundError(f"TripoSG source not found in {source}; run `just setup`")
        sys.path.insert(0, str(source))
        from triposg.pipelines.pipeline_triposg import (  # pyright: ignore[reportMissingImports]
            TripoSGPipeline,
        )

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("TripoSG needs a CUDA GPU")
        self._pipeline = TripoSGPipeline.from_pretrained(download()).to("cuda", torch.float16)
        self._pipeline.set_progress_bar_config(disable=True)
        self._rembg_session = rembg.new_session(BACKGROUND_MODEL)
        logger.info("TripoSG ready")

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        image = self._prepare(job.inputs["image"].data, job.params)
        # One sampling per seed: output i depends on seeds[i] only.
        return [self._one(image, seed, job.params) for seed in job.seeds]

    def _one(self, image: Image.Image, seed: int, params: Params) -> Output:
        import torch
        import trimesh

        pipeline = self._pipeline
        pipeline.transformer.to("cuda")
        pipeline.image_encoder_dinov2.to("cuda")
        try:
            vertices, faces = pipeline(
                image=image,
                num_inference_steps=params.steps,
                guidance_scale=params.guidance_scale,
                flash_octree_depth=params.octree_depth,
                generator=torch.Generator(device="cuda").manual_seed(seed),
                callback_on_step_end=self._free_for_surface,
            ).samples[0]
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            raise GenerationError(
                "out of GPU memory; free GPU memory or lower octree_depth", retryable=True
            ) from error
        finally:
            torch.cuda.empty_cache()
        if vertices is None or not len(faces):
            raise GenerationError("no surface found: the object may be too thin or the image empty")
        mesh = trimesh.Trimesh(vertices.astype(np.float32), np.ascontiguousarray(faces))
        mesh = reduce_faces(mesh, params.max_faces)
        return Output("model/gltf-binary", mesh.export(file_type="glb"), describe(mesh))

    def _free_for_surface(
        self, pipeline: Any, step: int, _timestep: Any, _tensors: dict[str, Any]
    ) -> dict[str, Any]:
        """After the last denoising step: move what the decoder does not need to RAM."""
        if step == pipeline.num_timesteps - 1:
            import torch

            pipeline.transformer.to("cpu")
            pipeline.image_encoder_dinov2.to("cpu")
            torch.cuda.empty_cache()
        return {}

    def _prepare(self, data: bytes, params: Params) -> Image.Image:
        try:
            image = Image.open(io.BytesIO(data))
            image.load()
        except (UnidentifiedImageError, OSError) as error:
            raise InvalidInput(f"cannot read the image: {error}") from error
        if params.remove_background and not is_cut_out(image):
            import rembg

            image = rembg.remove(image.convert("RGB"), session=self._rembg_session)
        return frame_on_white(clear_faint(image))
