"""Motion assets reach the web viewer as joint positions per frame."""

import json
import math

import pytest
from asset_processing.native_motion import FORMAT, JOINTS, smpl_bvh
from studio.services.skeleton import NotAMotion, skeleton


def test_smplx_bvh_forward_kinematics_rotates_children_about_their_parent() -> None:
    rest: list[list[float]] = []
    for _, parent in JOINTS:  # every bone 10 cm up from its parent
        rest.append([0.0, 0.0, 0.0] if parent < 0 else [0.0, rest[parent][1] + 0.1, 0.0])
    rotations = [[[0.0, 0.0, 0.0] for _ in range(22)] for _ in range(2)]
    rotations[1][0] = [90.0, 0.0, 0.0]  # ZYX degrees: the whole body turned 90 deg about Z
    bvh = smpl_bvh(rest, rotations, [[0.0, 1.0, 0.0], [0.5, 1.0, 0.0]])
    data = {"format": FORMAT, "fps": 30, "frames": 2, "retarget_bvh": bvh}
    result = skeleton(json.dumps(data).encode())
    assert (result["frames"], len(result["names"]), result["parents"][0]) == (2, 22, -1)
    first, second = result["positions"]
    assert first[:3] == [0.0, 1.0, 0.0]
    assert second[:3] == [0.5, 1.0, 0.0]
    # the pelvis' first child is 10 cm above it at rest, 10 cm to -X after the turn
    child = result["parents"].index(0)
    assert first[child * 3 : child * 3 + 3] == [0.0, 1.1, 0.0]
    assert math.dist(second[child * 3 : child * 3 + 3], [0.4, 1.0, 0.0]) < 1e-3


def test_soma_uses_the_stored_world_joints_and_the_nearest_public_parent() -> None:
    package = {
        "format": "studio.soma-motion.v1",
        "fps": 30,
        "joint_names": ["Root", "Hips", "Spine1"],
        # Root is its own parent; a twist helper sits between Hips and Spine1
        "rig": {
            "joint_names": ["Root", "Hips", "Twist", "Spine1"],
            "joint_parent_ids": [0, 0, 1, 2],
        },
        "world": {"joints": [[[0, 1, 0], [0, 1.2, 0]]]},
    }
    result = skeleton(json.dumps(package).encode())
    assert result["names"] == ["Hips", "Spine1"]
    assert result["parents"] == [-1, 0]
    assert result["positions"] == [[0, 1, 0, 0, 1.2, 0]]


def test_other_text_is_not_a_motion() -> None:
    with pytest.raises(NotAMotion):
        skeleton(b"just a description")
    with pytest.raises(NotAMotion):
        skeleton(b'{"format": "studio.pose-observations.v1"}')
