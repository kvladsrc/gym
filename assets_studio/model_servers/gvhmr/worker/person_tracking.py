"""Select the dominant single person across fragmented detector tracking IDs.

The server accepts one foreground actor. A tracker ID is not an actor identity:
never extrapolate a dead ID's crop across a new visible tracklet.
"""

import math
import statistics
from collections.abc import Sequence
from itertools import pairwise
from typing import Any


def area(box: Sequence[float]) -> float:
    return (box[2] - box[0]) * (box[3] - box[1])


def dominant_person(
    history: Sequence[Sequence[dict[str, Any]]],
    max_gap: int = 12,
    ignored_regions: Sequence[Sequence[float]] = (),
    image_size: tuple[int, int] | None = None,
) -> tuple[list[list[float]], dict[str, Any]]:
    if ignored_regions and (image_size is None or min(image_size) <= 0):
        raise ValueError("Image size is required for normalized person exclusion regions")
    ignored = 0
    candidates: list[list[tuple[int, list[float]]]] = []
    for frame in history:
        boxes = []
        for detection in frame:
            box = list(map(float, detection["bbx_xyxy"]))
            if (
                len(box) == 4
                and all(map(math.isfinite, box))
                and box[2] > box[0]
                and box[3] > box[1]
            ):
                if image_size is not None and any(
                    left <= (box[0] + box[2]) / (2 * image_size[0]) <= right
                    and top <= (box[1] + box[3]) / (2 * image_size[1]) <= bottom
                    for left, top, right, bottom in ignored_regions
                ):
                    ignored += 1
                    continue
                boxes.append((int(detection["id"]), box))
        candidates.append(boxes)
    heights = [max(b[3] - b[1] for _, b in frame) for frame in candidates if frame]
    if not heights:
        raise RuntimeError("No foreground person detected")
    reference_height = statistics.median(heights)
    selected: list[list[float] | None] = []
    identities: list[int | None] = []
    ambiguous: list[int] = []
    for frame_index, frame in enumerate(candidates):
        foreground = [
            (identity, b) for identity, b in frame if b[3] - b[1] >= 0.55 * reference_height
        ]
        foreground.sort(key=lambda item: area(item[1]), reverse=True)
        if not foreground:
            selected.append(None)
            identities.append(None)
            continue
        identity, box = foreground[0]
        if len(foreground) > 1:
            other = foreground[1][1]
            separation = math.hypot(
                (box[0] + box[2] - other[0] - other[2]) / 2,
                (box[1] + box[3] - other[1] - other[3]) / 2,
            )
            if area(other) > 0.65 * area(box) and separation > 0.5 * reference_height:
                ambiguous.append(frame_index)
                selected.append(None)
                identities.append(None)
                continue
        selected.append(box)
        identities.append(identity)
    # One or two isolated duplicate detections during blur may be bridged only
    # between unique observations. Never guess an identity across a real pair
    # of people or a long dissolve.
    for frame in ambiguous:
        if (
            frame == 0
            or frame == len(selected) - 1
            or (frame + 1 in ambiguous and frame + 2 in ambiguous)
        ):
            raise RuntimeError(
                f"Multiple foreground people at frame {frame + 1}: crop the intended actor first"
            )
    missing = [i for i, b in enumerate(selected) if b is None]
    i = 0
    largest_gap = 0
    while i < len(selected):
        if selected[i] is not None:
            i += 1
            continue
        start = i
        while i < len(selected) and selected[i] is None:
            i += 1
        length = i - start
        largest_gap = max(largest_gap, length)
        if length > max_gap:
            raise RuntimeError(f"Person tracking missing for {length} frames; refusing stale crop")
        before = selected[start - 1] if start else selected[i]
        after = selected[i] if i < len(selected) else before
        assert before is not None
        assert after is not None
        for f in range(start, i):
            weight = (
                (f - start + 1) / (length + 1)
                if start and i < len(selected)
                else (0 if start else 1)
            )
            selected[f] = [a + (b - a) * weight for a, b in zip(before, after, strict=True)]
    boxes = [b for b in selected if b is not None]
    assert len(boxes) == len(history)
    for first, second in pairwise(boxes):
        step = math.hypot(
            (first[0] + first[2] - second[0] - second[2]) / 2,
            (first[1] + first[3] - second[1] - second[3]) / 2,
        )
        if step > 0.75 * reference_height:
            raise RuntimeError(
                "Abrupt foreground tracking jump; check person identity or source edit"
            )
    observed_ids = [i for i in identities if i is not None]
    report = dict(
        method="dominant-person-tracklets-v1",
        ignored_person_regions=[list(region) for region in ignored_regions],
        ignored_detections=ignored,
        frames=len(boxes),
        track_ids=sorted(set(observed_ids)),
        tracklet_switches=sum(a != b for a, b in pairwise(observed_ids)),
        interpolated_frames=len(missing),
        max_missing_run=largest_gap,
        ambiguous_detection_frames=ambiguous,
    )
    return boxes, report
