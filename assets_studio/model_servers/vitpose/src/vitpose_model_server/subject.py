"""The subject's box: named by the caller or found on a plain background, then followed.

The pose model has no detector of its own and looks where it is told. Positions are in
pixels with pixel centres at whole numbers, so a picture's left edge is at -0.5.
"""

import cv2
import numpy as np
import numpy.typing as npt

from vitpose_model_server.pose import (
    LEFT_HIP,
    LEFT_SHOULDER,
    RIGHT_HIP,
    RIGHT_SHOULDER,
    SCORE_THRESHOLD,
    Box,
)

# A plain background's pixels are this close to its colour, in 8-bit levels, and all but
# this share of the picture's border are background.
BACKGROUND_SPREAD = 12
BORDER_SUBJECT_SHARE = 0.02
# The box stays where it is until the torso has moved this share of the box's shorter side.
SLACK = 0.1
TORSO = [LEFT_SHOULDER, RIGHT_SHOULDER, LEFT_HIP, RIGHT_HIP]


def parse_region(region: str) -> tuple[float, float, float, float] | None:
    """A region written as "left,top,right,bottom" in shares of the picture's width and
    height, or None for an empty string."""
    if not region.strip():
        return None
    try:
        left, top, right, bottom = map(float, region.split(","))
    except ValueError as error:
        raise ValueError("use four comma-separated numbers: left,top,right,bottom") from error
    if not (0 <= left < right <= 1 and 0 <= top < bottom <= 1):
        raise ValueError("a region needs 0 <= left < right <= 1 and 0 <= top < bottom <= 1")
    return left, top, right, bottom


def region_box(region: tuple[float, float, float, float], size: tuple[int, int]) -> Box:
    """A region of a picture of `size` (width, height) as a box in its pixels."""
    left, top, right, bottom = region
    width, height = size
    return (left * width - 0.5, top * height - 0.5, right * width - 0.5, bottom * height - 0.5)


def whole_picture(size: tuple[int, int]) -> Box:
    return region_box((0.0, 0.0, 1.0, 1.0), size)


def box_on_plain_background(frame: npt.NDArray[np.uint8]) -> Box | None:
    """The box of everything that stands out from the background of an RGB picture whose
    border is one colour; None when the border is not, or nothing stands out."""
    border = np.concatenate([frame[0], frame[-1], frame[:, 0], frame[:, -1]]).astype(np.int16)
    colour = np.median(border, axis=0)
    if (np.abs(border - colour).max(axis=1) > BACKGROUND_SPREAD).mean() > BORDER_SUBJECT_SHARE:
        return None
    subject = (np.abs(frame.astype(np.int16) - colour).max(axis=2) > 2 * BACKGROUND_SPREAD).astype(
        np.uint8
    )
    # Without the specks that video compression leaves.
    subject = cv2.morphologyEx(subject, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    rows, columns = np.nonzero(subject)
    if len(rows) == 0:
        return None
    return (
        float(columns.min()) - 0.5,
        float(rows.min()) - 0.5,
        float(columns.max()) + 0.5,
        float(rows.max()) + 0.5,
    )


class Follower:
    """A subject's box that keeps its size and moves with the subject's torso."""

    def __init__(self, box: Box) -> None:
        self.box = box
        # From the torso to the box's centre, as they were when the torso was first found.
        self._offset: npt.NDArray[np.float64] | None = None

    def follow(self, points: npt.NDArray[np.floating], scores: npt.NDArray[np.floating]) -> None:
        """Move the box after the joints found in it, for the next frame. A torso the
        model is unsure of moves nothing, and a small movement neither: the box does not
        tremble with the joints."""
        if scores[TORSO].min() < SCORE_THRESHOLD:
            return
        torso = points[TORSO].mean(axis=0).astype(np.float64)
        left, top, right, bottom = self.box
        centre = np.array([(left + right) / 2, (top + bottom) / 2])
        if self._offset is None:
            self._offset = centre - torso
            return
        drift = torso + self._offset - centre
        distance = float(np.linalg.norm(drift))
        slack = SLACK * min(right - left, bottom - top)
        if distance > slack:
            x, y = drift * (1 - slack / distance)
            self.box = (left + x, top + y, right + x, bottom + y)
