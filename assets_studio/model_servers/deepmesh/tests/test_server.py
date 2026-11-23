"""Declarations, the point cloud, the token window and placement; no GPU, weights or source."""

import io

import numpy as np
import pytest
import trimesh
from deepmesh_model_server.server import (
    END,
    EXTENT,
    POINTS,
    START,
    STRIDE,
    TASK,
    VOCABULARY,
    WINDOW,
    DeepMeshServer,
    Params,
    describe,
    edge_faults,
    frame_of,
    placed,
    point_cloud,
    read_mesh,
    window_start,
)
from pydantic import ValidationError

from model_server_sdk import InvalidInput, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(DeepMeshServer())
    assert TASK.task == "3d-retopo"
    assert [spec.role for spec in TASK.inputs] == ["mesh"]
    assert TASK.output_mime == ("model/gltf-binary",)


def test_the_vocabulary_is_upstreams() -> None:
    assert (VOCABULARY, START, END) == (4738, 4736, 4737)


def test_parameters_default_to_upstreams_and_are_bounded() -> None:
    assert (Params().temperature, Params().max_tokens) == (0.5, 90_000)
    with pytest.raises(ValidationError):
        Params(max_tokens=90_001)
    with pytest.raises(ValidationError):
        Params(temperature=-0.1)


def test_a_glb_is_read_and_a_bad_file_refused() -> None:
    buffer = io.BytesIO()
    trimesh.creation.box().export(buffer, file_type="glb")
    assert len(read_mesh(buffer.getvalue()).faces) == 12
    with pytest.raises(InvalidInput):
        read_mesh(b"not a mesh")


def test_the_point_cloud_is_in_the_models_frame() -> None:
    mesh = trimesh.creation.box(extents=(2.0, 4.0, 1.0))
    mesh.apply_translation((10.0, 0.0, -3.0))
    cloud = point_cloud(mesh)
    assert (cloud.shape, cloud.dtype) == ((POINTS, 6), np.float16)
    points, normals = cloud[:, :3].astype(np.float64), cloud[:, 3:].astype(np.float64)
    # Centred, the longest side (the input's Y) 1.9 long, the input's Z first.
    assert np.abs(points).max(axis=0) == pytest.approx(
        [EXTENT / 8, EXTENT / 4, EXTENT / 2], abs=2e-3
    )
    assert np.linalg.norm(normals, axis=1) == pytest.approx(1.0, abs=1e-3)
    # The points whose normal is the input's +Y lie on that side of the box.
    on_top = normals[:, 2] > 0.5
    assert on_top.any()
    assert points[on_top][:, 2] == pytest.approx(EXTENT / 2, abs=2e-3)
    assert np.array_equal(cloud, point_cloud(mesh))  # the same for every seed


def test_the_window_moves_on_every_stride_and_rereads_what_it_keeps() -> None:
    assert [window_start(position) for position in (1, 2, WINDOW)] == [0, 1, WINDOW - 1]
    # Full: the model is given the last STRIDE tokens and writes the next one.
    assert window_start(WINDOW + 1) == STRIDE
    assert window_start(WINDOW + 2) == WINDOW + 1
    assert window_start(WINDOW + STRIDE) == WINDOW + STRIDE - 1
    assert window_start(WINDOW + STRIDE + 1) == WINDOW
    rereads = [p for p in range(1, 30_000) if window_start(p) != p - 1]
    assert rereads == [9001, 13_501, 18_001, 22_501, 27_001]


def test_decoded_vertices_return_to_the_input_place_and_size() -> None:
    mesh = trimesh.creation.box(extents=(2.0, 4.0, 1.0))
    mesh.apply_translation((10.0, 0.0, -3.0))
    centre, size = frame_of(mesh)
    in_model_frame = (mesh.vertices - centre) * (EXTENT / size)
    assert np.abs(in_model_frame).max() == pytest.approx(EXTENT / 2)
    assert placed(in_model_frame, centre, size) == pytest.approx(mesh.vertices)


def test_holes_and_fins_are_counted() -> None:
    box = trimesh.creation.box()
    faces = np.asarray(box.faces)
    assert edge_faults(faces) == (0, 0)
    assert edge_faults(faces[1:]) == (3, 0)
    a, b, _ = faces[0]
    assert edge_faults(np.vstack([faces, [(a, b, 8)]])) == (2, 1)


def test_metadata_describes_the_mesh() -> None:
    meta = describe(trimesh.creation.box())
    assert (meta["vertices"], meta["triangles"], meta["watertight"]) == (8, 12, True)
    assert (meta["open_edges"], meta["non_manifold_edges"]) == (0, 0)
    assert (meta["up_axis"], meta["color"]) == ("Y", "none")
