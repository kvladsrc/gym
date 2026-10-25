"""Use an isolated reference frame to select a subject, then track original pixels.

An image editor can identify the desired subject once. Only its foreground box
and interior points are used as SAM prompts: generated clothes, anatomy and
weapons never replace pixels in the motion-capture input. The reference must
depict the first video frame in the same framing, on white or with transparency.
"""

import gc
import shutil
import tempfile
from typing import Any

import numpy as np
from model_server_sdk.images import open_image
from model_server_sdk.video import VideoEncodingError, encode_mp4
from PIL import Image
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
    ui,
)

REPO = "facebook/sam2.1-hiera-small"
REVISION = "ee5bba1d82bb8749febdf90f45e84b687142ba03"  # pragma: allowlist secret
MAX_SECONDS = 10
MAX_FRAMES = 601
MAX_PIXELS = 1920 * 1080


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    require_full_subject: bool = Field(
        default=True,
        description="For mocap: reject small/incomplete references and edge-clipped tracks",
        json_schema_extra=ui(ru="Требовать полный силуэт для мокапа"),
    )


TASK = TaskSpec(
    "video-to-video",
    Params,
    ("video/mp4",),
    prompt="none",
    inputs=(
        InputSpec(
            role="video",
            mime=["video/mp4"],
            description="Original video, at most 10 s, 1080p and 60 fps",
            labels={"ru": "Исходное видео, до 10 с, 1080p и 60 кадров/с"},
        ),
        InputSpec(
            role="reference",
            mime=["image/png", "image/jpeg", "image/webp"],
            description="First frame: only the selected subject on white or transparent; "
            "preserve the original framing and pose (e.g. Qwen 1K, 10 steps)",
            labels={
                "ru": "Первый кадр: выбранный персонаж на белом или прозрачном фоне; "
                "сохранить позу и кадрирование (например, Qwen 1K, 10 шагов)"
            },
        ),
    ),
    max_count=1,
)


def read_video(data: bytes, *, max_seconds: int = MAX_SECONDS) -> tuple[list[Image.Image], float]:
    import cv2

    with tempfile.NamedTemporaryFile(suffix=".mp4") as file:
        file.write(data)
        file.flush()
        capture = cv2.VideoCapture(file.name)
        try:
            fps = capture.get(cv2.CAP_PROP_FPS)
            if not 0 < fps <= 60.01:
                raise InvalidInput("video frame rate must be between 0 and 60 fps")
            frames = []
            while True:
                ok, bgr = capture.read()
                if not ok:
                    break
                if bgr.shape[0] * bgr.shape[1] > MAX_PIXELS:
                    raise InvalidInput("video must be at most 1920×1080 pixels")
                frames.append(Image.fromarray(cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)))
                if len(frames) > MAX_FRAMES or len(frames) > max_seconds * fps + 0.01:
                    raise InvalidInput(f"video must be at most {max_seconds} seconds")
        finally:
            capture.release()
    if not frames:
        raise InvalidInput("cannot read any video frame")
    return frames, fps


def reference_prompts(
    reference: Image.Image, size: tuple[int, int], *, require_full_subject: bool = True
) -> tuple[list[int], list[list[int]]]:
    """Ignore detached blades/shadows; prompt the original frame with body points.

    This deliberately does not use the edited silhouette as a final video mask.
    The model re-segments the original frame, so small generative pose changes
    in the reference do not directly become animation input.
    """
    import cv2

    if abs(reference.width / reference.height / (size[0] / size[1]) - 1) > 0.05:
        raise InvalidInput("reference must have the video's aspect ratio and framing")
    rgba = np.asarray(reference.convert("RGBA"))
    foreground = rgba[:, :, 3] >= 128
    if np.all(foreground):
        foreground = np.min(rgba[:, :, :3], axis=2) < 235
    foreground = cv2.resize(foreground.astype(np.uint8), size, interpolation=cv2.INTER_NEAREST)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(foreground, connectivity=8)
    if count < 2:
        raise InvalidInput("reference contains no foreground subject")
    component = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    mask = (labels == component).astype(np.uint8)
    share = float(mask.mean())
    if not 0.002 <= share <= 0.75:
        raise InvalidInput("reference must show one subject on white or transparent background")
    x, y, width, height, _ = map(int, stats[component])
    if require_full_subject and height < size[1] * 0.25:
        raise InvalidInput(
            "reference subject is too small or incomplete; provide a full-body frame"
        )
    box = [x, y, x + width, y + height]
    distance = cv2.distanceTransform(mask, cv2.DIST_L2, 5)
    points = []
    # Four vertical bands, each split horizontally: head, trunk and both legs.
    for row in range(4):
        for col in range(2):
            x0, x1 = x + col * width // 2, x + (col + 1) * width // 2
            y0, y1 = y + row * height // 4, y + (row + 1) * height // 4
            region = distance[y0:y1, x0:x1]
            if region.size and region.max() >= 2:
                yy, xx = np.unravel_index(region.argmax(), region.shape)
                points.append([int(x0 + xx), int(y0 + yy)])
    if not points:
        raise InvalidInput("reference subject is too small")
    return box, points


def composite(frame: Image.Image, mask: np.ndarray) -> bytes:
    pixels = np.asarray(frame)
    if mask.shape != pixels.shape[:2]:
        raise GenerationError("tracked mask dimensions differ from the original video")
    return np.where(mask[:, :, None], pixels, 255).astype(np.uint8).tobytes()


def remove_speckles(mask: np.ndarray) -> np.ndarray:
    """Remove tiny disconnected noise, retaining detached hands and thin edges."""
    import cv2

    count, labels, stats, _ = cv2.connectedComponentsWithStats(
        mask.astype(np.uint8), connectivity=8
    )
    if count < 2:
        return mask
    areas = stats[:, cv2.CC_STAT_AREA]
    keep = areas >= max(16, int(areas[1:].max()) * 0.0005)
    keep[0] = False
    return keep[labels]


def touches_edge(mask: np.ndarray) -> bool:
    """A thin sword tip may reach the edge; a clipped limb/body must be reviewed."""
    return sum(int(edge.sum()) for edge in (mask[0], mask[-1], mask[:, 0], mask[:, -1])) >= 12


class Sam2Server(ModelServer):
    model = ModelInfo(
        id="sam2-1-small",
        name="SAM 2.1 · selected subject",
        revision=REVISION[:12] + ".subject.6",
        license="Apache-2.0",
        source="https://huggingface.co/" + REPO,
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._model: Any = None
        self._processor: Any = None

    def load(self) -> None:
        # Metadata is available without downloading/loading weights. The first
        # valid job loads SAM on the SDK's inference thread.
        if not shutil.which("ffmpeg"):
            raise FileNotFoundError("ffmpeg is required")

    def _load_model(self) -> None:
        if self._model is not None:
            return
        import torch
        from transformers import Sam2VideoModel, Sam2VideoProcessor

        if not torch.cuda.is_available():
            raise RuntimeError("SAM 2 video tracking requires a CUDA GPU")
        self._processor = Sam2VideoProcessor.from_pretrained(REPO, revision=REVISION)
        self._model = (
            Sam2VideoModel.from_pretrained(REPO, revision=REVISION, torch_dtype=torch.float32)
            .to("cuda")
            .eval()
        )

    def generate(self, job: Job) -> list[Output]:
        import torch

        frames, fps = read_video(job.inputs["video"].data)
        reference = open_image(job.inputs["reference"].data, keep_alpha=True)
        assert isinstance(job.params, Params)
        box, points = reference_prompts(
            reference, frames[0].size, require_full_subject=job.params.require_full_subject
        )
        try:
            self._load_model()
            with torch.inference_mode(), torch.autocast("cuda", dtype=torch.bfloat16):
                return self._track(frames, fps, box, points, job.params.require_full_subject)
        except RuntimeError as error:
            if not is_out_of_memory(error):
                raise
            raise GenerationError("out of GPU memory tracking video", retryable=True) from error
        finally:
            gc.collect()
            torch.cuda.empty_cache()

    def _track(
        self,
        frames: list[Image.Image],
        fps: float,
        box: list[int],
        points: list[list[int]],
        require_full_subject: bool,
    ) -> list[Output]:
        width, height = frames[0].size
        session = self._processor.init_video_session(
            video=frames,
            inference_device="cuda",
            inference_state_device="cpu",
            processing_device="cpu",
            video_storage_device="cpu",
        )
        self._processor.add_inputs_to_inference_session(
            inference_session=session,
            frame_idx=0,
            obj_ids=1,
            input_boxes=[[box]],
            input_points=[[points]],
            input_labels=[[[1] * len(points)]],
        )
        shares: list[float] = []

        def matted():  # noqa: ANN202 -- RGB24 frames consumed by ffmpeg
            for prediction in self._model.propagate_in_video_iterator(session, start_frame_idx=0):
                index = prediction.frame_idx
                if index != len(shares):
                    raise GenerationError("tracker returned missing or reordered video frames")
                masks = self._processor.post_process_masks(
                    [prediction.pred_masks], original_sizes=[[height, width]], binarize=False
                )[0]
                mask = remove_speckles(masks[0, 0].float().cpu().numpy() > 0)
                if require_full_subject and touches_edge(mask):
                    raise GenerationError(
                        f"selected subject reaches the image edge at frame {index}; "
                        "use a wider crop or a shorter clip for full-body motion capture"
                    )
                shares.append(float(mask.mean()))
                if shares[-1] < 0.001:
                    raise GenerationError(f"selected subject lost at video frame {index}")
                yield composite(frames[index], mask)
            if len(shares) != len(frames):
                raise GenerationError("tracker did not return all video frames")

        try:
            data = encode_mp4(matted(), width=width, height=height, fps=fps)
        except VideoEncodingError as error:
            raise GenerationError(str(error)) from error
        return [
            Output(
                "video/mp4",
                data,
                {
                    "frames": len(frames),
                    "fps": round(fps, 6),
                    "background": "white",
                    "original_pixels": True,
                    "reference_box": box,
                    "reference_points": points,
                    "object_share_min": round(min(shares), 5),
                    "object_share_mean": round(float(np.mean(shares)), 5),
                },
            )
        ]
