"""Vertex colours of a GLB as 8-bit, the one type trimesh reads.

glTF allows COLOR_n as float or normalised 8- or 16-bit integers; Blender
writes 16-bit. trimesh, which Make-It-Animatable loads meshes with, does not
decode 16-bit colours: 4.x drops them, 5.x keeps the low byte (noise). Every
COLOR_n stored in another type is converted to normalised UNSIGNED_BYTE in
a new buffer view at the end of the binary chunk; the rest of the file is
kept as it is.
"""

import json
import struct

import numpy as np

_MAGIC = b"glTF"
_JSON, _BIN = 0x4E4F534A, 0x004E4942
_COMPONENTS = {5121: np.uint8, 5123: np.uint16, 5126: np.float32}
_WIDTH = {"VEC3": 3, "VEC4": 4}


def _pad(data: bytes, fill: bytes) -> bytes:
    return data + fill * (-len(data) % 4)


def byte_colours(data: bytes) -> bytes:
    """The GLB with 8-bit vertex colours; unchanged if they already are (or there are none)."""
    magic, version, _ = struct.unpack_from("<4sII", data)
    if magic != _MAGIC or version != 2:
        raise ValueError("not a glTF 2.0 binary")
    json_length, json_type = struct.unpack_from("<II", data, 12)
    if json_type != _JSON:
        raise ValueError("the first GLB chunk is not JSON")
    document = json.loads(data[20 : 20 + json_length])
    offset = 20 + json_length
    binary = b""
    if offset < len(data):
        bin_length, bin_type = struct.unpack_from("<II", data, offset)
        if bin_type == _BIN:
            binary = data[offset + 8 : offset + 8 + bin_length]
    accessors = document.get("accessors", [])
    colours = {
        index
        for mesh in document.get("meshes", [])
        for primitive in mesh.get("primitives", [])
        for name, index in primitive.get("attributes", {}).items()
        if name.startswith("COLOR_")
    }
    converted = bytearray(binary)
    changed = False
    for index in sorted(colours):
        accessor = accessors[index]
        kind = accessor.get("componentType")
        if kind == 5121 or kind not in _COMPONENTS or "bufferView" not in accessor:
            continue
        view = document["bufferViews"][accessor["bufferView"]]
        if view.get("buffer", 0) != 0:
            continue
        width = _WIDTH[accessor["type"]]
        dtype = np.dtype(_COMPONENTS[kind])
        stride = view.get("byteStride") or width * dtype.itemsize
        start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        count = accessor["count"]
        rows = np.frombuffer(
            binary,
            dtype=np.uint8,
            count=stride * (count - 1) + width * dtype.itemsize,
            offset=start,
        )
        values = (
            np.lib.stride_tricks.as_strided(
                rows.view(np.uint8), shape=(count, width * dtype.itemsize), strides=(stride, 1)
            )
            .copy()
            .view(dtype)
            .reshape(count, width)
        )
        scale = 255.0 if kind == 5126 else 255.0 / 65535.0
        as_bytes = np.clip(np.round(values.astype(np.float64) * scale), 0, 255).astype(np.uint8)
        while len(converted) % 4:
            converted.append(0)
        document["bufferViews"].append(
            {"buffer": 0, "byteOffset": len(converted), "byteLength": as_bytes.nbytes}
        )
        converted += as_bytes.tobytes()
        accessor.update(
            bufferView=len(document["bufferViews"]) - 1,
            byteOffset=0,
            componentType=5121,
            normalized=True,
        )
        accessor.pop("min", None)
        accessor.pop("max", None)
        changed = True
    if not changed:
        return data
    document["buffers"][0]["byteLength"] = len(converted)
    json_chunk = _pad(json.dumps(document, separators=(",", ":")).encode(), b" ")
    bin_chunk = _pad(bytes(converted), b"\0")
    total = 12 + 8 + len(json_chunk) + 8 + len(bin_chunk)
    return (
        struct.pack("<4sII", _MAGIC, 2, total)
        + struct.pack("<II", len(json_chunk), _JSON)
        + json_chunk
        + struct.pack("<II", len(bin_chunk), _BIN)
        + bin_chunk
    )
