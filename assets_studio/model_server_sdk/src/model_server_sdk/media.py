"""Minimal stdlib encoders and signature checks for PNG, WAV, GLB, text and video.

Used by the fake server and by the contract check to produce sample inputs
and to verify that returned files are what their MIME type claims.
"""

import io
import json
import math
import struct
import wave
import zlib
from collections.abc import Callable, Sequence
from importlib import resources

RGB = tuple[int, int, int]

# A valid canonical video (ADR-003): 32×32, 4 frames at 8 fps, H.264 High,
# yuv420p, BT.709, +faststart; made once with ``video.encode_mp4`` and kept
# as a package file, as the SDK has no encoder of its own.
SAMPLE_MP4 = resources.files(__package__).joinpath("data/sample.mp4").read_bytes()


def encode_png(width: int, height: int, pixel: Callable[[int, int], RGB]) -> bytes:
    """Encode an 8-bit RGB PNG; ``pixel(x, y)`` returns the colour of a pixel."""
    rows = bytearray()
    for y in range(height):
        rows.append(0)  # filter: none
        for x in range(width):
            rows.extend(pixel(x, y))

    def chunk(kind: bytes, payload: bytes) -> bytes:
        body = kind + payload
        return struct.pack(">I", len(payload)) + body + struct.pack(">I", zlib.crc32(body))

    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(bytes(rows)))
        + chunk(b"IEND", b"")
    )


def encode_wav(samples: Sequence[float], sample_rate: int = 22050) -> bytes:
    """Encode mono 16-bit PCM; samples are clipped to [-1, 1]."""
    frames = b"".join(
        struct.pack("<h", round(max(-1.0, min(1.0, sample)) * 32767)) for sample in samples
    )
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(sample_rate)
        writer.writeframes(frames)
    return buffer.getvalue()


def tone(frequency: float, duration_s: float, sample_rate: int = 22050) -> list[float]:
    count = max(1, round(duration_s * sample_rate))
    return [0.5 * math.sin(2 * math.pi * frequency * i / sample_rate) for i in range(count)]


def encode_glb(
    positions: Sequence[tuple[float, float, float]],
    triangles: Sequence[tuple[int, int, int]],
    color: tuple[float, float, float, float] = (0.8, 0.8, 0.8, 1.0),
) -> bytes:
    """Encode a single-mesh glTF 2.0 binary with one flat-coloured material."""
    position_bytes = b"".join(struct.pack("<3f", *point) for point in positions)
    index_bytes = b"".join(struct.pack("<3H", *triangle) for triangle in triangles)
    index_offset = _pad(len(position_bytes))
    binary = position_bytes.ljust(index_offset, b"\0") + index_bytes
    binary = binary.ljust(_pad(len(binary)), b"\0")
    document = {
        "asset": {"version": "2.0", "generator": "model_server_sdk.media"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"mesh": 0}],
        "meshes": [{"primitives": [{"attributes": {"POSITION": 0}, "indices": 1, "material": 0}]}],
        "materials": [{"pbrMetallicRoughness": {"baseColorFactor": list(color)}}],
        "buffers": [{"byteLength": len(binary)}],
        "bufferViews": [
            {"buffer": 0, "byteOffset": 0, "byteLength": len(position_bytes), "target": 34962},
            {
                "buffer": 0,
                "byteOffset": index_offset,
                "byteLength": len(index_bytes),
                "target": 34963,
            },
        ],
        "accessors": [
            {
                "bufferView": 0,
                "componentType": 5126,
                "count": len(positions),
                "type": "VEC3",
                "min": [min(point[axis] for point in positions) for axis in range(3)],
                "max": [max(point[axis] for point in positions) for axis in range(3)],
            },
            {
                "bufferView": 1,
                "componentType": 5123,
                "count": 3 * len(triangles),
                "type": "SCALAR",
            },
        ],
    }
    json_bytes = json.dumps(document, separators=(",", ":")).encode()
    json_bytes = json_bytes.ljust(_pad(len(json_bytes)), b" ")
    length = 12 + 8 + len(json_bytes) + 8 + len(binary)
    return (
        struct.pack("<4sII", b"glTF", 2, length)
        + struct.pack("<I4s", len(json_bytes), b"JSON")
        + json_bytes
        + struct.pack("<I4s", len(binary), b"BIN\0")
        + binary
    )


def tetrahedron(
    scale: float = 0.5,
) -> tuple[list[tuple[float, float, float]], list[tuple[int, int, int]]]:
    points = [
        (scale, scale, scale),
        (scale, -scale, -scale),
        (-scale, scale, -scale),
        (-scale, -scale, scale),
    ]
    return points, [(0, 1, 2), (0, 3, 1), (0, 2, 3), (1, 3, 2)]


def looks_like(mime: str, data: bytes) -> bool | None:
    """Check a file signature; ``None`` when the MIME type is not known here."""
    check = _SIGNATURES.get(mime)
    return None if check is None else check(data)


def _is_png(data: bytes) -> bool:
    return data.startswith(b"\x89PNG\r\n\x1a\n")


def _is_wav(data: bytes) -> bool:
    return len(data) >= 12 and data[:4] == b"RIFF" and data[8:12] == b"WAVE"


def _is_glb(data: bytes) -> bool:
    if len(data) < 20:
        return False
    magic, version, length = struct.unpack_from("<4sII", data)
    return magic == b"glTF" and version == 2 and length == len(data)


# Binary FBX (what Blender, Mixamo and Unity write); ASCII FBX is not accepted.
FBX_MAGIC = b"Kaydara FBX Binary  \x00\x1a\x00"


def _is_fbx(data: bytes) -> bool:
    return len(data) > len(FBX_MAGIC) + 4 and data.startswith(FBX_MAGIC)


def _is_text(data: bytes) -> bool:
    """Non-empty UTF-8 without control characters other than tab and line
    breaks (C0, DEL, C1); a BOM is allowed here and removed on import. A WAV
    header, with its NUL bytes, would pass a plain UTF-8 check."""
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError:
        return False
    return bool(text) and not any(
        (ord(char) < 32 and char not in "\t\n\r") or 0x7F <= ord(char) <= 0x9F for char in text
    )


# Major brands of MP4 video. Others share the box structure but are not
# video/mp4: M4A/M4B (audio), heic/avif (images), 3gp, qt (QuickTime).
_MP4_BRANDS = {
    b"isom", b"iso2", b"iso3", b"iso4", b"iso5", b"iso6",
    b"mp41", b"mp42", b"avc1", b"M4V ", b"M4VP", b"dash", b"mmp4",
}  # fmt: skip


def _is_mp4(data: bytes) -> bool:
    return len(data) >= 12 and data[4:8] == b"ftyp" and data[8:12] in _MP4_BRANDS


def _is_webm(data: bytes) -> bool:
    # An EBML header whose DocType is "webm" (Matroska, .mkv, says "matroska").
    return data.startswith(b"\x1a\x45\xdf\xa3") and b"\x42\x82\x84webm" in data[:64]


def _is_ogg(data: bytes) -> bool:
    # "OggS", stream structure version 0, a first page (beginning-of-stream flag).
    return len(data) >= 6 and data.startswith(b"OggS") and data[4] == 0 and data[5] & 0x02 != 0


def _is_flac(data: bytes) -> bool:
    # "fLaC" followed by the mandatory STREAMINFO block (type 0, 34 bytes).
    return (
        len(data) >= 8
        and data.startswith(b"fLaC")
        and data[4] & 0x7F == 0
        and data[5:8] == b"\x00\x00\x22"
    )


def mp4_problems(data: bytes) -> list[str]:
    """Where an MP4 departs from the canonical layout that a box walk can
    see: its top-level boxes, with ``moov`` (the index) before ``mdat`` so a
    browser can start playing before the whole file arrives, and an H.264
    (``avc1``) track. Codec profile and pixel format need a decoder."""
    boxes: list[bytes] = []
    moov = b""
    offset = 0
    while offset + 8 <= len(data):
        size, kind = struct.unpack_from(">I4s", data, offset)
        if size == 1 and offset + 16 <= len(data):
            size = struct.unpack_from(">Q", data, offset + 8)[0]
        elif size == 0:
            size = len(data) - offset
        if size < 8:
            return ["broken box structure"]
        boxes.append(kind)
        if kind == b"moov":
            moov = data[offset : offset + size]
        offset += size
    problems: list[str] = []
    if offset != len(data):
        problems.append("the last box is truncated")
    if b"moov" not in boxes:
        problems.append("no moov box")
    elif b"mdat" in boxes and boxes.index(b"mdat") < boxes.index(b"moov"):
        problems.append("moov after mdat (not +faststart)")
    # The sample description lives in the index; media data could contain
    # these bytes by chance.
    if moov and b"avc1" not in moov:
        problems.append("no H.264 (avc1) track")
    return problems


def _is_mp3(data: bytes) -> bool:
    if data.startswith(b"ID3"):
        # ID3v2.2-2.4: version, revision 0, then a size of four 7-bit bytes.
        return (
            len(data) >= 10
            and data[3] in (2, 3, 4)
            and data[4] == 0
            and all(byte < 0x80 for byte in data[6:10])
        )
    # An MPEG audio frame header: sync word, a valid layer and bitrate.
    return (
        len(data) >= 4
        and data[0] == 0xFF
        and data[1] & 0xE0 == 0xE0
        and data[1] & 0x06 != 0
        and data[2] & 0xF0 not in (0x00, 0xF0)
    )


_SIGNATURES: dict[str, Callable[[bytes], bool]] = {
    "image/png": _is_png,
    "image/jpeg": lambda data: data.startswith(b"\xff\xd8\xff"),
    "image/webp": lambda data: len(data) >= 12 and data[:4] == b"RIFF" and data[8:12] == b"WEBP",
    "audio/wav": _is_wav,
    "audio/x-wav": _is_wav,
    "audio/flac": _is_flac,
    "audio/ogg": _is_ogg,
    "audio/mpeg": _is_mp3,
    "model/gltf-binary": _is_glb,
    "model/x-fbx": _is_fbx,
    "video/mp4": _is_mp4,
    "video/webm": _is_webm,
    "text/plain": _is_text,
}


def _pad(size: int) -> int:
    return (size + 3) // 4 * 4
