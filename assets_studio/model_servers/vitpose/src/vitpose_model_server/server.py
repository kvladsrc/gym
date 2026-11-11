"""The joints of a character in a video, as the studio's 2D pose observations.

One task, `video-to-pose-observations`: ViTPose++ looks at every frame inside the
subject's box and answers with the 17 COCO joints. The file is the one GVHMR's
preprocessing writes and PromptHMR reads (`studio.pose-observations.v1`); here the box
comes from the caller instead of a person detector.
"""

import contextlib
import hashlib
import json
import math
import tempfile
from collections.abc import Iterator
from typing import Any

import cv2
import numpy as np
import numpy.typing as npt
from pydantic import BaseModel, ConfigDict, Field, field_validator

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
from vitpose_model_server.pose import REPO, REVISION, SCORE_THRESHOLD, Box, ViTPose
from vitpose_model_server.subject import (
    Follower,
    box_on_plain_background,
    parse_region,
    region_box,
    whole_picture,
)

FORMAT = "studio.pose-observations.v1"
MAX_SECONDS = 10
MAX_FRAMES = 601
MAX_PIXELS = 1920 * 1080

Picture = npt.NDArray[np.uint8]


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    subject_region: str = Field(
        default="",
        description="Where the character is in the first frame: left,top,right,bottom in "
        "normalized 0–1 coordinates. Empty: a character on a plain background is found "
        "by itself, otherwise the whole frame is taken",
        json_schema_extra=ui(
            ru="Где персонаж в первом кадре: лево,верх,право,низ в долях кадра (0–1). "
            "Пусто: на однотонном фоне персонаж находится сам, иначе берётся весь кадр",
            primary=True,
        ),
    )
    follow: bool = Field(
        default=True,
        description="Move the region with the character's torso from frame to frame",
        json_schema_extra=ui(ru="Вести область за корпусом персонажа от кадра к кадру"),
    )

    @field_validator("subject_region")
    @classmethod
    def valid_region(cls, region: str) -> str:
        parse_region(region)
        return region.strip()


TASK = TaskSpec(
    "video-to-pose-observations",
    Params,
    ("text/plain",),
    prompt="none",
    inputs=(
        InputSpec(
            role="video",
            mime=["video/mp4"],
            description=f"A video of one character, at most {MAX_SECONDS} s, 1080p and 60 fps",
            labels={"ru": f"Видео с одним персонажем, до {MAX_SECONDS} с, 1080p и 60 кадров/с"},
        ),
    ),
    max_count=1,
)


@contextlib.contextmanager
def open_video(data: bytes) -> Iterator[tuple[float, Iterator[Picture]]]:
    """A video's frame rate and its RGB frames, read one at a time."""
    with tempfile.NamedTemporaryFile(suffix=".mp4") as file:
        file.write(data)
        file.flush()
        capture = cv2.VideoCapture(file.name)
        try:
            fps = capture.get(cv2.CAP_PROP_FPS)
            if not 0 < fps <= 60.01:
                raise InvalidInput("video frame rate must be between 0 and 60 fps")

            def frames() -> Iterator[Picture]:
                count = 0
                while True:
                    ok, bgr = capture.read()
                    if not ok:
                        break
                    if bgr.shape[0] * bgr.shape[1] > MAX_PIXELS:
                        raise InvalidInput("video must be at most 1920×1080 pixels")
                    count += 1
                    if count > MAX_FRAMES or count > MAX_SECONDS * fps + 0.01:
                        raise InvalidInput(f"video must be at most {MAX_SECONDS} seconds")
                    yield cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB).astype(np.uint8)
                if count == 0:
                    raise InvalidInput("cannot read any video frame")

            yield fps, frames()
        finally:
            capture.release()


def assumed_intrinsics(size: tuple[int, int]) -> list[list[float]]:
    """The camera the format's readers assume when nothing is known about it, as GVHMR
    does: the focal length is the picture's diagonal, the principal point its centre."""
    width, height = size
    focal = math.hypot(width, height)
    return [[focal, 0.0, width / 2], [0.0, focal, height / 2], [0.0, 0.0, 1.0]]


def observations(
    points: npt.NDArray[np.floating],
    scores: npt.NDArray[np.floating],
    boxes: list[Box],
    fps: float,
    size: tuple[int, int],
    video: bytes,
    tracking: dict[str, Any],
) -> dict[str, Any]:
    """The observations file. Positions are in the video's pixels, x to the right and y
    down, from the centre of the top left pixel."""
    return {
        "format": FORMAT,
        "fps": round(fps, 6),
        "frames": len(points),
        "size": list(size),
        # The same bytes reach PromptHMR or other consumers as their video input.
        "source_sha256": hashlib.sha256(video).hexdigest(),
        # Where the model was told to look, per frame: left, top, right, bottom.
        "bbx_xyxy": np.round(np.array(boxes, dtype=np.float64), 2).tolist(),
        # Per frame and COCO joint: x, y and how sure the model is, from 0 to 1.
        "kp2d": np.concatenate(
            [
                np.round(points.astype(np.float64), 2),
                np.round(scores.astype(np.float64), 3)[:, :, None],
            ],
            axis=2,
        ).tolist(),
        "K_fullimg": [assumed_intrinsics(size)] * len(points),
        "intrinsics_source": "assumed: focal length from the image size, as GVHMR's estimate_K",
        "detector": "ViTPose++-H COCO-17 in the subject's box; no person detector",
        "tracking": tracking,
        "revision": REVISION,
    }


class VitPoseServer(ModelServer):
    model = ModelInfo(
        id="vitpose-plus-huge",
        name="ViTPose++ H · 2D joints",
        revision=REVISION[:12] + ".2",
        license="Apache-2.0",
        source="https://huggingface.co/" + REPO,
        details={
            "joints": "17 (COCO): face, shoulders, elbows, wrists, hips, knees, ankles",
            "subject": "one, in the region you name; there is no person detector",
            "precision": "fp32, as published",
            "VRAM": "~3.5 GB",
            "clip length": f"up to {MAX_SECONDS} s per job",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._model: ViTPose | None = None

    def load(self) -> None:
        self._model = ViTPose()

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        assert self._model is not None
        params = job.params
        video = job.inputs["video"].data
        with open_video(video) as (fps, frames):
            frame = next(frames, None)
            assert frame is not None
            size = (frame.shape[1], frame.shape[0])
            region = parse_region(params.subject_region)
            if region is not None:
                box, chosen = region_box(region, size), "named"
            elif (found := box_on_plain_background(frame)) is not None:
                box, chosen = found, "found on a plain background"
            else:
                box, chosen = whole_picture(size), "the whole frame"
            follower = Follower(box)
            points, scores, boxes = [], [], []
            try:
                while frame is not None:
                    boxes.append(follower.box)
                    found_points, found_scores = self._model.joints(frame, follower.box)
                    points.append(found_points)
                    scores.append(found_scores)
                    if params.follow:
                        follower.follow(found_points, found_scores)
                    frame = next(frames, None)
            except RuntimeError as error:
                if not is_out_of_memory(error):
                    raise
                raise GenerationError("out of GPU memory finding joints", retryable=True) from error
            finally:
                self._model.release()
        travelled = np.array(boxes[-1][:2]) - np.array(boxes[0][:2])
        tracking = {
            "subject": chosen,
            "follow": params.follow,
            "box_travel_px": np.round(travelled, 1).tolist(),
        }
        document = observations(
            np.array(points), np.array(scores), boxes, fps, size, video, tracking
        )
        return [
            Output(
                "text/plain",
                (json.dumps(document, allow_nan=False) + "\n").encode(),
                {
                    "format": FORMAT,
                    "frames": len(points),
                    "fps": round(fps, 6),
                    "subject": chosen,
                    "subject_box": document["bbx_xyxy"][0],
                    "box_travel_px": tracking["box_travel_px"],
                    "mean_score": round(float(np.mean(scores)), 3),
                    "unsure_joints": int((np.array(scores) < SCORE_THRESHOLD).sum()),
                },
            )
        ]
