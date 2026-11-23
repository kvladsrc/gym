"""Declarations, the maps' arithmetic and the loader on a tiny random model; no GPU or weights."""

import json
from pathlib import Path

import numpy as np
import pytest
from pbrfusion4_model_server import server
from pbrfusion4_model_server.server import (
    TASK,
    Params,
    PBRFusion4Server,
    fold,
    height_picture,
    load_model,
    model_size,
    normal_picture,
    normals,
    predict,
    read_height,
    render,
    resize,
    smooth,
    tilt_degrees,
    wrap_margin,
    wrap_pad,
)
from PIL import Image

from model_server_sdk import InvalidInput, create_app


def dome(side: int) -> np.ndarray:
    """A round hill in the middle of a flat field, H×W float32 in 0…1."""
    rows, columns = np.mgrid[0:side, 0:side].astype(np.float32)
    centre = (side - 1) / 2
    distance = np.hypot(columns - centre, rows - centre) / (side / 4)
    return np.clip(1 - distance**2, 0, 1).astype(np.float32)


def waves(height: int, width: int) -> np.ndarray:
    """A relief that tiles: whole periods along both sides."""
    rows, columns = np.mgrid[0:height, 0:width].astype(np.float32)
    relief = np.sin(2 * np.pi * columns / width) * np.cos(4 * np.pi * rows / height)
    return (relief * 0.5 + 0.5).astype(np.float32)


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(PBRFusion4Server())
    assert TASK.task == "image-to-image"
    assert TASK.prompt == "none"
    assert [spec.role for spec in TASK.inputs] == ["image"]
    assert TASK.output_mime == ("image/png",)
    assert Params().output == "normal"


def test_the_model_sees_the_proportions_within_its_range() -> None:
    assert model_size(1254, 1254, 1024) == (1024, 1024)
    assert model_size(1024, 512, 1024) == (1024, 512)
    assert model_size(2048, 2048, 2048) == (2048, 2048)
    # A small image is enlarged to the shortest side the model works at.
    assert model_size(200, 100, 1024) == (768, 384)


def test_a_strip_too_long_for_the_model_is_refused() -> None:
    with pytest.raises(InvalidInput, match="too elongated"):
        model_size(4096, 256, 1024)


def test_the_wrapped_border_is_an_eighth_in_whole_strides() -> None:
    assert wrap_margin(1024) == 128
    assert wrap_margin(512) == 64
    assert wrap_margin(384) == 64


def test_folding_a_tiling_prediction_returns_it_unchanged() -> None:
    relief = waves(128, 256)
    assert np.allclose(fold(wrap_pad(relief, 32, 16), 32, 16), relief, atol=1e-6)


def test_folding_makes_the_edges_of_a_tilted_prediction_meet() -> None:
    # The model's answer leans across the padded picture: its own part no longer tiles.
    relief = waves(128, 256)
    padded = wrap_pad(relief, 32, 16)
    slope = 0.002
    padded = padded + slope * np.arange(padded.shape[1], dtype=np.float32)[None, :]
    own = padded[16:-16, 32:-32]
    folded = fold(padded, 32, 16)
    between_neighbours = np.abs(np.diff(folded, axis=1)).max()
    assert np.abs(own[:, 0] - own[:, -1]).max() > 0.4
    # The lean's rise (0.5) is spread over the two cross-faded strips.
    assert np.abs(folded[:, 0] - folded[:, -1]).max() < between_neighbours + 0.5 / 64 + 1e-3
    assert np.allclose(folded[:, 32:-32], own[:, 32:-32], atol=1e-6)


def test_normals_of_a_hill_lean_away_from_its_top() -> None:
    side = 128
    picture = np.asarray(normal_picture(normals(dome(side), 4.0, wrap=False), "opengl"))
    middle = side // 2
    right, left = picture[middle, middle + 20], picture[middle, middle - 20]
    upper, lower = picture[middle - 20, middle], picture[middle + 20, middle]
    assert right[0] > 140 > 116 > left[0]  # red: to the right
    assert upper[1] > 140 > 116 > lower[1]  # green: up
    assert tuple(picture[2, 2]) == (128, 128, 255)  # the flat field


def test_directx_normals_have_green_pointing_down() -> None:
    vectors = normals(dome(128), 4.0, wrap=False)
    opengl = np.asarray(normal_picture(vectors, "opengl")).astype(int)
    directx = np.asarray(normal_picture(vectors, "directx")).astype(int)
    assert directx[44, 64, 1] < 116 < 140 < opengl[44, 64, 1]
    assert np.abs(opengl[:, :, 1] + directx[:, :, 1] - 256).max() <= 1
    assert (opengl[:, :, [0, 2]] == directx[:, :, [0, 2]]).all()


def test_the_strength_scales_the_slopes() -> None:
    gentle = normals(dome(128), 1.0, wrap=False)[64, 84]
    steep = normals(dome(128), 2.0, wrap=False)[64, 84]
    assert steep[0] / steep[2] == pytest.approx(2 * gentle[0] / gentle[2], rel=1e-4)


def test_the_slopes_do_not_depend_on_the_resolution() -> None:
    coarse = tilt_degrees(normals(dome(256), 1.0, wrap=False))
    fine = tilt_degrees(normals(dome(512), 1.0, wrap=False))
    assert fine.mean() == pytest.approx(coarse.mean(), rel=0.02)
    assert fine[256, 336] == pytest.approx(coarse[128, 168], rel=0.03)


def test_tileable_normals_continue_across_the_edges() -> None:
    relief = waves(96, 160)
    moved = np.roll(relief, (17, 23), axis=(0, 1))
    wrapped = normals(relief, 1.0, wrap=True)
    assert np.allclose(
        normals(moved, 1.0, wrap=True), np.roll(wrapped, (17, 23), axis=(0, 1)), atol=1e-5
    )
    # Without wrapping the border columns are those of a cut-off picture.
    assert not np.allclose(normals(relief, 1.0, wrap=False)[:, 0], wrapped[:, 0], atol=1e-3)


def test_smoothing_removes_noise_and_keeps_a_step() -> None:
    noise = np.random.default_rng(0).normal(0, 0.01, (256, 256)).astype(np.float32)
    relief = np.zeros((256, 256), dtype=np.float32)
    relief[:, 128:] = 0.8
    smoothed = smooth(relief + noise, wrap=False)
    assert smoothed[:, :100].std() < 0.5 * (relief + noise)[:, :100].std()
    assert (smoothed[:, 129] - smoothed[:, 126]).mean() > 0.75


def test_tileable_smoothing_continues_across_the_edges() -> None:
    relief = waves(96, 160)
    moved = np.roll(relief, (11, 40), axis=(0, 1))
    assert np.allclose(
        smooth(moved, wrap=True),
        np.roll(smooth(relief, wrap=True), (11, 40), axis=(0, 1)),
        atol=1e-5,
    )


def test_resizing_keeps_the_size_asked_and_wraps() -> None:
    relief = waves(64, 96)
    assert resize(relief, 96, 64, wrap=False) is relief
    larger = resize(relief, 192, 128, wrap=True)
    assert larger.shape == (128, 192)
    moved = resize(np.roll(relief, (8, 12), axis=(0, 1)), 192, 128, wrap=True)
    assert np.allclose(moved, np.roll(larger, (16, 24), axis=(0, 1)), atol=1e-5)


def test_the_height_is_a_grey_picture() -> None:
    picture = height_picture(np.array([[0.0, 0.5], [1.0, 1.2]], dtype=np.float32))
    assert picture.mode == "L"
    assert np.asarray(picture).ravel().tolist() == [0, 128, 255, 255]


def test_rendering_gives_the_map_asked_for() -> None:
    image = Image.new("RGB", (128, 128), (90, 120, 60))
    grey, numbers = render(dome(128), image, Params(output="height"))
    assert grey.mode == "L"
    assert grey.size == (128, 128)
    assert numbers == {}
    colour, numbers = render(dome(128), image, Params())
    assert colour.mode == "RGB"
    assert 0 < numbers["tilt_mean_deg"] < numbers["tilt_p99_deg"] < 90


def test_the_brightness_mix_adds_the_pictures_own_detail() -> None:
    # A flat height under a striped picture: only the mix gives relief.
    stripes = np.zeros((128, 128, 3), dtype=np.uint8)
    stripes[:, ::8] = 255
    image = Image.fromarray(stripes, "RGB")
    flat = np.full((128, 128), 0.5, dtype=np.float32)
    _, plain = render(flat, image, Params(brightness_mix=0))
    _, mixed = render(flat, image, Params(brightness_mix=0.3))
    assert plain["tilt_mean_deg"] == 0
    assert mixed["tilt_mean_deg"] > 1


def test_reading_a_tileable_height_returns_the_images_size_with_meeting_edges(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # In place of the networks: the picture's brightness, leaning to the right.
    def leaning(_model: object, pixels: np.ndarray) -> np.ndarray:
        lean = np.linspace(0, 0.3, pixels.shape[1], dtype=np.float32)[None, :]
        return pixels.mean(axis=2) + lean

    monkeypatch.setattr(server, "predict", leaning)
    relief = waves(600, 600)
    image = Image.fromarray(np.round(relief * 255).astype(np.uint8), "L").convert("RGB")
    height, seen = read_height(None, image, 512, tileable=True)  # type: ignore[arg-type]
    assert height.shape == (600, 600)
    assert seen == {"model_size": [512, 512], "wrap_margin": [64, 64]}
    assert 0 <= height.min() < height.max() <= 1
    across = np.abs(height[:, 0] - height[:, -1]).max()
    assert across < 3 * np.abs(np.diff(height, axis=1)).max()
    cut, seen = read_height(None, image, 512, tileable=False)  # type: ignore[arg-type]
    assert seen == {"model_size": [512, 512]}
    assert np.abs(cut[:, 0] - cut[:, -1]).max() > 5 * across


def tiny_weights(folder: Path) -> str:
    """A weights file laid out as PBRFusion 4's, of small random networks."""
    import torch
    from diffusers import AutoencoderKL, UNet2DConditionModel
    from safetensors.torch import save_file
    from transformers import CLIPTextConfig, CLIPTextModel

    torch.manual_seed(0)
    unet = UNet2DConditionModel(
        sample_size=8,
        in_channels=4,
        out_channels=4,
        block_out_channels=(16, 32),
        layers_per_block=1,
        down_block_types=("CrossAttnDownBlock2D", "DownBlock2D"),
        up_block_types=("UpBlock2D", "CrossAttnUpBlock2D"),
        cross_attention_dim=16,
        attention_head_dim=4,
        norm_num_groups=8,
        class_embed_type="projection",
        projection_class_embeddings_input_dim=4,
        use_linear_projection=True,
    )
    vae = AutoencoderKL(
        block_out_channels=(16, 32),
        down_block_types=("DownEncoderBlock2D", "DownEncoderBlock2D"),
        up_block_types=("UpDecoderBlock2D", "UpDecoderBlock2D"),
        layers_per_block=1,
        latent_channels=4,
        norm_num_groups=8,
        scaling_factor=0.18215,
    )
    text_config = CLIPTextConfig(
        vocab_size=3,
        hidden_size=16,
        intermediate_size=32,
        num_hidden_layers=1,
        num_attention_heads=2,
        max_position_embeddings=8,
        projection_dim=16,
        bos_token_id=1,
        eos_token_id=2,
        pad_token_id=0,
    )
    text_encoder = CLIPTextModel(text_config)
    tensors = {
        f"{prefix}.{name}": tensor
        for prefix, module in (("unet", unet), ("vae", vae), ("text_encoder", text_encoder))
        for name, tensor in module.state_dict().items()
    }
    special = {"bos_token": "<|startoftext|>", "eos_token": "<|endoftext|>", "pad_token": "!"}
    metadata = {
        "unet/config.json": unet.to_json_string(),
        "vae/config.json": vae.to_json_string(),
        "text_encoder/config.json": text_config.to_json_string(),
        "tokenizer/vocab.json": json.dumps({"!": 0, "<|startoftext|>": 1, "<|endoftext|>": 2}),
        "tokenizer/merges.txt": "#version: 0.2\n",
        "tokenizer/tokenizer_config.json": json.dumps(
            {**special, "unk_token": "<|endoftext|>", "model_max_length": 8}
        ),
        "tokenizer/special_tokens_map.json": json.dumps(special),
    }
    path = folder / "tiny.safetensors"
    save_file(tensors, str(path), metadata=metadata)
    return str(path)


def test_the_single_file_loads_and_predicts_a_height(tmp_path: Path) -> None:
    import torch

    torch.set_grad_enabled(False)
    model = load_model(tiny_weights(tmp_path), "cpu", torch.float32)
    # The empty prompt: start, end and padding, one embedding per position.
    assert tuple(model.prompt.shape) == (1, 8, 16)
    assert model.task.tolist() == [pytest.approx([np.sin(1), 0, np.cos(1), 1])]
    pixels = np.random.default_rng(1).random((32, 48, 3), dtype=np.float32)
    height = predict(model, pixels)
    assert height.shape == (32, 48)
    assert height.dtype == np.float32
    assert 0 <= height.min() <= height.max() <= 1
    assert np.array_equal(height, predict(model, pixels))  # nothing is sampled
