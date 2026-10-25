"""The native boundary preserves body channels and rejects incompatible artifacts."""

import json

import pytest
from asset_processing.native_motion import FORMAT, read_native_motion, smpl_bvh


def package() -> dict[str, object]:
    rest = [[0.0, i * 0.1, 0.0] for i in range(22)]
    rotations = [[[0.0, 0.0, 0.0] for _ in range(22)] for _ in range(2)]
    rotations[1][15] = [10.0, 20.0, 30.0]
    bvh = smpl_bvh(rest, rotations, [[0.0, 1.0, 0.0], [0.1, 1.1, 0.2]])
    return {
        "format": FORMAT,
        "fps": 30,
        "frames": 2,
        "coordinate_space": "model-world-y-up",
        "retarget_bvh": bvh,
    }


def test_native_body_roundtrip_preserves_head_rotation_and_root_channels() -> None:
    data = package()
    parsed = read_native_motion(json.dumps(data).encode())
    assert parsed.frames == 2
    assert parsed.bvh == data["retarget_bvh"]
    last = list(map(float, parsed.bvh.splitlines()[-1].split()))
    assert last[:3] == [0.1, 1.1, 0.2]
    assert "10.00000 20.00000 30.00000" in parsed.bvh
    assert len(last) == 69


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("fps", 25),
        ("format", "unknown"),
        ("coordinate_space", "camera"),
        ("frames", True),
        ("frames", 3),
    ],
)
def test_bad_native_boundary_is_rejected(field: str, value: object) -> None:
    data = package()
    data[field] = value
    with pytest.raises(ValueError, match=r"unsupported|requires|frames|timing"):
        read_native_motion(json.dumps(data).encode())


def test_nonfinite_bvh_is_rejected() -> None:
    data = package()
    data["retarget_bvh"] = str(data["retarget_bvh"]).replace("10.00000", "nan")
    with pytest.raises(ValueError, match="invalid channels"):
        read_native_motion(json.dumps(data).encode())


def test_wrong_skeleton_is_rejected() -> None:
    data = package()
    data["retarget_bvh"] = str(data["retarget_bvh"]).replace("LeftHand", "UnknownJoint")
    with pytest.raises(ValueError, match="22 SMPL"):
        read_native_motion(json.dumps(data).encode())
