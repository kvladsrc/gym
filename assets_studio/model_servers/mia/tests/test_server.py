"""Declarations and the export command; no GPU, weights or source."""

from pathlib import Path

import pytest
import trimesh
from mia_model_server.server import TASK, MiaServer, Params, blender_command, describe

from model_server_sdk import create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(MiaServer())
    assert TASK.task == "3d-to-rig"
    assert TASK.output_mime == ("model/x-fbx",)
    assert [spec.role for spec in TASK.inputs] == ["mesh"]


@pytest.mark.parametrize(
    ("params", "flags"),
    [
        (Params(), ["--reset_to_rest"]),
        (Params(rest_pose="input"), []),
        (Params(fingers=False), ["--reset_to_rest", "--remove_fingers"]),
    ],
)
def test_parameters_become_export_flags(params: Params, flags: list[str]) -> None:
    command = blender_command(params, Path("/w/rig.npz"), Path("/w/out.fbx"), Path("/s/bones.fbx"))
    assert command[1].endswith("mia_model_server/blender_export.py")
    assert command[2:8] == [
        "--input_path",
        "/w/rig.npz",
        "--output_path",
        "/w/out.fbx",
        "--template_path",
        "/s/bones.fbx",
    ]
    assert command[8:] == flags


def test_metadata_names_the_skeleton() -> None:
    assert describe(52, Params()) == {
        "skeleton": "mixamo",
        "bones": 52,
        "fingers": True,
        "rest_pose": "t-pose",
    }


def _glb_with_colours(colours: list[list[int]]) -> bytes:
    """A triangle with 16-bit normalised vertex colours, as Blender writes them."""
    import json
    import struct

    import numpy as np

    positions = np.array([[0, 0, 0], [1, 0, 0], [0, 1, 0]], dtype=np.float32).tobytes()
    rgba = np.array(colours, dtype=np.uint16).tobytes()
    binary = positions + rgba
    document = {
        "asset": {"version": "2.0"},
        "buffers": [{"byteLength": len(binary)}],
        "bufferViews": [
            {"buffer": 0, "byteOffset": 0, "byteLength": len(positions)},
            {"buffer": 0, "byteOffset": len(positions), "byteLength": len(rgba)},
        ],
        "accessors": [
            {
                "bufferView": 0,
                "componentType": 5126,
                "count": 3,
                "type": "VEC3",
                "min": [0, 0, 0],
                "max": [1, 1, 0],
            },
            {
                "bufferView": 1,
                "componentType": 5123,
                "normalized": True,
                "count": 3,
                "type": "VEC4",
            },
        ],
        "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "COLOR_0": 1}}]}],
        "nodes": [{"mesh": 0}],
        "scenes": [{"nodes": [0]}],
    }
    text = json.dumps(document).encode()
    text += b" " * (-len(text) % 4)
    binary += b"\0" * (-len(binary) % 4)
    total = 12 + 8 + len(text) + 8 + len(binary)
    return (
        struct.pack("<4sII", b"glTF", 2, total)
        + struct.pack("<II", len(text), 0x4E4F534A)
        + text
        + struct.pack("<II", len(binary), 0x004E4942)
        + binary
    )


def test_16_bit_vertex_colours_become_8_bit_for_trimesh(tmp_path: Path) -> None:
    from mia_model_server.glb import byte_colours

    data = _glb_with_colours(
        [[65535, 0, 32896, 65535], [0, 65535, 0, 65535], [13107, 26214, 39321, 65535]]
    )
    converted = byte_colours(data)
    path = tmp_path / "mesh.glb"
    path.write_bytes(converted)
    mesh = trimesh.load(path, force="mesh")
    assert mesh.visual.vertex_colors.tolist() == [
        [255, 0, 128, 255],
        [0, 255, 0, 255],
        [51, 102, 153, 255],
    ]
    assert byte_colours(converted) == converted  # already 8-bit: unchanged
