"""PBRFusion 4 model server: image-to-image (contract v1).

PBRFusion 4 (NightRaven109) reads the relief of a flat material texture: a
height map, and from it a tangent-space normal map. It is Lotus-D (He et al.,
2024): the UNet of Stable Diffusion 2 turned into a one-step predictor. The
image's latent goes in, the height's latent comes out; there is no noise and
no sampling, so the result does not depend on the seed.

It is made for textures seen straight on (ground, walls, bark, cloth), not for
pictures of objects or scenes. It predicts the height only: roughness and
metalness are not its outputs.

The normals follow the model's reference (the ComfyUI node of its authors):
the height is smoothed by a bilateral filter, a share of the image's own
brightness is mixed in for the finest detail, and Sobel slopes give the
vectors. Three things differ from the reference:

- everything after the model works on floating-point heights rather than
  8-bit ones, so gentle slopes have no steps;
- the strength is tied to the texture's size, not to its pixels: the same
  texture at 2048 and at 1024 pixels gets the same slopes;
- a texture that tiles can keep tiling (``tileable``): the model sees it with
  a wrapped border, the two predictions of each border strip are cross-faded,
  and the filters wrap around.

The reference also samples the VAE's latent; here its mean is taken, which
makes the result exactly repeatable (the spread is far below one grey level).

Licence: Apache-2.0 (weights and the reference code).
"""

import gc
import json
import logging
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Literal

import numpy as np
from model_server_sdk.images import encode_png, open_image
from PIL import Image
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

logger = logging.getLogger("pbrfusion4_model_server")

REPO = "NightRaven109/PBRFusion4"
# A Git revision, not a secret.
REVISION = "2bcb437856eec9fe18514c97d462d58b86e82b87"  # pragma: allowlist secret
# One file: the UNet, the VAE and the text encoder under their prefixes, their
# configurations and the tokenizer's files as its metadata.
WEIGHTS = "PBRFusion4.safetensors"

# Lotus' single step: the last timestep, and its task switch (1, 0) given to
# the UNet as sines and cosines.
TIMESTEP = 999
TASK_SWITCH = (1.0, 0.0)

# The VAE halves the image three times and the UNet the latent three more.
MULTIPLE = 64
# As the reference: smaller images are enlarged to this shortest side.
MIN_SIDE = 384
MAX_SIDE = 2048
# The side at which the reference's pixel constants below apply.
REFERENCE_SIDE = 1024
# The reference's bilateral filter of the height (diameter and spatial sigma
# in pixels, the range sigma as a share of the height's range).
SMOOTH_DIAMETER = 10
SMOOTH_SIGMA_SPACE = 50.0
SMOOTH_SIGMA_RANGE = 50 / 255
# The reference's factor on the Sobel slopes.
SLOPE_SCALE = 2.0
# A tileable image is shown to the model with this share of its side wrapped
# around on each side.
WRAP_SHARE = 1 / 8


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    output: Literal["normal", "height"] = Field(
        default="normal",
        description="The map to return: normal: a tangent-space normal map; "
        "height: the height the model predicts (white is high)",
        json_schema_extra=ui(
            ru="Какую карту вернуть: normal — карта нормалей; "
            "height — карта высот от модели (белое выше)",
            primary=True,
        ),
    )
    tileable: bool = Field(
        default=False,
        description="The image tiles: keep the map's opposite edges meeting",
        json_schema_extra=ui(
            ru="Картинка повторяется без швов: сохранить стыковку краёв карты",
            primary=True,
        ),
    )
    strength: float = Field(
        default=1.0,
        ge=0.1,
        le=10,
        description="Steepness of the normals; 1 is the model's reference for a 1024 px "
        "texture and does not depend on the image's size",
        json_schema_extra=ui(
            ru="Крутизна нормалей; 1 — как у авторов модели для текстуры 1024 пикселя, "
            "от размера картинки не зависит",
            primary=True,
        ),
    )
    brightness_mix: float = Field(
        default=0.3,
        ge=0,
        le=1,
        description="Share of the image's own brightness mixed into the height before the "
        "normals: finer detail, but painted light and dark turn into relief; 0: the "
        "model's height alone",
        json_schema_extra=ui(
            ru="Доля яркости самой картинки, подмешанная в высоту перед нормалями: "
            "больше мелких деталей, но нарисованные свет и тень становятся рельефом; "
            "0 — только высота от модели"
        ),
    )
    convention: Literal["opengl", "directx"] = Field(
        default="opengl",
        description="Green channel of the normals: opengl: up (Unity, Blender, glTF); "
        "directx: down (Unreal)",
        json_schema_extra=ui(
            ru="Зелёный канал нормалей: opengl — вверх (Unity, Blender, glTF); "
            "directx — вниз (Unreal)"
        ),
    )
    size: int = Field(
        default=REFERENCE_SIDE,
        ge=MIN_SIDE,
        le=MAX_SIDE,
        description="The longest side the model sees, px; the map keeps the image's own "
        "size. The model's authors use 1024",
        json_schema_extra=ui(
            ru="Наибольшая сторона, которую видит модель, в пикселях; карта остаётся "
            "размера самой картинки. Авторы модели используют 1024"
        ),
    )


TASK = TaskSpec(
    KnownTask.IMAGE_TO_IMAGE,
    Params,
    ("image/png",),
    prompt="none",
    inputs=(InputSpec(role="image", mime=["image/png", "image/jpeg", "image/webp"]),),
    # The maps do not depend on the seed.
    max_count=1,
)


def download() -> str:
    """The weights' path, downloaded on first use."""
    from huggingface_hub import hf_hub_download

    return hf_hub_download(REPO, WEIGHTS, revision=REVISION)


def model_size(width: int, height: int, size: int) -> tuple[int, int]:
    """The size the model sees: the image's proportions, the longest side at
    most ``size`` and the shortest at least MIN_SIDE, sides multiples of 64."""
    scale = min(1.0, size / max(width, height))
    scale = max(scale, MIN_SIDE / min(width, height))
    fitted = (
        max(MULTIPLE, round(width * scale / MULTIPLE) * MULTIPLE),
        max(MULTIPLE, round(height * scale / MULTIPLE) * MULTIPLE),
    )
    if max(fitted) > MAX_SIDE:
        raise InvalidInput(
            f"the image is too elongated ({width}x{height}): with its shortest side at "
            f"{MIN_SIDE} px the longest exceeds {MAX_SIDE} px"
        )
    return fitted


def wrap_margin(side: int) -> int:
    """How many model pixels of a tileable image's side are wrapped around on each side."""
    return MULTIPLE * max(1, round(side * WRAP_SHARE / MULTIPLE))


def wrap_pad(pixels: np.ndarray, margin_x: int, margin_y: int) -> np.ndarray:
    """An H×W(×C) image continued by its own opposite sides."""
    rest = ((0, 0),) * (pixels.ndim - 2)
    return np.pad(pixels, ((margin_y, margin_y), (margin_x, margin_x), *rest), mode="wrap")


def _fold_axis(padded: np.ndarray, margin: int, axis: int) -> np.ndarray:
    values = np.moveaxis(padded, axis, 0).astype(np.float32)
    length = values.shape[0] - 2 * margin
    core = values[margin : margin + length].copy()
    # The share of the copy predicted in the border: none where the border
    # touches the padded image's edge, half where it meets the image.
    share = ((np.arange(margin, dtype=np.float32) + 0.5) / (2 * margin)).reshape(
        (margin,) + (1,) * (values.ndim - 1)
    )
    core[length - margin :] = (1 - share) * core[length - margin :] + share * values[:margin]
    share = share[::-1]
    core[:margin] = (1 - share) * core[:margin] + share * values[margin + length :]
    return np.moveaxis(core, 0, axis)


def fold(padded: np.ndarray, margin_x: int, margin_y: int) -> np.ndarray:
    """The image's own part of a prediction made on its wrap-padded copy, the
    opposite edges meeting. Each strip beside an edge was predicted twice: in
    the image and in the border on the other side. Across the seam the two
    run on without a break, so cross-fading them along the strip leaves no
    step where the tiles join."""
    return _fold_axis(_fold_axis(padded, margin_x, 1), margin_y, 0)


def resize(values: np.ndarray, width: int, height: int, *, wrap: bool) -> np.ndarray:
    """An H×W float32 map at another size (bicubic); ``wrap``: its opposite
    edges are neighbours."""
    import cv2

    if values.shape == (height, width):
        return values
    columns = (np.arange(width, dtype=np.float32) + 0.5) * (values.shape[1] / width) - 0.5
    rows = (np.arange(height, dtype=np.float32) + 0.5) * (values.shape[0] / height) - 0.5
    map_x, map_y = np.meshgrid(columns, rows)
    return cv2.remap(
        values,
        map_x,
        map_y,
        cv2.INTER_CUBIC,
        borderMode=cv2.BORDER_WRAP if wrap else cv2.BORDER_REPLICATE,
    )


def normalise(values: np.ndarray) -> np.ndarray:
    """The map stretched to 0…1; a flat one becomes zeros."""
    low, high = float(values.min()), float(values.max())
    if high - low < 1e-6:
        return np.zeros_like(values, dtype=np.float32)
    return ((values - low) / (high - low)).astype(np.float32)


def smooth(height: np.ndarray, *, wrap: bool) -> np.ndarray:
    """The reference's bilateral filter: the model's fine noise goes, the
    steps between stones stay. Its window follows the map's size."""
    import cv2

    scale = max(height.shape) / REFERENCE_SIDE
    diameter = max(3, round(SMOOTH_DIAMETER * scale))
    padded = np.pad(height, diameter, mode="wrap" if wrap else "reflect")
    filtered = cv2.bilateralFilter(padded, diameter, SMOOTH_SIGMA_RANGE, SMOOTH_SIGMA_SPACE * scale)
    return filtered[diameter:-diameter, diameter:-diameter]


def brightness(image: Image.Image) -> np.ndarray:
    """The image's brightness as the reference takes it (HSV value), H×W float32 in 0…1."""
    return np.asarray(image.convert("RGB"), dtype=np.float32).max(axis=2) / 255


def normals(height: np.ndarray, strength: float, *, wrap: bool) -> np.ndarray:
    """Unit normals (H×W×3: x to the right, y up, z out of the surface) of a
    height map in 0…1 where white is high. The slopes are per share of the
    longest side, not per pixel."""
    import cv2

    padded = np.pad(height.astype(np.float32), 1, mode="wrap" if wrap else "edge")
    # Sobel: eight times the slope per pixel; rows go down.
    to_right = cv2.Sobel(padded, cv2.CV_32F, 1, 0, ksize=3)[1:-1, 1:-1]
    downwards = cv2.Sobel(padded, cv2.CV_32F, 0, 1, ksize=3)[1:-1, 1:-1]
    factor = strength * SLOPE_SCALE * max(height.shape) / REFERENCE_SIDE
    vectors = np.dstack([-factor * to_right, factor * downwards, np.ones_like(to_right)])
    return vectors / np.linalg.norm(vectors, axis=2, keepdims=True)


def normal_picture(vectors: np.ndarray, convention: str) -> Image.Image:
    """Normals as colours: flat is (128, 128, 255); DirectX has green pointing down."""
    if convention == "directx":
        vectors = vectors * np.array([1, -1, 1], dtype=np.float32)
    return Image.fromarray(np.round((vectors + 1) * 127.5).astype(np.uint8), "RGB")


def height_picture(height: np.ndarray) -> Image.Image:
    return Image.fromarray(np.round(np.clip(height, 0, 1) * 255).astype(np.uint8), "L")


def tilt_degrees(vectors: np.ndarray) -> np.ndarray:
    return np.degrees(np.arccos(np.clip(vectors[:, :, 2], -1, 1)))


@dataclass
class Model:
    unet: Any
    vae: Any
    # The empty prompt's embedding: Lotus was trained with it.
    prompt: Any
    task: Any
    device: str
    dtype: Any


def load_model(path: str, device: str, dtype: Any) -> Model:
    """The networks from the single weights file, straight in ``dtype`` on
    ``device``: tensor by tensor, without a second copy in RAM."""
    import torch
    from accelerate import init_empty_weights
    from diffusers import AutoencoderKL, UNet2DConditionModel
    from safetensors import safe_open
    from transformers import CLIPTextConfig, CLIPTextModel, CLIPTokenizer

    with safe_open(path, framework="pt", device="cpu") as file:
        metadata = file.metadata() or {}
        names = list(file.keys())

        def filled(module: Any, prefix: str) -> Any:
            state = {
                name[len(prefix) :]: file.get_tensor(name).to(device, dtype)
                for name in names
                if name.startswith(prefix)
            }
            module.load_state_dict(state, assign=True)
            # Buffers that are not stored (position ids) follow the weights.
            return module.to(device).eval()

        try:
            with init_empty_weights():
                unet = UNet2DConditionModel.from_config(json.loads(metadata["unet/config.json"]))
                vae = AutoencoderKL.from_config(json.loads(metadata["vae/config.json"]))
                text_encoder = CLIPTextModel(
                    CLIPTextConfig(**json.loads(metadata["text_encoder/config.json"]))
                )
        except KeyError as error:
            raise RuntimeError(f"{path}: no {error.args[0]} in the file's metadata") from error
        unet = filled(unet, "unet.")
        vae = filled(vae, "vae.")
        text_encoder = filled(text_encoder, "text_encoder.")

    with tempfile.TemporaryDirectory() as folder:
        for name, text in metadata.items():
            if name.startswith("tokenizer/"):
                Path(folder, name.partition("/")[2]).write_text(text, encoding="utf-8")
        tokenizer = CLIPTokenizer.from_pretrained(folder)
    tokens = tokenizer(
        "",
        padding="max_length",
        max_length=tokenizer.model_max_length,
        truncation=True,
        return_tensors="pt",
    ).input_ids
    prompt = text_encoder(tokens.to(device))[0].to(dtype)
    del text_encoder
    switch = torch.tensor([TASK_SWITCH], device=device)
    task = torch.cat([torch.sin(switch), torch.cos(switch)], dim=-1).to(dtype)
    return Model(unet=unet, vae=vae, prompt=prompt, task=task, device=device, dtype=dtype)


def predict(model: Model, pixels: np.ndarray) -> np.ndarray:
    """The model's answer for an H×W×3 float32 image in 0…1 (sides multiples
    of the networks' strides): H×W float32, white high; not yet stretched."""
    import torch

    batch = torch.from_numpy(np.ascontiguousarray(pixels)).permute(2, 0, 1)[None]
    batch = batch.to(model.device, model.dtype) * 2 - 1
    scaling = model.vae.config.scaling_factor
    latent = model.vae.encode(batch).latent_dist.mode() * scaling
    timestep = torch.tensor(TIMESTEP, device=model.device)
    predicted = model.unet(
        latent,
        timestep,
        encoder_hidden_states=model.prompt,
        class_labels=model.task,
        return_dict=False,
    )[0]
    decoded = model.vae.decode(predicted / scaling, return_dict=False)[0]
    # The three channels are one grey picture; their mean, as the reference.
    return (decoded.float() / 2 + 0.5).clamp(0, 1).mean(dim=1)[0].cpu().numpy()


def read_height(
    model: Model, image: Image.Image, size: int, *, tileable: bool
) -> tuple[np.ndarray, dict[str, Any]]:
    """The image's height map at its own size, H×W float32 in 0…1, and what
    the model saw."""
    width, height = model_size(*image.size, size)
    resized = image.convert("RGB").resize((width, height), Image.Resampling.LANCZOS)
    pixels = np.asarray(resized, dtype=np.float32) / 255
    seen: dict[str, Any] = {"model_size": [width, height]}
    if tileable:
        margin_x, margin_y = wrap_margin(width), wrap_margin(height)
        raw = fold(predict(model, wrap_pad(pixels, margin_x, margin_y)), margin_x, margin_y)
        seen["wrap_margin"] = [margin_x, margin_y]
    else:
        raw = predict(model, pixels)
    if not np.isfinite(raw).all():
        # Half precision overflowing somewhere in the networks; never seen, but a grey
        # picture of it would pass for a flat height.
        raise GenerationError("the model returned values that are not numbers")
    relief = resize(normalise(raw), *image.size, wrap=tileable)
    return np.clip(relief, 0, 1), seen


def render(height: np.ndarray, image: Image.Image, params: Params) -> tuple[Image.Image, dict]:
    """The requested map of a height map at the image's size, and its numbers."""
    if params.output == "height":
        return height_picture(height), {}
    relief = smooth(height, wrap=params.tileable)
    if params.brightness_mix > 0:
        mix = params.brightness_mix
        relief = (1 - mix) * relief + mix * brightness(image)
    vectors = normals(relief, params.strength, wrap=params.tileable)
    tilt = tilt_degrees(vectors)
    meta = {
        # How far the normals lean from straight up.
        "tilt_mean_deg": round(float(tilt.mean()), 2),
        "tilt_p99_deg": round(float(np.percentile(tilt, 99)), 2),
    }
    return normal_picture(vectors, params.convention), meta


class PBRFusion4Server(ModelServer):
    model = ModelInfo(
        id="pbrfusion4",
        name="PBRFusion 4",
        revision=REVISION[:12],
        license="Apache-2.0",
        source="https://huggingface.co/NightRaven109/PBRFusion4",
        details={
            "parameters": "0.87 B (the UNet of Stable Diffusion 2, one step)",
            "precision": "fp16",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._model: Model | None = None

    def load(self) -> None:
        import torch

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("PBRFusion 4 needs a CUDA GPU")
        self._model = load_model(download(), "cuda", torch.float16)
        logger.info("PBRFusion 4 ready")

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        assert self._model is not None
        params = job.params
        image = open_image(job.inputs["image"].data)
        try:
            height, seen = read_height(self._model, image, params.size, tileable=params.tileable)
        except RuntimeError as error:
            if not is_out_of_memory(error):
                raise
            raise GenerationError(
                "out of GPU memory; free GPU memory or pick a smaller size", retryable=True
            ) from error
        finally:
            self._free()
        picture, numbers = render(height, image, params)
        meta = {"output": params.output, "revision": REVISION[:12], **seen, **numbers}
        data = encode_png(picture)
        # One output: the maps do not depend on the seed.
        return [Output("image/png", data, meta) for _ in job.seeds]

    @staticmethod
    def _free() -> None:
        import torch

        gc.collect()
        torch.cuda.empty_cache()
