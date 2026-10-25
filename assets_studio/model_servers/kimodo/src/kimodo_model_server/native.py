"""Native SMPL-X body motion <-> Kimodo-SMPLX (22 joints); numpy only, no GPU or weights.

The studio's native package (`studio.smplx-motion.v1`) carries a 22-joint BVH:
the person's rest offsets, the pelvis path and local ZYX Euler rotations, Y-up
metres. GVHMR, HTD-Refine and joined clips all have it, so it is the one input
read here. Kimodo works in a canonical frame (root at XZ = 0 and facing +Z on a
chosen frame) on its own fixed body; positions are scaled by the leg-length ratio
on the way in and back on the way out, so a generated motion lands in the source
clip's world, at the source person's size.
"""

import json
import math
from dataclasses import dataclass
from typing import Any

import numpy as np
from asset_processing.native_motion import FORMAT, JOINTS, read_native_motion, smpl_bvh
from scipy.spatial.transform import Rotation

PARENTS = [parent for _, parent in JOINTS]
NAMES = [name for name, _ in JOINTS]
LEG = (1, 4, 7)  # left hip, knee, ankle: Kimodo's SMPL-X order is the same as JOINTS
FPS = 30


@dataclass(frozen=True)
class Clip:
    rest: np.ndarray  # (22, 3) joint positions in the rest pose, pelvis at the origin
    rotations: np.ndarray  # (T, 22, 3, 3) local rotation matrices
    pelvis: np.ndarray  # (T, 3) world pelvis positions, Y-up metres

    @property
    def frames(self) -> int:
        return len(self.pelvis)


def read_clip(data: bytes) -> Clip:
    """Parse a native package's BVH (validated by the shared reader first)."""
    native = read_native_motion(data)
    lines = native.bvh.splitlines()
    order, offsets = [], {}
    for i, line in enumerate(lines):
        words = line.split()
        if words and words[0] in ("ROOT", "JOINT"):
            name = words[1]
            order.append(NAMES.index(name))
            offsets[name] = [float(v) for v in lines[i + 2].split()[1:4]]
    rest = np.zeros((22, 3))
    for index, (name, parent) in enumerate(JOINTS):
        if parent >= 0:
            rest[index] = rest[parent] + offsets[name]
    motion = lines[lines.index("MOTION") + 3 :]
    values = np.array([[float(v) for v in line.split()] for line in motion])
    pelvis = values[:, :3]
    eulers = values[:, 3:].reshape(len(values), 22, 3)
    rotations = np.zeros((len(values), 22, 3, 3))
    matrices = Rotation.from_euler("ZYX", eulers.reshape(-1, 3), degrees=True).as_matrix()
    matrices = matrices.reshape(len(values), 22, 3, 3)
    for slot, joint in enumerate(order):
        rotations[:, joint] = matrices[:, slot]
    return Clip(rest, rotations, pelvis)


def forward_kinematics(clip: Clip) -> tuple[np.ndarray, np.ndarray]:
    """World joint positions (T, 22, 3) and rotations (T, 22, 3, 3)."""
    frames = clip.frames
    positions = np.zeros((frames, 22, 3))
    rotations = np.zeros((frames, 22, 3, 3))
    for joint, parent in enumerate(PARENTS):
        if parent < 0:
            rotations[:, joint] = clip.rotations[:, joint]
            positions[:, joint] = clip.pelvis
        else:
            rotations[:, joint] = rotations[:, parent] @ clip.rotations[:, joint]
            offset = clip.rest[joint] - clip.rest[parent]
            positions[:, joint] = positions[:, parent] + rotations[:, parent] @ offset
    return positions, rotations


def heading(positions: np.ndarray) -> np.ndarray:
    """Facing angle about +Y per frame (0 = +Z), from the hips as Kimodo does."""
    across = positions[..., 1, :] - positions[..., 2, :]  # left hip - right hip
    forward = np.cross(across, np.array([0.0, 1.0, 0.0]))
    return np.arctan2(forward[..., 0], forward[..., 2])


def leg_length(rest: np.ndarray) -> float:
    hip, knee, ankle = (rest[i] for i in LEG)
    return float(np.linalg.norm(knee - hip) + np.linalg.norm(ankle - knee))


@dataclass(frozen=True)
class Frame:
    """Similarity from a source world into Kimodo's canonical frame."""

    origin: np.ndarray  # source pelvis XZ at the reference frame; Y: the source floor
    yaw: float  # source heading at the reference frame
    scale: float  # Kimodo leg length / source leg length

    def rotation(self) -> np.ndarray:
        return Rotation.from_rotvec([0.0, -self.yaw, 0.0]).as_matrix()

    def to_canonical(self, points: np.ndarray) -> np.ndarray:
        return ((points - self.origin) @ self.rotation().T) * self.scale

    def from_canonical(self, points: np.ndarray) -> np.ndarray:
        return (points / self.scale) @ self.rotation() + self.origin


FEET = (10, 11)
KIMODO_FOOT_HEIGHT = 0.006  # SMPL-X foot joints above Kimodo's floor (y = 0) when standing


def canonical_frame(clip: Clip, index: int, kimodo_rest: np.ndarray) -> Frame:
    """Root of frame `index` to XZ = 0 facing +Z; the clip's floor to Kimodo's floor."""
    positions, _ = forward_kinematics(clip)
    scale = leg_length(kimodo_rest) / leg_length(clip.rest)
    # The lower foot touches the floor in most frames; a low percentile ignores outliers.
    floor = float(np.percentile(positions[:, FEET, 1].min(axis=1), 5))
    origin = np.array(
        [clip.pelvis[index, 0], floor - KIMODO_FOOT_HEIGHT / scale, clip.pelvis[index, 2]]
    )
    return Frame(origin, float(heading(positions[index])), scale)


def fullbody_keys(clip: Clip, indices: list[int], frame: Frame, at: list[int]) -> dict[str, Any]:
    """Kimodo full-body constraint for source frames `indices` placed at output frames `at`."""
    turn = frame.rotation()
    local = clip.rotations[indices].copy()
    local[:, 0] = turn @ local[:, 0]
    return {
        "type": "fullbody",
        "frame_indices": list(at),
        "local_joints_rot": Rotation.from_matrix(local.reshape(-1, 3, 3))
        .as_rotvec()
        .reshape(len(indices), 22, 3)
        .tolist(),
        "root_positions": frame.to_canonical(clip.pelvis[indices]).tolist(),
    }


def package(
    local: np.ndarray,
    root: np.ndarray,
    kimodo_rest: np.ndarray,
    frame: Frame,
    model: str,
    source: dict[str, Any],
) -> bytes:
    """A native package from Kimodo's local rotations (T, 22, 3, 3) and root path (T, 3)."""
    turn = frame.rotation()
    local = local.copy()
    local[:, 0] = turn.T @ local[:, 0]
    pelvis = frame.from_canonical(root)
    rest = (kimodo_rest - kimodo_rest[0]) / frame.scale
    eulers = (
        Rotation.from_matrix(local.reshape(-1, 3, 3))
        .as_euler("ZYX", degrees=True)
        .reshape(len(local), 22, 3)
    )
    data = {
        "format": FORMAT,
        "coordinate_space": "model-world-y-up",
        "model": model,
        "fps": FPS,
        "frames": len(local),
        "retarget_bvh": smpl_bvh(rest.tolist(), eulers.tolist(), pelvis.tolist()),
        "source": source,
    }
    encoded = json.dumps(data, allow_nan=False).encode()
    read_native_motion(encoded)
    return encoded


def pose_difference_deg(a: np.ndarray, b: np.ndarray) -> float:
    """Largest joint rotation difference between two (22, 3, 3) local poses, degrees."""
    relative = np.einsum("jki,jkl->jil", a, b)
    angles = np.arccos(np.clip((np.trace(relative, axis1=1, axis2=2) - 1) / 2, -1, 1))
    return math.degrees(float(angles.max()))
