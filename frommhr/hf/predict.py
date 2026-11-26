"""Read a bald head's MHR head coefficients and skin, lip, brow and iris pigments from two pictures:
the front and the left profile, each on a plain, even backdrop with some backdrop above the crown.

    pip install torch timm safetensors mediapipe numpy pillow huggingface_hub
    python predict.py front.png profile.png [--obj head.obj] [--package frommhr.safetensors]

Prints JSON: `mhr_head`, MHR's identity coefficients 20-39 (the head; its 20 body coefficients
and the hands are not read), the pigments by name and how each picture was framed. --obj writes
the head on MHR's mean body as SOMA's mesh (metres, Y up, the face towards +Z; no eyeballs).
MediaPipe's Face Landmarker finds the eye line, the backdrop the crown; each picture is framed to
its own head as the training renders were, read under nine slightly shifted and scaled crops, and
the outputs are averaged. A profile looking to the picture's right is mirrored. Runs on the CPU
in about ten seconds."""

import argparse
import json
import math
import urllib.request
from pathlib import Path

import numpy as np
import torch
import torch.nn.functional as F
from PIL import Image, ImageOps
from torch import nn

REPO = "zjgkkn/frommhr"
LANDMARKER_URL = (
    "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task"
)
CACHE = Path.home() / ".cache" / "frommhr"
JITTERS = ((1.0, 0.0, 0.0),) + tuple(
    (1 + s, dx, dy) for s in (-0.03, 0.03) for dx in (-0.02, 0.02) for dy in (-0.02, 0.02)
)
BACKDROP_STEP = 30 / 255  # a pixel belongs to the figure when it differs this much (RGB sum)
BORDER = 20  # pixels along each edge that show the backdrop
IRISES, NOSE_TIP = (468, 473), 1  # Face Landmarker's iris centres and nose tip


def load_package(path):
    """The package's tensors and JSON (see the repository's package.py)."""
    from safetensors import safe_open

    with safe_open(str(path), framework="pt") as f:
        tree = json.loads(f.metadata()["package"])

        def build(obj):
            if isinstance(obj, dict):
                if set(obj) == {"$tensor"}:
                    return f.get_tensor(obj["$tensor"])
                return {k: build(v) for k, v in obj.items()}
            if isinstance(obj, list):
                return [build(v) for v in obj]
            return obj

        return build(tree)


class EyeReader(nn.Module):
    def __init__(self, width=128):
        super().__init__()
        self.net = nn.Sequential(
            nn.Conv2d(3, 32, 3, padding=1),
            nn.GELU(),
            nn.Conv2d(32, 64, 3, padding=1),
            nn.GELU(),
            nn.Conv2d(64, 64, 3, stride=2, padding=1),
            nn.GELU(),
            nn.Conv2d(64, width, 3, padding=1),
            nn.GELU(),
        )

    def forward(self, z):
        h = self.net(z).flatten(2)
        return torch.cat([h.mean(-1), h.amax(-1)], 1)


class Network(nn.Module):
    """DINOv2 ViT-S/14 over both pictures ([CLS, mean patch token] of each), a small CNN over the
    front picture's eye strip, an MLP to 20 head coefficients and 14 slider logits."""

    def __init__(self, config):
        super().__init__()
        import timm

        self.size, self.strip = int(config["size"]), tuple(config["eye_strip"])
        self.backbone = timm.create_model(config["backbone"], pretrained=False, img_size=self.size, num_classes=0)
        d = self.backbone.num_features
        self.eyes = EyeReader(config["eye_width"])
        h1, h2 = config["head"]
        self.head = nn.Sequential(
            nn.Linear(4 * d + 2 * config["eye_width"], h1),
            nn.GELU(),
            nn.Linear(h1, h2),
            nn.GELU(),
            nn.Linear(h2, config["coefficients"] + len(config["sliders"])),
        )
        self.register_buffer("mean", torch.tensor([0.485, 0.456, 0.406]).view(1, 3, 1, 1))
        self.register_buffer("std", torch.tensor([0.229, 0.224, 0.225]).view(1, 3, 1, 1))

    def forward(self, x):
        """x (B, 2, 3, S, S): front and left pictures, sRGB in [0, 1]."""
        B, V, S = x.shape[0], x.shape[1], x.shape[-1]
        z = (x.reshape(B * V, 3, S, S) - self.mean) / self.std
        t = self.backbone.forward_features(z)
        p = self.backbone.num_prefix_tokens
        f = torch.cat([t[:, 0], t[:, p:].mean(1)], -1).reshape(B, -1)
        r0, r1, c0, c1 = (round(v * S) for v in self.strip)
        return self.head(torch.cat([f, self.eyes(z.view(B, V, 3, S, S)[:, 0, :, r0:r1, c0:c1])], 1))


def figure(rgb):
    """The figure's mask against a plain backdrop, the median colour of the borders."""
    H = rgb.shape[0]
    edges = np.concatenate(
        [
            rgb[:BORDER].reshape(-1, 3),
            rgb[-BORDER:].reshape(-1, 3),
            rgb[:, :BORDER].reshape(-1, 3),
            rgb[:, -BORDER:].reshape(-1, 3),
        ]
    )
    colour = np.median(edges, axis=0)
    upper = np.concatenate(
        [rgb[:BORDER].reshape(-1, 3), rgb[: H // 2, :BORDER].reshape(-1, 3), rgb[: H // 2, -BORDER:].reshape(-1, 3)]
    )
    if np.mean(np.abs(upper - colour).sum(-1) > BACKDROP_STEP) > 0.2:
        raise SystemExit("the backdrop is not plain: the reader needs a head on an even, plain background")
    return np.abs(rgb - colour).sum(-1) > BACKDROP_STEP


def framing(rgb, eye_y):
    """(crown row, eye row, h = twice the crown's height above the eyes, the head's middle)."""
    mask = figure(rgb)
    rows = np.nonzero(mask.any(1))[0]
    if len(rows) == 0:
        raise SystemExit("no head in front of the backdrop")
    crown = int(rows.min())
    if crown < 2:
        raise SystemExit("the top of the head is cut off: leave some backdrop above the crown")
    h = 2 * (eye_y - crown)
    if h <= 0:
        raise SystemExit("the eyes were found above the crown")
    cols = np.nonzero(mask[crown : int(eye_y + 0.5 * h)].any(0))[0]
    return float(crown), eye_y, h, (int(cols.min()) + int(cols.max())) / 2


def crop(rgb, frame, size, jitter):
    """(3, size, size): 1.5 h high, its middle 0.15 h below the eyes and at the head's middle."""
    _, eyes, h, middle = frame
    cx, cy, half = middle, eyes + 0.15 * h, 0.75 * h
    s, dx, dy = jitter
    H, W = rgb.shape[:2]
    theta = torch.tensor(
        [
            [
                [s * half / (W / 2), 0, (cx + dx * 2 * half) / (W / 2) - 1],
                [0, s * half / (H / 2), (cy + dy * 2 * half) / (H / 2) - 1],
            ]
        ],
        dtype=torch.float32,
    )
    src = torch.from_numpy(np.ascontiguousarray(rgb, dtype=np.float32)).permute(2, 0, 1)[None]
    grid = F.affine_grid(theta, [1, 3, size * 4, size * 4], align_corners=False)
    return F.avg_pool2d(F.grid_sample(src, grid, mode="bilinear", padding_mode="border", align_corners=False), 4)[0]


def eye_line(rgb8, landmarker):
    """The iris centres' height (in a profile the visible iris's) and whether the face looks to
    the picture's right."""
    import mediapipe as mp
    from mediapipe.tasks.python import BaseOptions
    from mediapipe.tasks.python.vision import FaceLandmarker, FaceLandmarkerOptions

    options = FaceLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=str(landmarker)),
        num_faces=1,
        min_face_detection_confidence=0.05,
        min_face_presence_confidence=0.05,
    )
    h, w = rgb8.shape[:2]
    with FaceLandmarker.create_from_options(options) as finder:
        found = finder.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(rgb8)))
    if not found.face_landmarks:
        raise SystemExit("no face found")
    points = found.face_landmarks[0]
    irises = [(points[i].x * w, points[i].y * h, points[i].z) for i in IRISES]
    near = min(irises, key=lambda p: p[2])
    right = points[NOSE_TIP].x * w > near[0]
    if abs(irises[0][0] - irises[1][0]) > 0.04 * w:
        return (irises[0][1] + irises[1][1]) / 2, right
    return near[1], right


def open_picture(path):
    """(H, W, 3) uint8, upright by its EXIF, transparency on white (as the Studio server reads it)."""
    im = ImageOps.exif_transpose(Image.open(path))
    if im.mode in ("RGBA", "LA", "PA") or "transparency" in im.info:
        rgba = im.convert("RGBA")
        im = Image.new("RGB", rgba.size, (255, 255, 255))
        im.paste(rgba, mask=rgba.getchannel("A"))
    return np.asarray(im.convert("RGB"))


def slider_values(sliders, ranges):
    out = {}
    for u, (name, low, high, scale) in zip(sliders, ranges, strict=True):
        out[name] = (
            math.exp(math.log(low) + (math.log(high) - math.log(low)) * u) if scale == "log" else low + (high - low) * u
        )
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("front")
    ap.add_argument(
        "profile", help="the left profile (the face turned to the picture's left; the other way is mirrored)"
    )
    ap.add_argument("--package", type=Path, help="frommhr.safetensors (default: downloaded from Hugging Face)")
    ap.add_argument("--revision", default=None, help="the Hugging Face revision to download")
    ap.add_argument("--obj", type=Path, help="write the head on MHR's mean body as OBJ")
    args = ap.parse_args()
    torch.set_grad_enabled(False)
    if args.package is None:
        from huggingface_hub import hf_hub_download

        args.package = Path(hf_hub_download(REPO, "frommhr.safetensors", revision=args.revision))
    landmarker = CACHE / "face_landmarker.task"
    if not landmarker.is_file():
        CACHE.mkdir(parents=True, exist_ok=True)
        urllib.request.urlretrieve(LANDMARKER_URL, landmarker.with_suffix(".part"))
        landmarker.with_suffix(".part").replace(landmarker)

    package = load_package(args.package)
    config = package["config"]
    network = Network(config).eval()
    network.load_state_dict(package["weights"])
    views, crops = [], {}
    for name, path in (("front", args.front), ("left", args.profile)):
        rgb8 = open_picture(path)
        eye_y, right = eye_line(rgb8, landmarker)
        mirrored = name == "left" and right  # the reader saw left profiles only
        rgb = (rgb8[:, ::-1] if mirrored else rgb8).astype(np.float32) / 255
        frame = framing(rgb, eye_y)
        views.append(torch.stack([crop(rgb, frame, network.size, j) for j in JITTERS]))
        crops[name] = {
            "crown_px": frame[0],
            "eyes_px": round(frame[1], 1),
            "head_px": round(frame[2], 1),
            "middle_px": frame[3],
            "mirrored": bool(mirrored),
        }
    out = network(torch.stack(views, 1)).mean(0)
    k = config["coefficients"]
    head = out[:k]
    sliders = torch.sigmoid(out[k:])
    result = {
        "mhr_head": [round(float(v), 4) for v in head],
        "pigments": {n: round(v, 5) for n, v in slider_values([float(v) for v in sliders], config["sliders"]).items()},
        "crops_px": crops,
    }
    if args.obj:
        m = package["model"]
        verts = m["mean"] + torch.einsum("j,jvc->vc", head, m["head"])
        lines = [f"v {x:.6f} {y:.6f} {z:.6f}" for x, y, z in verts.tolist()]
        lines += [f"f {a + 1} {b + 1} {c + 1}" for a, b, c in m["tris"].tolist()]
        args.obj.write_text("\n".join(lines) + "\n")
        result["obj"] = str(args.obj)
    print(json.dumps(result, indent=1))


if __name__ == "__main__":
    main()
