"""Declarations and the child-process protocol; no GPU, weights or source."""

from pathlib import Path

import pytest
from skintokens_model_server.server import (
    TASK,
    Params,
    SkinTokensServer,
    failure,
    result_of,
    rig_command,
)

from model_server_sdk import GenerationError, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(SkinTokensServer())
    assert TASK.task == "3d-to-rig"
    assert [spec.role for spec in TASK.inputs] == ["mesh"]
    assert TASK.output_mime == ("model/x-fbx",)


def test_the_child_gets_its_files_params_and_seed() -> None:
    command = rig_command(Path("/w"), Params(num_beams=4), 7)
    assert command[1:3] == ["-m", "skintokens_model_server.rig"]
    assert command[3:5] == ["/w/input.glb", "/w/rigged.fbx"]
    assert Params.model_validate_json(command[5]).num_beams == 4
    assert command[6] == "7"


def test_the_result_is_the_last_result_line() -> None:
    assert result_of('noise\nRESULT {"bones": 3}\nmore noise') == {"bones": 3}
    with pytest.raises(GenerationError):
        result_of("no result here")


def test_out_of_memory_in_the_child_is_retryable() -> None:
    assert failure("torch.OutOfMemoryError: CUDA out of memory").retryable
    assert not failure("ValueError: bad mesh").retryable
