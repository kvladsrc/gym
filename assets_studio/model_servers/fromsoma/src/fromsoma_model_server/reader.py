"""The face reader: a bald head's front and left-profile pictures and the character's sex and
age -> Anny's build and origin mix, the face's MHR coefficients and the pigment sliders.

The network was trained on renders of random Anny people with MHR's head variation. It sees
both pictures cropped the way its renders were framed: 1.5 h high, h = twice the crown's height
above the eyes, the middle 0.15 h below the eyes and, across, the head's middle above the chin
line. The eye line comes from MediaPipe's Face Landmarker (the iris centres), the crown and the
head's sides from the plain backdrop. Nine slightly shifted and scaled crops are read and their
outputs averaged.
"""

import math
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

# (scale, dx, dy) in units of the crop: the crop itself and eight around it
JITTERS = (
    (1.0, 0.0, 0.0),
    *((1 + s, dx, dy) for s in (-0.03, 0.03) for dx in (-0.02, 0.02) for dy in (-0.02, 0.02)),
)
BACKDROP_STEP = 30 / 255  # a pixel belongs to the figure when it differs this much (RGB sum)
BORDER = 20  # pixels along each edge that show the backdrop
MEAN, STD = (0.485, 0.456, 0.406), (0.229, 0.224, 0.225)  # DINOv2's input normalisation
IRISES = (468, 473)  # Face Landmarker's iris centres
NOSE_TIP = 1


class PictureError(ValueError):
    """A picture the reader cannot use; the message says why."""


@dataclass(frozen=True)
class Reading:
    """What the network read. `phenotype` is Anny's (gender, age, build, origin mix, ...);
    `coefficients` the face's MHR variation; `sliders` the pigments, normalised to [0, 1]."""

    phenotype: dict[str, float]
    coefficients: list[float]
    sliders: list[float]
    crops: dict[str, dict[str, float]]


class EyeReader(nn.Module):
    """The front picture's eye strip from its pixels: a small CNN, each feature's mean and
    maximum over the strip."""

    def __init__(self, width: int = 128) -> None:
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

    def forward(self, z: torch.Tensor) -> torch.Tensor:
        h = self.net(z).flatten(2)
        return torch.cat([h.mean(-1), h.amax(-1)], 1)


def condition_size(config: Mapping[str, Any]) -> int:
    """The known values' width; 0 for a reader without known inputs (frommhr's)."""
    if not config.get("known", True):
        return 0
    return 2 + len(config["age_bins"]) + 1 + len(config["free"]) + 1 + len(config["origin"])


def output_size(config: Mapping[str, Any]) -> int:
    return (
        len(config["phenotype"])
        + len(config["origin"])
        + config["coefficients"]
        + len(config["sliders"])
    )


class Network(nn.Module):
    """DINOv2 ViT-S/14 over both pictures ([CLS, mean patch token] of each), EyeReader over the
    front picture's eye strip and the known sex and age (none for frommhr), an MLP to the
    outputs."""

    def __init__(self, config: Mapping[str, Any]) -> None:
        super().__init__()
        import timm

        self.size = int(config["size"])
        self.strip = tuple(config["eye_strip"])
        self.backbone = timm.create_model(
            config["backbone"], pretrained=False, img_size=self.size, num_classes=0
        )
        d = self.backbone.num_features
        self.eyes = EyeReader(config["eye_width"])
        width = 4 * d + 2 * config["eye_width"] + condition_size(config)
        h1, h2 = config["head"]
        self.head = nn.Sequential(
            nn.Linear(width, h1),
            nn.GELU(),
            nn.Linear(h1, h2),
            nn.GELU(),
            nn.Linear(h2, output_size(config)),
        )
        self.register_buffer("mean", torch.tensor(MEAN).view(1, 3, 1, 1))
        self.register_buffer("std", torch.tensor(STD).view(1, 3, 1, 1))

    def forward(self, x: torch.Tensor, cond: torch.Tensor) -> torch.Tensor:
        """x (B, 2, 3, S, S): front and left pictures, sRGB in [0, 1]; cond (B, condition_size)."""
        B, V, S = x.shape[0], x.shape[1], x.shape[-1]
        z = (x.reshape(B * V, 3, S, S) - self.mean) / self.std
        t = self.backbone.forward_features(z)
        p = self.backbone.num_prefix_tokens
        f = torch.cat([t[:, 0], t[:, p:].mean(1)], -1).reshape(B, -1)
        r0, r1, c0, c1 = (round(v * S) for v in self.strip)
        strip = z.view(B, V, 3, S, S)[:, 0, :, r0:r1, c0:c1]
        return self.head(torch.cat([f, self.eyes(strip), cond], 1))


def condition(
    config: Mapping[str, Any],
    female: bool,
    years: float,
    anny_age: float,
    count: int,
    build: Mapping[str, float] | None = None,
    origin: Mapping[str, float] | None = None,
) -> torch.Tensor:
    """What we know about the character as the network's extra input, as in training: the sex
    (Anny's gender, 1 = female) and age always; the build (Anny's values of config["free"]) and
    the origin mix (summing to 1) where given, each behind its mask. What is not given the
    network reads from the pictures."""
    bins = torch.exp(
        -0.5 * ((years - torch.tensor(config["age_bins"], dtype=torch.float32)) / 6) ** 2
    )

    def given(values: Mapping[str, float] | None, names: Sequence[str]) -> torch.Tensor:
        if values is None:
            return torch.zeros(1 + len(names))
        return torch.tensor([1.0] + [float(values[name]) for name in names])

    row = torch.cat(
        [
            torch.tensor([1.0 if female else 0.0, anny_age]),
            bins,
            given(build, config["free"]),
            given(origin, config["origin"]),
        ]
    )
    return row[None].expand(count, -1)


def backdrop(rgb: np.ndarray) -> np.ndarray:
    """The figure's mask (H, W) against a plain backdrop, the median colour of the borders. The
    top border and the upper halves of the sides must show the backdrop (the shoulders may reach
    the lower edges)."""
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
        [
            rgb[:BORDER].reshape(-1, 3),
            rgb[: H // 2, :BORDER].reshape(-1, 3),
            rgb[: H // 2, -BORDER:].reshape(-1, 3),
        ]
    )
    if np.mean(np.abs(upper - colour).sum(-1) > BACKDROP_STEP) > 0.2:
        raise PictureError(
            "the backdrop is not plain: the reader needs a head on an even, plain background"
        )
    return np.abs(rgb - colour).sum(-1) > BACKDROP_STEP


@dataclass(frozen=True)
class Framing:
    """Where a picture's head is: the crown's row, the eye line, h (twice the crown's height
    above the eyes) and the head's middle across, pixels."""

    crown: float
    eyes: float
    h: float
    middle: float

    @classmethod
    def of(cls, rgb: np.ndarray, eye_y: float) -> "Framing":
        mask = backdrop(rgb)
        rows = np.nonzero(mask.any(1))[0]
        if len(rows) == 0:
            raise PictureError("no head in front of the backdrop")
        crown = int(rows.min())
        if crown < 2:
            raise PictureError(
                "the top of the head is cut off: leave some backdrop above the crown"
            )
        h = 2 * (eye_y - crown)
        if h <= 0:
            raise PictureError("the eyes were found above the crown")
        above = mask[crown : int(eye_y + 0.5 * h)]
        cols = np.nonzero(above.any(0))[0]
        return cls(float(crown), float(eye_y), float(h), (int(cols.min()) + int(cols.max())) / 2)


def crop(
    rgb: np.ndarray, framing: Framing, size: int, jitter: Sequence[float] = (1.0, 0.0, 0.0)
) -> torch.Tensor:
    """(3, size, size) sRGB in [0, 1], framed like the training renders: 1.5 h high, its middle
    0.15 h below the eyes and at the head's middle across; rgb (H, W, 3) in [0, 1]."""
    cx, cy, half = framing.middle, framing.eyes + 0.15 * framing.h, 0.75 * framing.h
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
    big = size * 4
    src = torch.from_numpy(np.ascontiguousarray(rgb, dtype=np.float32)).permute(2, 0, 1)[None]
    grid = F.affine_grid(theta, [1, 3, big, big], align_corners=False)
    return F.avg_pool2d(
        F.grid_sample(src, grid, mode="bilinear", padding_mode="border", align_corners=False), 4
    )[0]


class EyeFinder:
    """The eye line of a picture from MediaPipe's Face Landmarker: the iris centres' height, or
    in a profile the visible iris's (the nearer one); and whether the face looks to the
    picture's right (the nose tip right of the iris). A landmarker per call (~0.1 s): closed
    at once, none is left for MediaPipe's teardown at the interpreter's exit to trip over."""

    def __init__(self, model_path: Path) -> None:
        from mediapipe.tasks.python import BaseOptions
        from mediapipe.tasks.python.vision import FaceLandmarkerOptions

        self.options = FaceLandmarkerOptions(
            base_options=BaseOptions(model_asset_path=str(model_path)),
            num_faces=1,
            min_face_detection_confidence=0.05,
            min_face_presence_confidence=0.05,
        )

    def __call__(self, rgb8: np.ndarray) -> tuple[float, bool]:
        import mediapipe as mp
        from mediapipe.tasks.python.vision import FaceLandmarker

        h, w = rgb8.shape[:2]
        picture = mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(rgb8))
        with FaceLandmarker.create_from_options(self.options) as landmarker:
            found = landmarker.detect(picture)
        if not found.face_landmarks:
            raise PictureError("no face found")
        points = found.face_landmarks[0]
        irises = [(points[i].x * w, points[i].y * h, points[i].z) for i in IRISES]
        near = min(irises, key=lambda p: p[2])  # in a profile the hidden iris lies deeper
        right = points[NOSE_TIP].x * w > near[0]
        if abs(irises[0][0] - irises[1][0]) > 0.04 * w:
            return (irises[0][1] + irises[1][1]) / 2, right
        return near[1], right


class Reader:
    """The network with its eye finder; `read` takes the two pictures (H, W, 3) uint8."""

    def __init__(
        self,
        package: Mapping[str, Any],
        landmarker: Path,
        to_anny_age: Callable[[torch.Tensor], torch.Tensor],
    ) -> None:
        self.config = package["config"]
        self.network = Network(self.config).eval()
        self.network.load_state_dict(package["weights"])
        self.eyes = EyeFinder(landmarker)
        self.to_anny_age = to_anny_age  # years (tensor) -> Anny's age parameter

    @torch.no_grad()
    def read(
        self,
        front: np.ndarray,
        left: np.ndarray,
        female: bool,
        years: float,
        build: Mapping[str, float] | None = None,
        origin: Mapping[str, float] | None = None,
    ) -> Reading:
        """The face (MHR coefficients) and the pigment sliders from the two pictures, given the
        sex, the age and, if known, the build and the origin mix (summing to 1); Anny's phenotype
        holds the given values and the network's reading of the rest."""
        x, crops = self.pictures(front, left)
        age = float(self.to_anny_age(torch.tensor([float(years)]))[0])
        known = condition(self.config, female, years, age, len(JITTERS), build, origin)
        out = self.network(x, known).mean(0)
        c = self.config
        n, o, k = len(c["phenotype"]), len(c["origin"]), c["coefficients"]
        phenotype = {name: float(torch.sigmoid(out[i])) for i, name in enumerate(c["phenotype"])}
        phenotype["gender"], phenotype["age"] = (1.0 if female else 0.0), age
        if build is not None:
            phenotype.update({name: float(build[name]) for name in c["free"]})
        if origin is None:
            mix = torch.softmax(out[n : n + o], 0)
            origin = {name: float(mix[i]) for i, name in enumerate(c["origin"])}
        phenotype.update({name: float(origin[name]) for name in c["origin"]})
        phenotype["cupsize"] = phenotype["firmness"] = 0.5
        return Reading(
            phenotype=phenotype,
            coefficients=[float(v) for v in out[n + o : n + o + k]],
            sliders=[float(v) for v in torch.sigmoid(out[n + o + k :])],
            crops=crops,
        )

    def pictures(
        self, front: np.ndarray, left: np.ndarray
    ) -> tuple[torch.Tensor, dict[str, dict[str, Any]]]:
        """Both pictures framed like the training renders under each jitter, (jitters, 2, 3, S,
        S), and how each was framed; a right-looking profile is mirrored."""
        views, crops = [], {}
        for name, picture in (("front", front), ("left", left)):
            try:
                eye_y, right = self.eyes(picture)
                mirrored = name == "left" and right  # the reader saw left profiles only
                rgb = picture[:, ::-1] if mirrored else picture
                rgb = rgb.astype(np.float32) / 255
                framing = Framing.of(rgb, eye_y)
            except PictureError as error:
                raise PictureError(
                    f"{'profile' if name == 'left' else name} picture: {error}"
                ) from None
            views.append(torch.stack([crop(rgb, framing, self.network.size, j) for j in JITTERS]))
            crops[name] = {
                "crown_px": framing.crown,
                "eyes_px": round(framing.eyes, 1),
                "head_px": round(framing.h, 1),
                "middle_px": framing.middle,
                "mirrored": mirrored,
            }
        return torch.stack(views, 1), crops  # (jitters, 2, 3, S, S)


class MhrReader(Reader):
    """frommhr's reader: trained on renders of random MHR people (adults), with no known inputs;
    it reads the head's 20 MHR coefficients (MHR's 20-39) and the pigment sliders."""

    def __init__(self, package: Mapping[str, Any], landmarker: Path) -> None:
        super().__init__(package, landmarker, to_anny_age=lambda years: years)

    @torch.no_grad()
    def read_face(self, front: np.ndarray, left: np.ndarray) -> Reading:
        """The head's MHR coefficients and the sliders; `phenotype` stays empty."""
        x, crops = self.pictures(front, left)
        out = self.network(x, x.new_zeros(len(x), 0)).mean(0)
        k = self.config["coefficients"]
        return Reading(
            phenotype={},
            coefficients=[float(v) for v in out[:k]],
            sliders=[float(v) for v in torch.sigmoid(out[k:])],
            crops=crops,
        )


def slider_values(sliders: Sequence[float], ranges: Sequence[Sequence[Any]]) -> dict[str, float]:
    """Normalised sliders -> their physical values, log or linear over each range."""
    out = {}
    for u, (name, low, high, scale) in zip(sliders, ranges, strict=True):
        if scale == "log":
            out[name] = math.exp(math.log(low) + (math.log(high) - math.log(low)) * u)
        else:
            out[name] = low + (high - low) * u
    return out
