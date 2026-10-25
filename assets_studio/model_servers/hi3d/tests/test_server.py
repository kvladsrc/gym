"""Declarations, mesh reading and placement; no GPU, weights or source."""

import io

import pytest
import trimesh
from hi3d_model_server.server import (
    BUDGET_SHARES,
    TASK,
    Hi3DServer,
    OutOfGpuMemory,
    Params,
    describe,
    is_cut_out,
    place_like,
    read_mesh,
)
from PIL import Image
from pydantic import ValidationError

from model_server_sdk import InvalidInput, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(Hi3DServer())
    assert TASK.task == "3d-refine"
    assert [spec.role for spec in TASK.inputs] == ["mesh", "image"]
    assert TASK.output_mime == ("model/gltf-binary",)


def test_only_the_three_grids_are_accepted() -> None:
    assert Params(resolution=1536).resolution == 1536
    with pytest.raises(ValidationError):
        Params(resolution=768)  # type: ignore[arg-type]


def test_a_glb_is_read_and_a_bad_file_refused() -> None:
    buffer = io.BytesIO()
    trimesh.creation.box().export(buffer, file_type="glb")
    assert len(read_mesh(buffer.getvalue()).faces) == 12
    with pytest.raises(InvalidInput):
        read_mesh(b"not a mesh")


def test_the_result_is_put_where_the_coarse_mesh_was() -> None:
    coarse = trimesh.creation.box(extents=(2, 4, 1))
    coarse.apply_translation((10, 0, -3))
    normalised = trimesh.creation.box(extents=(0.5, 1, 0.25))
    placed = place_like(normalised, coarse)
    assert placed.bounds.ravel().tolist() == pytest.approx(coarse.bounds.ravel().tolist())


def test_a_cut_out_image_is_recognised() -> None:
    assert not is_cut_out(Image.new("RGB", (4, 4), "white"))
    assert is_cut_out(Image.new("RGBA", (4, 4), (255, 0, 0, 0)))


def test_metadata_describes_the_mesh() -> None:
    meta = describe(trimesh.creation.box(), 1024)
    assert (meta["triangles"], meta["resolution"], meta["up_axis"]) == (12, 1024, "Y")


def test_running_out_of_memory_is_not_retried_by_the_studio() -> None:
    error = OutOfGpuMemory("decoding")
    assert (error.phase, error.retryable) == ("decoding", False)
    assert "decoding" in str(error)
    assert BUDGET_SHARES[0] == 1.0
    assert list(BUDGET_SHARES) == sorted(BUDGET_SHARES, reverse=True)
