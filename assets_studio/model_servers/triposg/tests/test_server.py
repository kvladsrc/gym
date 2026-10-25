"""Declarations and image preparation; no GPU, weights or source."""

import pytest
import trimesh
from PIL import Image
from triposg_model_server.server import (
    TASK,
    TripoSGServer,
    clear_faint,
    frame_on_white,
    is_cut_out,
    reduce_faces,
)

from model_server_sdk import InvalidInput, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(TripoSGServer())
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


def test_the_object_is_centred_in_a_padded_white_square() -> None:
    image = Image.new("RGBA", (200, 100), (0, 0, 0, 0))
    image.paste((255, 0, 0, 255), (10, 20, 110, 70))  # 100 × 50
    framed = frame_on_white(image)
    assert framed.mode == "RGB"
    assert framed.size == (120, 120)
    assert framed.getpixel((60, 60)) == (255, 0, 0)
    assert framed.getpixel((5, 60)) == (255, 255, 255)
    assert framed.getpixel((60, 30)) == (255, 255, 255)
    assert framed.getpixel((60, 40)) == (255, 0, 0)


def test_an_empty_image_is_invalid_input() -> None:
    with pytest.raises(InvalidInput):
        frame_on_white(Image.new("RGBA", (4, 4), (0, 0, 0, 0)))


def test_a_dense_mesh_is_reduced() -> None:
    dense = trimesh.creation.icosphere(subdivisions=5)  # 20480 triangles
    assert len(reduce_faces(dense, 2000).faces) <= 2000
    assert reduce_faces(dense, 0) is dense
    assert reduce_faces(dense, 50000) is dense
