import io

import numpy as np
import pytest
import trimesh
from PIL import Image
from trellis2_paint_model_server.server import (
    TASK,
    TrellisPaintServer,
    prepare_mesh,
    read_image,
    read_mesh,
    verify_surface,
)

from model_server_sdk import GenerationError, InvalidInput, create_app


def test_sdk_contract():
    create_app(TrellisPaintServer())
    assert TASK.task == "3d-paint"
    assert [s.role for s in TASK.inputs] == ["mesh", "image"]


def test_invalid_inputs_are_explained():
    with pytest.raises(InvalidInput, match="mesh"):
        read_mesh(b"not glb")
    buffer = io.BytesIO()
    Image.new("RGB", (4, 4), "white").save(buffer, format="PNG")
    with pytest.raises(InvalidInput, match="BiRefNet"):
        read_image(buffer.getvalue())


def test_placement_uv_and_input_are_preserved():
    mesh = trimesh.creation.box(extents=(2, 4, 1))
    mesh.apply_translation((10, -3, 7))
    mesh.visual = trimesh.visual.TextureVisuals(uv=np.zeros((len(mesh.vertices), 2)))
    original = mesh.vertices.copy()
    prepared, center, scale = prepare_mesh(mesh)
    positions = prepared.vertices.copy()
    positions[:, 1], positions[:, 2] = positions[:, 2].copy(), -positions[:, 1].copy()
    assert np.allclose(positions / scale + center, original)
    assert np.array_equal(mesh.vertices, original)
    assert np.array_equal(prepared.visual.uv, mesh.visual.uv)


def test_uv_seam_duplicates_and_face_reordering_keep_the_surface():
    mesh = trimesh.creation.box()
    vertices = mesh.vertices[mesh.faces[::-1]].reshape(-1, 3)
    output = trimesh.Trimesh(vertices, np.arange(len(vertices)).reshape(-1, 3), process=False)
    assert verify_surface(mesh, output) < 1e-12
    output.vertices += (0.1, 0, 0)
    with pytest.raises(GenerationError, match="surface"):
        verify_surface(mesh, output)


def test_changed_triangle_count_is_rejected():
    mesh = trimesh.creation.box()
    with pytest.raises(GenerationError, match="triangle count"):
        verify_surface(mesh, trimesh.creation.icosphere(subdivisions=1))
