"""Mesh and image post-processing; no GPU or weights required."""

import numpy as np
import pytest
import trimesh
from PIL import Image
from triposr_model_server import mesh
from triposr_model_server.server import _compose_foreground, _flatten

from model_server_sdk import InvalidInput


def test_linear_vertex_colors_decode_srgb_and_keep_alpha() -> None:
    colors = np.array([[0, 128, 255, 77]], dtype=np.uint8)
    assert mesh.linear_vertex_colors(colors).tolist() == [[0, 55, 255, 77]]


def test_glb_is_y_up_with_metadata() -> None:
    box = trimesh.creation.box(extents=(1, 2, 3))  # x depth, y horizontal, z up
    box.visual.vertex_colors = [255, 0, 0, 255]
    data, meta = mesh.to_glb(box)
    assert data[:4] == b"glTF"
    assert meta["up_axis"] == "Y"
    assert meta["watertight"] is True
    size = np.subtract(*reversed(meta["bounds"]))
    assert size.tolist() == pytest.approx([2, 3, 1])  # tallest axis is now Y


def test_empty_mesh_is_rejected() -> None:
    with pytest.raises(mesh.EmptyMesh):
        mesh.to_glb(trimesh.Trimesh())


def test_foreground_is_centred_on_grey() -> None:
    rgba = np.zeros((100, 200, 4), dtype=np.uint8)
    rgba[40:60, 90:130] = [255, 0, 0, 255]  # 20 x 40 red object
    result = np.asarray(_compose_foreground(Image.fromarray(rgba), 0.5))
    assert result.shape == (78, 78, 3)  # side 39 (upstream crop), padded to 1 / 0.5
    assert result[0, 0].tolist() == [127, 127, 127]
    assert result[39, 39].tolist() == [255, 0, 0]


def test_empty_foreground_is_an_input_error() -> None:
    with pytest.raises(InvalidInput, match="no foreground"):
        _compose_foreground(Image.new("RGBA", (10, 10)), 0.85)


def test_transparent_pixels_become_grey_without_background_removal() -> None:
    rgba = np.zeros((4, 4, 4), dtype=np.uint8)
    rgba[0, 0] = [0, 255, 0, 255]
    result = np.asarray(_flatten(Image.fromarray(rgba)))
    assert result[0, 0].tolist() == [0, 255, 0]
    assert result[3, 3].tolist() == [127, 127, 127]
