import hashlib
import json

import pytest
from prompthmr_model_server.server import PromptHMRServer, observations

from model_server_sdk import InvalidInput, create_app


def track(video=b"video"):
    return {
        "format": "studio.pose-observations.v1",
        "fps": 30,
        "frames": 2,
        "size": [1920, 1080],
        "source_sha256": hashlib.sha256(video).hexdigest(),
        "K_fullimg": [[[1000, 0, 960], [0, 1000, 540], [0, 0, 1]]] * 2,
        "bbx_xyxy": [[10, 20, 200, 800]] * 2,
        "kp2d": [[[50, 80, 0.9]] * 17] * 2,
    }


def test_contract_returns_a_single_native_text_artifact():
    server = PromptHMRServer()
    create_app(server)
    assert len(server.tasks) == 1
    assert server.tasks[0].output_mime == ("application/vnd.studio.smplx-motion+json",)
    assert [i.role for i in server.tasks[0].inputs] == ["video", "observations"]


def test_exact_video_identity_is_required():
    with pytest.raises(InvalidInput, match="different video"):
        observations(json.dumps(track()).encode(), b"other video")


def test_valid_observations_keep_calibration_and_track():
    assert observations(json.dumps(track()).encode(), b"video") == track()


@pytest.mark.parametrize(
    ("field", "value"),
    [("fps", 25), ("frames", 301), ("kp2d", []), ("bbx_xyxy", [[0, 0, 0, 20]] * 2)],
)
def test_inconsistent_observations_are_rejected_before_loading_a_model(field, value):
    data = track()
    data[field] = value
    with pytest.raises(InvalidInput):
        observations(json.dumps(data).encode(), b"video")
