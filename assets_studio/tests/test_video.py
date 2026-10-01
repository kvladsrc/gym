"""Encoding frames into canonical MP4 (model_server_sdk.video)."""

import json
import shutil
import subprocess
from pathlib import Path

import pytest
from model_server_sdk.video import VideoEncodingError, encode_mp4

from model_server_sdk import media

pytestmark = pytest.mark.skipif(shutil.which("ffmpeg") is None, reason="needs ffmpeg")


def _frames(count: int, width: int, height: int) -> list[bytes]:
    return [
        bytes(
            (x * 7 + y * 3 + i * 11) % 256
            for y in range(height)
            for x in range(width)
            for _ in range(3)
        )
        for i in range(count)
    ]


def test_frames_become_canonical_mp4(tmp_path: Path) -> None:
    data = encode_mp4(_frames(9, 32, 24), width=32, height=24, fps=24)
    assert media.looks_like("video/mp4", data)
    assert media.mp4_problems(data) == []
    path = tmp_path / "clip.mp4"
    path.write_bytes(data)
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-show_streams", "-of", "json", str(path)],
        capture_output=True,
        check=True,
    )
    [stream] = json.loads(probe.stdout)["streams"]
    assert (stream["codec_name"], stream["profile"], stream["pix_fmt"]) == (
        "h264",
        "High",
        "yuv420p",
    )
    assert int(stream["nb_frames"]) == 9


def test_same_frames_give_the_same_bytes() -> None:
    frames = _frames(5, 32, 32)
    assert encode_mp4(frames, width=32, height=32, fps=24) == encode_mp4(
        frames, width=32, height=32, fps=24
    )


def test_odd_sides_are_padded() -> None:
    data = encode_mp4(_frames(3, 33, 17), width=33, height=17, fps=8)
    assert media.mp4_problems(data) == []


def test_a_wrong_frame_size_is_an_error() -> None:
    with pytest.raises(VideoEncodingError, match="frame 1 has"):
        encode_mp4([bytes(32 * 32 * 3), bytes(10)], width=32, height=32, fps=24)


def test_colours_are_bt709_and_tagged(tmp_path: Path) -> None:
    """Untagged HD video is shown as BT.709: converting with BT.601 shifts
    reds and greens. A pure red frame must land on BT.709's red."""
    red = bytes([255, 0, 0]) * (32 * 32)
    path = tmp_path / "red.mp4"
    path.write_bytes(encode_mp4([red] * 3, width=32, height=32, fps=8))
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-show_streams", "-of", "json", str(path)],
        capture_output=True,
        check=True,
    )
    [stream] = json.loads(probe.stdout)["streams"]
    assert {stream[key] for key in ("color_space", "color_transfer", "color_primaries")} == {
        "bt709"
    }
    raw = subprocess.run(
        [
            "ffmpeg",
            "-loglevel",
            "error",
            "-i",
            str(path),
            "-frames:v",
            "1",
            "-f",
            "rawvideo",
            "-pix_fmt",
            "yuv420p",
            "-",
        ],
        capture_output=True,
        check=True,
    ).stdout
    luma, u, v = raw[0], raw[32 * 32], raw[32 * 32 + 16 * 16]
    assert (luma, u, v) == (63, 102, 240)  # BT.601 would give 81, 90, 240
