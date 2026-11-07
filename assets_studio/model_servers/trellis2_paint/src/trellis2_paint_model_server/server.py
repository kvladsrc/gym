"""PBR texturing on an existing surface; no shape generation or simplification."""

import io
import json
import logging
import os
import sys
from pathlib import Path
from typing import Any, Literal

import numpy as np
import trimesh
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

SOURCE_REVISION = "75fbf0183001ed9876c8dbb35de6b68552ee08bd"  # pragma: allowlist secret
WEIGHTS_REVISION = "af44b45f2e35a493886929c6d786e563ec68364d"  # pragma: allowlist secret
DINO_REVISION = "3c276edd87d6f6e569ff0c4400e086807d0f3881"  # pragma: allowlist secret
WEIGHTS_REPO = "microsoft/TRELLIS.2-4B"
# Same DINOv3 checkpoint mirror used by the official Pixal3D inference script.
DINO_REPO = "camenduru/dinov3-vitl16-pretrain-lvd1689m"
MODELS = {
    "shape_slat_encoder": "ckpts/shape_enc_next_dc_f16c32_fp16",
    "tex_slat_decoder": "ckpts/tex_dec_next_dc_f16c32_fp16",
    "tex_slat_flow_model_512": "ckpts/slat_flow_imgshape2tex_dit_1_3B_512_bf16",
}
logger = logging.getLogger("trellis2_paint")


def source_directory() -> Path:
    return Path(
        os.environ.get("TRELLIS2_SOURCE", "~/.cache/assets-studio/trellis2-source")
    ).expanduser()


def weights_directory() -> Path:
    return Path(
        os.environ.get("TRELLIS2_PAINT_WEIGHTS", "~/.cache/assets-studio/trellis2-paint")
    ).expanduser()


def download() -> None:
    from huggingface_hub import snapshot_download

    root = weights_directory()
    files = [stem + suffix for stem in MODELS.values() for suffix in (".json", ".safetensors")]
    if not all((root / "weights" / file).is_file() for file in files):
        snapshot_download(
            WEIGHTS_REPO,
            revision=WEIGHTS_REVISION,
            allow_patterns=files,
            local_dir=root / "weights",
            max_workers=2,
        )
    files = ["config.json", "model.safetensors"]
    if not all((root / "dino" / file).is_file() for file in files):
        snapshot_download(
            DINO_REPO,
            revision=DINO_REVISION,
            allow_patterns=files,
            local_dir=root / "dino",
            max_workers=2,
        )


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    texture_size: Literal[1024, 2048, 4096] = Field(
        default=2048,
        description="Output texture size; the material field uses a 512 grid",
        json_schema_extra=ui(ru="Размер текстуры (внутренняя сетка материала — 512)", primary=True),
    )
    steps: int = Field(
        default=12,
        ge=1,
        le=50,
        description="Texture diffusion steps",
        json_schema_extra=ui(ru="Шаги генерации материала"),
    )
    match_colours: bool = Field(
        default=False,
        description="Shift the texture's Oklab mean and spread to the reference's; "
        "the model paints darker and drifts in hue",
        json_schema_extra=ui(ru="Подогнать цвета под картинку (модель темнит и уводит оттенок)"),
    )


TASK = TaskSpec(
    "3d-paint",
    Params,
    ("model/gltf-binary",),
    prompt="none",
    max_count=4,
    inputs=(
        InputSpec(
            role="mesh",
            mime=["model/gltf-binary"],
            description="Game mesh; its surface and existing UVs are preserved",
            labels={"ru": "Игровая сетка (поверхность и имеющиеся UV сохраняются)"},
        ),
        InputSpec(
            role="image",
            mime=["image/png"],
            description="Cut-out reference image (RGBA PNG)",
            labels={"ru": "Исходная картинка с удалённым фоном (RGBA PNG)"},
        ),
    ),
)


def read_mesh(data: bytes) -> Any:
    try:
        mesh = trimesh.load(io.BytesIO(data), file_type="glb", force="mesh", process=False)
    except Exception as error:
        raise InvalidInput(f"cannot read mesh: {error}") from error
    if not isinstance(mesh, trimesh.Trimesh) or not len(mesh.faces):
        raise InvalidInput("the input has no triangles")
    if not np.isfinite(mesh.vertices).all() or np.ptp(mesh.vertices, axis=0).max() < 1e-12:
        raise InvalidInput("the input mesh has invalid or zero-sized geometry")
    return mesh


def read_image(data: bytes) -> Image.Image:
    try:
        image = Image.open(io.BytesIO(data))
        image.load()
    except (UnidentifiedImageError, OSError) as error:
        raise InvalidInput(f"cannot read reference: {error}") from error
    if image.mode != "RGBA" or image.getchannel("A").getextrema()[0] >= 128:
        raise InvalidInput("remove the image background with BiRefNet first (RGBA PNG required)")
    if image.getchannel("A").getextrema()[1] < 204:
        raise InvalidInput("the reference has no opaque foreground")
    return image


def prepare_mesh(mesh: Any) -> tuple[Any, Any, float]:
    """Upstream's normalisation, keeping UVs and smooth normals on the copy."""
    result = mesh.copy()
    vertices, normals = result.vertices.copy(), result.vertex_normals.copy()
    lo, hi = vertices.min(axis=0), vertices.max(axis=0)
    center = (lo + hi) / 2
    scale = 0.99999 / np.max(hi - lo)
    vertices = (vertices - center) * scale
    vertices[:, 1], vertices[:, 2] = -vertices[:, 2].copy(), vertices[:, 1].copy()
    normals[:, 1], normals[:, 2] = -normals[:, 2].copy(), normals[:, 1].copy()
    result.vertices, result.vertex_normals = vertices, normals
    return result, center, float(scale)


# Oklab (Björn Ottosson): linear sRGB -> LMS, cube root, LMS' -> Lab.
RGB_TO_LMS = np.array(
    [
        [0.4122214708, 0.5363325363, 0.0514459929],
        [0.2119034982, 0.6806995451, 0.1073969566],
        [0.0883024619, 0.2817188376, 0.6299787005],
    ],
    dtype=np.float32,
)
LMS_TO_OKLAB = np.array(
    [
        [0.2104542553, 0.7936177850, -0.0040720468],
        [1.9779984951, -2.4285922050, 0.4505937099],
        [0.0259040371, 0.7827717662, -0.8086757660],
    ],
    dtype=np.float32,
)


def srgb_to_oklab(rgb: Any) -> Any:
    linear = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    return np.cbrt(linear @ RGB_TO_LMS.T) @ LMS_TO_OKLAB.T


def oklab_to_srgb(lab: Any) -> Any:
    lms = lab @ np.linalg.inv(LMS_TO_OKLAB).T
    linear = np.clip(lms**3 @ np.linalg.inv(RGB_TO_LMS).T, 0, 1)
    return np.where(linear <= 0.0031308, linear * 12.92, 1.055 * linear ** (1 / 2.4) - 0.055)


def surface_colours(mesh: Any, texture: Any, count: int = 200_000) -> Any:
    """Texture colours at area-weighted surface samples (trimesh UVs, texture rows top-down)."""
    rng = np.random.default_rng(0)
    faces = rng.choice(len(mesh.faces), size=count, p=mesh.area_faces / mesh.area)
    root, fraction = np.sqrt(rng.random(count)), rng.random(count)
    weights = np.stack([1 - root, root * (1 - fraction), root * fraction], axis=1)
    uv = np.einsum("ij,ijk->ik", weights, mesh.visual.uv[mesh.faces[faces]])
    height, width = texture.shape[:2]
    x = np.clip(uv[:, 0] * width, 0, width - 1).astype(int)
    y = np.clip((1 - uv[:, 1]) * height, 0, height - 1).astype(int)
    return texture[y, x]


MAX_SPREAD_SCALE = 3.0


def match_colours(mesh: Any, reference: Image.Image) -> dict[str, Any]:
    """Move the base colour's Oklab mean and spread over the surface to the reference foreground's.

    TRELLIS.2 paints darker than its reference, shifts the hue and, from an unlit
    reference, oversaturates; classifier-free guidance does not change that.
    The spread grows at most MAX_SPREAD_SCALE times: a near-flat paint (on low-poly
    meshes the model paints almost one colour) has only noise to stretch.
    """
    pixels = np.asarray(reference.convert("RGBA"), dtype=np.float32) / 255
    target = srgb_to_oklab(pixels[pixels[..., 3] >= 0.8][:, :3])
    texture = np.asarray(mesh.visual.material.baseColorTexture.convert("RGBA"), np.float32) / 255
    source = srgb_to_oklab(surface_colours(mesh, texture[..., :3]))
    spread = source.std(axis=0)
    ratio = target.std(axis=0) / np.maximum(spread, 1e-3)
    scale = np.where(spread > 1e-3, np.minimum(ratio, MAX_SPREAD_SCALE), 1.0)
    lab = (srgb_to_oklab(texture[..., :3]) - source.mean(axis=0)) * scale + target.mean(axis=0)
    texture[..., :3] = oklab_to_srgb(lab)
    mesh.visual.material.baseColorTexture = Image.fromarray(
        np.round(texture * 255).astype(np.uint8), "RGBA"
    )
    return {
        "reference_oklab_mean": target.mean(axis=0).round(4).tolist(),
        "texture_oklab_mean": source.mean(axis=0).round(4).tolist(),
        "spread_scale": np.round(scale, 3).tolist(),
    }


def verify_surface(reference: Any, output: Any) -> float:
    """UV unwrapping may duplicate/reorder vertices, but may not change triangles."""
    from scipy.spatial import cKDTree

    if len(output.faces) != len(reference.faces):
        raise GenerationError("texturing changed the triangle count")
    size = float(np.ptp(reference.vertices, axis=0).max())
    left = reference.vertices[reference.faces] / size
    right = output.vertices[output.faces] / size
    worst = 0.0
    for a, b in ((left, right), (right, left)):
        indices = cKDTree(b.mean(axis=1)).query(a.mean(axis=1))[1]
        distances = np.linalg.norm(a[:, :, None, :] - b[indices, None, :, :], axis=-1)
        worst = max(worst, float(distances.min(axis=2).max()))
    if not np.isfinite(worst) or worst > 1e-5:
        raise GenerationError(f"texturing changed the surface (relative error {worst})")
    return worst


class TrellisPaintServer(ModelServer):
    model = ModelInfo(
        id="trellis2-paint",
        name="TRELLIS.2 Paint",
        revision=WEIGHTS_REVISION[:12],
        license="MIT",
        source="https://github.com/microsoft/TRELLIS.2",
        details={
            "mode": "Texture only, 512 grid; keeps the input surface",
            "offloading": "image encoder, mesh encoder, transformer and decoder take turns on GPU",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self.pipeline: Any = None
        self.peaks: dict[str, float] = {}
        self.center: Any = None
        self.scale = 1.0

    def load(self) -> None:
        import torch

        sys.path.insert(0, str(source_directory()))
        from trellis2 import models
        from trellis2.modules.image_feature_extractor import DinoV3FeatureExtractor
        from trellis2.pipelines import samplers
        from trellis2.pipelines.trellis2_texturing import Trellis2TexturingPipeline

        torch.set_grad_enabled(False)
        download()
        args = json.loads(Path(__file__).with_name("texturing_pipeline.json").read_text())["args"]
        root = weights_directory()
        parts = {
            key: models.from_pretrained(str(root / "weights" / value))
            for key, value in MODELS.items()
        }
        spec = args["tex_slat_sampler"]
        self.pipeline = Trellis2TexturingPipeline(
            models=parts,
            tex_slat_sampler=getattr(samplers, spec["name"])(**spec["args"]),
            tex_slat_sampler_params=spec["params"],
            shape_slat_normalization=args["shape_slat_normalization"],
            tex_slat_normalization=args["tex_slat_normalization"],
            image_cond_model=DinoV3FeatureExtractor(str(root / "dino")),
            rembg_model=None,
            low_vram=True,
        )
        self.pipeline.cuda()

        def prepare(mesh: Any) -> Any:
            output, self.center, self.scale = prepare_mesh(mesh)
            return output

        self.pipeline.preprocess_mesh = prepare
        for name in (
            "get_cond",
            "encode_shape_slat",
            "sample_tex_slat",
            "decode_tex_slat",
            "postprocess_mesh",
        ):
            original = getattr(self.pipeline, name)

            def measured(*a: Any, _method: Any = original, _name: str = name, **kw: Any) -> Any:
                torch.cuda.empty_cache()
                torch.cuda.reset_peak_memory_stats()
                logger.info("%s begin", _name)
                result = _method(*a, **kw)
                self.peaks[_name] = round(torch.cuda.max_memory_allocated() / 2**30, 3)
                logger.info("%s peak %.3f GiB", _name, self.peaks[_name])
                return result

            setattr(self.pipeline, name, measured)

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        reference, image = read_mesh(job.inputs["mesh"].data), read_image(job.inputs["image"].data)
        return [self._one(reference, image, seed, job.params) for seed in job.seeds]

    def _one(self, reference: Any, image: Image.Image, seed: int, params: Params) -> Output:
        import torch

        self.peaks.clear()
        try:
            output = self.pipeline.run(
                reference,
                image,
                seed=seed,
                resolution=512,
                texture_size=params.texture_size,
                tex_slat_sampler_params={"steps": params.steps},
            )
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if is_out_of_memory(error):
                raise GenerationError("TRELLIS.2 paint ran out of GPU memory") from error
            raise
        colours = match_colours(output, image) if params.match_colours else None
        normals = output.vertex_normals.copy()
        output.vertices = output.vertices / self.scale + self.center
        output.vertex_normals = normals
        error = verify_surface(reference, output)
        return Output(
            "model/gltf-binary",
            output.export(file_type="glb"),
            {
                "triangles": len(output.faces),
                "vertices": len(output.vertices),
                "resolution": 512,
                "texture_size": params.texture_size,
                "steps": params.steps,
                "colour_match": colours,
                "surface_error_relative": error,
                "placement_restored": True,
                "existing_uv_preserved": getattr(reference.visual, "uv", None) is not None,
                "phase_peak_torch_gib": dict(self.peaks),
                "up_axis": "Y",
                "materials": "PBR",
                "source_revision": SOURCE_REVISION,
                "weights_revision": WEIGHTS_REVISION,
                "dino_revision": DINO_REVISION,
            },
        )
