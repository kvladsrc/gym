"""SMPL body-motion artifacts and BVH exchange; no SDK or neural dependencies."""

import json
import math
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Any

FORMAT = "studio.smplx-motion.v1"
MAX_NATIVE_FRAMES = 18_000  # Joined shots, not the neural model's input window.
# SMPL's first 22 joints, named for the existing SMPL-to-MIA transfer profile.
JOINTS = (
    ("Hips", -1),
    ("LeftUpLeg", 0),
    ("RightUpLeg", 0),
    ("Spine", 0),
    ("LeftLeg", 1),
    ("RightLeg", 2),
    ("Spine1", 3),
    ("LeftFoot", 4),
    ("RightFoot", 5),
    ("Spine2", 6),
    ("LeftToeBase", 7),
    ("RightToeBase", 8),
    ("Neck", 9),
    ("LeftShoulder", 9),
    ("RightShoulder", 9),
    ("Head", 12),
    ("LeftArm", 13),
    ("RightArm", 14),
    ("LeftForeArm", 16),
    ("RightForeArm", 17),
    ("LeftHand", 18),
    ("RightHand", 19),
)


def smpl_bvh(
    rest: Sequence[Sequence[float]],
    eulers_zyx: Sequence[Sequence[Sequence[float]]],
    pelvis: Sequence[Sequence[float]],
) -> str:
    """Y-up metres; Euler values already converted from local axis-angle by the worker."""
    frames = len(eulers_zyx)
    if not 2 <= frames <= 300 or len(rest) != 22 or len(pelvis) != frames:
        raise ValueError("SMPL motion needs 22 joints and 2–300 matching frames")
    children = {i: [j for j, (_, p) in enumerate(JOINTS) if p == i] for i in range(22)}
    lines = ["HIERARCHY"]
    order: list[int] = []

    def joint(i: int, depth: int) -> None:
        order.append(i)
        name, parent = JOINTS[i]
        pad = "  " * depth
        offset = [rest[i][a] - rest[parent][a] if parent >= 0 else 0.0 for a in range(3)]
        channels = "Xposition Yposition Zposition " if parent < 0 else ""
        lines.extend(
            [
                f"{pad}{'ROOT' if parent < 0 else 'JOINT'} {name}",
                f"{pad}{{",
                f"{pad}  OFFSET " + " ".join(f"{v:.6f}" for v in offset),
                f"{pad}  CHANNELS {6 if parent < 0 else 3} {channels}Zrotation Yrotation Xrotation",
            ]
        )
        for child in children[i]:
            joint(child, depth + 1)
        if not children[i]:
            lines.extend(
                [f"{pad}  End Site", f"{pad}  {{", f"{pad}    OFFSET 0 0.05 0", f"{pad}  }}"]
            )
        lines.append(f"{pad}}}")

    joint(0, 0)
    lines.extend(["MOTION", f"Frames: {frames}", "Frame Time: 0.033333"])
    for frame, position in zip(eulers_zyx, pelvis, strict=True):
        values = [*position, *(v for i in order for v in frame[i])]
        if len(values) != 69 or not all(math.isfinite(v) for v in values):
            raise ValueError("invalid SMPL motion channel values")
        lines.append(" ".join(f"{v:.5f}" for v in values))
    return "\n".join(lines) + "\n"


@dataclass(frozen=True)
class NativeMotion:
    bvh: str
    frames: int
    source: dict[str, Any]


def read_native_motion(data: bytes) -> NativeMotion:
    """Validate the fixed body/BVH exchange before passing data to Blender."""
    if len(data) > 32 * 1024 * 1024:
        raise ValueError("native motion exceeds 32 MiB")
    try:
        package = json.loads(data)
        if package["format"] != FORMAT or package["fps"] != 30:
            raise ValueError("unsupported native motion format/fps")
        frames = package["frames"]
        if type(frames) is not int or not 2 <= frames <= MAX_NATIVE_FRAMES:
            raise ValueError(f"native motion needs 2–{MAX_NATIVE_FRAMES} frames")
        if package["coordinate_space"] != "model-world-y-up":
            raise ValueError("native retargeting requires model-world-y-up coordinates")
        bvh = package["retarget_bvh"]
        lines = bvh.splitlines()
        bones = [line.split()[1] for line in lines if line.strip().startswith(("ROOT ", "JOINT "))]
        if len(bones) != 22 or set(bones) != {name for name, _ in JOINTS}:
            raise ValueError("native motion must contain the 22 SMPL/Mixamo body joints")
        marker = lines.index("MOTION")
        if lines[marker + 1] != f"Frames: {frames}" or lines[marker + 2] != "Frame Time: 0.033333":
            raise ValueError("native BVH timing disagrees with the package")
        motion = lines[marker + 3 :]
        if len(motion) != frames:
            raise ValueError("native BVH frame count disagrees with the package")
        for line in motion:
            values = list(map(float, line.split()))
            if len(values) != 69 or not all(math.isfinite(v) for v in values):
                raise ValueError("native BVH contains invalid channels")
        return NativeMotion(bvh, frames, package)
    except (KeyError, TypeError, AttributeError, IndexError, UnicodeError) as error:
        raise ValueError(f"invalid native motion: {error}") from error
