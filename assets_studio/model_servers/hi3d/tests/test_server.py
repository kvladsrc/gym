"""Declarations, mesh reading and placement; no GPU, weights or source."""

import io
import sys
from types import ModuleType, SimpleNamespace

import pytest
import trimesh
from hi3d_model_server.server import (
    BUDGET_SHARES,
    TASK,
    Hi3DServer,
    OutOfGpuMemory,
    Params,
    describe,
    drop_degenerate,
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


def test_zero_area_triangles_are_dropped() -> None:
    box = trimesh.creation.box()
    # A sliver whose corners lie on one line: no area, like pyfqmr leaves.
    vertices = [*box.vertices, (0.5, 0.5, 0.5), (0.5, 0.0, 0.5), (0.5, -0.5, 0.5)]
    faces = [*box.faces, (8, 9, 10)]
    mesh = trimesh.Trimesh(vertices=vertices, faces=faces, process=False)
    cleaned, removed = drop_degenerate(mesh)
    assert (removed, len(cleaned.faces), len(cleaned.vertices)) == (1, 12, 8)
    assert drop_degenerate(box)[1] == 0


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


def test_only_supported_starting_budgets_are_accepted() -> None:
    assert Params().voxel_budget_percent == 100
    assert Params(voxel_budget_percent=55).voxel_budget_percent == 55
    with pytest.raises(ValidationError):
        Params(voxel_budget_percent=50)  # type: ignore[arg-type]


@pytest.mark.parametrize(
    ("start", "failures", "expected_shares"),
    [(100, 2, (1.0, 0.75, 0.55)), (75, 1, (0.75, 0.55)), (55, 0, (0.55,))],
)
def test_generation_skips_higher_budgets_and_restores_pipeline_state(
    monkeypatch, start, failures, expected_shares
) -> None:
    import torch

    pipeline_module = ModuleType("sparc3d.pipelines.sparc3d_pipeline")
    original_budgets = (80000, 90000, 110000)
    pipeline_module.PASS_BUDGETS = original_budgets
    package = ModuleType("sparc3d.pipelines")
    package.sparc3d_pipeline = pipeline_module
    monkeypatch.setitem(sys.modules, "sparc3d", ModuleType("sparc3d"))
    monkeypatch.setitem(sys.modules, "sparc3d.pipelines", package)
    monkeypatch.setattr(torch.cuda, "empty_cache", lambda: None)
    attempted_budgets = []
    mesh = trimesh.creation.box()

    def pipeline(*args, **kwargs):
        attempted_budgets.append(pipeline_module.PASS_BUDGETS)
        assert kwargs["resolution"] == 1024
        if len(attempted_budgets) <= failures:
            raise OutOfGpuMemory("decoding")
        return SimpleNamespace(mesh=mesh)

    server = Hi3DServer()
    server._pipeline = pipeline
    output = server._one(Image.new("RGB", (4, 4)), mesh, 42, Params(voxel_budget_percent=start))
    assert attempted_budgets == [
        tuple(round(budget * share) for budget in original_budgets) for share in expected_shares
    ]
    assert output.meta["initial_voxel_budget_percent"] == start
    assert output.meta["voxel_budget_share"] == expected_shares[-1]
    assert pipeline_module.PASS_BUDGETS is original_budgets


def test_oom_at_the_smallest_budget_fails_and_restores_pipeline_state(monkeypatch) -> None:
    import torch

    pipeline_module = ModuleType("sparc3d.pipelines.sparc3d_pipeline")
    original_budgets = (80000, 90000, 110000)
    pipeline_module.PASS_BUDGETS = original_budgets
    package = ModuleType("sparc3d.pipelines")
    package.sparc3d_pipeline = pipeline_module
    monkeypatch.setitem(sys.modules, "sparc3d", ModuleType("sparc3d"))
    monkeypatch.setitem(sys.modules, "sparc3d.pipelines", package)
    monkeypatch.setattr(torch.cuda, "empty_cache", lambda: None)
    calls = 0

    def pipeline(*args, **kwargs):
        nonlocal calls
        calls += 1
        raise OutOfGpuMemory("decoding")

    server = Hi3DServer()
    server._pipeline = pipeline
    with pytest.raises(OutOfGpuMemory):
        server._one(
            Image.new("RGB", (4, 4)), trimesh.creation.box(), 42, Params(voxel_budget_percent=55)
        )
    assert calls == 1
    assert pipeline_module.PASS_BUDGETS is original_budgets
