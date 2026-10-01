"""Declarations, step accounting, the offload store and failure handling;
no GPU or weights required."""

import types
from collections.abc import Sequence
from typing import Any

import pytest
from flux_model_server.server import (
    TASKS,
    FluxServer,
    ImageToImageParams,
    TextToImageParams,
    denoising_steps,
)
from model_server_sdk.images import RESOLUTIONS, encode_png
from PIL import Image

from model_server_sdk import GenerationError, InputData, Job, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(FluxServer())
    assert [task.task for task in TASKS] == ["text-to-image", "image-to-image"]


def test_resolution_choices_match_the_sizes() -> None:
    choices = TextToImageParams.model_json_schema()["properties"]["resolution"]["enum"]
    assert set(choices) == set(RESOLUTIONS)


@pytest.mark.parametrize("steps", range(1, 9))
@pytest.mark.parametrize("strength", [0.05, 0.1, 0.2, 0.25, 0.5, 0.6, 0.75, 0.99, 1.0])
def test_denoising_steps_match_diffusers(steps: int, strength: float) -> None:
    from diffusers import FluxImg2ImgPipeline

    scheduler = types.SimpleNamespace(timesteps=list(range(steps)), order=1)
    fake = types.SimpleNamespace(scheduler=scheduler)
    _, runs = FluxImg2ImgPipeline.get_timesteps(fake, steps, strength, "cpu")  # type: ignore[arg-type]
    assert denoising_steps(steps, strength) == runs >= 1


# Generation with stand-in pipelines


class _Pipeline:
    def __init__(self, fail: Exception | None = None) -> None:
        self.fail = fail
        self.encoded: list[str] = []
        self.calls: list[dict[str, Any]] = []

    def encode_prompt(self, **kwargs: Any) -> tuple[str, str, None]:
        self.encoded.append(kwargs["prompt"])
        return "embeds", "pooled", None

    def __call__(self, **kwargs: Any) -> Any:
        if self.fail is not None:
            raise self.fail
        self.calls.append(kwargs)
        return types.SimpleNamespace(images=[Image.new("RGB", (8, 8))])


def _server(pipeline: _Pipeline) -> FluxServer:
    server = FluxServer()
    server._text_to_image = pipeline
    server._image_to_image = pipeline
    return server


def _job(task: str, params: Any, seeds: Sequence[int] = (1, 2), **inputs: InputData) -> Job:
    return Job(task=task, prompt="a lantern", inputs=inputs, params=params, seeds=tuple(seeds))


def test_prompt_is_encoded_once_for_all_variants() -> None:
    pipeline = _Pipeline()
    outputs = _server(pipeline).generate(_job("text-to-image", TextToImageParams()))
    assert pipeline.encoded == ["a lantern"]
    assert len(outputs) == 2
    assert {call["guidance_scale"] for call in pipeline.calls} == {0.0}
    assert {call["prompt_embeds"] for call in pipeline.calls} == {"embeds"}


def test_image_to_image_reports_the_steps_that_run() -> None:
    pipeline = _Pipeline()
    image = InputData("image/png", encode_png(Image.new("RGB", (64, 64))))
    [output] = _server(pipeline).generate(
        _job("image-to-image", ImageToImageParams(), seeds=(1,), image=image)
    )
    assert output.meta["denoising_steps"] == denoising_steps(4, 0.6) == 3


def test_out_of_memory_is_retryable() -> None:
    pipeline = _Pipeline(fail=RuntimeError("CUDA out of memory. Tried to allocate 2 GiB"))
    with pytest.raises(GenerationError, match="out of GPU memory") as raised:
        _server(pipeline).generate(_job("text-to-image", TextToImageParams()))
    assert raised.value.retryable
    assert raised.value.__context__ is None


def test_other_errors_propagate() -> None:
    pipeline = _Pipeline(fail=RuntimeError("shape mismatch"))
    with pytest.raises(RuntimeError, match="shape mismatch"):
        _server(pipeline).generate(_job("text-to-image", TextToImageParams()))
