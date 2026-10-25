"""Hi3D v1.0 mesh refinement server (contract v1).

Hi3D (formerly Sparc3D, open since 2026-10-23) does not make a shape from
nothing: it takes a coarse mesh and the image it was made from and rebuilds the
surface on a sparse grid of 512³, 1024³ or 1536³ (one, two or three passes of a
1.8B transformer, a VAE decoding each pass). Geometry only; colour it after with
`3d-paint`. A coarse mesh comes from `image-to-3d` (Hunyuan3D-2.1).

Upstream was measured on a 96 GB H20 only. On 8 GB the pipeline's parts take
turns on the GPU (image encoders, the pass's transformer, the VAE), the rest
waits in RAM; the weights stay as published (fp16 transformers).

Upstream declares Python 3.10; the server runs it on 3.11 with the exact kernels
it was verified with (torch 2.5.0+cu124, flash-attn 2.7.4.post1, spconv-cu120
2.3.6), importing the pinned source instead of installing it.

Licence: Hi3D Open License Agreement 1.0 (commercial use allowed; above 200,000
monthly users or USD 1,000,000 monthly revenue a separate licence; integrations
show "Powered by Hi3D 1.0").
"""

import io
import logging
import os
import sys
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
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
    ui,
)

logger = logging.getLogger("hi3d_model_server")

WEIGHTS_REPO = "hi3d-official/Hi3Dv1.0"
# Git revisions, not secrets.
WEIGHTS_REVISION = "d502e938cbedc6e87aba8884614c5ca6d177a4b6"  # pragma: allowlist secret
SOURCE_REVISION = "13657ad09afaa92e350b461a1cd66399e44a59c4"  # pragma: allowlist secret

# As in hunyuan3d21: IS-Net keeps thin parts (blades, crowns) that u2net cuts.
BACKGROUND_MODEL = "isnet-general-use"
TRANSPARENT_BELOW = 128

REFINE_3D = "3d-refine"

# Shares of upstream's per-pass voxel budgets (80k, 90k, 110k occupied voxels)
# tried in turn when a pass runs out of GPU memory: a round, massive toad
# overflowed the 1024³ decoding three times where the troll fitted.
BUDGET_SHARES = (1.0, 0.75, 0.55)


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    resolution: Literal[512, 1024, 1536] = Field(
        default=1024,
        description="Surface grid: 512 one pass, 1024 two, 1536 three",
        json_schema_extra=ui(
            ru="Сетка поверхности: 512 — один проход, 1024 — два, 1536 — три", primary=True
        ),
    )
    steps: int = Field(
        default=25,
        ge=5,
        le=50,
        description="Diffusion steps in each pass",
        json_schema_extra=ui(ru="Шаги диффузии в каждом проходе"),
    )
    max_faces: int = Field(
        default=0,
        ge=0,
        le=2_000_000,
        description=(
            "At most this many triangles, in Hi3D's own simplification of the full surface"
            " (0: its 1-2 million). Set it to what painting keeps (Paint 2.1: 200k at most)"
            " so the mesh is simplified once, not twice"
        ),
        json_schema_extra=ui(
            ru="Не больше стольких треугольников — в упрощении самого Hi3D, от полной"
            " поверхности (0 — его 1–2 млн). Ставьте столько, сколько оставит покраска"
            " (Paint 2.1 — до 200 тыс.), чтобы сетка упрощалась один раз, а не дважды"
        ),
    )
    keep_placement: bool = Field(
        default=True,
        description="Put the result where the coarse mesh was, at its size (Hi3D normalises it)",
        json_schema_extra=ui(
            ru="Поставить результат на место и в размер исходной сетки (Hi3D нормирует)"
        ),
    )
    remove_background: bool = Field(
        default=True,
        description="Remove the background (rembg) unless the image is already transparent",
        json_schema_extra=ui(ru="Удалить фон (rembg), если картинка не прозрачная"),
    )


TASK = TaskSpec(
    REFINE_3D,
    Params,
    ("model/gltf-binary",),
    prompt="none",
    inputs=(
        InputSpec(
            role="mesh",
            mime=["model/gltf-binary"],
            description="The coarse mesh to refine",
            labels={"ru": "Грубая сетка"},
        ),
        InputSpec(
            role="image",
            mime=["image/png", "image/jpeg", "image/webp"],
            description="The image the mesh was made from",
            labels={"ru": "Картинка, по которой сделана сетка"},
        ),
    ),
    max_count=4,
)


def source_directory() -> Path:
    return Path(os.environ.get("HI3D_SOURCE", "~/.cache/assets-studio/hi3d-source")).expanduser()


def weights_directory() -> Path:
    """Hi3D's artifact (model_directory) and the Hugging Face cache of its image
    encoders."""
    return Path(os.environ.get("HI3D_WEIGHTS", "~/.cache/assets-studio/hi3d")).expanduser()


def model_directory() -> Path:
    """The artifact as plain files: Hi3D's loader refuses the symlinks of a
    Hugging Face cache snapshot (other than the files it fetches itself)."""
    return weights_directory() / "Hi3Dv1.0"


def download() -> None:
    """The pipeline artifact at the pinned revision (~7.7 GB). Network only; the
    image encoders (DINOv2, CLIP) come on the first load."""
    from huggingface_hub import snapshot_download

    snapshot_download(WEIGHTS_REPO, revision=WEIGHTS_REVISION, local_dir=model_directory())


def is_cut_out(image: Image.Image) -> bool:
    """Whether the image already has a transparent background."""
    return (
        image.mode in ("RGBA", "LA", "PA")
        and image.getchannel("A").getextrema()[0] < TRANSPARENT_BELOW
    )


def read_mesh(data: bytes) -> Any:
    import trimesh

    try:
        mesh = trimesh.load(io.BytesIO(data), file_type="glb", force="mesh", process=False)
    except Exception as error:  # trimesh raises many kinds on a bad file
        raise InvalidInput(f"cannot read the mesh: {error}") from error
    if not isinstance(mesh, trimesh.Trimesh) or not len(mesh.faces):
        raise InvalidInput("the mesh has no triangles")
    return mesh


def place_like(mesh: Any, reference: Any) -> Any:
    """``mesh`` moved and scaled so its bounding box has the centre and the
    largest side of ``reference``'s."""
    lo, hi = np.asarray(mesh.bounds, dtype=float)
    ref_lo, ref_hi = np.asarray(reference.bounds, dtype=float)
    scale = float((ref_hi - ref_lo).max() / max((hi - lo).max(), 1e-12))
    mesh = mesh.copy()
    mesh.apply_translation(-(lo + hi) / 2)
    mesh.apply_scale(scale)
    mesh.apply_translation((ref_lo + ref_hi) / 2)
    return mesh


def describe(mesh: Any, resolution: int) -> dict[str, Any]:
    """Metadata of a trimesh in glTF axes (Y up)."""
    bounds = np.asarray(mesh.bounds, dtype=float)
    return {
        "vertices": len(mesh.vertices),
        "triangles": len(mesh.faces),
        "watertight": bool(mesh.is_watertight),
        "bounds": bounds.round(4).tolist(),
        "resolution": resolution,
        "up_axis": "Y",
        "color": "none",
    }


class _OnGpuWhileCalled:
    """The conditioner, moved to the GPU (and everything else off it) only
    while it encodes the image."""

    def __init__(self, conditioner: Any, only: Any) -> None:
        self._conditioner = conditioner
        self._only = only

    def __call__(self, *args: Any, **kwargs: Any) -> Any:
        self._only(self._conditioner)
        return self._conditioner(*args, **kwargs)

    def to(self, device: Any) -> "_OnGpuWhileCalled":
        self._conditioner.to(device)
        return self


def take_turns_on_gpu(pipeline: Any) -> None:
    """Each part on the GPU only for its phase: the image encoders for the
    condition, a pass's transformer for its sampling, the VAE for decoding.
    Upstream's sequential CPU offload moves layer by layer every step; this
    moves each part once a phase. The 1024 transformer is materialised in RAM
    first (the pipeline would put it straight on the GPU)."""
    import torch

    gpu = torch.device("cuda")
    pipeline.transformer_1024  # noqa: B018 -- loads it while the pipeline is still on the CPU
    conditioner = pipeline.conditioner
    parts = (conditioner, pipeline.transformer_512, pipeline.transformer_1024, pipeline.vae)

    def only(*on_gpu: Any) -> None:
        for part in parts:
            part.to(gpu if any(part is p for p in on_gpu) else "cpu")
        torch.cuda.empty_cache()

    sample, decode = pipeline._sample_latents, pipeline._decode_latents

    def sample_on_gpu(transformer: Any, *args: Any, **kwargs: Any) -> Any:
        only(transformer)
        return sample(transformer, *args, **kwargs)

    def decode_on_gpu(*args: Any, **kwargs: Any) -> Any:
        only(pipeline.vae)
        return decode(*args, **kwargs)

    pipeline.conditioner = _OnGpuWhileCalled(conditioner, only)
    pipeline._sample_latents = measured("sampling", sample_on_gpu)
    pipeline._decode_latents = measured("decoding", decode_on_gpu)
    pipeline._extract_mesh = measured("mesh extraction", pipeline._extract_mesh)
    pipeline._adaptive_voxelize = measured("voxelization", pipeline._adaptive_voxelize)
    pipeline._device = gpu  # where the pipeline makes its tensors
    only()


class OutOfGpuMemory(GenerationError):
    """A phase ran out of GPU memory. Not retryable: on the one GPU the same
    input runs out the same way (a retry repeated a 9-minute failure)."""

    def __init__(self, phase: str) -> None:
        super().__init__(f"out of GPU memory in {phase}; lower the resolution")
        self.phase = phase


def measured(phase: str, method: Any) -> Any:
    """``method`` logging its peak GPU memory, and naming itself when it runs
    out of it: a 1024³ job on 8 GB fits or not depending on the shape."""
    import torch

    def run(*args: Any, **kwargs: Any) -> Any:
        torch.cuda.reset_peak_memory_stats()
        try:
            result = method(*args, **kwargs)
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if is_out_of_memory(error):
                logger.warning("out of GPU memory in %s", phase)
                raise OutOfGpuMemory(phase) from error
            raise
        logger.info("%s: peak %.2f GiB", phase, torch.cuda.max_memory_allocated() / 2**30)
        return result

    return run


class Hi3DServer(ModelServer):
    model = ModelInfo(
        id="hi3d",
        name="Hi3D v1.0",
        revision=WEIGHTS_REVISION[:12],
        license="Hi3D Open License Agreement 1.0",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
        details={
            "parameters": "2 × 1.8 B transformers (512, 1024/1536 passes), 94 M VAE",
            "precision": "as published (fp16 transformers), nothing quantised",
            "offloading": "image encoders, transformer and VAE take turns on the GPU",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._pipeline: Any = None
        self._rembg_session: Any = None
        self._max_faces = 0  # the running job's limit, read by the simplification

    def load(self) -> None:
        import rembg
        import torch

        source = source_directory()
        if not (source / "sparc3d/pipelines").is_dir():
            raise FileNotFoundError(f"Hi3D source not found in {source}; run `just setup`")
        sys.path.insert(0, str(source))
        from sparc3d import Sparc3DPipeline  # pyright: ignore[reportMissingImports]

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("Hi3D needs a CUDA GPU")
        download()
        self._pipeline = Sparc3DPipeline.from_pretrained(
            model_directory(), cache_dir=weights_directory()
        )
        take_turns_on_gpu(self._pipeline)
        postprocess = self._pipeline._postprocess_mesh

        def simplified_to_the_limit(mesh: Any, *, target_face_count: int) -> Any:
            if self._max_faces:
                target_face_count = min(target_face_count, self._max_faces)
            return postprocess(mesh, target_face_count=target_face_count)

        self._pipeline._postprocess_mesh = simplified_to_the_limit
        self._rembg_session = rembg.new_session(BACKGROUND_MODEL)
        logger.info("Hi3D v1.0 ready (parts take turns on the GPU)")

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        coarse = read_mesh(job.inputs["mesh"].data)
        image = self._prepare(job.inputs["image"].data, job.params)
        return [self._one(image, coarse, seed, job.params) for seed in job.seeds]

    def _one(self, image: Image.Image, coarse: Any, seed: int, params: Params) -> Output:
        import torch
        from sparc3d.pipelines import sparc3d_pipeline  # pyright: ignore[reportMissingImports]

        self._max_faces = params.max_faces
        budgets = sparc3d_pipeline.PASS_BUDGETS
        try:
            for attempt, share in enumerate(BUDGET_SHARES):
                # Read by the pipeline at each voxelisation (a module global).
                sparc3d_pipeline.PASS_BUDGETS = tuple(round(b * share) for b in budgets)
                try:
                    mesh = self._pipeline(
                        image,
                        coarse,
                        resolution=params.resolution,
                        num_inference_steps=params.steps,
                        seed=seed,
                    ).mesh
                    break
                except OutOfGpuMemory as error:
                    torch.cuda.empty_cache()
                    if share == BUDGET_SHARES[-1] or error.phase == "voxelization":
                        raise
                    logger.warning(
                        "retrying with %.0f %% of the voxel budget",
                        100 * BUDGET_SHARES[attempt + 1],
                    )
        except GenerationError:
            raise
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                if "empty isosurface" in str(error):
                    raise GenerationError(
                        "no surface found: is the coarse mesh the image's object?"
                    ) from error
                raise
            torch.cuda.empty_cache()
            raise GenerationError(
                "out of GPU memory; free GPU memory or lower the resolution"
            ) from error
        finally:
            sparc3d_pipeline.PASS_BUDGETS = budgets
        if params.keep_placement:
            mesh = place_like(mesh, coarse)
        meta = {**describe(mesh, params.resolution), "voxel_budget_share": share}
        return Output("model/gltf-binary", mesh.export(file_type="glb"), meta)

    def _prepare(self, data: bytes, params: Params) -> Image.Image:
        try:
            image = Image.open(io.BytesIO(data))
            image.load()
        except (UnidentifiedImageError, OSError) as error:
            raise InvalidInput(f"cannot read the image: {error}") from error
        if params.remove_background and not is_cut_out(image):
            import rembg

            image = rembg.remove(image.convert("RGB"), session=self._rembg_session)
        return image
