"""Declarations and failure handling; no GPU or weights required."""

import io
import weakref

import pytest
from model_server_sdk.images import RESOLUTIONS
from PIL import Image
from sdxl_model_server.server import (
    TASKS,
    ImageToImageParams,
    SdxlServer,
    TextToImageParams,
)

from model_server_sdk import GenerationError, InputData, Job, create_app


def test_resolution_choices_match_the_sizes() -> None:
    choices = TextToImageParams.model_json_schema()["properties"]["resolution"]["enum"]
    assert set(choices) == set(RESOLUTIONS)


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(SdxlServer())  # validates tasks and parameter schemas
    assert [task.task for task in TASKS] == ["text-to-image", "image-to-image"]


def encode(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    return buffer.getvalue()


class _Activations:
    pass


class _RaisingPipeline:
    """Stands in for a diffusers pipeline: fails like CUDA running out of memory."""

    def __init__(self) -> None:
        self.freed = 0
        self.activations: weakref.ref[_Activations] | None = None

    def __call__(self, **_: object) -> None:
        # A local of the failed call, like the tensors of a real pipeline.
        activations = _Activations()
        self.activations = weakref.ref(activations)
        raise RuntimeError("CUDA out of memory. Tried to allocate 2.00 GiB")

    def maybe_free_model_hooks(self) -> None:
        self.freed += 1


def _job(task: str, params: object, inputs: dict[str, object] | None = None) -> Job:
    return Job(task=task, prompt="x", inputs=inputs or {}, params=params, seeds=(1,))  # type: ignore[arg-type]


def test_out_of_memory_is_retryable_and_frees_the_models() -> None:
    server = SdxlServer()
    pipeline = _RaisingPipeline()
    server._text_to_image = pipeline
    with pytest.raises(GenerationError, match="out of GPU memory") as raised:
        server.generate(_job("text-to-image", TextToImageParams()))
    assert raised.value.retryable
    assert raised.value.__cause__ is None
    assert raised.value.__context__ is None
    assert pipeline.freed == 1
    # The failed call's locals are gone before the cache is emptied.
    assert pipeline.activations is not None
    assert pipeline.activations() is None


def test_out_of_memory_in_image_to_image_frees_the_models() -> None:
    server = SdxlServer()
    server._text_to_image = pipeline = _RaisingPipeline()
    server._image_to_image = failing = _RaisingPipeline()
    image = InputData("image/png", encode(Image.new("RGB", (8, 8))))
    with pytest.raises(GenerationError) as raised:
        server.generate(_job("image-to-image", ImageToImageParams(), {"image": image}))
    assert raised.value.retryable
    assert pipeline.freed >= 1
    assert failing.activations is not None
    assert failing.activations() is None


def test_other_errors_propagate_and_free_after_image_to_image() -> None:
    class Broken(_RaisingPipeline):
        def __call__(self, **_: object) -> None:
            raise RuntimeError("shape mismatch")

    server = SdxlServer()
    server._text_to_image = pipeline = _RaisingPipeline()
    server._image_to_image = Broken()
    image = InputData("image/png", encode(Image.new("RGB", (8, 8))))
    with pytest.raises(RuntimeError, match="shape mismatch"):
        server.generate(_job("image-to-image", ImageToImageParams(), {"image": image}))
    assert pipeline.freed >= 1  # freeing twice is harmless
