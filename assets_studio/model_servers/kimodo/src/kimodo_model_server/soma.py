"""Native SOMA motion (`studio.soma-motion.v1`) <-> Kimodo-SOMA; numpy only, no GPU or weights.

GEM-X writes the 77 public SOMA joints per frame in a Y-up world (a pose is one frame), in the
same names and order as Kimodo's SOMA 77 skeleton. Kimodo's full-body key constrains global
joint positions only (its rotations are not used), so a key is the source pose carried onto
Kimodo's body: the same bone directions with Kimodo's bone lengths, in Kimodo's canonical frame
(the root of the first key at XZ = 0 facing +Z, the source floor on Kimodo's floor, sized by
the leg-length ratio). The result goes back into the source world at the source person's size,
as with SMPL-X (native.py).
"""

import json
from dataclasses import dataclass
from typing import Any

import numpy as np

from .native import Frame

FORMAT = "studio.soma-motion.v1"
FPS = 30
LEG = ("LeftLeg", "LeftShin", "LeftFoot")  # hip, knee, ankle
HIPS = ("LeftLeg", "RightLeg")
FEET = ("LeftFoot", "LeftToeBase", "LeftToeEnd", "RightFoot", "RightToeBase", "RightToeEnd")


@dataclass(frozen=True)
class Skeleton:
    """A body Kimodo generates on: joint names, parents (-1 for the root), rest positions."""

    names: list[str]
    parents: list[int]
    rest: np.ndarray  # (J, 3)

    def index(self, name: str) -> int:
        return self.names.index(name)


@dataclass(frozen=True)
class Clip:
    names: list[str]  # the 77 public joints, without Root
    joints: np.ndarray  # (T, 77, 3) world positions, Y up, metres
    # (T, 77, 3, 3) global joint rotations in the world on Kimodo's SOMA 77 skeleton, when the
    # package has them ("world": "rotations", rotation vectors): a pose fitted on Kimodo's body
    rotations: np.ndarray | None = None

    @property
    def frames(self) -> int:
        return len(self.joints)

    def at(self, name: str) -> np.ndarray:
        return self.joints[:, self.names.index(name)]


def read_clip(data: bytes) -> Clip:
    try:
        package = json.loads(data)
    except ValueError as error:
        raise ValueError(f"not a SOMA motion: {error}") from error
    if not isinstance(package, dict) or package.get("format") != FORMAT:
        raise ValueError(f"not a {FORMAT} package")
    names = [str(name) for name in package["joint_names"]]
    if names and names[0] == "Root":
        names = names[1:]
    joints = np.asarray(package["world"]["joints"], dtype=float)
    if joints.ndim != 3 or joints.shape[1:] != (len(names), 3) or not len(joints):
        raise ValueError(f"world joints must be (frames, {len(names)}, 3)")
    if not np.isfinite(joints).all():
        raise ValueError("world joints are not finite")
    rotations = None
    if "rotations" in package["world"]:
        from scipy.spatial.transform import Rotation

        vectors = np.asarray(package["world"]["rotations"], dtype=float)
        if vectors.shape != joints.shape:
            raise ValueError("world rotations must match the world joints")
        rotations = (
            Rotation.from_rotvec(vectors.reshape(-1, 3)).as_matrix().reshape(*vectors.shape, 3)
        )
    return Clip(names, joints, rotations)


def heading(points: np.ndarray, names: list[str]) -> np.ndarray:
    """Facing angle about +Y (0 = +Z), from the hips as Kimodo does."""
    left, right = (points[..., names.index(name), :] for name in HIPS)
    forward = np.cross(left - right, np.array([0.0, 1.0, 0.0]))
    return np.arctan2(forward[..., 0], forward[..., 2])


def leg_length(points: np.ndarray, names: list[str]) -> float:
    hip, knee, ankle = (points[names.index(name)] for name in LEG)
    return float(np.linalg.norm(knee - hip) + np.linalg.norm(ankle - knee))


def canonical_frame(clip: Clip, index: int, skeleton: Skeleton) -> Frame:
    """Root of frame `index` to XZ = 0 facing +Z, the clip's floor (its lowest foot joint, a
    low percentile over frames) to Kimodo's floor (y = 0), sized to Kimodo's legs."""
    source_leg = float(np.median([leg_length(pose, clip.names) for pose in clip.joints]))
    scale = leg_length(skeleton.rest, skeleton.names) / source_leg
    feet = [clip.names.index(name) for name in FEET if name in clip.names]
    floor = float(np.percentile(clip.joints[:, feet, 1].min(axis=1), 5))
    root = clip.at(skeleton.names[skeleton.parents.index(-1)])[index]
    origin = np.array([root[0], floor, root[2]])
    return Frame(origin, float(heading(clip.joints[index], clip.names)), scale)


def onto(skeleton: Skeleton, pose: np.ndarray, names: list[str], frame: Frame) -> np.ndarray:
    """One source pose (77, 3) on Kimodo's body (J, 3), in the canonical frame: the root where
    the frame puts it, each bone along the source bone with Kimodo's length."""
    source = frame.to_canonical(np.stack([pose[names.index(name)] for name in skeleton.names]))
    out = np.zeros_like(source)
    for joint, parent in enumerate(skeleton.parents):  # parents come first
        if parent < 0:
            out[joint] = source[joint]
            continue
        direction = source[joint] - source[parent]
        length = float(np.linalg.norm(skeleton.rest[joint] - skeleton.rest[parent]))
        norm = float(np.linalg.norm(direction))
        if norm < 1e-9:  # coincident in the source: keep Kimodo's rest offset
            out[joint] = out[parent] + skeleton.rest[joint] - skeleton.rest[parent]
        else:
            out[joint] = out[parent] + direction / norm * length
    return out


def _swing(a: np.ndarray, b: np.ndarray) -> np.ndarray:
    """The smallest rotation taking direction `a` to direction `b`."""
    a, b = a / np.linalg.norm(a), b / np.linalg.norm(b)
    axis, cos = np.cross(a, b), float(np.dot(a, b))
    if np.linalg.norm(axis) < 1e-9:
        if cos > 0:
            return np.eye(3)
        other = np.eye(3)[int(np.argmin(np.abs(a)))]
        axis = np.cross(a, other)
        axis /= np.linalg.norm(axis)
        return 2 * np.outer(axis, axis) - np.eye(3)  # half a turn
    k = np.array([[0, -axis[2], axis[1]], [axis[2], 0, -axis[0]], [-axis[1], axis[0], 0]])
    return np.eye(3) + k + k @ k / (1 + cos)


def global_rotations(skeleton: Skeleton, key: np.ndarray) -> np.ndarray:
    """Global joint rotations (J, 3, 3) that put Kimodo's rest body into `key` (J, 3), a pose
    on its own bone lengths (onto): forward kinematics with them gives the key back. A joint
    turns its children's bones: several children fix it (Kabsch), one child gives the swing on
    top of the parent's turn (the twist is the parent's), none keeps the parent's turn.
    Kimodo's post-processing pins the key frames to these rotations."""
    rotations = np.zeros((len(skeleton.names), 3, 3))
    children: list[list[int]] = [[] for _ in skeleton.names]
    for joint, parent in enumerate(skeleton.parents):
        if parent >= 0:
            children[parent].append(joint)
    for joint, parent in enumerate(skeleton.parents):  # parents come first
        inherited = np.eye(3) if parent < 0 else rotations[parent]
        rest = [skeleton.rest[c] - skeleton.rest[joint] for c in children[joint]]
        posed = [key[c] - key[joint] for c in children[joint]]
        rest = [r for r, q in zip(rest, posed, strict=True) if np.linalg.norm(r) > 1e-6]
        posed = [q for q in posed if np.linalg.norm(q) > 1e-6][: len(rest)]
        if len(rest) >= 2:
            h = np.array(rest).T @ np.array(posed)
            u, _, vt = np.linalg.svd(h)
            d = np.sign(np.linalg.det(vt.T @ u.T))
            rotations[joint] = vt.T @ np.diag([1, 1, d]) @ u.T
        elif rest:
            rotations[joint] = _swing(inherited @ rest[0], posed[0]) @ inherited
        else:
            rotations[joint] = inherited
    return rotations


def key_rotations(
    clip: Clip, indices: list[int], skeleton: Skeleton, frame: Frame, keys: np.ndarray
) -> np.ndarray:
    """Global key rotations (K, J, 3, 3) in the canonical frame: the clip's own where it has
    them (a pose fitted on Kimodo's body keeps its twists), else fitted to the key positions."""
    if clip.rotations is None:
        return np.stack([global_rotations(skeleton, key) for key in keys])
    joints = [clip.names.index(name) for name in skeleton.names]
    return np.einsum("ij,kmjl->kmil", frame.rotation(), clip.rotations[indices][:, joints])


def keys(clip: Clip, indices: list[int], skeleton: Skeleton, frame: Frame) -> np.ndarray:
    """Global key positions (K, J, 3) for source frames `indices`."""
    return np.stack([onto(skeleton, clip.joints[i], clip.names, frame) for i in indices])


def key_error_m(result: np.ndarray, wanted: np.ndarray) -> float:
    """Largest joint distance between generated and wanted key poses, metres (canonical)."""
    return float(np.linalg.norm(result - wanted, axis=-1).max())


def package(
    joints: np.ndarray,
    names: list[str],
    parents: list[int],
    frame: Frame,
    model: str,
    source: dict[str, Any],
    rotations: np.ndarray | None = None,
) -> bytes:
    """A native SOMA package from Kimodo's posed joints (T, 77, 3) in the canonical frame. The
    rig lists the joints with a Root before them (its own parent), as GEM-X's does.
    ``rotations`` (T, 77, 3, 3), Kimodo's global joint rotations, go with its canonical joints
    under "kimodo": what its SOMA skin needs for a body (skin there, then to the world)."""
    world = frame.from_canonical(joints)
    extra: dict[str, Any] = {}
    world_rotations = None
    if rotations is not None:
        from scipy.spatial.transform import Rotation

        rotvecs = Rotation.from_matrix(rotations.reshape(-1, 3, 3)).as_rotvec()
        turned = np.einsum("ji,tkjl->tkil", frame.rotation(), rotations)  # R^T: to the world
        world_rotations = (
            Rotation.from_matrix(turned.reshape(-1, 3, 3)).as_rotvec().reshape(*joints.shape)
        )
        extra["kimodo"] = {
            "skeleton": "somaskel77",
            "canonical": {
                "joints": np.asarray(joints).round(6).tolist(),
                "global_rotations": rotvecs.reshape(len(joints), -1, 3).round(6).tolist(),
            },
            "to_world": {
                "origin": frame.origin.round(6).tolist(),
                "yaw": round(frame.yaw, 6),
                "scale": round(frame.scale, 6),
            },
            "note": "rotation vectors; world = rotate_y(yaw)(canonical / scale) + origin, "
            "as the joints under world",
        }
    rig_names = ["Root", *names]
    rig_parents = [0] + [0 if parent < 0 else parent + 1 for parent in parents]
    data = {
        "format": FORMAT,
        "fps": FPS,
        "frames": len(world),
        "model": model,
        "joint_names": rig_names,
        "rig": {"joint_names": rig_names, "joint_parent_ids": rig_parents},
        "world": {"joints": world.round(6).tolist()}
        | ({} if world_rotations is None else {"rotations": world_rotations.round(6).tolist()}),
        "coordinate_spaces": {
            "world": "the source motion's world, Y up, metres, at the source person's size",
        },
        "source": source,
        **extra,
    }
    return json.dumps(data, allow_nan=False).encode()
