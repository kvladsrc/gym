"""Declarations, image preparation and the child protocol; no GPU or weights."""

import io
from pathlib import Path

import pytest
from hunyuan_paint21_model_server.paint import prepare_image
from hunyuan_paint21_model_server.server import (
    TASK,
    HunyuanPaint21Server,
    Params,
    failure,
    paint_command,
    result_of,
)
from PIL import Image

from model_server_sdk import GenerationError, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(HunyuanPaint21Server())
    assert TASK.task == "3d-paint"
    assert [spec.role for spec in TASK.inputs] == ["mesh", "image"]


def test_the_child_gets_the_work_directory_and_params() -> None:
    command = paint_command(Path("/w"), Params(views=8))
    assert command[1:4] == ["-m", "hunyuan_paint21_model_server.paint", "/w"]
    assert Params.model_validate_json(command[4]).views == 8


def test_a_cut_out_image_keeps_its_alpha_and_loses_faint_noise() -> None:
    image = Image.new("RGBA", (2, 1))
    image.putpixel((0, 0), (1, 2, 3, 20))
    image.putpixel((1, 0), (1, 2, 3, 200))
    data = io.BytesIO()
    image.save(data, "PNG")
    prepared = prepare_image(data.getvalue(), remove_background=True)
    assert [prepared.getchannel("A").getpixel((x, 0)) for x in range(2)] == [0, 200]


def test_results_and_failures() -> None:
    assert result_of('x\nRESULT {"views": 6}') == {"views": 6}
    with pytest.raises(GenerationError):
        result_of("nothing")
    assert failure("torch.OutOfMemoryError: CUDA out of memory").retryable


def test_the_doubled_specular_of_blenders_export_is_dropped(tmp_path) -> None:
    import json
    import struct

    from hunyuan_paint21_model_server.paint import plain_pbr

    specular = {"KHR_materials_specular": {"specularColorFactor": [2, 2, 2]}}
    pbr = {"metallicRoughnessTexture": {"index": 1}}
    gltf = {
        "asset": {"version": "2.0"},
        "extensionsUsed": ["KHR_materials_specular"],
        "materials": [{"name": "m", "extensions": specular, "pbrMetallicRoughness": pbr}],
    }
    chunk = json.dumps(gltf).encode()
    chunk += b" " * (-len(chunk) % 4)
    binary = b"\x01\x02\x03\x04"
    rest = struct.pack("<II", len(binary), 0x004E4942) + binary
    data = (
        struct.pack("<III", 0x46546C67, 2, 20 + len(chunk) + len(rest))
        + struct.pack("<II", len(chunk), 0x4E4F534A)
        + chunk
        + rest
    )
    path = tmp_path / "m.glb"
    path.write_bytes(data)
    plain_pbr(path)
    out = path.read_bytes()
    n = struct.unpack("<I", out[12:16])[0]
    fixed = json.loads(out[20 : 20 + n])
    assert "extensionsUsed" not in fixed
    assert "extensions" not in fixed["materials"][0]
    assert fixed["materials"][0]["pbrMetallicRoughness"] == {
        "metallicRoughnessTexture": {"index": 1}
    }
    assert out.endswith(rest)
    assert struct.unpack("<I", out[8:12])[0] == len(out)


def test_roughness_is_raised_to_the_floor_and_nothing_else_changes(tmp_path: Path) -> None:
    import io

    import numpy as np
    from hunyuan_paint21_model_server.paint import floor_roughness, read_glb, write_glb
    from PIL import Image

    def png(pixels: np.ndarray) -> bytes:
        buffer = io.BytesIO()
        Image.fromarray(pixels.astype(np.uint8)).save(buffer, format="PNG")
        return buffer.getvalue()

    colour = png(np.full((2, 2, 3), (10, 20, 30)))
    mr = png(np.array([[[0, 50, 200], [0, 250, 0]], [[0, 100, 7], [0, 0, 0]]]))
    gltf = {
        "asset": {"version": "2.0"},
        "buffers": [{"byteLength": len(colour) + len(mr)}],
        "bufferViews": [
            {"buffer": 0, "byteOffset": 0, "byteLength": len(colour)},
            {"buffer": 0, "byteOffset": len(colour), "byteLength": len(mr)},
        ],
        "images": [
            {"bufferView": 0, "mimeType": "image/png"},
            {"bufferView": 1, "mimeType": "image/png"},
        ],
        "textures": [{"source": 0}, {"source": 1}],
        "materials": [
            {
                "pbrMetallicRoughness": {
                    "baseColorTexture": {"index": 0},
                    "metallicRoughnessTexture": {"index": 1},
                }
            }
        ],
    }
    path = tmp_path / "m.glb"
    write_glb(path, gltf, colour + mr)
    floor_roughness(path, 0.6)
    fixed, binary = read_glb(path)

    def image(index: int) -> np.ndarray:
        view = fixed["bufferViews"][fixed["images"][index]["bufferView"]]
        start = view["byteOffset"]
        assert start % 4 == 0
        return np.asarray(Image.open(io.BytesIO(binary[start : start + view["byteLength"]])))

    assert (image(0) == (10, 20, 30)).all()
    painted = image(1)
    assert painted[..., 1].tolist() == [[153, 250], [153, 153]]  # 0.6 * 255, rounded
    assert painted[..., 2].tolist() == [[200, 0], [7, 0]]  # metallic as it was
    assert fixed["buffers"][0]["byteLength"] <= len(binary)


def test_fully_matte_means_roughness_one_and_no_metal(tmp_path: Path) -> None:
    import io

    import numpy as np
    from hunyuan_paint21_model_server.paint import floor_roughness, read_glb, write_glb
    from PIL import Image

    buffer = io.BytesIO()
    Image.fromarray(np.array([[[0, 40, 230], [0, 200, 10]]], dtype=np.uint8)).save(buffer, "PNG")
    mr = buffer.getvalue()
    gltf = {
        "asset": {"version": "2.0"},
        "buffers": [{"byteLength": len(mr)}],
        "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": len(mr)}],
        "images": [{"bufferView": 0, "mimeType": "image/png"}],
        "textures": [{"source": 0}],
        "materials": [{"pbrMetallicRoughness": {"metallicRoughnessTexture": {"index": 0}}}],
    }
    path = tmp_path / "m.glb"
    write_glb(path, gltf, mr)
    floor_roughness(path, 1.0, 0.0)
    fixed, binary = read_glb(path)
    view = fixed["bufferViews"][0]
    pixels = np.asarray(Image.open(io.BytesIO(binary[: view["byteLength"]])))
    assert pixels[..., 1].tolist() == [[255, 255]]
    assert pixels[..., 2].tolist() == [[0, 0]]
