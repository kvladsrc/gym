"""Static-camera PromptHMR-video pilot: original RGB + preserved observations."""

import argparse
import gc
import hashlib
import json
import sys
import time
from pathlib import Path
from typing import Any

import cv2
import numpy as np
import torch
from adapter import load_image_model
from PIL import Image, ImageOps
from scipy.ndimage import gaussian_filter1d
from scipy.signal import medfilt
from scipy.spatial.transform import Rotation
from torchvision.transforms import Compose, Normalize, ToTensor


def cpu(value: Any) -> Any:
    if torch.is_tensor(value):
        return value.detach().float().cpu()
    if isinstance(value, dict):
        return {k: cpu(v) for k, v in value.items()}
    return value


def main() -> None:
    parser = argparse.ArgumentParser()
    for name in ("video", "observations", "output", "source"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    source = args.source.resolve()
    sys.path[:0] = [str(source), str(source / "pipeline/gvhmr")]
    obs = json.loads(args.observations.read_text())
    frames_count = obs["frames"]
    assert obs["format"] == "studio.pose-observations.v1"
    assert obs["fps"] == 30
    assert 2 <= frames_count <= 300
    assert obs["source_sha256"] == hashlib.sha256(args.video.read_bytes()).hexdigest(), (
        "observations/video mismatch"
    )
    from hmr4d.model.gvhmr.gvhmr_pl_demo import DemoPL
    from hmr4d.utils.geo.hmr_cam import get_bbx_xys_from_xyxy, normalize_kp2d
    from hmr4d.utils.geo_transform import compute_cam_angvel
    from omegaconf import OmegaConf

    cap = cv2.VideoCapture(str(args.video))
    assert abs(cap.get(cv2.CAP_PROP_FPS) - 30) < 0.01, "video must be 30 fps"
    images = []
    while True:
        ok, frame = cap.read()
        if not ok:
            break
        assert len(images) < frames_count, "video has more frames than observations"
        images.append(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
    cap.release()
    assert len(images) == frames_count
    assert [images[0].shape[1], images[0].shape[0]] == obs["size"]
    K = torch.tensor(obs["K_fullimg"], dtype=torch.float32)
    boxes = torch.tensor(obs["bbx_xyxy"], dtype=torch.float32)
    keypoints = torch.tensor(obs["kp2d"], dtype=torch.float32)
    assert K.shape == (frames_count, 3, 3)
    assert boxes.shape == (frames_count, 4)
    assert keypoints.shape == (frames_count, 17, 3)
    torch.manual_seed(0)
    start = time.monotonic()
    model = load_image_model(source)
    normalization = Compose([ToTensor(), Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225])])
    features = []
    image_rot = []
    image_trans = []
    image_betas = []
    with torch.inference_mode():
        for i, image in enumerate(images):
            h, w = image.shape[:2]
            scale = 896 / max(w, h)
            offset = (np.array([896, 896]) - scale * np.array([w, h])) / 2
            img = np.array(
                ImageOps.pad(ImageOps.contain(Image.fromarray(image), (896, 896)), (896, 896))
            )
            k = K[i : i + 1].clone()
            k[:, :2] *= scale
            k[:, :2, 2] += torch.tensor(offset).float()
            box = boxes[i : i + 1] * scale
            box[:, :2] += torch.tensor(offset)
            box[:, 2:] += torch.tensor(offset)
            item = {
                "image": normalization(img),
                "cam_int": k,
                "boxes": torch.cat([box, torch.ones(1, 1)], -1),
                "kpts": None,
                "masks": None,
                "text": None,
            }
            with torch.autocast("cuda", dtype=torch.float16):
                out = model([item], mask_prompt=False)[0]
            out = cpu(out)
            features.append(out["features"][0])
            image_rot.append(out["rotmat"][0])
            image_trans.append(out["transl"][0])
            image_betas.append(out["betas"][0])
            if i % 30 == 0:
                print(f"IMAGE_FRAME {i}/{frames_count}", flush=True)
    image_time = time.monotonic() - start
    image_peak = torch.cuda.max_memory_allocated()
    del model, out
    gc.collect()
    torch.cuda.empty_cache()
    torch.cuda.reset_peak_memory_stats()
    head_cfg = OmegaConf.load(source / "data/pretrain/phmr_vid/prhmr_release_002.yaml")
    head = (
        DemoPL(
            pipeline=head_cfg.model.pipeline,
            smplx_path=str(source / "data/body_models/smplx/SMPLX_NEUTRAL.npz"),
        )
        .eval()
        .cuda()
    )
    # Select the released BEDLAM1+2 head, retaining the official architecture/config.
    state = torch.load(
        source / "data/pretrain/phmr_vid/phmr_b1b2.ckpt", map_location="cpu", weights_only=False
    )["state_dict"]
    missing, unexpected = head.load_state_dict(state, strict=False)
    if missing or unexpected:
        raise RuntimeError(f"Video checkpoint mismatch: missing={missing}, unexpected={unexpected}")
    del state
    bbx = get_bbx_xys_from_xyxy(boxes).numpy()
    bbx = np.array([gaussian_filter1d(medfilt(v, 11), 3) for v in bbx.T]).T
    bbx = torch.tensor(bbx, dtype=torch.float32)
    batch = {
        "length": torch.tensor([frames_count]),
        "obs": normalize_kp2d(keypoints, bbx)[None],
        "bbx_xys": bbx[None],
        "K_fullimg": K[None],
        "cam_angvel": compute_cam_angvel(torch.eye(3).repeat(frames_count, 1, 1))[None],
        "f_imgseq": torch.stack(features)[None],
    }
    batch = {k: v.cuda() for k, v in batch.items()}
    with torch.inference_mode(), torch.autocast("cuda", dtype=torch.float16):
        with_kp = cpu(head.pipeline.forward(batch, train=False, postproc=False, static_cam=True))
        batch["obs"] = torch.zeros_like(batch["obs"])
        no_kp = cpu(head.pipeline.forward(batch, train=False, postproc=False, static_cam=True))
    incam = {k: v[0] for k, v in no_kp["pred_smpl_params_incam"].items()}
    incam["transl"] = with_kp["pred_smpl_params_incam"]["transl"][0]
    world = {k: v[0] for k, v in no_kp["pred_smpl_params_global"].items()}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    torch.save(
        {
            "smpl_params_incam": incam,
            "smpl_params_global": world,
            "K_fullimg": K,
            "image_rotmat": torch.stack(image_rot),
            "image_transl_root": torch.stack(image_trans),
            "image_betas": torch.stack(image_betas),
            "with_keypoints": with_kp,
            "without_keypoints": no_kp,
            "source_observations": obs,
        },
        args.output.with_suffix(".pt"),
    )
    timings = {
        "image_s": image_time,
        "total_s": time.monotonic() - start,
        "image_peak_cuda_bytes": image_peak,
        "video_peak_cuda_bytes": torch.cuda.max_memory_allocated(),
    }
    import smplx

    sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "asset_processing/src"))
    from asset_processing.native_motion import smpl_bvh

    body = smplx.SMPLXLayer(
        str(source / "data/body_models/smplx"),
        gender="neutral",
        num_betas=10,
        use_pca=False,
        flat_hand_mean=True,
    )
    with torch.no_grad():
        rest = body(betas=world["betas"][:1]).joints[0, :22].numpy()
    rotations = torch.cat(
        [world["global_orient"][:, None], world["body_pose"].reshape(frames_count, 21, 3)], 1
    ).numpy()
    eulers = (
        Rotation.from_rotvec(rotations.reshape(-1, 3))
        .as_euler("ZYX", degrees=True)
        .reshape(frames_count, 22, 3)
    )
    pelvis = rest[0] + world["transl"].numpy()
    bvh = smpl_bvh(rest.tolist(), eulers.tolist(), pelvis.tolist())
    payload = {
        "format": "studio.smplx-motion.v1",
        "coordinate_space": "model-world-y-up",
        "retarget_bvh": bvh,
        "fps": 30,
        "frames": frames_count,
        "model": "prompthmr-video-bedlam1+2",
        "source_revision": "3b566b7dbb28ce506c7ea972c18693f4c705ce8c",  # pragma: allowlist secret
        "source_sha256": obs["source_sha256"],
        "camera": {
            "mode": "static-assumption",
            "intrinsics_source": obs["intrinsics_source"],
            "T_w2c": None,
        },
        "observations": obs,
        "image_predictions": {
            "rotmat": torch.stack(image_rot).tolist(),
            "root_translation": torch.stack(image_trans).tolist(),
            "betas": torch.stack(image_betas).tolist(),
        },
        "contact_probability": no_kp["model_output"]["static_conf_logits"][0].sigmoid().tolist(),
        "K_fullimg": K.tolist(),
        "smpl_params_incam": {k: v.tolist() for k, v in incam.items()},
        "smpl_params_global": {k: v.tolist() for k, v in world.items()},
        "timing": timings,
        "notes": [
            "Original RGB and box prompts; no image repainting or masking.",
            "Camera pose/transl fusion follows upstream phmr_vid.py. "
            "World output is the raw video head, without downstream global optimization.",
            "Image batch=1; image model unloaded before temporal head. "
            "Unused auxiliary SMPL mesh conversion skipped; learned image weights loaded strictly.",
        ],
    }
    args.output.write_text(json.dumps(payload, allow_nan=False, separators=(",", ":")) + "\n")
    print("NATIVE_SAVED", args.output, timings, flush=True)


if __name__ == "__main__":
    main()
