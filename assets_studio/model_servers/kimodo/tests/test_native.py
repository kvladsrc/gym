"""Native SMPL-X packages <-> Kimodo's canonical frame; no GPU, weights or Kimodo."""

import json

import numpy as np
import pytest
from asset_processing.native_motion import FORMAT, smpl_bvh
from kimodo_model_server import native
from kimodo_model_server.server import (
    INBETWEEN_TASK,
    KEYFRAMES_TASK,
    NATIVE_TEXT_TASK,
    KeyframesParams,
    KimodoServer,
)
from pydantic import ValidationError
from scipy.spatial.transform import Rotation

from model_server_sdk import create_app

REST = np.array(
    [
        [0, 0, 0],
        [0.09, -0.08, 0],
        [-0.09, -0.08, 0],
        [0, 0.1, 0],
        [0.1, -0.48, 0],
        [-0.1, -0.48, 0],
        [0, 0.23, 0],
        [0.1, -0.88, 0],
        [-0.1, -0.88, 0],
        [0, 0.29, 0],
        [0.1, -0.93, 0.12],
        [-0.1, -0.93, 0.12],
        [0, 0.5, 0],
        [0.07, 0.43, 0],
        [-0.07, 0.43, 0],
        [0, 0.6, 0],
        [0.18, 0.43, 0],
        [-0.18, 0.43, 0],
        [0.43, 0.43, 0],
        [-0.43, 0.43, 0],
        [0.68, 0.43, 0],
        [-0.68, 0.43, 0],
    ],
    dtype=float,
)


def clip_package(frames: int = 12, seed: int = 0) -> bytes:
    rng = np.random.default_rng(seed)
    rotvecs = rng.normal(scale=0.3, size=(frames, 22, 3))
    pelvis = np.stack(
        [np.linspace(1, 2, frames), np.full(frames, 0.95), np.linspace(-1, 0, frames)], 1
    )
    eulers = Rotation.from_rotvec(rotvecs.reshape(-1, 3)).as_euler("ZYX", degrees=True)
    bvh = smpl_bvh(REST.tolist(), eulers.reshape(frames, 22, 3).tolist(), pelvis.tolist())
    return json.dumps(
        {
            "format": FORMAT,
            "coordinate_space": "model-world-y-up",
            "fps": 30,
            "frames": frames,
            "retarget_bvh": bvh,
        }
    ).encode()


def test_bvh_is_read_back_exactly() -> None:
    clip = native.read_clip(clip_package())
    assert np.allclose(clip.rest, REST, atol=1e-5)
    assert clip.frames == 12
    positions, _ = native.forward_kinematics(clip)
    assert np.allclose(positions[:, 0], clip.pelvis)


def test_canonical_frame_puts_the_reference_frame_at_the_origin_facing_z() -> None:
    clip = native.read_clip(clip_package())
    frame = native.canonical_frame(clip, 5, REST * 1.1)
    assert np.isclose(frame.scale, 1.1)
    keys = native.fullbody_keys(clip, [5], frame, [0])
    root = np.array(keys["root_positions"][0])
    assert np.allclose(root[[0, 2]], 0, atol=1e-9)
    # Kimodo-side FK of the key: the hips face +Z.
    local = Rotation.from_rotvec(np.array(keys["local_joints_rot"][0])).as_matrix()
    turned = native.Clip(REST * 1.1, local[None], root[None])
    positions, _ = native.forward_kinematics(turned)
    assert abs(native.heading(positions[0])) < 1e-6


def test_package_round_trip_restores_world_poses_and_path() -> None:
    clip = native.read_clip(clip_package())
    kimodo_rest = REST * 1.07
    frame = native.canonical_frame(clip, clip.frames - 1, kimodo_rest)
    keys = native.fullbody_keys(clip, list(range(clip.frames)), frame, list(range(clip.frames)))
    local = Rotation.from_rotvec(np.array(keys["local_joints_rot"]).reshape(-1, 3)).as_matrix()
    data = native.package(
        local.reshape(clip.frames, 22, 3, 3),
        np.array(keys["root_positions"]),
        kimodo_rest,
        frame,
        "test",
        {},
    )
    back = native.read_clip(data)
    assert np.allclose(back.pelvis, clip.pelvis, atol=1e-4)
    for a, b in zip(back.rotations, clip.rotations, strict=True):
        assert native.pose_difference_deg(a, b) < 0.01
    # Rest proportions are Kimodo's, at the source person's size.
    assert np.isclose(native.leg_length(back.rest), native.leg_length(clip.rest), atol=1e-4)


def test_smplx_variant_declares_native_tasks() -> None:
    server = KimodoServer("smplx")
    create_app(server)
    assert server.model.id == "kimodo-smplx"
    assert [t.task for t in server.tasks] == [
        "text-to-native-motion",
        "inbetween-native-motion",
        "keyframes-to-native-motion",
    ]
    assert (
        NATIVE_TEXT_TASK.output_mime
        == INBETWEEN_TASK.output_mime
        == ("application/vnd.studio.smplx-motion+json",)
    )
    assert [i.role for i in INBETWEEN_TASK.inputs] == ["motion", "continuation"]
    assert KimodoServer().model.id == "kimodo"  # the default stays SOMA


def test_key_frames_are_a_comma_separated_list_of_frame_numbers() -> None:
    assert [i.role for i in KEYFRAMES_TASK.inputs] == ["motion"]
    assert KeyframesParams(frames=" 1, 40 ,95").frames == " 1, 40 ,95"
    with pytest.raises(ValidationError):
        KeyframesParams(frames="1;40")
    with pytest.raises(ValidationError):
        KeyframesParams(frames="")
