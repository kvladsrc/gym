"""The server's task, its file and the subject box, with a stand-in for the model."""

import hashlib
import json
import math
import tempfile

import cv2
import numpy as np
import pytest
from model_server_sdk.server import InputData
from pydantic import ValidationError
from vitpose_model_server import server
from vitpose_model_server.pose import crop_window as pose_window
from vitpose_model_server.subject import (
    Follower,
    box_on_plain_background,
    parse_region,
    region_box,
    whole_picture,
)

from model_server_sdk import InvalidInput, Job, create_app

SIZE = (96, 64)
FRAMES = 12
TASK = "video-to-pose-observations"


class DrawnFigure:
    """Stands in for the model: "finds" the joints on the dark figure in the window that
    the model would be shown for the box."""

    released = 0

    def joints(self, frame, box):
        center, size = pose_window(box)
        left, top = np.maximum(np.round(center - size / 2).astype(int), 0)
        right, bottom = np.round(center + size / 2).astype(int)
        rows, columns = np.nonzero(frame[top:bottom, left:right, 0] < 128)
        centre = np.array([columns.mean() + left, rows.mean() + top])
        offsets = np.array([[joint % 3 - 1, joint - 8] for joint in range(17)], dtype=np.float32)
        return (centre + offsets).astype(np.float32), np.full(17, 0.9, dtype=np.float32)

    def release(self):
        self.released += 1


def video(frames=FRAMES, step=3):
    """A dark figure that moves to the right on white."""
    with tempfile.NamedTemporaryFile(suffix=".mp4") as file:
        writer = cv2.VideoWriter(file.name, cv2.VideoWriter.fourcc(*"mp4v"), 12, SIZE)
        assert writer.isOpened()
        for index in range(frames):
            picture = np.full((SIZE[1], SIZE[0], 3), 255, dtype=np.uint8)
            cv2.rectangle(
                picture, (10 + step * index, 20), (20 + step * index, 50), (20, 20, 20), -1
            )
            writer.write(picture)
        writer.release()
        return file.read()


def run(params=None, *, clip, model=None):
    served = server.VitPoseServer()
    served._model = model or DrawnFigure()
    job = Job(
        task=TASK,
        prompt=None,
        inputs={"video": InputData("video/mp4", clip)},
        params=server.Params(**(params or {})),
        seeds=(0,),
    )
    (output,) = served.generate(job)
    return output


@pytest.fixture(scope="module")
def clip():
    return video()


@pytest.fixture(scope="module")
def found(clip):
    return run(clip=clip)


def test_contract_has_one_task_from_a_video_to_observations():
    create_app(server.VitPoseServer())
    (task,) = server.VitPoseServer.tasks
    assert task.task == TASK
    assert task.output_mime == ("text/plain",)
    assert [(spec.role, spec.mime, spec.required) for spec in task.inputs] == [
        ("video", ["video/mp4"], True)
    ]


def test_a_region_is_four_shares_of_the_picture():
    assert parse_region("") is None
    assert parse_region(" 0.25, 0.5,0.75,1 ") == (0.25, 0.5, 0.75, 1.0)
    assert region_box((0.25, 0.5, 0.75, 1.0), (200, 100)) == (49.5, 49.5, 149.5, 99.5)
    assert whole_picture((200, 100)) == (-0.5, -0.5, 199.5, 99.5)
    for wrong in ("1,2,3", "a,b,c,d", "0.5,0.1,0.4,0.9", "0,0,1,1.5"):
        with pytest.raises(ValidationError):
            server.Params(subject_region=wrong)
    assert server.Params(subject_region=" 0,0,1,1 ").subject_region == "0,0,1,1"


def test_a_subject_on_a_plain_background_is_found():
    picture = np.full((64, 96, 3), 250, dtype=np.uint8)
    assert box_on_plain_background(picture) is None
    cv2.rectangle(picture, (30, 10), (50, 40), (40, 60, 80), -1)
    picture[5, 80] = 0  # a speck
    assert box_on_plain_background(picture) == (29.5, 9.5, 50.5, 40.5)
    # A picture with something at its border has no plain background.
    cv2.rectangle(picture, (0, 0), (95, 12), (40, 60, 80), -1)
    assert box_on_plain_background(picture) is None


def test_the_box_follows_the_torso_but_does_not_tremble():
    points = np.zeros((17, 2))
    points[[5, 6, 11, 12]] = [[48, 20], [52, 20], [48, 40], [52, 40]]  # torso at (50, 30)
    scores = np.ones(17)
    follower = Follower((30.0, 10.0, 70.0, 70.0))  # 40 wide: moves after 4 pixels
    follower.follow(points, scores)
    follower.follow(points + np.array([3, 0]), scores)
    assert follower.box == (30.0, 10.0, 70.0, 70.0)
    follower.follow(points + np.array([14, 0]), scores)
    assert follower.box == pytest.approx((40.0, 10.0, 80.0, 70.0))
    follower.follow(points + np.array([60, 0]), scores * 0.1)
    assert follower.box == pytest.approx((40.0, 10.0, 80.0, 70.0))


def test_joints_are_found_in_every_frame_of_a_followed_subject(found):
    document = json.loads(found.data)
    assert found.mime == "text/plain"
    assert found.data.endswith(b"\n")
    assert b"\r" not in found.data
    assert (document["format"], document["frames"], document["size"]) == (
        "studio.pose-observations.v1",
        FRAMES,
        list(SIZE),
    )
    assert document["fps"] == pytest.approx(12)
    joints = np.array(document["kp2d"])
    assert joints.shape == (FRAMES, 17, 3)
    # The figure's middle moves 3 pixels a frame from (15, 35), and the box goes with it.
    assert joints[:, 8, :2] == pytest.approx(
        np.stack([15 + 3.0 * np.arange(FRAMES), np.full(FRAMES, 35.0)], axis=1), abs=1.5
    )
    assert document["bbx_xyxy"][0] == pytest.approx([9.5, 19.5, 20.5, 50.5], abs=1.5)
    assert found.meta["subject"] == "found on a plain background"
    assert document["tracking"] == {
        "subject": "found on a plain background",
        "follow": True,
        "box_travel_px": found.meta["box_travel_px"],
    }
    assert found.meta["box_travel_px"][0] > 25


def test_the_file_has_what_a_reader_of_the_format_checks(found, clip):
    """The checks of PromptHMR's `observations` input, apart from its own frame rate."""
    raw = json.loads(found.data)
    frames = raw["frames"]
    assert type(frames) is int
    assert raw["source_sha256"] == hashlib.sha256(clip).hexdigest()
    for field, rows, columns in (("K_fullimg", 3, 3), ("kp2d", 17, 3)):
        values = raw[field]
        assert len(values) == frames
        assert all(len(frame) == rows for frame in values)
        assert all(len(row) == columns for frame in values for row in frame)
        assert all(math.isfinite(value) for frame in values for row in frame for value in row)
    assert len(raw["bbx_xyxy"]) == frames
    assert all(len(box) == 4 and box[2] > box[0] and box[3] > box[1] for box in raw["bbx_xyxy"])
    # The assumed camera: the focal length is the picture's diagonal, its centre the middle.
    assert raw["K_fullimg"][0] == [
        [pytest.approx(math.hypot(*SIZE)), 0, SIZE[0] / 2],
        [0, pytest.approx(math.hypot(*SIZE)), SIZE[1] / 2],
        [0, 0, 1],
    ]


def test_the_subject_can_be_named_and_left_unfollowed(clip):
    output = run({"subject_region": "0,0,0.5,1", "follow": False}, clip=clip)
    document = json.loads(output.data)
    assert document["bbx_xyxy"][0] == document["bbx_xyxy"][-1] == [-0.5, -0.5, 47.5, 63.5]
    assert document["tracking"] == {"subject": "named", "follow": False, "box_travel_px": [0, 0]}
    assert output.meta["subject"] == "named"


def test_the_model_gives_back_memory_after_a_video(clip):
    model = DrawnFigure()
    run(clip=clip, model=model)
    assert model.released == 1


def test_an_unreadable_video_is_rejected():
    with pytest.raises(InvalidInput, match=r"frame rate|cannot read"):
        run(clip=b"not a video")
