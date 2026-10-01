"""Text and video assets, audio formats and their conversion (ADR-003)."""

import json
import shutil
import sqlite3
import subprocess
from importlib import resources
from pathlib import Path

import pytest
from pydantic import BaseModel
from studio.domain import CANONICAL_MIME, MIME_KINDS, AssetKind, JobStatus
from studio.media import UnsupportedMedia, detect_mime, to_canonical
from studio.services.library import ImportError_
from studio.storage.db import Database
from support import live_server
from test_studio import model_server, running_studio, wait_for

from model_server_sdk import InputSpec, ModelInfo, ModelServer, Output, TaskSpec, create_app, media
from model_server_sdk import Job as ServerJob
from studio import media as studio_media

needs_ffmpeg = pytest.mark.skipif(shutil.which("ffmpeg") is None, reason="needs ffmpeg")


def _has_encoder(name: str) -> bool:
    if shutil.which("ffmpeg") is None:
        return False
    listing = subprocess.run(
        ["ffmpeg", "-hide_banner", "-encoders"], capture_output=True, text=True, check=False
    )
    return f" {name} " in listing.stdout


# Content detection


def test_text_is_detected_last() -> None:
    assert list(MIME_KINDS)[-1] == "text/plain"
    assert detect_mime("Мох на камне.\n".encode()) == "text/plain"
    # A WAV header is valid UTF-8 (NUL bytes) but must stay audio.
    assert detect_mime(media.encode_wav(media.tone(440, 0.1))) == "audio/wav"


def test_binary_is_not_text() -> None:
    with pytest.raises(UnsupportedMedia):
        detect_mime(b"\x00\x01\x02 not text")


def test_video_signatures() -> None:
    assert detect_mime(media.SAMPLE_MP4) == "video/mp4"
    quicktime = media.SAMPLE_MP4[:8] + b"qt  " + media.SAMPLE_MP4[12:]
    with pytest.raises(UnsupportedMedia):
        detect_mime(quicktime)


def test_every_kind_has_a_canonical_format() -> None:
    assert set(CANONICAL_MIME) == set(AssetKind)
    assert CANONICAL_MIME[AssetKind.TEXT] == "text/plain"
    assert CANONICAL_MIME[AssetKind.VIDEO] == "video/mp4"


# Conversion with ffmpeg


def _encode(tmp_path: Path, source: str, suffix: str, *options: str) -> bytes:
    target = tmp_path / f"sample-{len(list(tmp_path.glob('sample-*')))}{suffix}"
    subprocess.run(
        [
            "ffmpeg",
            "-nostdin",
            "-loglevel",
            "error",
            "-f",
            "lavfi",
            "-i",
            source,
            *options,
            str(target),
        ],
        check=True,
    )
    return target.read_bytes()


@needs_ffmpeg
@pytest.mark.parametrize(
    ("mime", "suffix", "codec"),
    [
        ("audio/flac", ".flac", "flac"),
        ("audio/ogg", ".ogg", "libvorbis"),
        pytest.param(
            "audio/mpeg",
            ".mp3",
            "libmp3lame",
            # ffmpeg decodes MP3 natively; only making the sample needs LAME.
            marks=pytest.mark.skipif(not _has_encoder("libmp3lame"), reason="no MP3 encoder"),
        ),
    ],
)
def test_audio_becomes_wav(tmp_path: Path, mime: str, suffix: str, codec: str) -> None:
    data = _encode(tmp_path, "sine=frequency=440:duration=1", suffix, "-c:a", codec)
    assert detect_mime(data) == mime
    converted, canonical = to_canonical(data, mime)
    assert canonical == "audio/wav"
    assert detect_mime(converted) == "audio/wav"


@needs_ffmpeg
def test_webm_becomes_browser_ready_mp4(tmp_path: Path) -> None:
    data = _encode(
        tmp_path, "testsrc=size=33x17:rate=8:duration=0.5", ".webm", "-c:v", "libvpx-vp9"
    )
    assert detect_mime(data) == "video/webm"
    converted, canonical = to_canonical(data, "video/webm")
    assert canonical == "video/mp4"
    target = tmp_path / "converted.mp4"
    target.write_bytes(converted)
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-show_streams", "-of", "json", str(target)],
        capture_output=True,
        check=True,
    )
    [stream] = json.loads(probe.stdout)["streams"]
    assert (stream["codec_name"], stream["pix_fmt"]) == ("h264", "yuv420p")
    assert (stream["width"], stream["height"]) == (34, 18)  # padded to even sides


def test_conversion_without_ffmpeg_is_explained(monkeypatch: pytest.MonkeyPatch) -> None:
    def missing(name: str) -> None:
        return None

    monkeypatch.setattr("shutil.which", missing)
    with pytest.raises(UnsupportedMedia, match="needs ffmpeg"):
        to_canonical(b"fLaC....", "audio/flac")


@needs_ffmpeg
def test_undecodable_audio_is_rejected() -> None:
    with pytest.raises(UnsupportedMedia, match="cannot convert"):
        to_canonical(b"fLaC" + b"\x00" * 64, "audio/flac")


def test_canonical_formats_pass_through() -> None:
    text = "строка\n".encode()
    assert to_canonical(text, "text/plain") == (text, "text/plain")
    assert to_canonical(media.SAMPLE_MP4, "video/mp4") == (media.SAMPLE_MP4, "video/mp4")


# The schema upgrade


def test_upgrade_keeps_assets_tags_and_references(tmp_path: Path) -> None:
    path = tmp_path / "studio.db"
    migrations = resources.files("studio.storage").joinpath("migrations")
    old = sqlite3.connect(path)
    for name in ("0001_initial.sql", "0002_job_version.sql"):
        old.executescript(migrations.joinpath(name).read_text())
    old.executescript(
        """
        PRAGMA user_version = 2;
        INSERT INTO assets (id, kind, blob_sha256, mime, size_bytes, origin, created_at)
          VALUES ('a1', 'image', 'x', 'image/png', 1, 'upload', '2026-01-01');
        INSERT INTO asset_tags VALUES ('a1', 'мох');
        INSERT INTO jobs (id, tab, task, count, status, created_at)
          VALUES ('j1', 'image', 'image-to-image', 1, 'succeeded', '2026-01-01');
        INSERT INTO job_inputs VALUES ('j1', 'image', 'a1');
        """
    )
    old.commit()
    old.close()

    database = Database(path)
    connection = database.connection
    assert database.schema_version == 4
    assert connection.execute("SELECT tag FROM asset_tags").fetchall()[0][0] == "мох"
    assert connection.execute("SELECT asset_id FROM job_inputs").fetchall()[0][0] == "a1"
    assert connection.execute("PRAGMA foreign_key_check").fetchall() == []
    assert connection.execute("PRAGMA foreign_keys").fetchone()[0] == 1
    connection.execute(
        "INSERT INTO assets (id, kind, blob_sha256, mime, size_bytes, origin, created_at) "
        "VALUES ('a2', 'video', 'y', 'video/mp4', 1, 'generated', '2026-01-02')"
    )
    # References still point at the rebuilt table and are enforced.
    with pytest.raises(sqlite3.IntegrityError):
        connection.execute("INSERT INTO asset_tags VALUES ('missing', 't')")
    database.close()


def test_failed_migration_rolls_back(tmp_path: Path) -> None:
    database = Database(tmp_path / "studio.db")
    version = database.schema_version
    with pytest.raises(sqlite3.OperationalError):
        database._apply(
            "9999_broken.sql", "CREATE TABLE t (x); SELECT * FROM nowhere;", version + 1
        )
    assert database.schema_version == version
    assert (
        database.connection.execute("SELECT name FROM sqlite_schema WHERE name = 't'").fetchall()
        == []
    )
    assert database.connection.execute("PRAGMA foreign_keys").fetchone()[0] == 1
    database.close()


# Detection must not misfile (review of ADR-003)


def _ftyp(brand: bytes) -> bytes:
    return b"\x00\x00\x00\x18ftyp" + brand + b"\x00\x00\x02\x00" + brand + b"mp41"


@pytest.mark.parametrize("brand", [b"M4A ", b"M4B ", b"heic", b"avif", b"3gp4", b"qt  "])
def test_other_iso_media_is_not_video(brand: bytes) -> None:
    with pytest.raises(UnsupportedMedia):
        detect_mime(_ftyp(brand))


@pytest.mark.parametrize(
    "text", [b"ID3 tags store metadata", b"OggS is a container", b"fLaC header"]
)
def test_text_that_starts_like_audio_is_text(text: bytes) -> None:
    assert detect_mime(text) == "text/plain"


@pytest.mark.parametrize("data", [b"", b"del \x7f", "c1 \u0085".encode()])
def test_empty_or_control_characters_are_not_text(data: bytes) -> None:
    with pytest.raises(UnsupportedMedia):
        detect_mime(data)


def test_matroska_is_not_webm() -> None:
    header = b"\x1a\x45\xdf\xa3\x9f\x42\x86\x81\x01\x42\x82\x88matroska"
    with pytest.raises(UnsupportedMedia):
        detect_mime(header)


def test_mp4_layout_problems() -> None:
    assert media.mp4_problems(media.SAMPLE_MP4) == []
    # moov after mdat: not +faststart.
    boxes: list[bytes] = []
    offset = 0
    while offset < len(media.SAMPLE_MP4):
        size = int.from_bytes(media.SAMPLE_MP4[offset : offset + 4], "big")
        boxes.append(media.SAMPLE_MP4[offset : offset + size])
        offset += size
    kinds = [box[4:8] for box in boxes]
    moov, mdat = boxes[kinds.index(b"moov")], boxes[kinds.index(b"mdat")]
    reordered = b"".join(box for box in boxes if box not in (moov, mdat)) + mdat + moov
    assert "moov after mdat (not +faststart)" in media.mp4_problems(reordered)
    assert "the last box is truncated" in media.mp4_problems(media.SAMPLE_MP4[:-3])


def test_checker_flags_non_canonical_text_and_video() -> None:
    from model_server_sdk.check import _canonical_problems  # pyright: ignore[reportPrivateUsage]

    assert _canonical_problems("text/plain", "строка\n".encode()) == []
    assert len(_canonical_problems("text/plain", "﻿a\r\nb".encode())) == 2
    assert _canonical_problems("video/mp4", media.SAMPLE_MP4) == []


# What the library stores


def test_text_is_normalised_on_import(tmp_path: Path) -> None:
    with running_studio(tmp_path) as studio:
        asset = studio.library.import_bytes("﻿первая\r\nвторая\rтретья".encode())
        assert asset.kind == "text"
        assert studio.library.file(asset).read_bytes() == "первая\nвторая\nтретья".encode()


@needs_ffmpeg
def test_non_canonical_mp4_is_converted_on_import(tmp_path: Path) -> None:
    ten_bit = _encode(
        tmp_path,
        "testsrc=size=32x32:rate=8:duration=0.5",
        ".mp4",
        "-c:v",
        "libx264",
        "-pix_fmt",
        "yuv420p10le",
    )
    assert studio_media.video_problems(ten_bit)
    with running_studio(tmp_path / "studio") as studio:
        asset = studio.library.import_bytes(ten_bit)
        stored = studio.library.file(asset).read_bytes()
    assert asset.mime == "video/mp4"
    assert stored != ten_bit
    assert studio_media.video_problems(stored) == []


@needs_ffmpeg
@pytest.mark.parametrize("codec", ["libopus", "flac"])
def test_mp4_with_other_audio_than_aac_is_converted(tmp_path: Path, codec: str) -> None:
    clip = _encode(
        tmp_path,
        "testsrc=size=32x32:rate=8:duration=0.5",
        ".mp4",
        *("-f", "lavfi", "-i", "sine=frequency=440:duration=0.5"),
        *("-map", "0:v", "-map", "1:a", "-c:v", "libx264", "-pix_fmt", "yuv420p"),
        *("-c:a", codec, "-strict", "-2", "-movflags", "+faststart"),
    )
    assert f"audio {codec.removeprefix('lib')}, not AAC" in studio_media.video_problems(clip)
    with running_studio(tmp_path / "studio") as studio:
        stored = studio.library.file(studio.library.import_bytes(clip)).read_bytes()
    assert studio_media.video_problems(stored) == []


@needs_ffmpeg
def test_untagged_mp4_is_converted_to_bt709(tmp_path: Path) -> None:
    untagged = _encode(
        tmp_path,
        "testsrc=size=64x64:rate=8:duration=0.5",
        ".mp4",
        "-c:v",
        "libx264",
        "-pix_fmt",
        "yuv420p",
        "-movflags",
        "+faststart",
    )
    assert "colour space unknown, not BT.709" in studio_media.video_problems(untagged)
    with running_studio(tmp_path / "studio") as studio:
        stored = studio.library.file(studio.library.import_bytes(untagged)).read_bytes()
    assert studio_media.video_problems(stored) == []


def _close(actual: tuple[int, int, int], expected: tuple[int, int, int]) -> bool:
    """Equal give or take the codec's rounding (±2)."""
    return all(abs(a - b) <= 2 for a, b in zip(actual, expected, strict=True))


def _first_yuv(tmp_path: Path, data: bytes, width: int) -> tuple[int, int, int]:
    path = tmp_path / "probe.mp4"
    path.write_bytes(data)
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
    height = len(raw) * 2 // 3 // width
    return raw[0], raw[width * height], raw[width * height + (width // 2) * (height // 2)]


@needs_ffmpeg
@pytest.mark.parametrize(
    ("size", "expected"), [("1280x720", (63, 102, 240)), ("320x240", (81, 90, 240))]
)
def test_untagged_video_keeps_its_colours(
    tmp_path: Path, size: str, expected: tuple[int, int, int]
) -> None:
    """Untagged HD is BT.709 and SD is BT.601 by convention, as players show
    them; converting to tagged BT.709 must not change the colours."""
    matrix = "bt709" if size == "1280x720" else "bt601"
    clip = _encode(
        tmp_path,
        f"color=c=red:size={size}:rate=8:duration=0.5",
        ".mp4",
        *(
            "-vf",
            f"scale=out_color_matrix={matrix}:out_range=tv,format=yuv420p,setparams=colorspace=unknown:color_primaries=unknown:color_trc=unknown",
        ),
        *("-c:v", "libx264", "-movflags", "+faststart"),
    )
    width = int(size.split("x")[0])
    assert _close(_first_yuv(tmp_path, clip, width), expected)
    with running_studio(tmp_path / "studio") as studio:
        stored = studio.library.file(studio.library.import_bytes(clip)).read_bytes()
    assert studio_media.video_problems(stored) == []
    # Now BT.709 red, give or take the codec's rounding (the wrong matrix
    # gave 58, 105, 229).
    assert all(
        abs(a - b) <= 2
        for a, b in zip(_first_yuv(tmp_path, stored, width), (63, 102, 240), strict=True)
    )


@needs_ffmpeg
def test_canonical_mp4_is_stored_as_is(tmp_path: Path) -> None:
    with running_studio(tmp_path) as studio:
        asset = studio.library.import_bytes(media.SAMPLE_MP4)
        assert studio.library.file(asset).read_bytes() == media.SAMPLE_MP4


@needs_ffmpeg
def test_webm_is_stored_as_mp4_and_audio_only_webm_is_rejected(tmp_path: Path) -> None:
    video = _encode(
        tmp_path, "testsrc=size=32x32:rate=8:duration=0.5", ".webm", "-c:v", "libvpx-vp9"
    )
    sound = _encode(tmp_path, "sine=frequency=440:duration=1", ".webm", "-c:a", "libopus")
    with running_studio(tmp_path / "studio") as studio:
        asset = studio.library.import_bytes(video)
        assert (asset.kind, asset.mime) == ("video", "video/mp4")
        with pytest.raises(ImportError_, match="no video stream"):
            studio.library.import_bytes(sound)


# Sending to a model server


class _EchoParams(BaseModel):
    pass


class WavOnlyServer(ModelServer):
    """Accepts only WAV and echoes it back, to observe conversions."""

    model = ModelInfo(id="wav-only", name="WAV only")
    tasks = (
        TaskSpec(
            "audio-to-audio",
            _EchoParams,
            ("audio/wav",),
            prompt="none",
            inputs=(InputSpec(role="audio", mime=["audio/wav"]),),
        ),
    )

    def load(self) -> None: ...

    def generate(self, job: ServerJob) -> list[Output]:
        received = job.inputs["audio"]
        assert received.mime == "audio/wav"
        return [Output("audio/wav", received.data) for _ in job.seeds]


class AudioLookingTextServer(ModelServer):
    model = ModelInfo(id="text", name="Text")
    tasks = (TaskSpec("text-to-text", _EchoParams, ("text/plain",)),)

    def load(self) -> None: ...

    def generate(self, job: ServerJob) -> list[Output]:
        return [Output("text/plain", b"ID3 tags hold the title of a song.\n") for _ in job.seeds]


@needs_ffmpeg
def test_flac_input_is_sent_as_wav(tmp_path: Path) -> None:
    flac = _encode(tmp_path, "sine=frequency=440:duration=1", ".flac", "-c:a", "flac")
    with (
        live_server(create_app(WavOnlyServer())) as url,
        running_studio(tmp_path / "studio", model_server("sound", url)) as studio,
    ):
        source = studio.library.import_bytes(flac)
        assert source.mime == "audio/flac"
        job = studio.generation.submit("sound", "audio-to-audio", inputs={"audio": source.id})
        done = wait_for(studio, job.id, JobStatus.SUCCEEDED)
        output = studio.library.asset(done.outputs[0])
        assert output is not None
        assert output.mime == "audio/wav"


def test_without_ffmpeg_the_job_fails_clearly(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    flac = b"fLaC\x00\x00\x00\x22" + bytes(34)
    with (
        live_server(create_app(WavOnlyServer())) as url,
        running_studio(tmp_path, model_server("sound", url)) as studio,
    ):
        source = studio.library.import_bytes(flac)

        def missing(name: str) -> None:
            return None

        monkeypatch.setattr("shutil.which", missing)
        job = studio.generation.submit("sound", "audio-to-audio", inputs={"audio": source.id})
        failed = wait_for(studio, job.id, JobStatus.FAILED)
        assert failed.error_code == "invalid_request"
        assert "needs ffmpeg" in (failed.error_message or "")


def test_text_output_that_starts_like_audio_is_accepted(tmp_path: Path) -> None:
    with (
        live_server(create_app(AudioLookingTextServer())) as url,
        running_studio(tmp_path, model_server("text", url)) as studio,
    ):
        job = studio.generation.submit("text", "text-to-text", prompt="песня")
        done = wait_for(studio, job.id, JobStatus.SUCCEEDED)
        output = studio.library.asset(done.outputs[0])
        assert output is not None
        assert (output.kind, output.mime) == ("text", "text/plain")


# Migrations


def test_migration_that_breaks_references_is_rolled_back(tmp_path: Path) -> None:
    database = Database(tmp_path / "studio.db")
    connection = database.connection
    connection.execute(
        "INSERT INTO assets (id, kind, blob_sha256, mime, size_bytes, origin, created_at) "
        "VALUES ('a1', 'image', 'x', 'image/png', 1, 'upload', '2026-01-01')"
    )
    connection.execute("INSERT INTO asset_tags VALUES ('a1', 'мох')")
    version = database.schema_version
    with pytest.raises(RuntimeError, match="breaks 1 references"):
        database._apply("9999_bad.sql", "DELETE FROM assets;", version + 1)
    assert database.schema_version == version
    assert connection.execute("SELECT count(*) FROM assets").fetchone()[0] == 1
    database.close()


def test_tags_cascade_after_the_rebuild(tmp_path: Path) -> None:
    database = Database(tmp_path / "studio.db")
    connection = database.connection
    connection.execute(
        "INSERT INTO assets (id, kind, blob_sha256, mime, size_bytes, origin, created_at) "
        "VALUES ('a1', 'text', 'x', 'text/plain', 1, 'upload', '2026-01-01')"
    )
    connection.execute("INSERT INTO asset_tags VALUES ('a1', 'мох')")
    connection.execute("DELETE FROM assets WHERE id = 'a1'")
    assert connection.execute("SELECT count(*) FROM asset_tags").fetchone()[0] == 0
    database.close()
