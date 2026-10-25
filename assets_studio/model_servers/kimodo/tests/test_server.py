"""Declarations, prompts and the bone names; no GPU, weights or bpy."""

from kimodo_model_server.server import TASK, KimodoServer, sentences
from kimodo_model_server.to_fbx import mixamo_name

from model_server_sdk import create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(KimodoServer())
    assert TASK.task == "text-to-motion"
    assert TASK.prompt == "required"
    assert TASK.output_mime == ("model/x-fbx",)
    assert len(TASK.inputs) == 1
    assert TASK.inputs[0].role == "constraints"
    assert TASK.inputs[0].required is False


def test_each_sentence_is_a_motion() -> None:
    assert sentences("A person walks forward. Then jumps.") == [
        "A person walks forward.",
        "Then jumps.",
    ]
    assert sentences(" . ") == []


def test_soma_joints_take_mixamo_names() -> None:
    assert mixamo_name("Hips") == "mixamorig:Hips"
    assert mixamo_name("Chest") == "mixamorig:Spine2"
    assert mixamo_name("LeftLeg") == "mixamorig:LeftUpLeg"
    assert mixamo_name("LeftShin") == "mixamorig:LeftLeg"
    assert mixamo_name("RightToeEnd") == "mixamorig:RightToe_End"
    assert mixamo_name("LeftHandThumb1") == "mixamorig:LeftHandThumb1"
    assert mixamo_name("LeftHandThumbEnd") == "mixamorig:LeftHandThumb4"
    assert mixamo_name("RightHandIndex2") == "mixamorig:RightHandIndex1"
    assert mixamo_name("RightHandIndexEnd") == "mixamorig:RightHandIndex4"


def test_joints_mixamo_lacks_keep_their_names() -> None:
    for soma in ("Root", "Neck2", "Jaw", "LeftEye", "LeftHandIndex1"):
        assert mixamo_name(soma) is None
