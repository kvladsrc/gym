"""Isolated Python 3.10 HTD stages. Frame encoder batching does not shorten temporal context."""

import json
import sys
import time
from pathlib import Path
from typing import Any

import numpy as np
import torch
from scipy.spatial.transform import Rotation

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "asset_processing/src"))
from asset_processing.native_motion import JOINTS


def tensors(raw: dict[str, Any]) -> dict[str, Any]:
    motion = {
        key: {name: torch.tensor(value, dtype=torch.float32) for name, value in raw[key].items()}
        for key in ("smpl_params_global", "smpl_params_incam")
    }
    motion["K_fullimg"] = torch.tensor(raw["K_fullimg"], dtype=torch.float32)
    return motion


def layout(package: dict[str, Any]) -> tuple[list[str], list[int], np.ndarray, np.ndarray]:
    lines = package["retarget_bvh"].splitlines()
    marker = lines.index("MOTION")
    names = {name: i for i, (name, _) in enumerate(JOINTS)}
    order, offsets = [], np.zeros((22, 3))
    pending = None
    for line in lines[:marker]:
        words = line.split()
        if words and words[0] in ("ROOT", "JOINT"):
            pending = names[words[1]]
            order.append(pending)
        elif words and words[0] == "OFFSET" and pending is not None:
            offsets[pending] = list(map(float, words[1:]))
            pending = None
    rows = np.array([list(map(float, line.split())) for line in lines[marker + 3 :]])
    return lines[:marker], order, offsets, rows


def rotations(params: dict[str, np.ndarray]) -> np.ndarray:
    return np.concatenate(
        [params["global_orient"][:, None], params["body_pose"].reshape(-1, 21, 3)], axis=1
    )


def verify_input(package: dict[str, Any], params: dict[str, np.ndarray]) -> None:
    _, order, _, rows = layout(package)
    n = package["frames"]
    encoded = Rotation.from_euler("ZYX", rows[:, 3:].reshape(-1, 3), degrees=True)
    predicted = Rotation.from_rotvec(rotations(params)[:, order].reshape(-1, 3))
    if np.max((encoded.inv() * predicted).magnitude()) > 1e-5:
        raise ValueError("BVH and raw SMPL-X rotations disagree; use an unmodified raw capture")
    rest = rows[:, :3] - params["transl"]
    if np.max(abs(rest - rest[:1])) > 2e-5 or len(rows) != n:
        raise ValueError(
            "BVH and raw SMPL-X root path disagree; joined captures are not valid HTD inputs"
        )


def prepare(work: Path) -> None:
    package = json.loads((work / "input.json").read_text())
    raw = package["smplx"]
    verify_input(package, {k: np.asarray(v) for k, v in raw["smpl_params_global"].items()})
    motion = tensors(raw)
    torch.save(motion, work / "prediction.pt")
    torch.save(motion["K_fullimg"], work / "K.pt")
    torch.save(
        dict(
            video_path=str(work / "source.mp4"),
            bbx_xyxy=torch.tensor(raw["bbx_xyxy"]),
            K_fullimg=motion["K_fullimg"],
        ),
        work / "sample.pt",
    )


def pva(work: Path) -> None:
    from scripts import demo_pvanet

    original = demo_pvanet.load_full_model

    def chunked(*args: Any, **kwargs: Any) -> Any:
        model = original(*args, **kwargs)
        encode = model.vitpose_encoder.forward

        def forward(images: torch.Tensor, *a: Any, **kw: Any) -> torch.Tensor:
            return torch.cat(
                [encode(images[:, i : i + 4], *a, **kw) for i in range(0, images.shape[1], 4)],
                dim=1,
            )

        model.vitpose_encoder.forward = forward
        return model

    demo_pvanet.load_full_model = chunked
    sys.argv = [
        sys.argv[0],
        "--input",
        str(work / "sample.pt"),
        "--output",
        str(work / "pva.pt"),
        "--no_yolo",
        "--window_length",
        "120",
        "--overlap",
        "30",
    ]
    started = time.monotonic()
    demo_pvanet.main()
    (work / "pva_runtime.json").write_text(
        json.dumps(
            dict(
                seconds=time.monotonic() - started,
                peak_cuda_allocated_bytes=torch.cuda.max_memory_allocated(),
                temporal_window=120,
                image_encoder_batch=4,
            )
        )
    )


def joints(angles: np.ndarray, pelvis: np.ndarray, offsets: np.ndarray) -> np.ndarray:
    local = Rotation.from_rotvec(angles.reshape(-1, 3)).as_matrix().reshape(-1, 22, 3, 3)
    world = np.empty_like(local)
    positions = np.zeros((len(angles), 22, 3))
    for j, (_, parent) in enumerate(JOINTS):
        if parent < 0:
            world[:, j], positions[:, j] = local[:, j], pelvis
        else:
            world[:, j] = world[:, parent] @ local[:, j]
            positions[:, j] = positions[:, parent] + np.einsum(
                "fij,j->fi", world[:, parent], offsets[j]
            )
    return positions - positions[:, :1]


def export(work: Path) -> None:
    package = json.loads((work / "input.json").read_text())
    result = torch.load(work / "optimization/source/opt_results.pt", map_location="cpu")
    before = {k: v.numpy() for k, v in result["smpl_params_global_input"].items()}
    after = {k: v.numpy() for k, v in result["smpl_params_global_optimized"].items()}
    n = package["frames"]
    if not all(np.isfinite(v).all() and len(v) == n for v in after.values()):
        raise ValueError("HTD returned invalid parameters or changed frame count")
    if not np.allclose(before["betas"], after["betas"], atol=1e-6, rtol=0):
        raise ValueError("HTD changed body shape; fixed-skeleton export would be invalid")
    original = package["smplx"]["smpl_params_global"]
    if any(np.max(abs(before[k] - np.asarray(original[k]))) > 1e-6 for k in before):
        raise ValueError("HTD control parameters differ from the input capture")
    header, order, offsets, rows = layout(package)
    rest_pelvis = rows[0, :3] - before["transl"][0]
    angles = rotations(after)
    eulers = (
        Rotation.from_rotvec(angles[:, order].reshape(-1, 3))
        .as_euler("ZYX", degrees=True)
        .reshape(n, 66)
    )
    values = np.concatenate([after["transl"] + rest_pelvis, eulers], axis=1)
    body = [*header, "MOTION", f"Frames: {n}", "Frame Time: 0.033333"]
    body.extend(" ".join(f"{v:.6f}" for v in row) for row in values)
    package.pop("smplx")  # Original camera-space parameters no longer describe the refined body.
    package.update(model="gvhmr-htd-refine", retarget_bvh="\n".join(body) + "\n")
    package["refined_smplx"] = {k: v.tolist() for k, v in after.items()}
    package["source"] = dict(
        package["source"],
        refinement=dict(
            model="HTD-Refine", temporal_window=120, overlap_frames=30, source_clock_preserved=True
        ),
    )
    report = dict(variants={})
    trajectories = {}
    for name, params in (("before", before), ("after", after)):
        points = joints(rotations(params), params["transl"] + rest_pelvis, offsets)
        trajectories[name] = points
        report["variants"][name] = dict(
            mean_joint_jerk_root_relative_m_s3=float(
                np.linalg.norm(np.diff(points, n=3, axis=0) * 30**3, axis=-1).mean()
            ),
            peak_wrist_speed_root_relative_m_s=float(
                np.linalg.norm(np.diff(points[:, [20, 21]], axis=0) * 30, axis=-1).max()
            ),
        )
    report["change_percent"] = {
        k: 100 * (report["variants"]["after"][k] / report["variants"]["before"][k] - 1)
        if report["variants"]["before"][k]
        else None
        for k in report["variants"]["before"]
    }
    report["wrist_position_change_root_relative_m_p50_p95_max"] = np.percentile(
        np.linalg.norm((trajectories["after"] - trajectories["before"])[:, [20, 21]], axis=-1),
        [50, 95, 100],
    ).tolist()
    report.update(
        input_parameters_preserved=True,
        source_clock_preserved=True,
        pva=json.loads((work / "pva_runtime.json").read_text()),
        note=(
            "Finite-difference diagnostics, not calibrated accuracy; "
            "smoothing may change real strikes."
        ),
    )
    (work / "output.json").write_text(json.dumps(package, allow_nan=False))
    (work / "report.json").write_text(json.dumps(report, allow_nan=False))


if __name__ == "__main__":
    {"prepare": prepare, "pva": pva, "export": export}[sys.argv[1]](Path(sys.argv[2]))
