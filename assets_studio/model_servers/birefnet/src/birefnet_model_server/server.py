"""BiRefNet background removal server: image-to-image (contract v1).

BiRefNet (Zheng Peng et al., CAAI AIR 2024) separates the object from the
background (dichotomous segmentation): a Swin-L encoder and a decoder that
keeps referring to the full-resolution image, so thin parts survive: a mane,
legs, branches. The result is the input with an alpha channel; the image-to-3D
and paint servers skip their own rembg cut-out for an image that is already
transparent.

The same cut-out works on a video (video-to-video): each frame is cut out
and put on white, as MoCapAnything's demo clips are ("a matte works best");
frame by frame, so the edges may flicker.

The author publishes several weights of one architecture: general ones
trained at 1024², at 2048² (HR) or at any shape (dynamic, 256²…2304²), and
matting ones trained for soft alpha (hair, fur). The variant is a parameter;
one is kept on the GPU at a time (~0.9 GB in fp16).

The model code comes with the weights (birefnet.py, transformers' remote
code), pinned to the same revision as them.

Licence: MIT (code and weights).
"""

import gc
import logging
from dataclasses import dataclass
from typing import Any, Literal

import numpy as np
from model_server_sdk.images import encode_png, open_image
from model_server_sdk.video import VideoEncodingError, encode_mp4
from PIL import Image
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
    InputData,
    InputSpec,
    InvalidInput,
    Job,
    KnownTask,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
    ui,
)

logger = logging.getLogger("birefnet_model_server")


@dataclass(frozen=True)
class Variant:
    repo: str
    revision: str
    # The square side it was trained at; None: any shape (dynamic).
    size: int | None


# Git revisions, not secrets.
VARIANTS = {
    "dynamic": Variant(
        "ZhengPeng7/BiRefNet_dynamic",
        "280306042f57b7a33854319da62fd86aaa89ec4c",  # pragma: allowlist secret
        None,
    ),
    "general": Variant(
        "ZhengPeng7/BiRefNet",
        "e2bf8e4460fc8fa32bba5ea4d94b3233d367b0e4",  # pragma: allowlist secret
        1024,
    ),
    "hr": Variant(
        "ZhengPeng7/BiRefNet_HR",
        "a7a562f6fd16021180f2f4348f4de003a2d3d1e1",  # pragma: allowlist secret
        2048,
    ),
    "matting": Variant(
        "ZhengPeng7/BiRefNet-matting",
        "eccde0a8cbdce7ac5fecfeb06340fe7b949e85d9",  # pragma: allowlist secret
        1024,
    ),
    "dynamic-matting": Variant(
        "ZhengPeng7/BiRefNet_dynamic-matting",
        "074df545be87034e74a96bf71566ecbbc4c15f0a",  # pragma: allowlist secret
        None,
    ),
    "hr-matting": Variant(
        "ZhengPeng7/BiRefNet_HR-matting",
        "5d6b6f8adcb5b417c871b1d84ceaae9871355b7f",  # pragma: allowlist secret
        2048,
    ),
}
VariantName = Literal["dynamic", "general", "hr", "matting", "dynamic-matting", "hr-matting"]
DEFAULT_VARIANT: VariantName = "dynamic"

# The range of sides the dynamic variants were trained on.
DYNAMIC_MIN_SIDE = 256
DYNAMIC_MAX_SIDE = 2304
# The encoder halves the image five times.
MULTIPLE = 32
# ImageNet normalisation, as the encoder was trained.
MEAN = (0.485, 0.456, 0.406)
STD = (0.229, 0.224, 0.225)
# Blur radii of the foreground estimate: a wide pass for the colours around
# the edge, a narrow one to refine (upstream's refine_foreground).
WIDE_BLUR = 90
NARROW_BLUR = 6
# Longer videos are refused rather than cut (a 1080p frame took ~1 s).
MAX_VIDEO_SECONDS = 60
# Alpha at or above this counts as the object, for the metadata and the
# check that something was found.
OPAQUE = 128


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    variant: VariantName = Field(
        default=DEFAULT_VARIANT,
        description="Weights: dynamic: any size up to 2304 px; general: 1024 px; "
        "hr: 2048 px; matting: soft edges (hair, fur)",
        json_schema_extra=ui(
            ru="Веса: dynamic — любой размер до 2304 пикселей; general — 1024; hr — 2048; "
            "matting — мягкие края (волосы, шерсть)",
            primary=True,
        ),
    )
    refine_foreground: bool = Field(
        default=True,
        description="Clear the background's tint from the edges' colours",
        json_schema_extra=ui(ru="Убрать оттенок фона из цвета краёв"),
    )
    largest_only: bool = Field(
        default=False,
        description="Keep only the largest object: drop separate pieces such as a logo or "
        "a caption in a corner",
        json_schema_extra=ui(
            ru="Только самый большой объект: убрать отдельные куски вроде "
            "логотипа или подписи в углу"
        ),
    )


TASK = TaskSpec(
    KnownTask.IMAGE_TO_IMAGE,
    Params,
    ("image/png",),
    prompt="none",
    inputs=(InputSpec(role="image", mime=["image/png", "image/jpeg", "image/webp"]),),
    # The cut-out does not depend on the seed.
    max_count=1,
)


# The background of a video is white, as in MoCapAnything's demos.
VIDEO_TASK = TaskSpec(
    "video-to-video",
    Params,
    ("video/mp4",),
    prompt="none",
    inputs=(
        InputSpec(
            role="video",
            mime=["video/mp4"],
            description=f"At most {MAX_VIDEO_SECONDS} s",
            labels={"ru": f"Не дольше {MAX_VIDEO_SECONDS} с"},
        ),
    ),
    max_count=1,
)


def read_frames(data: bytes) -> tuple[list[Image.Image], float]:
    """The video's frames (RGB) and frame rate."""
    import tempfile

    import cv2

    with tempfile.NamedTemporaryFile(suffix=".mp4") as file:
        file.write(data)
        file.flush()
        capture = cv2.VideoCapture(file.name)
        try:
            fps = capture.get(cv2.CAP_PROP_FPS) or 30.0
            frames = []
            while True:
                ok, frame = capture.read()
                if not ok:
                    break
                frames.append(Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)))
                if len(frames) > MAX_VIDEO_SECONDS * fps:
                    raise InvalidInput(f"the video is longer than {MAX_VIDEO_SECONDS} s")
        finally:
            capture.release()
    if not frames:
        raise InvalidInput("cannot read any frame of the video")
    return frames, fps


def on_white(cut: Image.Image) -> Image.Image:
    white = Image.new("RGB", cut.size, "white")
    white.paste(cut, mask=cut.getchannel("A"))
    return white


def download() -> None:
    from huggingface_hub import snapshot_download

    for variant in VARIANTS.values():
        snapshot_download(variant.repo, revision=variant.revision)


def inference_size(width: int, height: int, size: int | None) -> tuple[int, int]:
    """The size the model sees: its training square, or for a dynamic
    variant the image's own proportions within the trained range."""
    if size is not None:
        return size, size
    scale = min(1.0, DYNAMIC_MAX_SIDE / max(width, height))
    scale = max(scale, DYNAMIC_MIN_SIDE / min(width, height))
    return (
        max(MULTIPLE, round(width * scale / MULTIPLE) * MULTIPLE),
        max(MULTIPLE, round(height * scale / MULTIPLE) * MULTIPLE),
    )


def _blur_fusion(
    image: np.ndarray, foreground: np.ndarray, background: np.ndarray, alpha: np.ndarray, r: int
) -> tuple[np.ndarray, np.ndarray]:
    import cv2

    blurred_alpha = cv2.blur(alpha, (r, r))[:, :, None]
    blurred_foreground = cv2.blur(foreground * alpha[:, :, None], (r, r)) / (blurred_alpha + 1e-5)
    blurred_background = cv2.blur(background * (1 - alpha[:, :, None]), (r, r)) / (
        1 - blurred_alpha + 1e-5
    )
    alpha3 = alpha[:, :, None]
    estimate = blurred_foreground + alpha3 * (
        image - alpha3 * blurred_foreground - (1 - alpha3) * blurred_background
    )
    return np.clip(estimate, 0, 1), blurred_background


def estimate_foreground(image: np.ndarray, alpha: np.ndarray) -> np.ndarray:
    """The object's own colours where alpha is partial (fast blur fusion,
    Photoroom's fast-foreground-estimation, MIT): without it a mane cut from
    grass keeps a green fringe. ``image`` is H×W×3 and ``alpha`` H×W, both
    float32 in 0…1."""
    foreground, background = _blur_fusion(image, image, image, alpha, WIDE_BLUR)
    return _blur_fusion(image, foreground, background, alpha, NARROW_BLUR)[0]


EDGE = 15  # px kept around the largest object: its soft, partly transparent edge


def largest_object(alpha: np.ndarray) -> np.ndarray:
    """``alpha`` (H×W float32 in 0…1) with only its largest connected object, the soft edge
    around it kept; empty stays empty."""
    import cv2

    solid = (alpha >= 0.5).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(solid, connectivity=8)
    if count <= 2:  # background and at most one object
        return alpha
    largest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    region = cv2.dilate((labels == largest).astype(np.uint8), np.ones((EDGE, EDGE), np.uint8))
    return alpha * region


def cut_out(image: Image.Image, alpha: np.ndarray, refine: bool) -> Image.Image:
    """The RGB image with ``alpha`` (H×W float32 in 0…1) as its transparency."""
    rgb = np.asarray(image.convert("RGB"), dtype=np.float32) / 255
    if refine:
        rgb = estimate_foreground(rgb, alpha)
    rgba = np.dstack([rgb, alpha])
    return Image.fromarray(np.round(rgba * 255).astype(np.uint8), "RGBA")


def describe(result: Image.Image, variant: str, size: tuple[int, int]) -> dict[str, Any]:
    opaque = np.asarray(result.getchannel("A")) >= OPAQUE
    rows, columns = np.nonzero(opaque)
    box = (
        [int(columns.min()), int(rows.min()), int(columns.max()) + 1, int(rows.max()) + 1]
        if rows.size
        else []
    )
    return {
        "variant": variant,
        "revision": VARIANTS[variant].revision[:12],
        "inference_size": list(size),
        "object_share": round(float(opaque.mean()), 4),
        # left, top, right, bottom; empty when nothing was found
        "object_box": box,
    }


class BiRefNetServer(ModelServer):
    model = ModelInfo(
        id="birefnet",
        name="BiRefNet",
        revision=VARIANTS[DEFAULT_VARIANT].revision[:12],
        license="MIT",
        source="https://github.com/ZhengPeng7/BiRefNet",
        details={
            "precision": "fp16",
            "VRAM": "~0.9 GB: one variant on the GPU at a time",
        },
    )
    tasks = (TASK, VIDEO_TASK)

    def __init__(self) -> None:
        self._model: Any = None
        self._variant: str | None = None

    def load(self) -> None:
        import torch

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("BiRefNet needs a CUDA GPU")
        self._use(DEFAULT_VARIANT)

    def _use(self, name: str) -> Any:
        """The model of this variant on the GPU, the previous one freed."""
        if self._variant == name:
            return self._model
        import torch
        from transformers import AutoModelForImageSegmentation

        self._model = None
        self._variant = None
        gc.collect()
        torch.cuda.empty_cache()
        variant = VARIANTS[name]
        model = AutoModelForImageSegmentation.from_pretrained(
            variant.repo, revision=variant.revision, trust_remote_code=True
        )
        self._model = model.to("cuda").eval().half()
        self._variant = name
        logger.info("BiRefNet %s ready", name)
        return self._model

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        if job.task == VIDEO_TASK.task:
            return self._video(job.inputs["video"], job.params, len(job.seeds))
        params = job.params
        image = open_image(job.inputs["image"].data)
        result, size = self._cut(image, params)
        # Nothing found is an answer too (object_share 0, no object_box): the
        # image-to-3D servers then report the empty image themselves.
        meta = describe(result, params.variant, size)
        # One output: the cut-out does not depend on the seed.
        return [Output("image/png", encode_png(result), meta) for _ in job.seeds]

    def _cut(self, image: Image.Image, params: Params) -> tuple[Image.Image, tuple[int, int]]:
        model = self._use(params.variant)
        size = inference_size(*image.size, VARIANTS[params.variant].size)
        try:
            alpha = self._predict(model, image, size)
        except RuntimeError as error:
            if not is_out_of_memory(error):
                raise
            raise GenerationError(
                "out of GPU memory; free GPU memory or pick a smaller variant", retryable=True
            ) from error
        if params.largest_only:
            alpha = largest_object(alpha)
        return cut_out(image, alpha, params.refine_foreground), size

    def _video(self, video: InputData, params: Params, count: int) -> list[Output]:
        frames, fps = read_frames(video.data)
        width, height = frames[0].size
        shares = []

        def matted():  # noqa: ANN202 -- a generator of RGB24 frames
            for frame in frames:
                cut, _ = self._cut(frame, params)
                shares.append(float((np.asarray(cut.getchannel("A")) >= OPAQUE).mean()))
                yield on_white(cut).tobytes()

        try:
            data = encode_mp4(matted(), width=width, height=height, fps=fps)
        except VideoEncodingError as error:
            raise GenerationError(str(error)) from error
        meta = {
            "variant": params.variant,
            "revision": VARIANTS[params.variant].revision[:12],
            "frames": len(frames),
            "fps": round(fps, 3),
            "background": "white",
            # Frames where nothing was found: the animal left the frame or was lost.
            "empty_frames": sum(share == 0 for share in shares),
            "object_share_mean": round(float(np.mean(shares)), 4),
        }
        return [Output("video/mp4", data, meta) for _ in range(count)]

    def _predict(self, model: Any, image: Image.Image, size: tuple[int, int]) -> np.ndarray:
        """The model's alpha at the image's own size, H×W float32 in 0…1."""
        import torch
        import torch.nn.functional as functional

        pixels = np.asarray(image.resize(size, Image.Resampling.BILINEAR), dtype=np.float32) / 255
        pixels = (pixels - np.array(MEAN, dtype=np.float32)) / np.array(STD, dtype=np.float32)
        batch = torch.from_numpy(pixels).permute(2, 0, 1)[None].to("cuda", torch.float16)
        try:
            prediction = model(batch)[-1].sigmoid().float()
            alpha = functional.interpolate(
                prediction, size=(image.height, image.width), mode="bilinear", align_corners=False
            )
            return alpha[0, 0].clamp(0, 1).cpu().numpy()
        finally:
            del batch
            torch.cuda.empty_cache()
