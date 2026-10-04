"""Declarations, prompts, transparency and failure handling; no GPU or
weights required."""

import io
import types
from collections.abc import Sequence
from typing import Any

import pytest
from model_server_sdk.images import encode_png
from PIL import Image
from qwen_image_model_server.server import (
    SIZES,
    TASKS,
    TRANSPARENT_PROMPT,
    ImageToImageParams,
    QwenImageServer,
    TextToImageParams,
    decode_stray_gguf,
    edit_source,
    finished,
    full_prompt,
    separate_shared_storage,
)

from model_server_sdk import GenerationError, InputData, Job, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(QwenImageServer())
    assert [task.task for task in TASKS] == ["text-to-image", "image-to-image"]


def test_size_choices_match_the_sizes() -> None:
    choices = TextToImageParams.model_json_schema()["properties"]["resolution"]["enum"]
    assert set(choices) == set(SIZES)


@pytest.mark.parametrize("size", sorted(SIZES))
def test_sizes_are_multiples_of_32_and_match_their_names(size: str) -> None:
    width, height = SIZES[size]
    assert f"{width}x{height}" == size
    assert width % 32 == 0
    assert height % 32 == 0


def test_transparent_prompt_wraps_the_description() -> None:
    assert full_prompt("a red lantern.", True) == TRANSPARENT_PROMPT.format(prompt="a red lantern")
    assert full_prompt("a red lantern", False) == "a red lantern"


def test_alpha_is_kept_only_where_asked_for_and_something_is_transparent() -> None:
    opaque = Image.new("RGBA", (4, 4), (1, 2, 3, 255))
    assert finished(opaque, keep_alpha=True).mode == "RGB"
    noisy = opaque.copy()
    noisy.putpixel((0, 0), (1, 2, 3, 203))
    assert finished(noisy, keep_alpha=True).mode == "RGB"  # noise, not transparency
    clear = opaque.copy()
    clear.putpixel((0, 0), (1, 2, 3, 0))
    assert finished(clear, keep_alpha=True).mode == "RGBA"
    assert finished(Image.new("RGB", (4, 4)), keep_alpha=True).mode == "RGB"


def test_opaque_noise_of_a_transparent_result_is_made_opaque() -> None:
    sprite = Image.new("RGBA", (4, 1), (1, 2, 3, 0))
    for x, alpha in enumerate((0, 100, 239, 247)):
        sprite.putpixel((x, 0), (1, 2, 3, alpha))
    kept = finished(sprite, keep_alpha=True).getchannel("A")
    # Transparent and soft edges stay; the body's noise becomes opaque.
    assert [kept.getpixel((x, 0)) for x in range(4)] == [0, 100, 239, 255]


def test_background_noise_of_a_transparent_result_is_cleared() -> None:
    sprite = Image.new("RGBA", (3, 1), (1, 2, 3, 0))
    for x, alpha in enumerate((17, 31, 32)):
        sprite.putpixel((x, 0), (1, 2, 3, alpha))
    kept = finished(sprite, keep_alpha=True).getchannel("A")
    assert [kept.getpixel((x, 0)) for x in range(3)] == [0, 0, 32]


def test_alpha_noise_of_an_opaque_request_is_dropped() -> None:
    # The VAE's alpha for an opaque image: mostly 255, a few pixels lower.
    noisy = Image.new("RGBA", (4, 4), (1, 2, 3, 255))
    noisy.putpixel((1, 1), (1, 2, 3, 203))
    assert finished(noisy, keep_alpha=False).mode == "RGB"


def test_gguf_weights_outside_linear_layers_are_decoded() -> None:
    import gguf
    import torch
    from diffusers.quantizers.gguf.utils import GGUFLinear, GGUFParameter

    values = torch.tensor([1.5, -2.0, 0.25, 3.0], dtype=torch.bfloat16)
    raw = values.view(torch.uint8)  # how a BF16 GGUF tensor is loaded: 2 bytes per value
    model = torch.nn.Module()
    model.norm = torch.nn.Module()
    model.norm.weight = GGUFParameter(raw.clone(), quant_type=gguf.GGMLQuantizationType.BF16)
    model.linear = GGUFLinear(4, 4, bias=False, compute_dtype=torch.bfloat16)
    model.linear.weight = GGUFParameter(
        torch.zeros(4, 8, dtype=torch.uint8), quant_type=gguf.GGMLQuantizationType.BF16
    )
    assert decode_stray_gguf(model, torch.bfloat16) == ["norm.weight"]
    assert type(model.norm.weight) is torch.nn.Parameter
    assert torch.equal(model.norm.weight.data, values)
    assert isinstance(model.linear.weight, GGUFParameter)  # GGUFLinear decodes it itself


def test_parameters_sharing_storage_get_their_own_copies() -> None:
    import torch

    fused = torch.arange(16, dtype=torch.float32).reshape(4, 4)
    gate, up = fused.chunk(2, dim=0)
    layer = torch.nn.Module()
    layer.gate = torch.nn.Parameter(gate, requires_grad=False)
    layer.up = torch.nn.Parameter(up, requires_grad=False)
    layer.alone = torch.nn.Parameter(torch.ones(2), requires_grad=False)

    def storage(name: str) -> int:
        return getattr(layer, name).data.untyped_storage().data_ptr()

    alone = storage("alone")
    assert separate_shared_storage(layer) == 2
    assert storage("gate") != storage("up")
    assert storage("alone") == alone  # not copied
    assert torch.equal(layer.gate.data, gate)
    assert torch.equal(layer.up.data, up)


# Generation with a stand-in pipeline


class _Pipeline:
    def __init__(self, fail: Exception | None = None, alpha: int = 255) -> None:
        self.fail = fail
        self.alpha = alpha
        self.encoded: list[dict[str, Any]] = []
        self.calls: list[dict[str, Any]] = []

    def encode_prompt(self, **kwargs: Any) -> tuple[str, str, str]:
        self.encoded.append(kwargs)
        return "embeds", "mask", "pads"

    def __call__(self, **kwargs: Any) -> Any:
        if self.fail is not None:
            raise self.fail
        self.calls.append(kwargs)
        if "image" in kwargs:  # as the real pipeline does, once per call
            self.encode_prompt(prompt=kwargs["prompt"], image=kwargs["image"])
        return types.SimpleNamespace(images=[Image.new("RGBA", (8, 8), (9, 9, 9, self.alpha))])


def _server(pipeline: _Pipeline) -> QwenImageServer:
    server = QwenImageServer()
    server._pipeline = pipeline
    return server


def _job(task: str, params: Any, seeds: Sequence[int] = (1, 2), **inputs: InputData) -> Job:
    return Job(task=task, prompt="a lantern", inputs=inputs, params=params, seeds=tuple(seeds))


def _png(image: Image.Image) -> InputData:
    return InputData(mime="image/png", data=encode_png(image))


def test_prompt_is_encoded_once_for_all_variants() -> None:
    pipeline = _Pipeline(alpha=203)  # noise: not asked for, so dropped
    outputs = _server(pipeline).generate(
        _job("text-to-image", TextToImageParams(resolution="2048x2048"))
    )
    assert [call["prompt"] for call in pipeline.encoded] == ["a lantern"]
    assert len(outputs) == 2
    assert {(call["width"], call["height"]) for call in pipeline.calls} == {(2048, 2048)}
    assert {call["prompt_embeds"] for call in pipeline.calls} == {"embeds"}
    assert all(output.meta["transparent"] is False for output in outputs)


def test_transparent_request_uses_the_rgba_prompt_and_keeps_alpha() -> None:
    pipeline = _Pipeline(alpha=0)
    (output,) = _server(pipeline).generate(
        _job("text-to-image", TextToImageParams(transparent=True), seeds=(1,))
    )
    assert pipeline.encoded[0]["prompt"].startswith("This is an RGBA image")
    assert Image.open(io.BytesIO(output.data)).mode == "RGBA"
    assert output.meta["transparent"] is True


def test_variants_differ_by_seed() -> None:
    pipeline = _Pipeline()
    _server(pipeline).generate(_job("text-to-image", TextToImageParams(), seeds=(5, 6)))
    noise = [call["generator"].initial_seed() for call in pipeline.calls]
    assert noise == [5, 6]


def test_edit_encodes_once_and_keeps_input_alpha() -> None:
    pipeline = _Pipeline()
    source = Image.new("RGBA", (300, 200), (0, 0, 0, 0))
    _server(pipeline).generate(
        _job("image-to-image", ImageToImageParams(size="2K"), seeds=(1, 2, 3), image=_png(source))
    )
    assert len(pipeline.calls) == 3
    assert len(pipeline.encoded) == 1  # the other two calls got the cached result
    assert {call["output_resolution"] for call in pipeline.calls} == {2048}
    # The 2K condition's key-value cache would not fit: recomputed every step.
    assert {call["use_kv_cache"] for call in pipeline.calls} == {False}
    assert {call["image"].mode for call in pipeline.calls} == {"RGBA"}
    assert "encode_prompt" not in vars(pipeline)  # the cache is gone after the job


@pytest.mark.parametrize(
    ("source_alpha", "transparent", "kept"),
    [(None, False, False), (0, False, True), (203, False, False), (None, True, True)],
)
def test_edits_keep_alpha_when_the_source_has_it_or_it_is_asked_for(
    source_alpha: int | None, transparent: bool, kept: bool
) -> None:
    pipeline = _Pipeline(alpha=0)
    source = Image.new("RGB" if source_alpha is None else "RGBA", (64, 64), (9, 9, 9, 255))
    if source_alpha is not None:  # one pixel: transparent (0) or alpha noise (203)
        source.putpixel((0, 0), (0, 0, 0, source_alpha))
    (output,) = _server(pipeline).generate(
        _job(
            "image-to-image",
            ImageToImageParams(transparent=transparent),
            seeds=(1,),
            image=_png(source),
        )
    )
    assert output.meta["transparent"] is kept


def test_1k_edits_use_the_key_value_cache() -> None:
    pipeline = _Pipeline()
    source = Image.new("RGB", (64, 64))
    _server(pipeline).generate(
        _job("image-to-image", ImageToImageParams(size="1K"), seeds=(1,), image=_png(source))
    )
    assert pipeline.calls[0]["use_kv_cache"] is True
    assert pipeline.calls[0]["output_resolution"] == 1024


def test_edit_source_keeps_its_size_for_the_pipeline_to_scale() -> None:
    big = edit_source(encode_png(Image.new("RGB", (2400, 1600))))
    assert big.size == (2400, 1600)


@pytest.mark.parametrize(
    ("size", "expected"), [((5000, 500), (2000, 500)), ((500, 5000), (500, 2000))]
)
def test_edit_source_is_cropped_only_beyond_1_to_4(
    size: tuple[int, int], expected: tuple[int, int]
) -> None:
    assert edit_source(encode_png(Image.new("RGBA", size, (0, 0, 0, 0)))).size == expected


def test_bad_input_fails_before_any_model_call() -> None:
    from model_server_sdk import InvalidInput

    pipeline = _Pipeline()
    bad = InputData(mime="image/png", data=b"not an image")
    with pytest.raises(InvalidInput):
        _server(pipeline).generate(_job("image-to-image", ImageToImageParams(), image=bad))
    assert pipeline.calls == []
    assert pipeline.encoded == []


def test_out_of_memory_is_a_retryable_failure() -> None:
    pipeline = _Pipeline(fail=RuntimeError("CUDA out of memory. Tried to allocate 2 GiB"))
    with pytest.raises(GenerationError) as caught:
        _server(pipeline).generate(_job("text-to-image", TextToImageParams()))
    assert caught.value.retryable


def test_other_errors_are_not_swallowed() -> None:
    pipeline = _Pipeline(fail=RuntimeError("shape mismatch"))
    with pytest.raises(RuntimeError, match="shape mismatch"):
        _server(pipeline).generate(_job("text-to-image", TextToImageParams()))
