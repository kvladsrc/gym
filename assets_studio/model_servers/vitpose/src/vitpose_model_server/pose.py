"""ViTPose++: the 17 COCO joints of one subject in a picture, from its box.

A top-down pose estimator sees one subject at a time: the picture is cut to the subject's
box, and the model answers with a heatmap per joint. This module is that protocol as the
model was evaluated (the crop, the flip test and the heatmap decoding of MMPose in their
unbiased, "UDP", form) around the Hugging Face port of ViTPose++-H. There is no person
detector here: the caller names the box.
"""

import gc
from typing import Any

import cv2
import numpy as np
import numpy.typing as npt

REPO = "usyd-community/vitpose-plus-huge"
REVISION = "9f36d7aec1800d23e97f10c2e74393aee92aa53f"  # pragma: allowlist secret
FILES = ["config.json", "model.safetensors"]

JOINTS = (
    "nose",
    "left_eye",
    "right_eye",
    "left_ear",
    "right_ear",
    "left_shoulder",
    "right_shoulder",
    "left_elbow",
    "right_elbow",
    "left_wrist",
    "right_wrist",
    "left_hip",
    "right_hip",
    "left_knee",
    "right_knee",
    "left_ankle",
    "right_ankle",
)
# The joint each one becomes in a mirrored picture.
MIRRORED = (0, 2, 1, 4, 3, 6, 5, 8, 7, 10, 9, 12, 11, 14, 13, 16, 15)
LEFT_SHOULDER, RIGHT_SHOULDER, LEFT_HIP, RIGHT_HIP = 5, 6, 11, 12
# Under this score the model is unsure where a joint is.
SCORE_THRESHOLD = 0.3

INPUT_SIZE = (192, 256)  # width, height
HEATMAP_SIZE = (48, 64)
BOX_PADDING = 1.25
BLUR_KERNEL = 11
MEAN = (0.485, 0.456, 0.406)
STD = (0.229, 0.224, 0.225)
# ViTPose++ has one head per training dataset; this one is COCO, people.
COCO_HEAD = 0

Array = npt.NDArray[np.float32]
Box = tuple[float, float, float, float]


def download() -> None:
    from huggingface_hub import snapshot_download

    snapshot_download(REPO, revision=REVISION, allow_patterns=FILES)


def crop_window(box: Box) -> tuple[Array, Array]:
    """The centre and size of the picture area the model sees for a subject box
    (left, top, right, bottom): the box with a margin, widened to the model's 3:4 shape."""
    left, top, right, bottom = box
    center = np.array([(left + right) / 2, (top + bottom) / 2], dtype=np.float32)
    width, height = (right - left) * BOX_PADDING, (bottom - top) * BOX_PADDING
    aspect = INPUT_SIZE[0] / INPUT_SIZE[1]
    if width > height * aspect:
        height = width / aspect
    else:
        width = height * aspect
    return center, np.array([width, height], dtype=np.float32)


def crop_matrix(center: Array, size: Array) -> Array:
    """The affine map of the window to the model's input: the centres of the window's
    corner pixels land on the centres of the input's corner pixels."""
    scale = (np.array(INPUT_SIZE, dtype=np.float32) - 1) / size
    matrix = np.zeros((2, 3), dtype=np.float32)
    matrix[0, 0], matrix[1, 1] = scale
    matrix[:, 2] = scale * (size / 2 - center)
    return matrix


def crop(frame: npt.NDArray[np.uint8], center: Array, size: Array) -> npt.NDArray[np.uint8]:
    """The window of an RGB frame at the model's input size. A large subject is blurred
    before it is reduced, a small one is enlarged with a bicubic filter, which keeps a
    drawing's outlines."""
    reduction = float(size[1]) / INPUT_SIZE[1]
    if reduction > 1.1:
        frame = cv2.GaussianBlur(frame, (0, 0), (reduction - 1) / 2)
    return cv2.warpAffine(
        frame,
        crop_matrix(center, size),
        INPUT_SIZE,
        flags=cv2.INTER_LINEAR if reduction > 1.1 else cv2.INTER_CUBIC,
        borderMode=cv2.BORDER_REPLICATE,
    )


def decode(heatmaps: Array) -> tuple[Array, Array]:
    """Joint positions in heatmap pixels and their scores (the heatmap maxima), from
    heatmaps of shape (joints, height, width). The position is the maximum, moved to
    sub-pixel accuracy by the curvature of the blurred heatmap's logarithm (DARK)."""
    count, _height, width = heatmaps.shape
    flat = heatmaps.reshape(count, -1)
    best = flat.argmax(axis=1)
    scores = flat[np.arange(count), best].astype(np.float32)
    points = np.stack([best % width, best // width], axis=1).astype(np.float32)

    logs = np.stack(
        [
            np.pad(
                np.log(np.clip(cv2.GaussianBlur(heatmap, (BLUR_KERNEL, BLUR_KERNEL), 0), 1e-3, 50)),
                1,
                mode="edge",
            )
            for heatmap in heatmaps
        ]
    )
    rows = np.arange(count)
    x, y = best % width + 1, best // width + 1
    here = logs[rows, y, x]
    right, left = logs[rows, y, x + 1], logs[rows, y, x - 1]
    below, above = logs[rows, y + 1, x], logs[rows, y - 1, x]
    gradient = np.stack([0.5 * (right - left), 0.5 * (below - above)], axis=1)
    dxx = right - 2 * here + left
    dyy = below - 2 * here + above
    dxy = 0.5 * (
        logs[rows, y + 1, x + 1]
        - right
        - below
        + 2 * here
        - left
        - above
        + logs[rows, y - 1, x - 1]
    )
    hessian = np.stack([dxx, dxy, dxy, dyy], axis=1).reshape(count, 2, 2)
    hessian = hessian + np.finfo(np.float32).eps * np.eye(2, dtype=np.float32)
    points -= np.einsum("kij,kj->ki", np.linalg.inv(hessian), gradient).astype(np.float32)
    return points, scores


def to_frame(points: Array, center: Array, size: Array) -> Array:
    """Heatmap positions in the frame the window was cut from."""
    return points / (np.array(HEATMAP_SIZE, dtype=np.float32) - 1) * size + center - size / 2


class ViTPose:
    """The loaded model."""

    def __init__(self, *, device: str = "cuda") -> None:
        import torch
        from transformers import VitPoseForPoseEstimation

        if device == "cuda" and not torch.cuda.is_available():
            raise RuntimeError("ViTPose++ requires a CUDA GPU")
        self._torch = torch
        self._device = device
        self._model: Any = (
            VitPoseForPoseEstimation.from_pretrained(REPO, revision=REVISION).to(device).eval()
        )
        self._mean = torch.tensor(MEAN, device=device).view(3, 1, 1)
        self._std = torch.tensor(STD, device=device).view(3, 1, 1)
        self._head = torch.full((2,), COCO_HEAD, dtype=torch.long, device=device)

    def joints(self, frame: npt.NDArray[np.uint8], box: Box) -> tuple[Array, Array]:
        """The joints of the subject of an RGB frame within its box: positions in frame
        pixels, shape (17, 2), and scores, shape (17,). The heatmaps are averaged with
        those of the mirrored picture, as in the model's evaluation."""
        torch = self._torch
        center, size = crop_window(box)
        with torch.inference_mode():
            picture = torch.from_numpy(crop(frame, center, size)).to(self._device)
            picture = (picture.permute(2, 0, 1).float() / 255 - self._mean) / self._std
            straight, mirrored = self._model(
                torch.stack([picture, picture.flip(-1)]), dataset_index=self._head
            ).heatmaps
            heatmaps = (straight + mirrored.flip(-1)[list(MIRRORED)]) / 2
        points, scores = decode(heatmaps.float().cpu().numpy())
        return to_frame(points, center, size), scores

    def release(self) -> None:
        """Give back the GPU memory that a video's frames took; the weights stay."""
        gc.collect()
        if self._device == "cuda":
            self._torch.cuda.empty_cache()
