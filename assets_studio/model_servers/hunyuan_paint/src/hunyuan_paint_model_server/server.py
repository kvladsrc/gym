"""Hunyuan3D-Paint v2.0 turbo server (contract v1, task 3d-paint).

A mesh and the image it was made from in; the mesh with a UV texture out.
The model removes the lighting from the image (delight), generates six
views of the object conditioned on the mesh's normals and positions, and
bakes them into the texture, so hidden sides get plausible colours. The
pair for shape-only meshes from Hunyuan3D (same frame as its shapes).

Low VRAM: the two diffusion pipelines (~8 GB of fp16 weights) are offloaded
to the CPU module by module. Licence: Tencent Hunyuan Community License (not
valid in the EU, UK and South Korea).
"""

import contextlib
import gc
import io
import logging
import os
import sys
from collections.abc import Iterator
from pathlib import Path
from typing import Any, Literal

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

logger = logging.getLogger("hunyuan_paint_model_server")

WEIGHTS_REPO = "tencent/Hunyuan3D-2"
PAINT = "hunyuan3d-paint-v2-0-turbo"
DELIGHT = "hunyuan3d-delight-v2-0"
# Git revisions, not secrets.
WEIGHTS_REVISION = "9cd649ba6913f7a852e3286bad86bfa9a2d83dcf"  # pragma: allowlist secret
SOURCE_REVISION = "f8db63096c8282cb27354314d896feba5ba6ff8a"  # pragma: allowlist secret

# rembg's model for cutting out the background. Its default (u2net) cut away
# whole tree crowns (silver, crimson, green alike) and a sword's silver
# blade, and the 3D followed; IS-Net keeps them (~1 s an image).
BACKGROUND_MODEL = "isnet-general-use"
# Alpha below this is background noise (image models leave it over the whole
# frame).
FAINT_ALPHA = 32
# Alpha below this somewhere in the image: it already comes cut out.
TRANSPARENT_BELOW = 128


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    texture_size: Literal[1024, 2048] = Field(
        default=2048,
        description="Texture size, pixels",
        json_schema_extra=ui(ru="Размер текстуры, пикселей", primary=True),
    )
    max_faces: int = Field(
        default=60000,
        ge=0,
        description="Reduce the mesh to this many triangles before painting (0: as it is); "
        "the UV unwrap of a mesh of hundreds of thousands takes tens of minutes on the CPU",
        json_schema_extra=ui(
            ru="Упростить модель до стольких треугольников перед покраской (0: как есть); "
            "развёртка сотен тысяч идёт десятки минут на CPU"
        ),
    )
    remove_background: bool = Field(
        default=True,
        description="Remove the image's background (rembg) unless it is already transparent",
        json_schema_extra=ui(ru="Удалить фон картинки (rembg), если она не прозрачная"),
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
        os.environ.get("HUNYUAN_PAINT_SOURCE", "~/.cache/assets-studio/hunyuan-paint-source")
    ).expanduser()


def models_directory() -> Path:
    return Path(os.environ.get("HY3DGEN_MODELS", "~/.cache/assets-studio/hy3dgen")).expanduser()


def download() -> Path:
    """The paint and delight weights at the pinned revision. The UNet is in
    the repository twice; the model's own loader (its modules.py) reads the
    .bin, so both are kept."""
    from huggingface_hub import snapshot_download

    target = models_directory() / WEIGHTS_REPO
    snapshot_download(
        WEIGHTS_REPO,
        revision=WEIGHTS_REVISION,
        allow_patterns=[f"{PAINT}/*", f"{DELIGHT}/*"],
        local_dir=target,
    )
    return target


@contextlib.contextmanager
def _mapped_loads() -> Iterator[None]:
    """``torch.load`` of weight files maps them instead of reading them in.
    The model's own loader reads its .bin checkpoints whole and the model
    then copies them: loading peaked at 14.6 GB of RAM and the kernel killed
    the server on a 30 GB laptop."""
    import torch

    original = torch.load

    def load(file: Any, *args: Any, **kwargs: Any) -> Any:
        if isinstance(file, (str, os.PathLike)) and str(file).endswith((".bin", ".ckpt", ".pt")):
            kwargs.setdefault("mmap", True)
        return original(file, *args, **kwargs)

    torch.load = load
    try:
        yield
    finally:
        torch.load = original


def clear_faint(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    rgba.putalpha(rgba.getchannel("A").point(lambda a: 0 if a < FAINT_ALPHA else a))
    return rgba


def is_cut_out(image: Image.Image) -> bool:
    return (
        image.mode in ("RGBA", "LA", "PA")
        and image.getchannel("A").getextrema()[0] < TRANSPARENT_BELOW
    )


def reduce_faces(mesh: Any, faces: int) -> Any:
    """``mesh`` with at most ``faces`` triangles: quadric edge collapse with
    the settings of Hunyuan3D-2's own FaceReducer, which its reference
    pipeline runs before painting."""
    import pymeshlab
    import trimesh

    if not faces or len(mesh.faces) <= faces:
        return mesh
    meshes = pymeshlab.MeshSet()
    meshes.add_mesh(pymeshlab.Mesh(vertex_matrix=mesh.vertices, face_matrix=mesh.faces))
    meshes.meshing_decimation_quadric_edge_collapse(
        targetfacenum=faces,
        qualitythr=1.0,
        preserveboundary=True,
        boundaryweight=3,
        preservenormal=True,
        preservetopology=True,
        autoclean=True,
    )
    reduced = meshes.current_mesh()
    return trimesh.Trimesh(reduced.vertex_matrix(), reduced.face_matrix(), process=False)


def describe(mesh: Any, texture_size: int, faces_in: int | None = None) -> dict[str, Any]:
    bounds = np.asarray(mesh.bounds, dtype=float)
    reduced = {} if faces_in is None or faces_in == len(mesh.faces) else {"reduced_from": faces_in}
    return {
        **reduced,
        "vertices": len(mesh.vertices),
        "triangles": len(mesh.faces),
        "bounds": bounds.round(4).tolist(),
        "up_axis": "Y",
        "color": "texture",
        "texture_size": texture_size,
    }


class HunyuanPaintServer(ModelServer):
    model = ModelInfo(
        id="hunyuan3d-paint",
        name="Hunyuan3D-Paint v2.0 turbo",
        revision=WEIGHTS_REVISION[:12],
        license="Tencent Hunyuan Community License (not valid in the EU, UK, South Korea)",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._pipeline: Any = None
        self._rembg_session: Any = None
        self._texture_size = 0

    def load(self) -> None:
        import rembg
        import torch

        source = source_directory()
        if not (source / "hy3dgen/texgen").is_dir():
            raise FileNotFoundError(f"Hunyuan3D-2 source not found in {source}; run `just setup`")
        if not torch.cuda.is_available():
            raise RuntimeError("Hunyuan3D-Paint needs a CUDA GPU")
        weights = download()
        sys.path.insert(0, str(source))
        from hy3dgen.texgen import Hunyuan3DPaintPipeline  # pyright: ignore[reportMissingImports]

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        with _mapped_loads():
            pipeline = Hunyuan3DPaintPipeline.from_pretrained(str(weights), subfolder=PAINT)
        gc.collect()
        pipeline.enable_model_cpu_offload()
        self._pipeline = pipeline
        self._texture_size = pipeline.config.texture_size
        self._rembg_session = rembg.new_session(BACKGROUND_MODEL)
        logger.info("Hunyuan3D-Paint ready")

    def generate(self, job: Job) -> list[Output]:
        import torch
        import trimesh

        assert isinstance(job.params, Params)
        params = job.params
        image = self._prepare(job.inputs["image"].data, params)
        try:
            mesh = trimesh.load(io.BytesIO(job.inputs["mesh"].data), file_type="glb", force="mesh")
        except Exception as error:  # trimesh raises many kinds on a bad file
            raise InvalidInput(f"cannot read the mesh: {error}") from error
        if not isinstance(mesh, trimesh.Trimesh) or not len(mesh.faces):
            raise InvalidInput("the mesh has no faces")
        faces_in = len(mesh.faces)
        mesh = reduce_faces(mesh, params.max_faces)
        self._set_texture_size(params.texture_size)
        try:
            painted = self._pipeline(mesh, image=image)
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            torch.cuda.empty_cache()
            raise GenerationError(
                "out of GPU memory; free GPU memory or use a 1024 texture", retryable=True
            ) from error
        data = painted.export(file_type="glb")
        # Deterministic for a given mesh and image: one candidate (max_count 1).
        return [
            Output("model/gltf-binary", data, describe(painted, params.texture_size, faces_in))
            for _ in job.seeds
        ]

    def _set_texture_size(self, size: int) -> None:
        """The renderer bakes at a fixed size: a new one for another size."""
        if size == self._texture_size:
            return
        from hy3dgen.texgen.differentiable_renderer.mesh_render import (  # pyright: ignore[reportMissingImports]
            MeshRender,
        )

        self._pipeline.config.render_size = size
        self._pipeline.config.texture_size = size
        self._pipeline.render = MeshRender(default_resolution=size, texture_size=size)
        self._texture_size = size

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
            raise InvalidInput("no foreground object found in the image")
        return image
