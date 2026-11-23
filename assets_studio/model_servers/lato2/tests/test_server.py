"""Declarations, mesh reading, placement and winding; no GPU, weights or source."""

import io

import numpy as np
import pytest
import trimesh
from lato2_model_server.server import (
    TASK,
    Lato2Server,
    Params,
    describe,
    edge_faults,
    frame_of,
    read_mesh,
    vertices_from_grid,
    wound_like,
)
from pydantic import ValidationError

from model_server_sdk import InvalidInput, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(Lato2Server())
    assert TASK.task == "3d-retopo"
    assert [spec.role for spec in TASK.inputs] == ["mesh"]
    assert TASK.output_mime == ("model/gltf-binary",)


def test_the_vertex_target_stays_in_the_range_the_model_knows() -> None:
    assert Params().vertices == 2000
    for count in (199, 5001):
        with pytest.raises(ValidationError):
            Params(vertices=count)


def test_a_glb_is_read_and_a_bad_file_refused() -> None:
    buffer = io.BytesIO()
    trimesh.creation.box().export(buffer, file_type="glb")
    assert len(read_mesh(buffer.getvalue()).faces) == 12
    with pytest.raises(InvalidInput):
        read_mesh(b"not a mesh")


def test_generated_vertices_return_to_the_input_place_and_size() -> None:
    mesh = trimesh.creation.icosphere(subdivisions=2)
    mesh.apply_scale((1.0, 2.0, 0.5))
    mesh.apply_translation((10.0, 0.0, -3.0))
    centre, size = frame_of(mesh)
    assert [*centre, size] == pytest.approx([10.0, 0.0, -3.0, 4.0])

    # As upstream quantises its input: a voxel of the 1024³ grid over the unit
    # box and an offset from the voxel's centre, in half voxels.
    resolution = 1024
    unit = (mesh.vertices - centre) / size + 0.5
    grid = np.clip(np.floor(unit * resolution), 0, resolution - 1).astype(np.int32)
    offsets = ((unit - (grid + 0.5) / resolution) * 2 * resolution).astype(np.float32)

    back = vertices_from_grid(grid, offsets, resolution, centre, size)
    assert np.abs(back - mesh.vertices).max() < 1e-5
    # Upstream's export drops the half voxel: every vertex half a cell off.
    shifted = vertices_from_grid(grid - 0.5, offsets, resolution, centre, size)
    assert np.abs(shifted - mesh.vertices).max() == pytest.approx(size / (2 * resolution))


def test_triangles_are_wound_like_the_input_surface() -> None:
    detailed = trimesh.creation.icosphere(subdivisions=4)
    coarse = trimesh.creation.icosphere(subdivisions=1)
    faces = np.asarray(coarse.faces).copy()
    faces[::2] = faces[::2][:, ::-1]  # every second one inside out
    vertices = np.asarray(coarse.vertices)

    wound, turned = wound_like(vertices, faces, detailed)
    assert turned == len(faces[::2])
    mesh = trimesh.Trimesh(vertices=vertices, faces=wound, process=False)
    assert mesh.is_winding_consistent
    assert mesh.volume > 0  # outwards
    assert wound_like(vertices, wound, detailed)[1] == 0


def test_holes_and_fins_are_counted() -> None:
    box = trimesh.creation.box()
    faces = np.asarray(box.faces)
    assert edge_faults(faces) == (0, 0)
    assert edge_faults(faces[1:]) == (3, 0)
    a, b, _ = faces[0]
    # A third triangle on an edge of the box; its two other edges are open.
    assert edge_faults(np.vstack([faces, [(a, b, 8)]])) == (2, 1)


def test_metadata_describes_the_mesh() -> None:
    meta = describe(trimesh.creation.box())
    assert (meta["vertices"], meta["triangles"], meta["watertight"]) == (8, 12, True)
    assert (meta["open_edges"], meta["non_manifold_edges"]) == (0, 0)
    assert (meta["up_axis"], meta["color"]) == ("Y", "none")
