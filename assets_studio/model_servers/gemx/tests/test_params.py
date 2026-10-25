import pytest
from gemx_model_server.server import TASK, Params
from pydantic import ValidationError


def test_defaults():
    assert Params().camera == "static"
    assert Params().postprocess is True
    assert TASK.task == "video-to-soma-motion"


def test_rejects_unknown_camera():
    with pytest.raises(ValidationError):
        Params(camera="moving")


def test_image_to_pose_takes_one_picture():
    from gemx_model_server.server import POSE_TASK

    assert POSE_TASK.task == "image-to-pose"
    assert [spec.role for spec in POSE_TASK.inputs] == ["image"]
    assert POSE_TASK.output_mime == ("application/vnd.studio.soma-motion+json",)
