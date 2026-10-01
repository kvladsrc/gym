"""Image helpers shared by image model servers (needs Pillow: the "images" extra).

Diffusion models want inputs of about one megapixel, sides multiples of 64,
and in aspect ratios they were trained on; users upload whatever they have.
"""

import io
import logging
import math
import struct

from PIL import Image, ImageOps, UnidentifiedImageError

from model_server_sdk.server import InvalidInput

logger = logging.getLogger("model_server_sdk.images")

# Sizes of about one megapixel in the aspect ratios SDXL and FLUX were trained on.
RESOLUTIONS = {
    "1024x1024": (1024, 1024),
    "1152x896": (1152, 896),
    "896x1152": (896, 1152),
    "1216x832": (1216, 832),
    "832x1216": (832, 1216),
    "1344x768": (1344, 768),
    "768x1344": (768, 1344),
}
_TARGET_PIXELS = 1024 * 1024
_MULTIPLE = 64
# Beyond this the image is cropped: the models have not seen narrower images,
# and the 64-pixel minimum side would otherwise inflate the pixel count.
MAX_ASPECT = 4.0
_ALPHA_MODES = {"RGBA", "LA", "PA", "RGBa", "La"}


def fit_input(width: int, height: int) -> tuple[int, int]:
    """Size for an image-to-image input: its aspect ratio (clamped to
    1:4…4:1) at about one megapixel, both sides multiples of 64."""
    if width <= 0 or height <= 0:
        raise ValueError("image has no pixels")
    aspect = min(max(width / height, 1 / MAX_ASPECT), MAX_ASPECT)
    ideal_height = math.sqrt(_TARGET_PIXELS / aspect)
    return (
        max(_MULTIPLE, round(ideal_height * aspect / _MULTIPLE) * _MULTIPLE),
        max(_MULTIPLE, round(ideal_height / _MULTIPLE) * _MULTIPLE),
    )


def denoising_steps(steps: int, strength: float) -> int:
    """Steps SDXL image-to-image really runs (``int(steps * strength)``).
    Other pipelines round differently: FLUX runs at least one step."""
    return int(steps * strength)


def min_steps(strength: float) -> int:
    """The fewest steps that denoise at least once at this strength."""
    return math.ceil(1 / strength - 1e-9)


def check_denoising(steps: int, strength: float) -> int:
    """Denoising steps for image-to-image; InvalidInput if there would be none."""
    denoising = denoising_steps(steps, strength)
    if denoising < 1:
        raise InvalidInput(
            f"at strength {strength} {steps} steps denoise nothing; "
            f"use at least {min_steps(strength)} steps"
        )
    return denoising


def open_image(data: bytes) -> Image.Image:
    """Decode an input as RGB: upright (EXIF), transparency composited on white."""
    try:
        image = Image.open(io.BytesIO(data))
        image.load()
    except (UnidentifiedImageError, OSError, Image.DecompressionBombError) as error:
        raise InvalidInput(f"cannot read the image: {error}") from error
    try:
        image = ImageOps.exif_transpose(image)
    except (SyntaxError, ValueError, KeyError, TypeError, struct.error):
        logger.warning("ignoring malformed EXIF orientation")
    if image.mode in _ALPHA_MODES or "transparency" in image.info:
        # Palette and grey images with alpha too: dropping alpha would leave
        # black where the image is transparent.
        rgba = image.convert("RGBA")
        background = Image.new("RGB", rgba.size, (255, 255, 255))
        background.paste(rgba, mask=rgba.getchannel("A"))
        return background
    return image.convert("RGB")


def prepare_input(data: bytes) -> Image.Image:
    """An image-to-image input: decoded, cropped to a supported aspect ratio
    and scaled to about a megapixel."""
    image = open_image(data)
    return ImageOps.fit(image, fit_input(*image.size), Image.Resampling.LANCZOS)


def encode_png(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    return buffer.getvalue()
