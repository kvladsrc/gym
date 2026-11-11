"""The crop, the heatmap decoding and the way back to the frame, without weights."""

import numpy as np
import pytest
from vitpose_model_server import pose


def test_window_is_the_box_with_a_margin_in_the_models_shape():
    center, size = pose.crop_window((100, 200, 140, 300))
    assert center.tolist() == [120, 250]
    assert size.tolist() == pytest.approx([93.75, 125])
    # A wide box grows in height instead.
    _, size = pose.crop_window((0, 0, 200, 100))
    assert size.tolist() == pytest.approx([250, 250 * 4 / 3])


def test_crop_shows_the_window_at_the_models_size():
    frame = np.zeros((200, 300, 3), dtype=np.uint8)
    frame[118:123, 148:153] = 255  # centred on (150, 120)
    center, size = pose.crop_window((120, 80, 180, 160))
    picture = pose.crop(frame, center, size)
    assert picture.shape == (256, 192, 3)
    rows, columns = np.nonzero(picture[:, :, 0] > 128)
    matrix = pose.crop_matrix(center, size)
    assert [columns.mean(), rows.mean()] == pytest.approx(matrix @ [150, 120, 1], abs=0.5)


def test_a_position_in_the_heatmap_is_the_same_place_in_the_frame():
    center, size = pose.crop_window((207, 553, 297, 675))
    place = np.array([251.3, 600.7], dtype=np.float32)
    matrix = pose.crop_matrix(center, size)
    in_input = matrix[:, :2] @ place + matrix[:, 2]
    in_heatmap = in_input * (np.array(pose.HEATMAP_SIZE) - 1) / (np.array(pose.INPUT_SIZE) - 1)
    back = pose.to_frame(in_heatmap[None].astype(np.float32), center, size)
    assert back[0] == pytest.approx(place, abs=1e-3)


def test_decoding_finds_a_peak_between_heatmap_pixels():
    width, height = pose.HEATMAP_SIZE
    x, y = np.meshgrid(np.arange(width), np.arange(height))
    peaks = np.array([[10.3 + joint, 20.7 + 1.5 * joint] for joint in range(17)])
    heatmaps = np.stack(
        [0.8 * np.exp(-((x - px) ** 2 + (y - py) ** 2) / (2 * 2.0**2)) for px, py in peaks]
    ).astype(np.float32)
    points, scores = pose.decode(heatmaps)
    assert points == pytest.approx(peaks, abs=0.05)
    assert scores == pytest.approx(0.8, abs=0.05)


def test_a_mirrored_joint_is_the_other_sides_joint():
    for index, mirrored in enumerate(pose.MIRRORED):
        name = pose.JOINTS[index]
        if name.startswith("left_"):
            assert pose.JOINTS[mirrored] == "right_" + name.removeprefix("left_")
        elif name.startswith("right_"):
            assert pose.JOINTS[mirrored] == "left_" + name.removeprefix("right_")
        else:
            assert mirrored == index
