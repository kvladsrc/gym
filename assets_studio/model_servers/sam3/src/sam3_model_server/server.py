"""SAM 3.1 video segmentation server (contract v1).

Meta's SAM 3.1 (March 2026, Object Multiplex) finds, segments and tracks
objects in video. What stays in the result is chosen in one of three ways:

- a reference: as with the sam2 server, the first frame with only the subject
  on white or transparent. Its box and interior points prompt the tracker on
  the original first frame; the edited silhouette itself is never used.
- a prompt: a short English noun phrase ("person", "red car"). The detector
  finds every instance in every frame and they are tracked together.
- both: the phrase finds its instances on the first frame and the one that
  overlaps the reference subject most is kept.

The result has the original pixels of what was chosen on white, at the
video's size, frame count and rate, without audio.
"""

import ctypes
import gc
import shutil
import tempfile
from collections.abc import Iterator
from pathlib import Path
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

REPO = "facebook/sam3.1"
REVISION = "daa63191845a41281374e725f4c9e51c7a824460"  # pragma: allowlist secret
CHECKPOINT = "sam3.1_multiplex.pt"
FILES = ("config.json", CHECKPOINT)
MAX_SECONDS = 10
MAX_FRAMES = 601
MAX_PIXELS = 1920 * 1080
MAX_PROMPT_LENGTH = 100
# The share of a frame below which a selected subject counts as lost.
LOST_SHARE = 0.001
# The least overlap with the reference subject for a found instance to be it.
MIN_REFERENCE_IOU = 0.2
SUBJECT_ID = 1


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
    prompt="optional",
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
            required=False,
            description="First frame: only the selected subject on white or transparent; "
            "preserve the original framing and pose. Without it, the prompt (a short "
            "English noun phrase) keeps every instance it names",
            labels={
                "ru": "Первый кадр: выбранный персонаж на белом или прозрачном фоне, "
                "в исходной позе и кадрировании. Без него остаются все объекты, "
                "названные в запросе (короткое название по-английски)"
            },
        ),
    ),
    max_count=1,
)


def video_frames(path: str) -> Iterator[np.ndarray]:
    """The RGB frames of a video file, one at a time."""
    import cv2

    capture = cv2.VideoCapture(path)
    try:
        while True:
            ok, bgr = capture.read()
            if not ok:
                return
            yield cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    finally:
        capture.release()


def unpack_video(
    data: bytes, directory: Path, *, max_seconds: int = MAX_SECONDS
) -> tuple[Image.Image, int, float]:
    """Write the video and its frames as JPEGs, the way SAM 3 loads them most
    sparingly (float16, unlike its video-file loader). Returns the first frame,
    the frame count and the rate."""
    import cv2

    video = directory / "video.mp4"
    video.write_bytes(data)
    capture = cv2.VideoCapture(str(video))
    fps = capture.get(cv2.CAP_PROP_FPS)
    capture.release()
    if not 0 < fps <= 60.01:
        raise InvalidInput("video frame rate must be between 0 and 60 fps")
    frames = directory / "frames"
    frames.mkdir()
    first = None
    count = 0
    for rgb in video_frames(str(video)):
        if rgb.shape[0] * rgb.shape[1] > MAX_PIXELS:
            raise InvalidInput("video must be at most 1920×1080 pixels")
        if count + 1 > MAX_FRAMES or count + 1 > max_seconds * fps + 0.01:
            raise InvalidInput(f"video must be at most {max_seconds} seconds")
        if first is None:
            first = Image.fromarray(rgb)
        bgr = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
        cv2.imwrite(str(frames / f"{count:05d}.jpg"), bgr, [cv2.IMWRITE_JPEG_QUALITY, 95])
        count += 1
    if first is None:
        raise InvalidInput("cannot read any video frame")
    return first, count, fps


def check_prompt(prompt: str | None) -> str | None:
    text = (prompt or "").strip()
    if not text:
        return None
    if len(text) > MAX_PROMPT_LENGTH:
        raise InvalidInput(
            f"the prompt names what to keep in a short noun phrase, "
            f"at most {MAX_PROMPT_LENGTH} characters"
        )
    # SAM 3.1 was trained on English phrases: others find nothing, after a whole
    # pass over the video.
    if any(char.isalpha() and not char.isascii() for char in text):
        raise InvalidInput("the prompt must be in English, for example 'person' or 'wolf'")
    return text


def reference_mask(reference: Image.Image, size: tuple[int, int]) -> np.ndarray:
    """The largest foreground region of the reference at the video's size."""
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
    mask = labels == component
    if not 0.002 <= float(mask.mean()) <= 0.75:
        raise InvalidInput("reference must show one subject on white or transparent background")
    return mask


def reference_prompts(
    mask: np.ndarray, *, require_full_subject: bool = True
) -> tuple[list[int], list[list[int]]]:
    """The subject's box and interior points (head, trunk, legs), as for SAM 2.1.

    They prompt the model on the original frame, so small generative changes
    of pose in the reference do not become the result.
    """
    import cv2

    height = mask.shape[0]
    ys, xs = np.nonzero(mask)
    x, y = int(xs.min()), int(ys.min())
    box_width, box_height = int(xs.max()) - x + 1, int(ys.max()) - y + 1
    if require_full_subject and box_height < height * 0.25:
        raise InvalidInput(
            "reference subject is too small or incomplete; provide a full-body frame"
        )
    distance = cv2.distanceTransform(mask.astype(np.uint8), cv2.DIST_L2, 5)
    points = []
    # Four vertical bands, each split horizontally: head, trunk and both legs.
    for row in range(4):
        for col in range(2):
            x0, x1 = x + col * box_width // 2, x + (col + 1) * box_width // 2
            y0, y1 = y + row * box_height // 4, y + (row + 1) * box_height // 4
            region = distance[y0:y1, x0:x1]
            if region.size and region.max() >= 2:
                yy, xx = np.unravel_index(region.argmax(), region.shape)
                points.append([int(x0 + xx), int(y0 + yy)])
    if not points:
        raise InvalidInput("reference subject is too small")
    return [x, y, x + box_width, y + box_height], points


def best_match(obj_ids: np.ndarray, masks: np.ndarray, subject: np.ndarray) -> int | None:
    """The found instance that overlaps the reference subject most, if enough."""
    best, best_iou = None, MIN_REFERENCE_IOU
    for obj_id, mask in zip(obj_ids, masks, strict=True):
        union = np.logical_or(mask, subject).sum()
        iou = np.logical_and(mask, subject).sum() / union if union else 0.0
        if iou >= best_iou:
            best, best_iou = int(obj_id), float(iou)
    return best


def composite(pixels: np.ndarray, mask: np.ndarray) -> bytes:
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


def cached() -> str | None:
    """The checkpoint when every file of it is in the Hugging Face cache, else None."""
    from huggingface_hub import try_to_load_from_cache

    paths = [try_to_load_from_cache(REPO, name, revision=REVISION) for name in FILES]
    if all(isinstance(path, str) for path in paths):
        return str(paths[-1])
    return None


def weights() -> str:
    """The checkpoint, downloaded when missing (a gated repository: needs HF_TOKEN)."""
    from huggingface_hub import hf_hub_download

    found = cached()
    if found:
        return found
    for name in FILES:
        path = hf_hub_download(REPO, name, revision=REVISION)
    return path


def adapt(model: Any) -> None:
    """Fit upstream's SAM 3.1 video model to one 8 GB GPU and to point prompts alone."""
    # start_session passes offload_state_to_cpu, which the SAM 3.1 model does not
    # take (facebookresearch/sam3#544, open): drop it.
    init_state = model.init_state

    def init_state_without_state_offload(*args: Any, **kwargs: Any) -> Any:
        kwargs.pop("offload_state_to_cpu", None)
        return init_state(*args, **kwargs)

    model.init_state = init_state_without_state_offload
    # Upstream batches 16 frames through the detector and postprocessing for an
    # 80 GB GPU; one frame at a time fits 8 GB.
    model.use_batched_grounding = False
    model.postprocess_batch_size = 1
    # The tracker keeps every tracked frame's memory and image features on the
    # GPU, 12.7 MB a frame for one subject: a 10-second clip ran out of 8 GB. It
    # reads back at most num_maskmem frames of memory and max_obj_ptrs_in_encoder
    # frames of object pointers (7 and 16), so frames twice that far are
    # forgotten. Upstream's own trimming (trim_past_non_cond_mem_for_eval) fails
    # on this model with a KeyError, and keeps entries that removing an object
    # can no longer slice.
    tracking = model.tracker.model
    lookback = 2 * max(
        tracking.num_maskmem * tracking.memory_temporal_stride_for_eval,
        tracking.max_obj_ptrs_in_encoder,
    )
    trim_output_and_memory = tracking._trim_output_and_memory

    def forget_old_frames(
        frame_idx: int, output_dict: dict[str, Any], *args: Any, **kwargs: Any
    ) -> Any:
        tracked = output_dict["non_cond_frame_outputs"]
        for index in [index for index in tracked if abs(frame_idx - index) > lookback]:
            del tracked[index]
        return trim_output_and_memory(frame_idx, output_dict, *args, **kwargs)

    tracking._trim_output_and_memory = forget_old_frames
    # Point prompts refine a text prompt's tracks: their propagation returns a
    # frame only where the detector's pass left an output, so a subject given by
    # points alone vanished after the first frame. Start such frames empty.
    build_output = model._build_sam2_output

    def build_output_without_detections(
        inference_state: dict[str, Any], frame_idx: int, *args: Any, **kwargs: Any
    ) -> Any:
        inference_state["cached_frame_outputs"].setdefault(frame_idx, {})
        return build_output(inference_state, frame_idx, *args, **kwargs)

    model._build_sam2_output = build_output_without_detections


class Sam3Server(ModelServer):
    model = ModelInfo(
        id="sam3-1",
        name="SAM 3.1 · subject or concept",
        revision=REVISION[:12],
        license="SAM License (Meta)",
        source="https://huggingface.co/" + REPO,
        details={"parameters": "0.85 B", "prompt language": "English"},
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._predictor: Any = None

    def load(self) -> None:
        import torch

        if not shutil.which("ffmpeg"):
            raise FileNotFoundError("ffmpeg is required")
        if not torch.cuda.is_available():
            raise RuntimeError("SAM 3.1 needs a CUDA GPU")
        from sam3.model_builder import build_sam3_multiplex_video_predictor

        # FlashAttention 3 runs on Hopper GPUs only; PyTorch's attention elsewhere.
        self._predictor = build_sam3_multiplex_video_predictor(
            checkpoint_path=weights(), use_fa3=False, async_loading_frames=False
        )
        adapt(self._predictor.model)
        # Upstream reads the 3.5 GB checkpoint twice on the CPU; return that memory.
        gc.collect()
        ctypes.CDLL("libc.so.6").malloc_trim(0)

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, Params)
        prompt = check_prompt(job.prompt)
        reference = job.inputs.get("reference")
        if reference is None and prompt is None:
            raise InvalidInput("give a reference frame, a prompt naming what to keep, or both")
        with tempfile.TemporaryDirectory(prefix="sam3-") as scratch:
            directory = Path(scratch)
            first, count, fps = unpack_video(job.inputs["video"].data, directory)
            subject = None
            if reference is not None:
                subject = reference_mask(open_image(reference.data, keep_alpha=True), first.size)
            try:
                with torch.inference_mode(), torch.autocast("cuda", dtype=torch.bfloat16):
                    return self._segment(
                        directory,
                        first.size,
                        count,
                        fps,
                        prompt,
                        subject,
                        job.params.require_full_subject,
                    )
            except RuntimeError as error:
                if not is_out_of_memory(error):
                    raise
                raise GenerationError(
                    "out of GPU memory segmenting video", retryable=True
                ) from error
            finally:
                gc.collect()
                torch.cuda.empty_cache()

    def _segment(
        self,
        directory: Path,
        size: tuple[int, int],
        count: int,
        fps: float,
        prompt: str | None,
        subject: np.ndarray | None,
        require_full_subject: bool,
    ) -> list[Output]:
        predictor = self._predictor
        session = predictor.handle_request(
            {
                "type": "start_session",
                "resource_path": str(directory / "frames"),
                "offload_video_to_cpu": True,
            }
        )["session_id"]
        try:
            width, height = size
            meta: dict[str, Any] = {}
            keep: int | None = None
            box: list[int] = []
            points: list[list[int]] = []
            if subject is not None:
                box, points = reference_prompts(subject, require_full_subject=require_full_subject)
                meta.update(reference_box=box, reference_points=points)
            if prompt is not None:
                found = predictor.handle_request(
                    {"type": "add_prompt", "session_id": session, "frame_index": 0, "text": prompt}
                )["outputs"]
                meta["prompt"] = prompt
                meta["found_on_first_frame"] = len(found["out_obj_ids"])
                if subject is not None:
                    keep = best_match(found["out_obj_ids"], found["out_binary_masks"], subject)
                    if keep is None:
                        raise GenerationError(
                            "nothing the prompt names overlaps the reference subject "
                            "on the first frame"
                        )
            else:
                # The box as SAM's two corner points (labels 2 and 3), then the
                # interior points, all relative to the frame.
                corners = [[box[0], box[1]], [box[2], box[3]]]
                predictor.handle_request(
                    {
                        "type": "add_prompt",
                        "session_id": session,
                        "frame_index": 0,
                        "points": [[x / width, y / height] for x, y in corners + points],
                        "point_labels": [2, 3] + [1] * len(points),
                        "obj_id": SUBJECT_ID,
                    }
                )
                keep = SUBJECT_ID
            return [
                self._propagate(
                    directory, session, size, count, fps, keep, require_full_subject, meta
                )
            ]
        finally:
            predictor.handle_request({"type": "close_session", "session_id": session})

    def _propagate(
        self,
        directory: Path,
        session: str,
        size: tuple[int, int],
        count: int,
        fps: float,
        keep: int | None,
        require_full_subject: bool,
        meta: dict[str, Any],
    ) -> Output:
        shares: list[float] = []
        seen: set[int] = set()
        pixels = video_frames(str(directory / "video.mp4"))
        width, height = size

        def matted() -> Iterator[bytes]:
            for response in self._predictor.handle_stream_request(
                {
                    "type": "propagate_in_video",
                    "session_id": session,
                    "propagation_direction": "forward",
                    "start_frame_index": 0,
                    "evict_cached_frame_outputs": True,
                }
            ):
                index = response["frame_index"]
                if index != len(shares):
                    raise GenerationError("tracker returned missing or reordered video frames")
                outputs = response["outputs"]
                mask = np.zeros((height, width), dtype=bool)
                for obj_id, object_mask in zip(
                    outputs["out_obj_ids"], outputs["out_binary_masks"], strict=True
                ):
                    if keep is None or int(obj_id) == keep:
                        mask |= object_mask.astype(bool)
                        seen.add(int(obj_id))
                mask = remove_speckles(mask)
                if require_full_subject and touches_edge(mask):
                    raise GenerationError(
                        f"selected subject reaches the image edge at frame {index}; "
                        "use a wider crop or a shorter clip for full-body motion capture"
                    )
                shares.append(float(mask.mean()))
                if keep is not None and shares[-1] < LOST_SHARE:
                    raise GenerationError(f"selected subject lost at video frame {index}")
                frame = next(pixels, None)
                if frame is None:
                    raise GenerationError("the video has fewer frames than the tracker returned")
                yield composite(frame, mask)
            if len(shares) != count:
                raise GenerationError("tracker did not return all video frames")
            if not seen:
                raise GenerationError("nothing the prompt names was found in the video")

        try:
            data = encode_mp4(matted(), width=width, height=height, fps=fps)
        except VideoEncodingError as error:
            raise GenerationError(str(error)) from error
        return Output(
            "video/mp4",
            data,
            {
                "frames": count,
                "fps": round(fps, 6),
                "background": "white",
                "original_pixels": True,
                "objects": len(seen),
                "object_share_min": round(min(shares), 5),
                "object_share_mean": round(float(np.mean(shares)), 5),
                **meta,
            },
        )
