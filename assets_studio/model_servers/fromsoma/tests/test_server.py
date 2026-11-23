"""Declarations, parameters, the GLB writer, the texture rasteriser and the picture framing; no
weights, data or GPU."""

import io
import json
import math
import struct
from pathlib import Path

import numpy as np
import pytest
import torch
import trimesh
from fromsoma_model_server.bake import pad, rasterize
from fromsoma_model_server.glb import Skeleton, Surface, build_glb
from fromsoma_model_server.reader import Framing, PictureError, condition, crop, slider_values
from fromsoma_model_server.server import BUILD, ORIGIN, TASK, FromSomaServer, Params, missing_files
from PIL import Image
from pydantic import ValidationError

from model_server_sdk import create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(FromSomaServer())
    assert TASK.task == "image-to-3d"
    assert [spec.role for spec in TASK.inputs] == ["image", "profile"]
    assert TASK.output_mime == ("model/gltf-binary",)


def test_parameters_are_annys_with_the_age_in_years() -> None:
    defaults = Params().model_dump()
    assert (defaults.pop("sex"), defaults.pop("age")) == ("female", 30.0)
    assert defaults.pop("texture") == "pictures"
    assert defaults == dict.fromkeys((*BUILD, *ORIGIN, "cupsize", "firmness"), 0.5)
    assert Params(african=0.0, asian=0.25, caucasian=0.75).origin() == {
        "african": 0.0,
        "asian": 0.25,
        "caucasian": 0.75,
    }
    assert sum(Params().origin().values()) == pytest.approx(1.0)
    with pytest.raises(ValidationError):
        Params(sex="other")
    with pytest.raises(ValidationError):
        Params(age=2.0)
    with pytest.raises(ValidationError):
        Params(height=1.5)
    with pytest.raises(ValidationError):
        Params(african=0.0, asian=0.0, caucasian=0.0)
    with pytest.raises(ValidationError):
        Params(steps=10)
    with pytest.raises(ValidationError):
        Params(texture="photo")


def test_the_condition_holds_the_given_values_behind_their_masks() -> None:
    config = {
        "age_bins": [5.0, 10.0, 15.0, 20.0, 30.0, 45.0, 60.0, 75.0],
        "free": BUILD,
        "origin": ORIGIN,
    }
    build = {"muscle": 0.2, "weight": 0.4, "height": 0.6, "proportions": 0.8}
    origin = {"african": 0.0, "asian": 1.0, "caucasian": 0.0}
    row = condition(config, True, 12.0, 0.466, 3, build, origin)
    assert row.shape == (3, 19)
    assert row[0, :2].tolist() == pytest.approx([1.0, 0.466])
    assert row[0, 3].item() == pytest.approx(math.exp(-0.5 * (2 / 6) ** 2))  # the 10-year bin
    assert row[0, 10:].tolist() == pytest.approx([1.0, 0.2, 0.4, 0.6, 0.8, 1.0, 0.0, 1.0, 0.0])
    unknown = condition(config, False, 30.0, 0.79, 1)
    assert unknown[0, 0].item() == 0.0
    assert unknown[0, 10:].abs().sum().item() == 0.0


def test_missing_files_are_listed(tmp_path: Path) -> None:
    assert "model.pt" in missing_files(tmp_path)
    for name in missing_files(tmp_path):
        (tmp_path / name).parent.mkdir(parents=True, exist_ok=True)
        (tmp_path / name).touch()
    assert missing_files(tmp_path) == []


def _rotation(axis: str, degrees: float) -> np.ndarray:
    c, s = math.cos(math.radians(degrees)), math.sin(math.radians(degrees))
    i, j = {"x": (1, 2), "y": (2, 0), "z": (0, 1)}[axis]
    m = np.eye(4)
    m[i, i], m[i, j], m[j, i], m[j, j] = c, -s, s, c
    return m


def _translation(x: float, y: float, z: float) -> np.ndarray:
    m = np.eye(4)
    m[:3, 3] = (x, y, z)
    return m


def _from_node(node: dict) -> np.ndarray:
    """A node's TR matrix, by Rodrigues' formula (independent of glb.matrix)."""
    x, y, z, w = node.get("rotation", (0, 0, 0, 1))
    angle = 2 * math.acos(max(-1.0, min(1.0, w)))
    m = np.eye(4)
    if angle > 1e-9:
        k = np.array([x, y, z]) / math.sin(angle / 2)
        K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
        m[:3, :3] = np.eye(3) + math.sin(angle) * K + (1 - math.cos(angle)) * K @ K
    m[:3, 3] = node.get("translation", (0, 0, 0))
    return m


def _read_glb(data: bytes) -> tuple[dict, bytes]:
    magic, version, total = struct.unpack_from("<4sII", data)
    assert (magic, version, total) == (b"glTF", 2, len(data))
    size = struct.unpack_from("<I", data, 12)[0]
    return json.loads(data[20 : 20 + size]), data[20 + size + 8 :]


def _accessor(document: dict, body: bytes, index: int) -> np.ndarray:
    entry = document["accessors"][index]
    view = document["bufferViews"][entry["bufferView"]]
    dtype = {5121: np.uint8, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}[
        entry["componentType"]
    ]
    width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[entry["type"]]
    return np.frombuffer(body, dtype, entry["count"] * width, view["byteOffset"]).reshape(
        entry["count"], width
    )


def test_the_glb_joints_compose_to_the_bind_pose() -> None:
    world = np.stack(
        [
            _translation(0, 1, 0),
            _translation(0, 1, 0) @ _translation(0.1, 0.2, 0) @ _rotation("z", 30),
            _translation(0, 1, 0)
            @ _translation(0.1, 0.2, 0)
            @ _rotation("z", 30)
            @ _translation(0, 0.3, 0)
            @ _rotation("x", -45),
        ]
    )
    skeleton = Skeleton(["Root", "Spine", "Head"], [-1, 0, 1], world)
    square = np.array([[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]], np.float32)
    surface = Surface(
        name="body",
        positions=square,
        normals=np.tile(np.float32([0, 0, 1]), (4, 1)),
        uv=square[:, :2],
        joints=np.array([[0, 1, 0, 0], [1, 2, 0, 0], [2, 0, 0, 0], [1, 0, 0, 0]]),
        weights=np.float32([[0.5, 0.5, 0, 0], [0.75, 0.25, 0, 0], [1, 0, 0, 0], [1, 0, 0, 0]]),
        triangles=np.array([[0, 1, 2], [0, 2, 3]]),
        roughness=0.5,
    )
    texture = io.BytesIO()
    Image.new("RGB", (4, 4), (200, 150, 120)).save(texture, format="PNG")
    data = build_glb([surface], skeleton, texture.getvalue())

    document, body = _read_glb(data)
    nodes = document["nodes"]
    assert [node["name"] for node in nodes] == ["Root", "Spine", "Head", "body"]
    assert nodes[0]["children"] == [1]
    assert nodes[1]["children"] == [2]
    assert document["skins"][0]["joints"] == [0, 1, 2]
    composed = [_from_node(nodes[0])]
    for j in (1, 2):
        composed.append(composed[j - 1] @ _from_node(nodes[j]))
    np.testing.assert_allclose(np.stack(composed), world, atol=1e-6)
    inverse = _accessor(document, body, document["skins"][0]["inverseBindMatrices"]).reshape(
        3, 4, 4
    )
    for j in range(3):
        np.testing.assert_allclose(composed[j] @ inverse[j].T, np.eye(4), atol=1e-5)  # column-major
    primitive = document["meshes"][0]["primitives"][0]
    np.testing.assert_allclose(
        _accessor(document, body, primitive["attributes"]["WEIGHTS_0"]).sum(1), 1
    )
    assert document["materials"][0]["pbrMetallicRoughness"]["roughnessFactor"] == 0.5

    scene = trimesh.load(io.BytesIO(data), file_type="glb")
    assert isinstance(scene, trimesh.Scene)
    assert sum(len(g.faces) for g in scene.geometry.values()) == 2


def test_the_rasteriser_covers_triangles_and_the_lower_index_wins() -> None:
    xy = torch.tensor([[0.0, 0.0], [8.0, 0.0], [0.0, 8.0], [8.0, 8.0]])
    tris = torch.tensor([[0, 1, 2], [1, 3, 2], [0, 1, 2]])  # the third repeats the first
    tri, bary = rasterize(xy, tris, 8, 8, max_pairs=10)  # one triangle per group
    assert (tri >= 0).all()
    assert set(tri.unique().tolist()) == {0, 1}
    assert torch.allclose(bary.sum(-1), torch.ones(8, 8))
    centres = (bary[..., None] * xy[tris[tri]]).sum(-2)
    ys, xs = torch.meshgrid(torch.arange(8.0), torch.arange(8.0), indexing="ij")
    assert torch.allclose(centres, torch.stack([xs + 0.5, ys + 0.5], -1), atol=1e-5)


def test_padding_grows_islands_by_rings() -> None:
    image, filled = torch.zeros(3, 9, 9), torch.zeros(9, 9, dtype=torch.bool)
    image[:, 4, 4], filled[4, 4] = torch.tensor([0.2, 0.4, 0.6]), True
    grown = pad(image, filled, texels=2)
    assert torch.allclose(grown[:, 2, 2], image[:, 4, 4])
    assert (grown[:, 0, 0] == 0).all()


BACKDROP, SKIN = (0.66, 0.66, 0.66), (0.85, 0.7, 0.6)


def _head(top: int = 100, height: int = 600, width: int = 500) -> np.ndarray:
    """A head (an ellipse, its crown at row `top`, middle at column 250) with shoulders that
    reach the lower edges, on a plain backdrop."""
    rgb = np.empty((height, width, 3), np.float32)
    rgb[:] = BACKDROP
    yy, xx = np.mgrid[:height, :width]
    rgb[((xx - 250) / 120) ** 2 + ((yy - top - 180) / 180) ** 2 <= 1] = SKIN
    rgb[yy >= height - 120] = SKIN
    return rgb


def test_framing_finds_the_crown_and_the_middle() -> None:
    framing = Framing.of(_head(), eye_y=260.0)
    assert (framing.crown, framing.h, framing.middle) == (100.0, 320.0, 250.0)
    picture = crop(_head(), framing, 32)
    assert picture.shape == (3, 32, 32)
    assert torch.allclose(
        picture[:, 0, 16], torch.tensor(BACKDROP), atol=1e-3
    )  # backdrop above the crown
    assert torch.allclose(picture[:, 12, 16], torch.tensor(SKIN), atol=1e-3)


def test_unreadable_pictures_are_refused() -> None:
    with pytest.raises(PictureError, match="no head"):
        Framing.of(np.full((300, 300, 3), 0.66, np.float32), eye_y=150.0)
    with pytest.raises(PictureError, match="cut off"):
        Framing.of(_head(top=-20), eye_y=150.0)
    noise = np.random.default_rng(0).random((300, 300, 3), dtype=np.float32)
    with pytest.raises(PictureError, match="not plain"):
        Framing.of(noise, eye_y=150.0)


def test_sliders_map_onto_their_ranges() -> None:
    values = slider_values([0.5, 0.25], [("melanin", 0.01, 1.0, "log"), ("eu", 0.0, 2.0, "linear")])
    assert values == pytest.approx({"melanin": 0.1, "eu": 0.5})
