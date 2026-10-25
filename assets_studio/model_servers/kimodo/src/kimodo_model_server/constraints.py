"""Validate Kimodo's documented sparse constraints without loading GPU code."""

import json
import math

from model_server_sdk import InvalidInput

TYPES = {"root2d", "fullbody", "left-hand", "right-hand", "left-foot", "right-foot", "end-effector"}


def read_constraints(data: bytes, frames: int, joints: tuple[int, ...] = (30, 77)) -> list[dict]:
    if len(data) > 4_000_000:
        raise InvalidInput("constraints JSON exceeds 4 MB")
    try:
        items = json.loads(data)
    except (ValueError, UnicodeError) as error:
        raise InvalidInput("constraints must be JSON") from error
    if not isinstance(items, list) or not 1 <= len(items) <= 128:
        raise InvalidInput("constraints must be a non-empty list with at most 128 groups")

    def array(value: object, shape: tuple[int, ...]) -> None:
        if not shape:
            if type(value) not in (int, float) or not math.isfinite(value):
                raise InvalidInput("constraint values must be finite numbers")
        elif not isinstance(value, list) or len(value) != shape[0]:
            raise InvalidInput(f"constraint array shape mismatch: expected {shape}")
        else:
            for child in value:
                array(child, shape[1:])

    for item in items:
        if not isinstance(item, dict) or item.get("type") not in TYPES:
            raise InvalidInput("unknown Kimodo constraint type")
        indices = item.get("frame_indices")
        if (
            not isinstance(indices, list)
            or not indices
            or any(type(i) is not int or not 0 <= i < frames for i in indices)
            or indices != sorted(set(indices))
        ):
            raise InvalidInput(
                "constraint frames must be sorted unique 0-based indices within the clip"
            )
        count = len(indices)
        if item["type"] == "root2d":
            array(item.get("smooth_root_2d"), (count, 2))
            if "global_root_heading" in item:
                array(item["global_root_heading"], (count, 2))
        else:
            rots = item.get("local_joints_rot")
            if (
                not isinstance(rots, list)
                or not rots
                or not isinstance(rots[0], list)
                or len(rots[0]) not in joints
            ):
                skeleton = "SMPL-X 22" if joints == (22,) else "SOMA 30 or 77"
                raise InvalidInput(f"constraint rotations must use {skeleton} joints")
            array(rots, (count, len(rots[0]), 3))
            array(item.get("root_positions"), (count, 3))
            if "smooth_root_2d" in item:
                array(item["smooth_root_2d"], (count, 2))
            if item["type"] == "end-effector":
                names = item.get("joint_names")
                if (
                    not isinstance(names, list)
                    or not names
                    or any(
                        n not in ("Hips", "LeftHand", "RightHand", "LeftFoot", "RightFoot")
                        for n in names
                    )
                ):
                    raise InvalidInput("unsupported end-effector joint names")
    return items
