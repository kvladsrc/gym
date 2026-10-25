import importlib.util

import pytest
from gvhmr_model_server.server import WORKER

spec = importlib.util.spec_from_file_location(
    "person_tracking", WORKER.with_name("person_tracking.py")
)
assert spec
assert spec.loader
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)


def detection(identity, x=0, height=100):
    return {"id": identity, "bbx_xyxy": [x, 0, x + 40, height]}


def test_same_actor_id_change_follows_visible_person_instead_of_holding_old_box():
    history = [[detection(1, i)] for i in range(10)] + [[detection(2, i)] for i in range(10, 20)]
    boxes, report = adapter.dominant_person(history)
    assert [b[0] for b in boxes] == list(range(20))
    assert report["tracklet_switches"] == 1
    assert report["interpolated_frames"] == 0


def test_background_person_does_not_replace_missing_foreground_actor():
    history = [[detection(1, 0)], [detection(90, 200, 20)], [detection(2, 2)]]
    boxes, report = adapter.dominant_person(history)
    assert boxes[1][0] == 1
    assert report["max_missing_run"] == 1


def test_long_detection_gap_is_rejected_instead_of_extrapolated():
    with pytest.raises(RuntimeError, match="refusing stale crop"):
        adapter.dominant_person([[detection(1)]] + [[]] * 20 + [[detection(2)]])


def test_multiple_foreground_people_require_explicit_crop():
    with pytest.raises(RuntimeError, match="Multiple foreground"):
        adapter.dominant_person([[detection(1), detection(2, 200)]])


def test_short_edge_gaps_are_bounded():
    boxes, report = adapter.dominant_person([[], [detection(1)], []])
    assert len(boxes) == 3
    assert boxes[0] == boxes[1] == boxes[2]
    assert report["interpolated_frames"] == 2


def test_isolated_duplicate_during_blur_is_bridged_between_unique_observations():
    boxes, report = adapter.dominant_person(
        [
            [detection(1)],
            [detection(1), detection(2, 200)],
            [detection(1, 2)],
        ]
    )
    assert boxes[1][0] == 1
    assert report["ambiguous_detection_frames"] == [1]


def test_persistent_two_people_are_not_silently_selected():
    with pytest.raises(RuntimeError, match="Multiple foreground"):
        adapter.dominant_person(
            [[detection(1)]] + [[detection(1), detection(2, 200)]] * 3 + [[detection(1)]]
        )


def test_explicit_overlay_region_excludes_illustration_without_cropping_live_actor():
    live = {"id": 1, "bbx_xyxy": [1221, 425, 1593, 1005]}
    illustration = {"id": 2, "bbx_xyxy": [303, 14, 576, 533]}
    history = [[live, illustration]] * 4
    with pytest.raises(RuntimeError, match="Multiple foreground"):
        adapter.dominant_person(history)
    boxes, report = adapter.dominant_person(
        history, ignored_regions=[(0, 0, 0.35, 0.55)], image_size=(1920, 1080)
    )
    assert boxes == [live["bbx_xyxy"]] * 4
    assert report["ignored_detections"] == 4


def test_overlay_exclusion_does_not_disable_two_people_protection_elsewhere():
    with pytest.raises(RuntimeError, match="Multiple foreground"):
        adapter.dominant_person(
            [[detection(1, 500), detection(2, 800)]] * 4,
            ignored_regions=[(0, 0, 0.3, 0.5)],
            image_size=(1000, 1000),
        )
