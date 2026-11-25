"""Declarations, sizes, the upsampler's requests and the edit inputs; no GPU or
weights required."""

import base64
import io
import json
import math

import pytest
from flux3_model_server.server import (
    EDIT_PIXELS,
    SIZES,
    TASKS,
    WHOLE_VAE_PIXELS,
    Flux3Server,
    ImageToImageParams,
    TextToImageParams,
    decode_unquantized_gguf,
    edit_source,
    fitted_size,
    resized_source,
)
from flux3_model_server.upsampler import (
    PRESENCE_PENALTY,
    Upsampler,
    messages,
    reference_jpeg,
    reference_size,
    request_text,
)
from model_server_sdk.images import encode_png
from PIL import Image

from model_server_sdk import InputData, InvalidInput, Job, create_app

PROMPTS = {"t2i": "text system", "i2i": "edit system"}


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(Flux3Server())
    assert [task.task for task in TASKS] == ["text-to-image", "image-to-image"]
    assert [(spec.role, spec.required) for spec in TASKS[1].inputs] == [
        ("image", True),
        ("reference", False),
    ]


def test_size_choices_match_the_sizes() -> None:
    choices = TextToImageParams.model_json_schema()["properties"]["resolution"]["enum"]
    assert set(choices) == set(SIZES)


@pytest.mark.parametrize("size", sorted(SIZES))
def test_sizes_sit_on_the_grid_and_match_their_canvas(size: str) -> None:
    width, height = (int(side) for side in size.split("x"))
    assert width % 16 == 0
    assert height % 16 == 0
    ratio_w, ratio_h = (int(part) for part in SIZES[size].split(":"))
    assert abs(width / height - ratio_w / ratio_h) < 0.01
    assert 0.9e6 < width * height < 4.3e6


def test_a_canvas_line_leads_a_non_square_request() -> None:
    assert request_text("  кот на крыше ", "16:9") == "TARGET CANVAS: 16:9\nкот на крыше"
    assert request_text("кот", None) == "кот"


def test_a_text_request_uses_the_text_system_prompt() -> None:
    mode, chat = messages(PROMPTS, "a red bicycle", [], "3:2")
    assert mode == "t2i"
    assert chat == [
        {"role": "system", "content": "text system"},
        {"role": "user", "content": "TARGET CANVAS: 3:2\na red bicycle"},
    ]
    assert (PRESENCE_PENALTY["t2i"], PRESENCE_PENALTY["i2i"]) == (1.5, 0.0)


def test_an_edit_sends_the_references_first_then_the_scaffold() -> None:
    references = [Image.new("RGB", (300, 200), "red"), Image.new("RGB", (100, 100), "blue")]
    mode, chat = messages(PROMPTS, " make it blue ", references, None)
    assert mode == "i2i"
    assert chat[0] == {"role": "system", "content": "edit system"}
    parts = chat[1]["content"]
    assert [part["type"] for part in parts] == ["image_url", "image_url", "text"]
    assert parts[2]["text"] == (
        "Reference images follow in order (the first image, the second image, ...).\n"
        "User request: make it blue\n"
        "Output the caption."
    )
    jpeg = base64.b64decode(parts[0]["image_url"]["url"].removeprefix("data:image/jpeg;base64,"))
    assert Image.open(io.BytesIO(jpeg)).size == (288, 192)


@pytest.mark.parametrize(
    ("size", "count", "expected"),
    [
        ((1000, 1000), 1, (992, 992)),
        ((4000, 3000), 1, (1152, 864)),
        ((4000, 3000), 2, (832, 608)),
        ((30, 30), 1, (32, 32)),
    ],
)
def test_references_get_the_reference_codes_sizes(size, count, expected) -> None:
    assert reference_size(*size, count) == expected


def test_a_transparent_reference_is_shown_on_white() -> None:
    image = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    jpeg = Image.open(io.BytesIO(reference_jpeg(image, 1)))
    assert jpeg.mode == "RGB"
    assert min(jpeg.getpixel((10, 10))) > 245


def test_the_system_prompts_are_read_from_the_shipped_file(tmp_path) -> None:
    path = tmp_path / "system_prompts.json"
    path.write_text(json.dumps(PROMPTS))
    assert Upsampler("w.gguf", "p.gguf", str(path)).system_prompts == PROMPTS


def test_an_edit_source_is_flattened_on_white_and_cropped_beyond_1_to_4() -> None:
    clear = Image.new("RGBA", (100, 50), (0, 0, 0, 0))
    flat = edit_source(encode_png(clear))
    assert flat.mode == "RGB"
    assert flat.getpixel((5, 5)) == (255, 255, 255)
    wide = edit_source(encode_png(Image.new("RGB", (1000, 100), "red")))
    assert wide.size == (400, 100)


def test_edit_sizes_keep_the_proportions() -> None:
    assert fitted_size(1600, 900, 1024 * 1024) == (1360, 768)
    assert fitted_size(100, 100, 2048 * 2048) == (2048, 2048)
    assert fitted_size(4000, 1000, 2048 * 2048) == (4096, 1024)
    image = resized_source(Image.new("RGB", (1500, 1000)), 1024 * 1024)
    assert image.size == (1254, 836)


def test_edit_params_default_to_one_megapixel_and_the_upsampler() -> None:
    params = ImageToImageParams()
    assert (params.size, params.upsample) == ("1K", True)


def test_a_broken_image_is_refused_before_the_upsampler_runs() -> None:
    job = Job(
        task="image-to-image",
        prompt="make it blue",
        inputs={"image": InputData(mime="image/png", data=b"not a png")},
        params=ImageToImageParams(),
        seeds=(1,),
    )
    with pytest.raises(InvalidInput):
        Flux3Server().generate(job)


def test_unquantized_gguf_weights_become_plain_bf16() -> None:
    import gguf
    import torch
    from diffusers.quantizers.gguf.utils import GGUFLinear, GGUFParameter

    layer = GGUFLinear(4, 3, bias=False, compute_dtype=torch.bfloat16)
    weight = torch.arange(12, dtype=torch.bfloat16).reshape(3, 4)
    raw = weight.view(torch.uint8)  # how diffusers holds a BF16 GGUF tensor
    layer.weight = GGUFParameter(raw, quant_type=gguf.GGMLQuantizationType.BF16)
    model = torch.nn.Sequential(layer)
    assert next(model.parameters()).dtype == torch.uint8
    assert decode_unquantized_gguf(model, torch.bfloat16) == ["0.weight"]
    assert type(layer.weight) is torch.nn.Parameter
    assert torch.equal(layer.weight, weight)
    inputs = torch.ones(1, 4, dtype=torch.bfloat16)
    assert torch.equal(layer(inputs), inputs @ weight.T)


def test_only_2k_images_go_through_the_vae_in_tiles() -> None:
    pixels = sorted(math.prod(int(side) for side in size.split("x")) for size in SIZES)
    one_megapixel, four_megapixels = pixels[:7], pixels[7:]
    assert max(one_megapixel) <= WHOLE_VAE_PIXELS < min(four_megapixels)
    assert EDIT_PIXELS["1K"] <= WHOLE_VAE_PIXELS < EDIT_PIXELS["2K"]
