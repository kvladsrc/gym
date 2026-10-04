"""Declarations and image preparation; no GPU, weights or source."""

import pytest
import trimesh
from hunyuan_paint_model_server.server import (
    TASK,
    HunyuanPaintServer,
    clear_faint,
    describe,
    is_cut_out,
    reduce_faces,
)
from PIL import Image

from model_server_sdk import create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(HunyuanPaintServer())
    assert TASK.task == "3d-paint"
    assert [spec.role for spec in TASK.inputs] == ["mesh", "image"]
    assert TASK.output_mime == ("model/gltf-binary",)


def test_a_cut_out_image_is_recognised() -> None:
    opaque = Image.new("RGB", (4, 4), "white")
    cut = Image.new("RGBA", (4, 4), (255, 0, 0, 0))
    assert not is_cut_out(opaque)
    assert is_cut_out(cut)


def test_faint_alpha_is_cleared() -> None:
    image = Image.new("RGBA", (2, 1))
    image.putpixel((0, 0), (1, 2, 3, 20))
    image.putpixel((1, 0), (1, 2, 3, 200))
    assert [clear_faint(image).getchannel("A").getpixel((x, 0)) for x in range(2)] == [0, 200]


def test_a_dense_mesh_is_reduced_before_painting() -> None:
    pytest.importorskip("pymeshlab")
    dense = trimesh.creation.icosphere(subdivisions=5)  # 20480 triangles
    assert len(reduce_faces(dense, 2000).faces) <= 2000
    assert reduce_faces(dense, 0) is dense
    assert reduce_faces(dense, 50000) is dense


def test_metadata_tells_a_reduction() -> None:
    box = trimesh.creation.box()
    assert describe(box, 2048, 5000)["reduced_from"] == 5000
    assert "reduced_from" not in describe(box, 2048, len(box.faces))
