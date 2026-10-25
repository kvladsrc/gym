"""Declarations and image preparation; no GPU, weights or source."""

import trimesh
from hunyuan3d21_model_server.server import (
    TASK,
    Hunyuan3D21Server,
    clear_faint,
    describe,
    is_cut_out,
)
from PIL import Image

from model_server_sdk import create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(Hunyuan3D21Server())
    assert TASK.task == "image-to-3d"
    assert [spec.role for spec in TASK.inputs] == ["image"]
    assert TASK.output_mime == ("model/gltf-binary",)


def test_a_cut_out_image_is_recognised() -> None:
    assert not is_cut_out(Image.new("RGB", (4, 4), "white"))
    assert is_cut_out(Image.new("RGBA", (4, 4), (255, 0, 0, 0)))


def test_faint_alpha_is_cleared() -> None:
    image = Image.new("RGBA", (2, 1))
    image.putpixel((0, 0), (1, 2, 3, 20))
    image.putpixel((1, 0), (1, 2, 3, 200))
    assert [clear_faint(image).getchannel("A").getpixel((x, 0)) for x in range(2)] == [0, 200]


def test_metadata_describes_the_mesh() -> None:
    meta = describe(trimesh.creation.box())
    assert (meta["triangles"], meta["watertight"], meta["up_axis"]) == (12, True, "Y")
