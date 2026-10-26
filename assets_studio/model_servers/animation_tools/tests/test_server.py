import subprocess

import pytest
from animation_tools_model_server.server import (
    BAKE,
    RETOPO,
    AnimationToolsServer,
    BakeParams,
    RetargetParams,
    RetopoParams,
    mesh_report,
    prepare_report,
    run,
)
from pydantic import ValidationError

from model_server_sdk import GenerationError, InvalidInput


def test_bad_params_are_refused():
    with pytest.raises(ValidationError):
        RetargetParams(root_motion="guess")


def test_preparation_report_is_required():
    with pytest.raises(GenerationError, match="report"):
        prepare_report("Blender quit")


def test_invalid_rig_is_user_error(monkeypatch, tmp_path):
    monkeypatch.setattr(
        subprocess,
        "run",
        lambda *a, **k: subprocess.CompletedProcess(a[0], 3, "RIG_INVALID no weights\n", ""),
    )
    with pytest.raises(InvalidInput, match="no weights"):
        run("blender", tmp_path / "prepare.py", "in.fbx", "out.fbx")


def test_timeout_is_a_generation_failure(monkeypatch, tmp_path):
    def timeout(*a, **k):
        raise subprocess.TimeoutExpired(a[0], 1)

    monkeypatch.setattr(subprocess, "run", timeout)
    with pytest.raises(GenerationError, match="exceeded"):
        run("blender", tmp_path / "prepare.py")


def test_server_has_no_gpu_or_model_requirement(monkeypatch):
    monkeypatch.setattr("animation_tools_model_server.server.blender", lambda: "/blender")
    AnimationToolsServer().load()


def test_mesh_tasks_take_glbs_and_bounded_params():
    assert [spec.role for spec in RETOPO.inputs] == ["mesh"]
    assert [spec.role for spec in BAKE.inputs] == ["high", "low"]
    assert RetopoParams().faces == 20_000
    assert RetopoParams(method="autoremesher").method == "autoremesher"
    with pytest.raises(ValidationError):
        RetopoParams(method="instant-meshes")
    with pytest.raises(ValidationError):
        BakeParams(texture_size=3000)


def test_mesh_report_is_required_and_parsed():
    assert mesh_report('noise\nMESH_REPORT {"faces": 12}\n') == {"faces": 12}
    with pytest.raises(GenerationError, match="report"):
        mesh_report("Blender quit")


def test_unusable_mesh_is_user_error(monkeypatch, tmp_path):
    monkeypatch.setattr(
        subprocess,
        "run",
        lambda *a, **k: subprocess.CompletedProcess(
            a[0], 1, "MESH_INVALID the painted mesh has no UVs\n", ""
        ),
    )
    with pytest.raises(InvalidInput, match="no UVs"):
        run("blender", tmp_path / "mesh_tools.py", "bake")
