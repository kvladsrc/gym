"""Declarations and the step chain; no GPU, weights or source."""

from pathlib import Path

from unirig_model_server.server import (
    TASK,
    Params,
    UniRigServer,
    counts_of,
    failure,
    steps,
)

from model_server_sdk import create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(UniRigServer())
    assert TASK.task == "3d-to-rig"
    assert [spec.role for spec in TASK.inputs] == ["mesh"]
    assert TASK.output_mime == ("model/x-fbx",)


def test_the_chain_feeds_each_step_the_previous_ones_files() -> None:
    chain = dict(steps(Path("/w"), Params(faces=20000), 7))
    assert list(chain) == ["extract", "skeleton", "extract skeleton", "skin", "merge", "fbx"]
    assert "--faces_target_count=20000" in chain["extract"]
    assert "--seed=7" in chain["skeleton"]
    assert "--npz_dir=/w/mesh" in chain["skeleton"]
    assert "--input=/w/skeleton.fbx" in chain["extract skeleton"]
    assert "--npz_dir=/w/skeleton" in chain["skin"]
    assert "--source=/w/skin.fbx" in chain["merge"]
    assert chain["fbx"][-2:] == ["/w/rigged.glb", "/w/rigged.fbx"]


def test_counts_come_from_the_converter() -> None:
    assert counts_of("noise\n{'bones': 41, 'triangles': 60000}\n") == {
        "bones": 41,
        "triangles": 60000,
    }
    assert counts_of("nothing") == {}


def test_out_of_memory_is_retryable() -> None:
    assert failure("skin", "torch.OutOfMemoryError: CUDA out of memory").retryable
    assert not failure("merge", "KeyError: 'x'").retryable
