"""Selection, original-pixel compositing and validation without model weights."""

import io

import numpy as np
import pytest
from PIL import Image, ImageDraw
from sam2_model_server.server import (
    TASK,
    Sam2Server,
    composite,
    read_video,
    reference_prompts,
    remove_speckles,
    touches_edge,
)

from model_server_sdk import InvalidInput, create_app


def reference():
    image = Image.new("RGB", (200, 160), "white")
    ImageDraw.Draw(image).rectangle((30, 20, 70, 140), fill=(15, 20, 30))
    # Detached sword fragment must not move the body's box to the opponent.
    ImageDraw.Draw(image).rectangle((100, 80, 180, 81), fill="black")
    return image


def test_contract_is_a_regular_video_task_with_reference():
    create_app(Sam2Server())
    assert TASK.task == "video-to-video"
    assert [spec.role for spec in TASK.inputs] == ["video", "reference"]
    assert TASK.output_mime == ("video/mp4",)


def test_reference_scales_to_original_coordinates_and_ignores_detached_blade():
    box, points = reference_prompts(reference(), (400, 320))
    assert box == [60, 40, 142, 282]
    assert len(points) >= 4
    assert all(60 < x < 142 and 40 < y < 282 for x, y in points)


def test_transparent_reference_can_contain_a_white_subject():
    image = Image.new("RGBA", (200, 160), (255, 255, 255, 0))
    ImageDraw.Draw(image).rectangle((30, 20, 70, 140), fill=(255, 255, 255, 255))
    box, _ = reference_prompts(image, image.size)
    assert box == [30, 20, 71, 141]


@pytest.mark.parametrize("color", ["white", "black"])
def test_empty_or_background_filled_reference_is_rejected(color):
    with pytest.raises(InvalidInput, match=r"foreground|one subject"):
        reference_prompts(Image.new("RGB", (200, 160), color), (200, 160))


def test_reframed_reference_is_rejected():
    with pytest.raises(InvalidInput, match="aspect ratio"):
        reference_prompts(reference(), (160, 200))


def test_composite_uses_original_pixels_and_white_only():
    pixels = np.array([[[2, 3, 4], [120, 90, 14]], [[17, 81, 92], [4, 2, 1]]], dtype=np.uint8)
    mask = np.array([[True, False], [False, True]])
    result = np.frombuffer(composite(Image.fromarray(pixels), mask), dtype=np.uint8).reshape(
        2, 2, 3
    )
    np.testing.assert_array_equal(result[mask], pixels[mask])
    assert np.all(result[~mask] == 255)


def test_invalid_video_is_rejected_before_loading_model():
    with pytest.raises(InvalidInput, match=r"frame rate|cannot read"):
        read_video(b"not a video")


def test_reference_from_png_does_not_require_a_generated_alpha():
    buffer = io.BytesIO()
    reference().save(buffer, format="PNG")
    assert reference_prompts(Image.open(io.BytesIO(buffer.getvalue())), (200, 160))[0][0] == 30


def test_mask_dust_is_removed_but_detached_hand_survives():
    mask = np.zeros((100, 100), dtype=bool)
    mask[20:80, 20:60] = True
    mask[30:40, 70:80] = True
    mask[5:7, 5:7] = True
    clean = remove_speckles(mask)
    assert clean[20:80, 20:60].all()
    assert clean[30:40, 70:80].all()
    assert not clean[5:7, 5:7].any()


def test_incomplete_reference_is_rejected_for_motion_capture():
    image = Image.new("RGB", (200, 160), "white")
    ImageDraw.Draw(image).rectangle((80, 60, 110, 88), fill="black")
    with pytest.raises(InvalidInput, match="too small or incomplete"):
        reference_prompts(image, image.size)
    assert reference_prompts(image, image.size, require_full_subject=False)[0] == [80, 60, 111, 89]


def test_clipped_body_is_detected_but_a_thin_sword_tip_is_allowed():
    mask = np.zeros((100, 100), dtype=bool)
    mask[20:80, 0:60] = True
    assert touches_edge(mask)
    mask[:, 0] = False
    mask[50, 0] = True
    assert not touches_edge(mask)
