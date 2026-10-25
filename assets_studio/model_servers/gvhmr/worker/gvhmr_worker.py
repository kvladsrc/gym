"""GVHMR on one video, the body's motion written as a BVH with Mixamo's names.

    python gvhmr_worker.py VIDEO OUT_DIR static|moving|observations [CAMERA_VIDEO]
    (cwd: GVHMR's source)

``observations`` runs only the preprocessing (person track, ViTPose) as for a static
camera and writes OUT_DIR/observations.json: boxes, COCO-17 keypoints and the K that
GVHMR would use, for other models (PromptHMR) or fitting.

Runs in GVHMR's own environment (Python 3.10, PyTorch 2.3; see pyproject).
Upstream's demo steps (tools/demo/demo.py): YOLO tracks the person, ViTPose
finds 2D keypoints, HMR2 encodes the crops, a visual odometry (SimpleVO)
estimates the camera's turn unless it is static, and GVHMR predicts SMPL-X
in a gravity-aligned world (Y up). The 22 body joints of SMPL-X, posed by
its rotations, become OUT_DIR/motion.bvh, the joints named as Mixamo's
(Hips, LeftUpLeg...) so that a Mixamo rig takes the motion by name. The
video must be at 30 fps (the model's rate). Prints ``FRAMES n`` at the end.
"""

import json
import os
import sys
from pathlib import Path

import numpy as np
import torch
from camera_reference import preprocess
from scipy.spatial.transform import Rotation

# SMPL-X's body joints and their parents, and Mixamo's names for them.
JOINTS = [
    ("pelvis", -1, "Hips"),
    ("left_hip", 0, "LeftUpLeg"),
    ("right_hip", 0, "RightUpLeg"),
    ("spine1", 0, "Spine"),
    ("left_knee", 1, "LeftLeg"),
    ("right_knee", 2, "RightLeg"),
    ("spine2", 3, "Spine1"),
    ("left_ankle", 4, "LeftFoot"),
    ("right_ankle", 5, "RightFoot"),
    ("spine3", 6, "Spine2"),
    ("left_foot", 7, "LeftToeBase"),
    ("right_foot", 8, "RightToeBase"),
    ("neck", 9, "Neck"),
    ("left_collar", 9, "LeftShoulder"),
    ("right_collar", 9, "RightShoulder"),
    ("head", 12, "Head"),
    ("left_shoulder", 13, "LeftArm"),
    ("right_shoulder", 14, "RightArm"),
    ("left_elbow", 16, "LeftForeArm"),
    ("right_elbow", 17, "RightForeArm"),
    ("left_wrist", 18, "LeftHand"),
    ("right_wrist", 19, "RightHand"),
]
FPS = 30


def config(video: Path, out_dir: Path, static: bool):  # noqa: ANN201 -- an omegaconf config
    from hmr4d.configs import register_store_gvhmr
    from hydra import compose, initialize_config_module

    with initialize_config_module(version_base="1.3", config_module="hmr4d.configs"):
        register_store_gvhmr()
        return compose(
            config_name="demo",
            overrides=[
                f"video_name={video.stem}",
                f"static_cam={static}",
                "verbose=False",
                "use_dpvo=False",
                f"output_root={out_dir}",
            ],
        )


def predict(
    video: Path,
    out_dir: Path,
    static: bool,
    camera_reference: Path | None = None,
    ignored_person_regions: list | None = None,
) -> dict:
    """Upstream's demo up to the prediction (no rendering)."""
    import shutil

    import hydra

    sys.path.insert(0, "tools/demo")
    import demo  # pyright: ignore[reportMissingImports]
    from hmr4d.utils.net_utils import detach_to_cpu

    cfg = config(video, out_dir, static)
    Path(cfg.output_dir).mkdir(parents=True, exist_ok=True)
    Path(cfg.preprocess_dir).mkdir(parents=True, exist_ok=True)
    shutil.copy(video, cfg.video_path)  # already 30 fps H.264: upstream re-encodes otherwise
    preprocess(cfg, demo, camera_reference, ignored_person_regions)
    data = demo.load_data_dict(cfg)
    model = hydra.utils.instantiate(cfg.model, _recursive_=False)
    model.load_pretrained_model(cfg.ckpt_path)
    model = model.eval().cuda()
    with torch.no_grad():
        return detach_to_cpu(model.predict(data, static_cam=static))


def body_joints(params: dict) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Rest joints (22 × 3, the person's own proportions), local rotations
    (frames × 22 × 3, axis-angle) and the pelvis' path (frames × 3), metres."""
    from hmr4d.utils.smplx_utils import make_smplx

    smplx = make_smplx("supermotion")
    betas = params["betas"][:1]
    rest = smplx(betas=betas, global_orient=torch.zeros(1, 3), body_pose=torch.zeros(1, 63))
    rest_joints = rest.joints[0, :22].numpy()
    frames = params["body_pose"].shape[0]
    rotations = torch.cat(
        [params["global_orient"][:, None], params["body_pose"].reshape(frames, 21, 3)], 1
    )
    pelvis = rest_joints[0] + params["transl"].numpy()
    return rest_joints, rotations.numpy(), pelvis


def write_bvh(path: Path, rest: np.ndarray, rotations: np.ndarray, pelvis: np.ndarray) -> None:
    children = {
        i: [j for j, (_, parent, _) in enumerate(JOINTS) if parent == i] for i in range(len(JOINTS))
    }
    lines = ["HIERARCHY"]

    def joint(i: int, depth: int) -> None:
        _name, parent, mixamo = JOINTS[i]
        pad = "  " * depth
        offset = rest[i] - rest[parent] if parent >= 0 else np.zeros(3)
        lines.append(f"{pad}{'ROOT' if parent < 0 else 'JOINT'} {mixamo}")
        lines.append(f"{pad}{{")
        lines.append(f"{pad}  OFFSET {offset[0]:.6f} {offset[1]:.6f} {offset[2]:.6f}")
        channels = "Xposition Yposition Zposition " if parent < 0 else ""
        lines.append(
            f"{pad}  CHANNELS {6 if parent < 0 else 3} {channels}Zrotation Yrotation Xrotation"
        )
        for child in children[i]:
            joint(child, depth + 1)
        if not children[i]:
            lines.append(f"{pad}  End Site")
            lines.append(f"{pad}  {{")
            lines.append(f"{pad}    OFFSET 0.000000 0.050000 0.000000")
            lines.append(f"{pad}  }}")
        lines.append(f"{pad}}}")

    joint(0, 0)
    order = []

    def visit(i: int) -> None:
        order.append(i)
        for child in children[i]:
            visit(child)

    visit(0)
    frames = rotations.shape[0]
    lines += ["MOTION", f"Frames: {frames}", f"Frame Time: {1 / FPS:.6f}"]
    eulers = (
        Rotation.from_rotvec(rotations.reshape(-1, 3))
        .as_euler("ZYX", degrees=True)
        .reshape(frames, -1, 3)
    )
    for f in range(frames):
        values = [*pelvis[f]]
        for i in order:
            values += list(eulers[f, i])
        lines.append(" ".join(f"{v:.5f}" for v in values))
    path.write_text("\n".join(lines) + "\n")


def observations(video: Path, out_dir: Path, ignored_person_regions: list | None) -> None:
    """Upstream's preprocessing only: the dominant person's boxes and ViTPose keypoints."""
    import shutil

    sys.path.insert(0, "tools/demo")
    import demo  # pyright: ignore[reportMissingImports]

    cfg = config(video, out_dir, True)
    Path(cfg.output_dir).mkdir(parents=True, exist_ok=True)
    Path(cfg.preprocess_dir).mkdir(parents=True, exist_ok=True)
    shutil.copy(video, cfg.video_path)
    preprocess(cfg, demo, None, ignored_person_regions)
    length, width, height = demo.get_video_lwh(cfg.video_path)
    boxes = torch.load(cfg.paths.bbx, map_location="cpu")["bbx_xyxy"]
    keypoints = torch.load(cfg.paths.vitpose, map_location="cpu")
    K = demo.estimate_K(width, height)
    data = {
        "frames": int(length),
        "size": [int(width), int(height)],
        "bbx_xyxy": boxes.tolist(),
        "kp2d": keypoints.tolist(),
        "K_fullimg": K.repeat(int(length), 1, 1).tolist(),
    }
    (out_dir / "observations.json").write_text(json.dumps(data, allow_nan=False))
    print(f"FRAMES {length}")


def main() -> None:
    video, out_dir, camera = Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3]
    sys.path.insert(0, str(Path.cwd()))
    camera_reference = Path(sys.argv[4]) if len(sys.argv) > 4 else None
    selection_path = out_dir / "person_selection.json"
    selection = json.loads(selection_path.read_text()) if selection_path.exists() else {}
    if camera == "observations":
        observations(video, out_dir, selection.get("ignored_person_regions"))
        return
    pred = predict(
        video,
        out_dir,
        static=camera == "static",
        camera_reference=camera_reference,
        ignored_person_regions=selection.get("ignored_person_regions"),
    )
    rest, rotations, pelvis = body_joints(pred["smpl_params_global"])
    # Preserve native parameters for refiners without depending on debug caches.
    # The server owns this optional artifact; the SDK only transports its JSON.
    boxes = torch.load(out_dir / "clip/preprocess/bbx.pt", map_location="cpu")["bbx_xyxy"]
    native = {
        "format": "studio.smplx-parameters.v1",
        "fps": FPS,
        "smpl_params_global": {
            k: pred["smpl_params_global"][k].detach().cpu().tolist()
            for k in ("global_orient", "body_pose", "betas", "transl")
        },
        "smpl_params_incam": {
            k: pred["smpl_params_incam"][k].detach().cpu().tolist()
            for k in ("global_orient", "body_pose", "betas", "transl")
        },
        "K_fullimg": pred["K_fullimg"].detach().cpu().tolist(),
        "bbx_xyxy": boxes.detach().cpu().tolist(),
    }
    (out_dir / "smplx.json").write_text(json.dumps(native, allow_nan=False))
    if os.environ.get("ASSETS_STUDIO_DEBUG_DIR"):
        np.savez_compressed(
            out_dir / "body_motion.npz", rest=rest, rotations=rotations, pelvis=pelvis
        )
        torch.save(pred, out_dir / "prediction.pt")
    write_bvh(out_dir / "motion.bvh", rest, rotations, pelvis)
    print(f"FRAMES {rotations.shape[0]}")


if __name__ == "__main__":
    main()
