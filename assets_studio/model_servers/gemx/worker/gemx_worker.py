"""GEM-X Body Motion for the gemx model server (runs in the gemx worker environment).

    python gemx_worker.py CLIP.mp4 WORK_DIR OPTIONS.json
    python gemx_worker.py IMAGE.png WORK_DIR OPTIONS.json      (OPTIONS "mode": "image")

OPTIONS: {"weights": dir, "soma_assets": dir, "camera": "static"|"background", "postprocess": bool,
"mode": "video"|"image"}. An image gives one frame: GEM-X Body Pose (YOLOX finds the person).
GEM's code comes from the pinned source checkout on PYTHONPATH. Writes WORK_DIR/soma_motion.json:
SOMA parameters in camera and world coordinates, their 77 joints (forward kinematics with the
runtime's correctives), the rig's joint names and parents, intrinsics, the tracked boxes and the
77 2D keypoint observations.
"""

import json
import sys
from pathlib import Path

import numpy as np
import torch


def plain(value: object) -> object:
    """JSON-ready: tensors and arrays to rounded lists, recursively through lists and dicts."""
    if isinstance(value, torch.Tensor):
        return value.detach().cpu().float().numpy().round(6).tolist()
    if isinstance(value, np.ndarray):
        return value.astype(float).round(6).tolist()
    if isinstance(value, np.generic):
        return value.item()
    if isinstance(value, dict):
        return {str(k): plain(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [plain(v) for v in value]
    return value


def small(value: object) -> bool:
    """Scalars and short arrays only, for the diagnostics."""
    if isinstance(value, torch.Tensor):
        return value.numel() < 64
    if isinstance(value, (np.ndarray, list, tuple)):
        return np.asarray(value, dtype=object).size < 64
    return isinstance(value, (int, float, str, bool)) or value is None


BACKDROP = 128  # grey under a cut-out person: neither skin nor clothing nor the floor


def opaque(image: Path, work: Path) -> Path:
    """A picture with transparency (a person cut out, e.g. by BiRefNet) on a plain backdrop.
    GEM-X reads three channels; the colours under transparent pixels are still the original
    background, so they are replaced by the alpha, not dropped."""
    import cv2

    pixels = cv2.imread(str(image), cv2.IMREAD_UNCHANGED)
    if pixels is None:
        raise SystemExit(f"cannot read the image {image.name}")
    if pixels.ndim != 3 or pixels.shape[2] != 4:
        return image
    alpha = pixels[:, :, 3:].astype(np.float32) / 255
    flat = pixels[:, :, :3].astype(np.float32) * alpha + BACKDROP * (1 - alpha)
    target = work / "opaque.png"
    cv2.imwrite(str(target), np.round(flat).astype(np.uint8))
    print("TRANSPARENT image on a grey backdrop", flush=True)
    return target


def image_pose(image: Path, work: Path, options: dict) -> None:
    """One picture: GEM-X Body Pose of the main person, in camera space; the "world" is that
    camera turned to Y up (one image has no gravity estimate)."""
    from gem.inference.body import detect_body_annotations
    from gem.inference.pose import load_predictor, run_pose
    from soma.io import load_rig_from_usd

    weights, assets = Path(options["weights"]), options["soma_assets"]
    image = opaque(image, work)
    annotations, _ = detect_body_annotations(image, weights / "yolox_humanart.onnx", device="cpu")
    if not annotations or not annotations[0]["instances"]:
        raise SystemExit("PERSON_NOT_FOUND no person in the image")
    predictor, _ = load_predictor(
        weights / "body_s240000.ckpt",
        task="body",
        release="body",
        soma_assets=assets,
        device="cuda",
    )
    result = run_pose(predictor, image, annotations, task="body", video=False, release="body")
    frame = result["frames"][0]
    person = frame["instances"][0]
    joints = np.asarray(plain(person["joints_camera"]), dtype=float)
    print("POSE_PARAMETERS", sorted(person["parameters"]), joints.shape, flush=True)
    from gem.motion.geometry import MotionGeometry

    names = [
        str(n)
        for n in getattr(
            MotionGeometry(assets, device="cpu").body_model.runtime, "_public_joint_names", []
        )
    ]
    rig = load_rig_from_usd(Path(assets) / "SOMA_template_rig.usda")
    turn = np.diag([1.0, -1.0, -1.0])  # OpenCV camera (y down, z forward) to Y up
    world = joints @ turn.T
    world[:, 1] -= world[:, 1].min()  # the lowest joint on the floor
    out = {
        "frames": 1,
        # the public joints with Root first, as the video path writes them
        "joint_names": names if len(names) == len(joints) + 1 else None,
        "rig": {
            "joint_names": [str(n) for n in rig["joint_names"]],
            "joint_parent_ids": plain(np.asarray(rig["joint_parent_ids"])),
        },
        "camera": {"joints": [joints.round(6).tolist()], "parameters": plain(person["parameters"])},
        "world": {"joints": [world.round(6).tolist()]},
        "K_fullimg": [plain(frame["intrinsics"])],
        "bbx_xys": [plain(person["bbox"])],
        "keypoints_2d": [plain(person["keypoints_2d"])],
    }
    (work / "soma_motion.json").write_text(json.dumps(out))
    print("SOMA_POSE", len(joints), "joints", flush=True)


def main() -> None:
    clip, work, options = (
        Path(sys.argv[1]),
        Path(sys.argv[2]),
        json.loads(Path(sys.argv[3]).read_text()),
    )
    if options.get("mode") == "image":
        image_pose(clip, work, options)
        return
    from gem.inference.body_video import load_body_video_config, run_body_video
    from gem.motion.geometry import MotionGeometry

    config = load_body_video_config(Path(options["weights"]) / "body_video_native77.yaml")
    config["camera"]["mode"] = options["camera"]
    result = run_body_video(
        clip, config, device="cuda", progress=lambda s: print("STAGE", s, flush=True)
    )
    if options["postprocess"]:
        from gem.inference.postprocess import postprocess_motion_result

        result = postprocess_motion_result(
            result, soma_assets=options["soma_assets"], device="cuda"
        )
    torch.cuda.empty_cache()
    geometry = MotionGeometry(options["soma_assets"], device="cuda", chunk_size=16)
    out = {"coordinates": result.get("coordinates"), "postprocessed": bool(options["postprocess"])}
    for space, key in (("camera", "pred_soma_params_incam"), ("world", "pred_soma_params_global")):
        params = result[key]
        joints = geometry(params)["joints"][0]
        out[space] = {
            name: plain(params[name][0]) for name in ("global_orient", "body_pose", "transl")
        }
        out[space]["joints"] = plain(joints)
        if space == "camera":
            out["identity_coeffs"] = plain(params["identity_coeffs"][0])
            out["scale_params"] = plain(params["scale_params"][0])
    runtime = geometry.body_model.runtime
    out["joint_names"] = [
        str(n) for n in getattr(runtime, "_public_joint_names", [])
    ]  # the 77 public joints
    from soma.io import load_rig_from_usd

    rig = load_rig_from_usd(Path(options["soma_assets"]) / "SOMA_template_rig.usda")
    out["rig"] = {
        "joint_names": [str(n) for n in rig["joint_names"]],
        "joint_parent_ids": plain(np.asarray(rig["joint_parent_ids"])),
        "t_pose_local": plain(np.asarray(rig["t_pose_local"])),
        "bind_pose_world": plain(np.asarray(rig["bind_pose_world"])),
    }
    conditions = result["conditions"]
    out["K_fullimg"] = plain(conditions["K_fullimg"][0])
    out["bbx_xys"] = plain(conditions["bbx_xys"][0])
    out["kp2d_native77"] = plain(conditions["kp2d_native77"][0])
    out["static_conf_logits"] = plain(result["static_conf_logits"][0])
    out["tracking"] = {k: plain(v) for k, v in (result.get("tracking") or {}).items() if small(v)}
    out["camera_diagnostics"] = {
        k: plain(v) for k, v in (result.get("camera") or {}).items() if small(v)
    }
    out["frames"] = int(result["length"].reshape(-1)[0])
    (work / "soma_motion.json").write_text(json.dumps(out))
    print("SOMA_MOTION", out["frames"], "frames", flush=True)


if __name__ == "__main__":
    main()
