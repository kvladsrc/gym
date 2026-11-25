"""Declarations, the video and reference checks, selection and compositing, and the
adaptation of upstream's model; no GPU or weights."""

from types import SimpleNamespace

import numpy as np
import pytest
from model_server_sdk.video import encode_mp4
from PIL import Image, ImageDraw
from sam3_model_server.server import (
    FILES,
    MAX_PROMPT_LENGTH,
    REPO,
    REVISION,
    TASK,
    Sam3Server,
    adapt,
    best_match,
    cached,
    check_prompt,
    composite,
    reference_mask,
    reference_prompts,
    remove_speckles,
    touches_edge,
    unpack_video,
)

from model_server_sdk import InvalidInput, Job, create_app


def reference(size=(200, 160)):
    image = Image.new("RGB", size, "white")
    ImageDraw.Draw(image).rectangle((30, 20, 70, 140), fill=(15, 20, 30))
    # A detached sword fragment must not move the body's box to the opponent.
    ImageDraw.Draw(image).rectangle((100, 80, 180, 81), fill="black")
    return image


def video(frames=6, size=(64, 48), fps=24.0) -> bytes:
    width, height = size
    shades = (np.full((height, width, 3), 40 * n, dtype=np.uint8).tobytes() for n in range(frames))
    return encode_mp4(shades, width=width, height=height, fps=fps)


def test_declarations_pass_the_sdk_validation():
    create_app(Sam3Server())
    assert TASK.task == "video-to-video"
    assert TASK.prompt == "optional"
    assert [(spec.role, spec.required) for spec in TASK.inputs] == [
        ("video", True),
        ("reference", False),
    ]


def test_the_reference_subject_is_its_largest_region_at_the_video_size():
    mask = reference_mask(reference(), (400, 320))
    assert mask.shape == (320, 400)
    box, points = reference_prompts(mask)
    assert box == [60, 40, 142, 282]
    assert len(points) >= 4
    assert all(60 < x < 142 and 40 < y < 282 for x, y in points)


def test_a_transparent_reference_works_as_a_white_one():
    rgba = Image.new("RGBA", (200, 160), (0, 0, 0, 0))
    ImageDraw.Draw(rgba).rectangle((30, 20, 70, 140), fill=(200, 200, 200, 255))
    assert reference_mask(rgba, (200, 160)).sum() == reference_mask(reference(), (200, 160)).sum()


@pytest.mark.parametrize(
    ("image", "message"),
    [
        (Image.new("RGB", (200, 100), "white"), "aspect ratio"),
        (Image.new("RGB", (200, 160), "white"), "no foreground"),
        (Image.new("RGB", (200, 160), "black"), "one subject"),
    ],
)
def test_unusable_references_are_refused(image, message):
    with pytest.raises(InvalidInput, match=message):
        reference_mask(image, (200, 160))


def test_a_small_subject_is_refused_for_mocap_only():
    image = Image.new("RGB", (200, 160), "white")
    ImageDraw.Draw(image).rectangle((30, 20, 50, 50), fill="black")
    mask = reference_mask(image, (200, 160))
    with pytest.raises(InvalidInput, match="too small"):
        reference_prompts(mask)
    assert reference_prompts(mask, require_full_subject=False)[0] == [30, 20, 51, 51]


def test_prompts_are_short_english_phrases_and_blank_means_none():
    assert check_prompt("  person ") == "person"
    assert check_prompt("   ") is None
    assert check_prompt(None) is None
    with pytest.raises(InvalidInput, match="short noun phrase"):
        check_prompt("a" * (MAX_PROMPT_LENGTH + 1))
    assert check_prompt("yellow robot, 2nd") == "yellow robot, 2nd"
    with pytest.raises(InvalidInput, match="in English"):
        check_prompt("волк")


def test_a_job_needs_a_reference_or_a_prompt():
    job = Job(task="video-to-video", prompt=None, inputs={}, params=TASK.params(), seeds=(1,))
    with pytest.raises(InvalidInput, match="reference frame, a prompt"):
        Sam3Server().generate(job)


def test_the_video_is_unpacked_into_numbered_frames(tmp_path):
    first, count, fps = unpack_video(video(), tmp_path)
    assert (count, fps, first.size) == (6, 24.0, (64, 48))
    assert sorted(p.name for p in (tmp_path / "frames").iterdir())[:2] == ["00000.jpg", "00001.jpg"]


def test_a_long_video_is_refused(tmp_path):
    with pytest.raises(InvalidInput, match="at most 1 seconds"):
        unpack_video(video(frames=30), tmp_path, max_seconds=1)


def test_the_instance_overlapping_the_reference_is_kept():
    subject = np.zeros((10, 10), dtype=bool)
    subject[2:8, 2:5] = True
    other, same = np.zeros_like(subject), np.zeros_like(subject)
    other[2:8, 6:9] = True
    same[2:8, 2:6] = True
    assert best_match(np.array([4, 7]), np.stack([other, same]), subject) == 7
    assert best_match(np.array([4]), np.stack([other]), subject) is None


def test_compositing_keeps_original_pixels_on_white():
    pixels = np.arange(2 * 3 * 3, dtype=np.uint8).reshape(2, 3, 3)
    mask = np.array([[True, False, False], [False, False, True]])
    result = np.frombuffer(composite(pixels, mask), dtype=np.uint8).reshape(2, 3, 3)
    assert (result[0, 0] == pixels[0, 0]).all()
    assert (result[1, 2] == pixels[1, 2]).all()
    assert (result[0, 1] == 255).all()


def test_speckles_go_and_edges_count():
    mask = np.zeros((100, 100), dtype=bool)
    mask[20:80, 30:60] = True
    mask[5, 5] = True
    cleaned = remove_speckles(mask)
    assert not cleaned[5, 5]
    assert cleaned[50, 40]
    assert not touches_edge(cleaned)
    mask[0, 30:60] = True
    assert touches_edge(mask)


def test_a_partial_checkpoint_is_not_taken(tmp_path, monkeypatch):
    snapshot = tmp_path / f"models--{REPO.replace('/', '--')}" / "snapshots" / REVISION
    snapshot.mkdir(parents=True)
    monkeypatch.setattr("huggingface_hub.constants.HF_HUB_CACHE", str(tmp_path))
    (snapshot / FILES[0]).write_text("{}")
    assert cached() is None
    (snapshot / FILES[1]).write_text("x")
    assert cached() == str(snapshot / FILES[1])


class FakeTracking:
    num_maskmem = 7
    memory_temporal_stride_for_eval = 1
    max_obj_ptrs_in_encoder = 16

    def _trim_output_and_memory(self, frame_idx, output_dict, current_out, memory_encoder_was_used):
        return current_out


class FakeModel:
    use_batched_grounding = True
    postprocess_batch_size = 16

    def __init__(self):
        self.tracker = SimpleNamespace(model=FakeTracking())

    def init_state(self, **kwargs):
        return kwargs

    def _build_sam2_output(self, inference_state, frame_idx, refined_obj_id_to_mask=None):
        if frame_idx not in inference_state["cached_frame_outputs"]:
            return {}
        return {**inference_state["cached_frame_outputs"][frame_idx], **refined_obj_id_to_mask}


def test_upstream_is_fitted_to_a_small_gpu_and_to_points_alone():
    model = FakeModel()
    adapt(model)
    assert model.init_state(resource_path="v", offload_state_to_cpu=True) == {"resource_path": "v"}
    assert (model.use_batched_grounding, model.postprocess_batch_size) == (False, 1)
    assert model._build_sam2_output({"cached_frame_outputs": {}}, 5, {1: "mask"}) == {1: "mask"}
    output_dict = {
        "cond_frame_outputs": {0: {}},
        "non_cond_frame_outputs": {index: {} for index in range(1, 100)},
    }
    tracking = model.tracker.model
    assert (
        tracking._trim_output_and_memory(100, output_dict, "current", memory_encoder_was_used=True)
        == "current"
    )
    assert sorted(output_dict["non_cond_frame_outputs"]) == list(range(68, 100))
    assert output_dict["cond_frame_outputs"] == {0: {}}
