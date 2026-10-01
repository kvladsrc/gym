"""Declarations, sizes and generation with a stand-in pipeline; no GPU or weights."""

import io
import types
from typing import Any

import numpy as np
import pytest
from PIL import Image
from wan_model_server.server import (
    FPS,
    SIDE_MULTIPLE,
    TASK,
    Params,
    WanServer,
    frame_count,
    video_size,
)

from model_server_sdk import GenerationError, InputData, InvalidInput, Job, create_app, media


def test_declarations_pass_the_sdk_validation() -> None:
    create_app(WanServer())
    assert TASK.task == "image-to-video"


@pytest.mark.parametrize(
    ("seconds", "frames"), [(5.0, 121), (1.0, 25), (2.5, 61), (1.1, 25), (4.99, 121)]
)
def test_frame_count(seconds: float, frames: int) -> None:
    assert frame_count(seconds) == frames
    assert (frame_count(seconds) - 1) % 4 == 0


@pytest.mark.parametrize(
    ("image", "size", "expected"),
    [
        ((1280, 704), "720p", (1280, 704)),
        ((704, 1280), "720p", (704, 1280)),
        ((1024, 1024), "720p", (928, 928)),
        ((1920, 1080), "480p", (832, 448)),
        # Extremes are clamped to 1:4 (cropped later), not 9472×64.
        ((10_000, 100), "720p", (1888, 448)),
        ((100, 10_000), "720p", (448, 1888)),
    ],
)
def test_video_size_keeps_the_aspect(
    image: tuple[int, int], size: Any, expected: tuple[int, int]
) -> None:
    width, height = video_size(*image, size)
    assert (width, height) == expected
    assert width % SIDE_MULTIPLE == 0
    assert height % SIDE_MULTIPLE == 0


class _Pipeline:
    def __init__(self, fail: Exception | None = None, drop_frames: int = 0) -> None:
        self.encoded: list[dict[str, Any]] = []
        self.calls: list[dict[str, Any]] = []
        self.fail = fail
        self.drop_frames = drop_frames

    def encode_prompt(self, **kwargs: Any) -> tuple[str, str | None]:
        self.encoded.append(kwargs)
        return "embeds", "negative" if kwargs["do_classifier_free_guidance"] else None

    def __call__(self, **kwargs: Any) -> Any:
        if self.fail is not None:
            raise self.fail
        self.calls.append(kwargs)
        frames, height, width = (
            kwargs["num_frames"] - self.drop_frames,
            kwargs["height"],
            kwargs["width"],
        )
        seed = kwargs["generator"].initial_seed()
        video = np.full((frames, height, width, 3), (seed % 10) / 10, dtype=np.float32)
        return types.SimpleNamespace(frames=[video])


def _png(width: int, height: int) -> InputData:
    buffer = io.BytesIO()
    Image.new("RGB", (width, height), (200, 120, 40)).save(buffer, format="PNG")
    return InputData("image/png", buffer.getvalue())


def test_generation_encodes_canonical_mp4() -> None:
    server = WanServer()
    pipeline = _Pipeline()
    server._pipeline = pipeline
    job = Job(
        task="image-to-video",
        prompt="фонарь мерцает",
        inputs={"image": _png(640, 360)},
        params=Params(seconds=1.0, size="480p"),
        seeds=(1, 2),
    )
    outputs = server.generate(job)
    assert len(pipeline.encoded) == 1  # one text encoding for all variants
    assert len(outputs) == 2
    assert outputs[0].data != outputs[1].data
    for output in outputs:
        assert media.looks_like("video/mp4", output.data)
        assert media.mp4_problems(output.data) == []
    assert outputs[0].meta == {
        "width": 832,
        "height": 448,
        "frames": 25,
        "fps": FPS,
        "seconds": 1.04,
    }
    call = pipeline.calls[0]
    assert (call["width"], call["height"]) == (832, 448)
    assert call["image"].size == (832, 448)


def test_bad_image_is_rejected_before_the_model() -> None:
    server = WanServer()
    pipeline = _Pipeline()
    server._pipeline = pipeline
    job = Job(
        task="image-to-video",
        prompt=None,
        inputs={"image": InputData("image/png", b"\x89PNG\r\n\x1a\nbroken")},
        params=Params(),
        seeds=(1,),
    )
    with pytest.raises(InvalidInput):
        server.generate(job)
    assert pipeline.encoded == []


def _job(**params: Any) -> Job:
    return Job(
        task="image-to-video",
        prompt="x",
        inputs={"image": _png(1600, 900)},
        params=Params(seconds=1.0, size="480p", **params),
        seeds=(1,),
    )


def test_the_first_frame_is_cropped_not_stretched() -> None:
    server = WanServer()
    pipeline = _Pipeline()
    server._pipeline = pipeline
    buffer = io.BytesIO()
    image = Image.new("RGB", (1000, 100), (0, 0, 255))
    image.paste((255, 0, 0), (0, 0, 100, 100))  # a red square at the left edge
    image.save(buffer, format="PNG")
    job = Job(
        task="image-to-video",
        prompt=None,
        inputs={"image": InputData("image/png", buffer.getvalue())},
        params=Params(seconds=1.0, size="480p"),
        seeds=(1,),
    )
    server.generate(job)
    first = pipeline.calls[0]["image"]
    assert first.size[0] / first.size[1] == pytest.approx(4, rel=0.1)
    assert first.getpixel((0, first.height // 2)) == (0, 0, 255)  # centre crop: no red


def test_no_guidance_means_no_negative_pass() -> None:
    server = WanServer()
    pipeline = _Pipeline()
    server._pipeline = pipeline
    server.generate(_job(guidance=1.0))
    assert pipeline.encoded[0]["do_classifier_free_guidance"] is False
    assert pipeline.calls[0]["negative_prompt_embeds"] is None


def test_out_of_memory_is_retryable() -> None:
    server = WanServer()
    server._pipeline = _Pipeline(fail=RuntimeError("CUDA out of memory. Tried to allocate 2 GiB"))
    with pytest.raises(GenerationError, match="out of GPU memory") as raised:
        server.generate(_job())
    assert raised.value.retryable


def test_a_short_video_from_the_model_is_an_error() -> None:
    server = WanServer()
    server._pipeline = _Pipeline(drop_frames=4)
    with pytest.raises(GenerationError, match="frames, not"):
        server.generate(_job())
