"""A motion asset as a light skeleton for the web viewer: joint positions per frame.

Two native formats are read: ``studio.smplx-motion.v1`` (its BVH, 22 SMPL-X joints, forward
kinematics here) and ``studio.soma-motion.v1`` (GEM-X; its 77 world joints are stored). Both
are Y-up metres. A 10 MB package becomes a few hundred kilobytes; very long clips are sampled.
"""

import json
import math
from typing import Any, cast

MAX_FRAMES = 3600  # longer clips are sampled down to about this many frames


class NotAMotion(ValueError):
    """The asset is not a motion the viewer can show."""


def skeleton(data: bytes) -> dict[str, Any]:
    try:
        loaded: object = json.loads(data)
    except (ValueError, UnicodeError) as error:
        raise NotAMotion("not JSON") from error
    if not isinstance(loaded, dict):
        raise NotAMotion("not a motion package")
    package = cast(dict[str, Any], loaded)
    kind = package.get("format")
    if kind == "studio.smplx-motion.v1":
        names, parents, frames = _bvh(str(package["retarget_bvh"]))
    elif kind == "studio.soma-motion.v1":
        names, parents, frames = _soma(package)
    else:
        raise NotAMotion(f"unsupported format: {kind}")
    fps = float(package.get("fps", 30))
    stride = max(1, math.ceil(len(frames) / MAX_FRAMES))
    return {
        "format": kind,
        "fps": fps / stride,
        "stride": stride,
        "frames": len(frames[::stride]),
        "source_frames": len(frames),
        "names": names,
        "parents": parents,
        # metres, millimetre precision: per frame a flat [x, y, z, x, y, z, ...]
        "positions": [
            [round(v, 3) for point in frame for v in point] for frame in frames[::stride]
        ],
    }


def _soma(package: dict[str, Any]) -> tuple[list[str], list[int], list[list[list[float]]]]:
    names = [str(n) for n in package["joint_names"][1:]]  # without Root: the 77 public joints
    rig = package["rig"]
    rig_names = [str(n) for n in rig["joint_names"]]
    rig_parents = [int(p) for p in rig["joint_parent_ids"]]
    index = {name: i for i, name in enumerate(names)}
    parents: list[int] = []
    for name in names:
        # the nearest public ancestor in the full rig (it also has twist helpers and Root)
        joint = rig_names.index(name)
        current = rig_parents[joint]
        # the rig's Root is its own parent
        while current != joint and rig_names[current] not in index:
            joint, current = current, rig_parents[current]
        parents.append(index.get(rig_names[current], -1))
    frames = cast(list[list[list[float]]], package["world"]["joints"])
    return names, parents, frames


def _bvh(text: str) -> tuple[list[str], list[int], list[list[list[float]]]]:
    lines = text.splitlines()
    names: list[str] = []
    parents: list[int] = []
    offsets: list[list[float]] = []
    channels: list[list[str]] = []
    stack: list[int] = []
    end_site = False
    i = 0
    while i < len(lines) and lines[i].strip() != "MOTION":
        words = lines[i].split()
        if words and words[0] in ("ROOT", "JOINT"):
            parents.append(stack[-1] if stack else -1)
            names.append(words[1])
            offsets.append([0.0, 0.0, 0.0])
            channels.append([])
        elif words and words[0] == "End":
            end_site = True
        elif words and words[0] == "{":
            stack.append(-2 if end_site else len(names) - 1)
        elif words and words[0] == "}":
            if stack.pop() == -2:
                end_site = False
        elif words and words[0] == "OFFSET" and not end_site:
            offsets[-1] = [float(v) for v in words[1:4]]
        elif words and words[0] == "CHANNELS":
            channels[-1] = words[2:]
        i += 1
    rows = [list(map(float, line.split())) for line in lines[i + 3 :] if line.strip()]
    frames: list[list[list[float]]] = []
    for row in rows:
        at = 0
        world_pos: list[list[float]] = []
        world_rot: list[list[list[float]]] = []
        for joint, parent in enumerate(parents):
            position = list(offsets[joint])
            rotation = _identity()
            for channel in channels[joint]:
                value = row[at]
                at += 1
                if channel.endswith("position"):
                    position["XYZ".index(channel[0])] = value
                else:
                    rotation = _matmul(rotation, _axis(channel[0], math.radians(value)))
            if parent < 0:
                world_rot.append(rotation)
                world_pos.append(position)
            else:
                world_rot.append(_matmul(world_rot[parent], rotation))
                offset = _apply(world_rot[parent], offsets[joint])
                world_pos.append([world_pos[parent][k] + offset[k] for k in range(3)])
        frames.append(world_pos)
    return names, parents, frames


def _identity() -> list[list[float]]:
    return [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]


def _axis(axis: str, angle: float) -> list[list[float]]:
    c, s = math.cos(angle), math.sin(angle)
    if axis == "X":
        return [[1, 0, 0], [0, c, -s], [0, s, c]]
    if axis == "Y":
        return [[c, 0, s], [0, 1, 0], [-s, 0, c]]
    return [[c, -s, 0], [s, c, 0], [0, 0, 1]]


def _matmul(a: list[list[float]], b: list[list[float]]) -> list[list[float]]:
    return [[sum(a[r][k] * b[k][c] for k in range(3)) for c in range(3)] for r in range(3)]


def _apply(m: list[list[float]], v: list[float]) -> list[float]:
    return [sum(m[r][k] * v[k] for k in range(3)) for r in range(3)]
