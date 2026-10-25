"""How well the model's two answers agree: its joint positions and the
positions its rotations (the BVH) put the joints at.

    python consistency.py OUT_DIR   (cwd: the source, for its BVH reader)

The rig moves by the rotations; the positions are predicted first. Upstream
normalises the positions differently, so each frame is matched by the best
rotation, translation and scale, and what is left is the disagreement, as a
share of the body's size (the largest distance between two joints at rest).
On the Truebones horse of upstream's demo data it was 0.04; on a SkinTokens
horse whose front rose oddly, 0.09. Prints one line: CONSISTENCY {json}.
"""

import json
import sys
from pathlib import Path

import numpy as np


def residual(positions: np.ndarray, rotated: np.ndarray) -> np.ndarray:
    """Per frame and joint, the distance left after the best similarity
    transform of ``positions`` onto ``rotated`` (frames × joints × 3)."""
    left = []
    for a, b in zip(positions, rotated, strict=True):
        a, b = a - a.mean(0), b - b.mean(0)
        u, s, vt = np.linalg.svd(a.T @ b)
        d = np.diag([1, 1, np.sign(np.linalg.det(u @ vt))])
        scale = (s * np.diag(d)).sum() / max((a**2).sum(), 1e-12)
        left.append(np.linalg.norm(scale * a @ (u @ d @ vt) - b, axis=-1))
    return np.array(left)


def main() -> None:
    from utils import animation  # pyright: ignore[reportMissingImports]
    from utils import bvh as BVH  # pyright: ignore[reportMissingImports]

    out = Path(sys.argv[1])
    motion = next(out.rglob("*_rot6d_pred.bvh"))
    positions = np.load(next(out.rglob("*_pose_pred.npy")))
    anim, names, _ = BVH.load(str(motion))
    rotated = animation.positions_global(anim)
    joints = len(names)
    rest = rotated[0]
    size = max(np.linalg.norm(rest[i] - rest[j]) for i in range(joints) for j in range(i))
    left = residual(positions[:, :joints], rotated) / size
    per_joint = left.mean(0)
    worst = np.argsort(-per_joint)[:3]
    print(
        "CONSISTENCY "
        + json.dumps(
            {
                "mean": round(float(left.mean()), 3),
                "worst_joints": {names[j]: round(float(per_joint[j]), 3) for j in worst},
            }
        )
    )


if __name__ == "__main__":
    main()
