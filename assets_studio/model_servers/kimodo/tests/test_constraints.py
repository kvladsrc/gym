import json

import pytest
from kimodo_model_server.constraints import read_constraints

from model_server_sdk import InvalidInput


def test_sparse_pose_preserves_explicit_source_values_and_last_frame():
    value = [
        dict(
            type="right-hand",
            frame_indices=[0, 59],
            local_joints_rot=[[[0, 0, 0]] * 77] * 2,
            root_positions=[[0, 1, 0], [0, 1, 0.2]],
        )
    ]
    assert read_constraints(json.dumps(value).encode(), 60) == value


@pytest.mark.parametrize("indices", [[-1], [60], [1, 0], [0, 0], [False]])
def test_frame_indices_must_follow_the_generated_motion_clock(indices):
    value = [dict(type="root2d", frame_indices=indices, smooth_root_2d=[[0, 0]] * len(indices))]
    with pytest.raises(InvalidInput, match="frames"):
        read_constraints(json.dumps(value).encode(), 60)


def test_target_rig_joint_count_is_not_silently_treated_as_soma():
    value = [
        dict(
            type="fullbody",
            frame_indices=[0],
            local_joints_rot=[[[0, 0, 0]] * 52],
            root_positions=[[0, 1, 0]],
        )
    ]
    with pytest.raises(InvalidInput, match="SOMA"):
        read_constraints(json.dumps(value).encode(), 60)


@pytest.mark.parametrize(
    "value",
    [
        [],
        {},
        [dict(type="weapon")],
        [dict(type="root2d", frame_indices=[0], smooth_root_2d=[[float("nan"), 0]])],
    ],
)
def test_invalid_or_nonfinite_constraints_are_rejected(value):
    with pytest.raises(InvalidInput):
        read_constraints(json.dumps(value).encode(), 60)
