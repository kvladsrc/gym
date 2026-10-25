"""Declarations and the cut-out; no GPU or weights."""

import numpy as np
from birefnet_model_server.server import (
    DEFAULT_VARIANT,
    TASK,
    VARIANTS,
    VIDEO_TASK,
    BiRefNetServer,
    Params,
    cut_out,
    describe,
    inference_size,
    on_white,
)
from PIL import Image

from model_server_sdk import create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(BiRefNetServer())
    assert TASK.task == "image-to-image"
    assert TASK.prompt == "none"
    assert [spec.role for spec in TASK.inputs] == ["image"]
    assert TASK.output_mime == ("image/png",)


def test_every_variant_choice_has_weights() -> None:
    choices = Params.model_json_schema()["properties"]["variant"]["enum"]
    assert sorted(choices) == sorted(VARIANTS)
    assert DEFAULT_VARIANT in VARIANTS


def test_a_fixed_variant_sees_its_training_square() -> None:
    assert inference_size(1920, 1080, 1024) == (1024, 1024)


def test_a_dynamic_variant_keeps_the_proportions_within_the_trained_range() -> None:
    assert inference_size(1920, 1080, None) == (1920, 1088)
    assert inference_size(4000, 3000, None) == (2304, 1728)
    assert inference_size(200, 100, None) == (512, 256)


def test_the_alpha_becomes_the_transparency() -> None:
    image = Image.new("RGB", (4, 2), (200, 50, 50))
    alpha = np.zeros((2, 4), dtype=np.float32)
    alpha[:, 2:] = 1.0
    result = cut_out(image, alpha, refine=False)
    assert result.mode == "RGBA"
    assert result.getpixel((0, 0)) == (200, 50, 50, 0)
    assert result.getpixel((3, 1)) == (200, 50, 50, 255)


def test_refining_clears_the_background_tint_at_soft_edges() -> None:
    # A red object on green; its soft edge is a mix of both.
    width = 200
    alpha = np.zeros((width, width), dtype=np.float32)
    alpha[:, :100] = 1.0
    alpha[:, 100:110] = 0.5
    red, green = np.array([1.0, 0, 0]), np.array([0, 1.0, 0])
    pixels = alpha[:, :, None] * red + (1 - alpha[:, :, None]) * green
    image = Image.fromarray(np.round(pixels * 255).astype(np.uint8), "RGB")
    plain = cut_out(image, alpha, refine=False).getpixel((105, 100))
    refined = cut_out(image, alpha, refine=True).getpixel((105, 100))
    assert plain[1] > 100  # half green
    assert refined[1] < plain[1] / 2
    assert refined[0] > plain[0]


def test_the_metadata_tells_how_much_is_the_object() -> None:
    alpha = np.zeros((10, 10), dtype=np.float32)
    alpha[2:4, 3:8] = 1.0
    result = cut_out(Image.new("RGB", (10, 10)), alpha, refine=False)
    meta = describe(result, "dynamic", (32, 32))
    assert meta["object_share"] == 0.1
    assert meta["object_box"] == [3, 2, 8, 4]
    assert meta["variant"] == "dynamic"


def test_nothing_found_is_an_empty_box() -> None:
    result = cut_out(Image.new("RGB", (4, 4)), np.zeros((4, 4), dtype=np.float32), refine=True)
    meta = describe(result, "general", (1024, 1024))
    assert meta["object_share"] == 0
    assert meta["object_box"] == []


def test_a_video_is_cut_out_onto_white() -> None:
    assert VIDEO_TASK.task == "video-to-video"
    assert [spec.role for spec in VIDEO_TASK.inputs] == ["video"]
    assert VIDEO_TASK.output_mime == ("video/mp4",)
    alpha = np.zeros((2, 2), dtype=np.float32)
    alpha[0, 0] = 1.0
    frame = on_white(cut_out(Image.new("RGB", (2, 2), (10, 20, 30)), alpha, refine=False))
    assert frame.mode == "RGB"
    assert frame.getpixel((0, 0)) == (10, 20, 30)
    assert frame.getpixel((1, 1)) == (255, 255, 255)


def test_largest_only_drops_a_separate_corner_piece() -> None:
    from birefnet_model_server.server import largest_object

    alpha = np.zeros((100, 200), np.float32)
    alpha[30:90, 80:120] = 1.0  # the person
    alpha[30:90, 79] = 0.4  # its soft edge
    alpha[2:20, 2:12] = 1.0  # a logo in the corner
    kept = largest_object(alpha)
    assert kept[2:20, 2:12].max() == 0
    assert (kept[30:90, 79:120] == alpha[30:90, 79:120]).all()
    one = np.zeros((10, 10), np.float32)
    assert largest_object(one) is one
