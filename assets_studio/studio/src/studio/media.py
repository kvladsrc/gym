"""File formats: detection by content and conversion to canonical formats.

What is stored (ADR-003): images and audio as uploaded, in formats the
browser plays, converted to the canonical format when sent to a model server;
text normalised and video converted to the canonical MP4 on import, so the
library holds only video the browser can play. Images are converted with
Pillow; audio and video with the system ``ffmpeg``: without it those
conversions fail with a clear message.
"""

import io
import json
import shutil
import subprocess
import tempfile
from pathlib import Path

from model_server_sdk.video import canonical_h264_options
from PIL import Image, UnidentifiedImageError

from model_server_sdk import media as signatures
from studio.domain import CANONICAL_MIME, MIME_KINDS, AssetKind


class UnsupportedMedia(ValueError):
    pass


FFMPEG_TIMEOUT_S = 600

# ffmpeg is told the container rather than left to guess it from the content:
# probing could pick a demuxer that reads other files (e.g. a playlist).
_DEMUXERS = {
    "audio/wav": "wav",
    "audio/flac": "flac",
    "audio/ogg": "ogg",
    "audio/mpeg": "mp3",
    "video/mp4": "mov",
    "video/webm": "matroska",
}
# The first video and audio streams; the video settings are those model
# servers use (canonical_h264_options: BT.709, even sides, …).
_VIDEO_STREAMS = ["-map", "0:v:0", "-map", "0:a:0?", "-sn", "-dn"]
# Above SD height, untagged video is BT.709 by convention: players show it so.
_SD_HEIGHT = 576
# H.264 profiles every browser decodes (no High 10, 4:2:2 or 4:4:4).
_PLAYABLE_PROFILES = {"Constrained Baseline", "Baseline", "Main", "High"}


def detect_mime(data: bytes) -> str:
    """The stored MIME type of ``data``, judged by content, not by a claimed type."""
    for mime in MIME_KINDS:
        if signatures.looks_like(mime, data):
            return mime
    raise UnsupportedMedia(f"unsupported file format; supported: {', '.join(sorted(MIME_KINDS))}")


def for_storage(data: bytes, mime: str) -> tuple[bytes, str]:
    """What the library keeps of an imported file: text normalised, video in
    the canonical MP4; everything else as it is."""
    kind = MIME_KINDS[mime]
    if kind is AssetKind.TEXT:
        return normalize_text(data), mime
    if kind is AssetKind.VIDEO:
        streams = _probe(data, mime)  # also rejects files without video, clearly
        if mime == "video/mp4" and not video_problems(data, streams):
            return data, mime
        options = [*_VIDEO_STREAMS, *canonical_h264_options(source_matrix=_source_matrix(streams))]
        return _ffmpeg(data, mime, ".mp4", [*options, "-c:a", "aac"]), "video/mp4"
    return data, mime


def to_canonical(data: bytes, mime: str) -> tuple[bytes, str]:
    """Convert to the canonical format of the file's kind (every server accepts it)."""
    kind = MIME_KINDS[mime]
    canonical = CANONICAL_MIME[kind]
    if kind is AssetKind.TEXT:
        return normalize_text(data), canonical
    if kind is AssetKind.VIDEO:
        # Stored video was made canonical on import; only its layout is
        # re-checked (no ffprobe on every send).
        if mime == canonical and not signatures.mp4_problems(data):
            return data, mime
        return for_storage(data, mime)
    if mime == canonical:
        return data, mime
    if kind is AssetKind.IMAGE:
        try:
            image = Image.open(io.BytesIO(data))
            image.load()
        except (UnidentifiedImageError, OSError) as error:
            raise UnsupportedMedia(f"cannot decode {mime}: {error}") from error
        if image.mode not in ("RGB", "RGBA"):
            image = image.convert("RGBA" if "A" in image.getbands() else "RGB")
        buffer = io.BytesIO()
        image.save(buffer, format="PNG")
        return buffer.getvalue(), canonical
    if kind is AssetKind.AUDIO:
        # PCM 16 bit; the source's sample rate and channels are kept.
        options = ["-map", "0:a:0", "-vn", "-sn", "-dn", "-c:a", "pcm_s16le"]
        return _ffmpeg(data, mime, ".wav", options), canonical
    raise UnsupportedMedia(f"no conversion from {mime} to {canonical}")


def normalize_text(data: bytes) -> bytes:
    """Canonical text: UTF-8 without a BOM, lines ending in ``\\n``."""
    text = data.decode("utf-8-sig")
    return text.replace("\r\n", "\n").replace("\r", "\n").encode()


def video_problems(data: bytes, streams: list[dict[str, object]] | None = None) -> list[str]:
    """How an MP4 departs from the canonical video (ADR-003); empty if it
    conforms. The layout is checked here; codecs, profile and pixel format
    with ``ffprobe`` (without it, a file is taken as not conforming)."""
    problems = signatures.mp4_problems(data)
    if problems:
        return problems
    if streams is None:
        streams = _probe(data, "video/mp4")
    video = next(stream for stream in streams if stream.get("codec_type") == "video")
    if video.get("codec_name") != "h264":
        problems.append(f"codec {video.get('codec_name')}, not H.264")
    if video.get("profile") not in _PLAYABLE_PROFILES:
        problems.append(f"H.264 profile {video.get('profile')}")
    if video.get("pix_fmt") != "yuv420p":
        problems.append(f"pixel format {video.get('pix_fmt')}, not yuv420p")
    if video.get("color_space") != "bt709":
        # Untagged or BT.601: browsers would show shifted colours.
        problems.append(f"colour space {video.get('color_space', 'unknown')}, not BT.709")
    for stream in streams:
        if stream.get("codec_type") == "audio" and stream.get("codec_name") != "aac":
            problems.append(f"audio {stream.get('codec_name')}, not AAC")
    return problems


def _source_matrix(streams: list[dict[str, object]]) -> str | None:
    """The YUV matrix to assume for an untagged source, as players do: BT.709
    for HD, BT.601 for SD. Tagged sources are converted by their tags."""
    video = next(stream for stream in streams if stream.get("codec_type") == "video")
    if video.get("color_space") not in (None, "unknown"):
        return None
    height = video.get("height")
    return "bt709" if isinstance(height, int) and height > _SD_HEIGHT else "bt601"


def _probe(data: bytes, mime: str) -> list[dict[str, object]]:
    """The streams of a video file, by ``ffprobe``; it must have a video stream."""
    ffprobe = shutil.which("ffprobe")
    if ffprobe is None:
        raise UnsupportedMedia(f"checking {mime} needs ffprobe (ffmpeg), which is not installed")
    with tempfile.TemporaryDirectory(prefix="studio-media-") as directory:
        source = Path(directory) / "source"
        source.write_bytes(data)
        command = [ffprobe, "-v", "error", "-protocol_whitelist", "file", "-f", _DEMUXERS[mime]]
        command += ["-show_streams", "-of", "json", str(source)]
        try:
            result = subprocess.run(  # noqa: S603 -- fixed argv, no shell; paths are ours
                command, capture_output=True, timeout=60, check=False
            )
        except subprocess.TimeoutExpired as error:
            raise UnsupportedMedia("probing the video took over 60 s") from error
    if result.returncode != 0:
        raise UnsupportedMedia(f"cannot read the {mime} file")
    report: dict[str, list[dict[str, object]]] = json.loads(result.stdout or b"{}")
    streams = report.get("streams") or []
    if not any(stream.get("codec_type") == "video" for stream in streams):
        raise UnsupportedMedia("the file has no video stream")
    return streams


def _ffmpeg(data: bytes, mime: str, suffix: str, options: list[str]) -> bytes:
    """Convert with ffmpeg through temporary files (MP4 and WAV need seekable output)."""
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        raise UnsupportedMedia(f"converting {mime} needs ffmpeg, which is not installed")
    with tempfile.TemporaryDirectory(prefix="studio-media-") as directory:
        source, target = Path(directory) / "source", Path(directory) / f"target{suffix}"
        source.write_bytes(data)
        command = [ffmpeg, "-nostdin", "-loglevel", "error", "-protocol_whitelist", "file"]
        command += ["-f", _DEMUXERS[mime], "-i", str(source), *options]
        try:
            result = subprocess.run(  # noqa: S603 -- fixed argv, no shell; paths are ours
                [*command, "-map_metadata", "-1", str(target)],
                capture_output=True,
                timeout=FFMPEG_TIMEOUT_S,
                check=False,
            )
        except subprocess.TimeoutExpired as error:
            raise UnsupportedMedia(f"converting {mime} took over {FFMPEG_TIMEOUT_S} s") from error
        if result.returncode != 0 or not target.is_file():
            message = result.stderr.decode(errors="replace").strip().splitlines()
            reason = message[-1] if message else "ffmpeg failed"
            raise UnsupportedMedia(f"cannot convert {mime}: {reason}")
        return target.read_bytes()
