"""Declarations, the point cloud, the checkpoint reader and placement; no GPU, weights or source."""

import collections
import enum
import io
import pickle
import sys
import types
from pathlib import Path

import numpy as np
import pytest
import torch
import trimesh
from bpt_model_server.server import (
    EXTENT,
    MAX_TOKENS,
    POINTS,
    TASK,
    TRAINING_STATE,
    BptServer,
    Params,
    describe,
    edge_faults,
    frame_of,
    placed,
    point_cloud,
    read_mesh,
    read_weights,
)
from pydantic import ValidationError

from model_server_sdk import InvalidInput, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(BptServer())
    assert TASK.task == "3d-retopo"
    assert [spec.role for spec in TASK.inputs] == ["mesh"]
    assert TASK.output_mime == ("model/gltf-binary",)
    assert str(MAX_TOKENS // 1000) in BptServer.model.details["limits"]


def test_the_temperature_defaults_to_upstreams_and_is_never_zero() -> None:
    assert Params().temperature == 0.5
    for value in (0.0, 1.6):  # upstream divides the logits by it
        with pytest.raises(ValidationError):
            Params(temperature=value)


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
    # Centred, the longest side (Y) 1.9 long, the axes in the input's order.
    assert np.abs(points).max(axis=0) == pytest.approx(
        [EXTENT / 4, EXTENT / 2, EXTENT / 8], abs=2e-3
    )
    assert np.linalg.norm(normals, axis=1) == pytest.approx(1.0, abs=1e-3)
    on_top = normals[:, 1] > 0.5
    assert on_top.any()
    assert points[on_top][:, 1] == pytest.approx(EXTENT / 2, abs=2e-3)
    assert np.array_equal(cloud, point_cloud(mesh))  # the same for every seed


def test_decoded_vertices_return_to_the_input_place_and_size() -> None:
    mesh = trimesh.creation.box(extents=(2.0, 4.0, 1.0))
    mesh.apply_translation((10.0, 0.0, -3.0))
    centre, size = frame_of(mesh)
    in_model_frame = (mesh.vertices - centre) * (EXTENT / size)
    assert np.abs(in_model_frame).max() == pytest.approx(EXTENT / 2)
    assert placed(in_model_frame, centre, size) == pytest.approx(mesh.vertices)


def training_run_modules() -> dict[str, types.ModuleType]:
    """Modules with the classes a training run leaves in its checkpoint,
    under the names the published one refers to."""

    class LossScaler:
        def __init__(self) -> None:
            self.cur_scale = 65536.0

    class ZeroStageEnum(enum.Enum):
        gradients = 2

    fragment_address = collections.namedtuple("fragment_address", "numel start")
    modules: dict[str, types.ModuleType] = {}
    for made, (path, name) in zip(
        (LossScaler, ZeroStageEnum, fragment_address), TRAINING_STATE, strict=True
    ):
        parts = path.split(".")
        for depth in range(1, len(parts) + 1):
            modules.setdefault(".".join(parts[:depth]), types.ModuleType(".".join(parts[:depth])))
        made.__module__, made.__qualname__ = path, name
        setattr(modules[path], name, made)
    return modules


def test_weights_are_read_without_the_training_runs_classes(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    weights = collections.OrderedDict(a=torch.arange(6, dtype=torch.bfloat16).reshape(2, 3))
    with monkeypatch.context() as patch:
        modules = training_run_modules()
        for path, module in modules.items():
            patch.setitem(sys.modules, path, module)
        scaler, stage, address = (getattr(modules[path], name) for path, name in TRAINING_STATE)
        checkpoint = {
            "model": weights,
            "optimizer": {"scaler": scaler(), "stage": stage.gradients, "address": address(4, 0)},
        }
        torch.save(checkpoint, tmp_path / "checkpoint.pt")
    assert not any(path in sys.modules for path, _ in TRAINING_STATE)
    read = read_weights(tmp_path / "checkpoint.pt")
    assert list(read) == ["a"]
    assert read["a"].dtype == torch.bfloat16
    assert torch.equal(read["a"], weights["a"])


def test_a_checkpoint_naming_anything_else_is_refused(tmp_path: Path) -> None:
    torch.save({"model": {}, "other": Path("somewhere")}, tmp_path / "checkpoint.pt")
    with pytest.raises(pickle.UnpicklingError):
        read_weights(tmp_path / "checkpoint.pt")


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
