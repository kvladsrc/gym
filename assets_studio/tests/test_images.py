"""model_server_sdk.images: input preparation shared by image model servers."""

import io
from typing import Any

import pytest
from model_server_sdk.images import (
    RESOLUTIONS,
    check_denoising,
    denoising_steps,
    fit_input,
    min_steps,
    open_image,
    prepare_input,
)
from PIL import Image

from model_server_sdk import InvalidInput

MEGAPIXEL = 1024 * 1024


@pytest.mark.parametrize(
    ("size", "expected"),
    [((512, 512), (1024, 1024)), ((1920, 1080), (1344, 768)), ((1080, 1920), (768, 1344))],
)
def test_inputs_keep_their_aspect_at_about_a_megapixel(
    size: tuple[int, int], expected: tuple[int, int]
) -> None:
    assert fit_input(*size) == expected


@pytest.mark.parametrize("size", [(1, 2000), (2000, 1), (1, 10000), (333, 777), (256, 1024)])
def test_extreme_aspect_ratios_are_clamped(size: tuple[int, int]) -> None:
    width, height = fit_input(*size)
    assert width % 64 == 0
    assert height % 64 == 0
    assert width * height <= 1.1 * MEGAPIXEL
    assert 1 / 4.5 <= width / height <= 4.5


def test_resolutions_are_multiples_of_64() -> None:
    for width, height in RESOLUTIONS.values():
        assert width % 64 == 0
        assert height % 64 == 0


@pytest.mark.parametrize(
    ("steps", "strength", "runs"), [(30, 0.5, 15), (3, 0.3, 0), (1, 0.5, 0), (4, 0.25, 1)]
)
def test_denoising_steps(steps: int, strength: float, runs: int) -> None:
    assert denoising_steps(steps, strength) == runs
    assert denoising_steps(min_steps(strength), strength) >= 1


def test_no_denoising_is_an_input_error() -> None:
    assert check_denoising(4, 0.6) == 2
    with pytest.raises(InvalidInput, match="use at least 4 steps"):
        check_denoising(3, 0.3)


def encode(image: Image.Image, **options: Any) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", **options)
    return buffer.getvalue()


@pytest.mark.parametrize("mode", ["RGBA", "LA"])
def test_transparency_becomes_white(mode: str) -> None:
    image = Image.new(mode, (4, 4), 0)  # fully transparent black
    assert open_image(encode(image)).getpixel((0, 0)) == (255, 255, 255)


def test_palette_transparency_becomes_white() -> None:
    image = Image.new("P", (4, 4), 0)
    image.putpalette([0, 0, 0] * 256)
    assert open_image(encode(image, transparency=0)).getpixel((0, 0)) == (255, 255, 255)


def test_opaque_images_keep_their_colour() -> None:
    assert open_image(encode(Image.new("RGB", (4, 4), (10, 20, 30)))).getpixel((0, 0)) == (
        10,
        20,
        30,
    )


def test_exif_orientation_is_applied() -> None:
    image = Image.new("RGB", (4, 2), (0, 0, 0))
    exif = Image.Exif()
    exif[0x0112] = 6  # rotate 90° clockwise to display
    buffer = io.BytesIO()
    image.save(buffer, format="JPEG", exif=exif)
    assert open_image(buffer.getvalue()).size == (2, 4)


def test_invalid_image_is_an_input_error() -> None:
    with pytest.raises(InvalidInput, match="cannot read"):
        open_image(b"not an image")


def test_prepared_input_is_cropped_and_scaled() -> None:
    assert prepare_input(encode(Image.new("RGB", (1, 2000)))).size == fit_input(1, 2000)
