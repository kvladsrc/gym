# pyright: basic
"""A SOMA-like person and HairDeck-like hair for 3d-fit-hair, in Blender:

    blender -b -P hair_fixture.py -- HAIRCAP.json OUT_DIR

body.glb: a closed ellipsoid head in SOMA's vertex layout (the haircap indices
on its top and back, the other vertices spread over the rest, seam copies
filling the count), skinned to Head on a skeleton with SOMA's joint names.
hair.glb: HairDeck's frame and format: tube cards of four-vertex rings rooted
on a shell 8 % outside the head proxy and falling down over it, the root ring
stored with v = 1, and BRAID_CARDS hanging straight down behind the head;
no head_proxy node (HairDeck's default).
not_soma.glb: the same head without SOMA's vertex count.
"""

import json
import struct
import sys
from pathlib import Path

import bmesh  # pyright: ignore[reportMissingImports]
import numpy as np

HEAD_CENTRE = np.array([0.0, 1.55, -0.02])
HEAD_AXES = np.array([0.075, 0.10, 0.09])
JOINTS = {  # name: (parent, world position)
    "Root": (None, (0.0, 0.0, 0.0)),
    "Chest": ("Root", (0.0, 1.25, 0.0)),
    "Head": ("Chest", (0.0, 1.45, 0.0)),
    "HeadEnd": ("Head", (0.0, 1.66, 0.0)),
    "LeftEye": ("Head", (0.033, 1.55, 0.06)),
    "RightEye": ("Head", (-0.033, 1.55, 0.06)),
}
PROXY_CENTRE = np.array([0.0, 0.42202, 0.00028])
PROXY_AXES = np.array([0.051677, 0.063799, 0.063799])
RINGS = 8
BRAID_CARDS = 3


def glb(document: dict, binary: bytes) -> bytes:
    binary += b"\0" * (-len(binary) % 4)
    document["buffers"] = [{"byteLength": len(binary)}]
    text = json.dumps(document).encode()
    text += b" " * (-len(text) % 4)
    return (
        struct.pack("<4sII", b"glTF", 2, 28 + len(text) + len(binary))
        + struct.pack("<I4s", len(text), b"JSON")
        + text
        + struct.pack("<I4s", len(binary), b"BIN\0")
        + binary
    )


class Writer:
    def __init__(self) -> None:
        self.data = bytearray()
        self.views: list[dict] = []
        self.accessors: list[dict] = []

    def add(self, rows: np.ndarray, kind: str, component: int, *, normalized: bool = False) -> int:
        while len(self.data) % 4:
            self.data.append(0)
        self.views.append({"buffer": 0, "byteOffset": len(self.data), "byteLength": rows.nbytes})
        self.data += np.ascontiguousarray(rows).tobytes()
        accessor = {
            "bufferView": len(self.views) - 1,
            "componentType": component,
            "count": len(rows),
            "type": kind,
        }
        if normalized:
            accessor["normalized"] = True
        if kind == "VEC3" and component == 5126:
            accessor["min"] = rows.min(0).tolist()
            accessor["max"] = rows.max(0).tolist()
        self.accessors.append(accessor)
        return len(self.accessors) - 1


def sphere() -> tuple[np.ndarray, np.ndarray]:
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1.0)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    points = np.array([v.co[:] for v in bm.verts])
    triangles = np.array([[v.index for v in f.verts] for f in bm.faces])
    bm.free()
    return points, triangles


def body(haircap: list[int], count: int) -> bytes:
    unit, triangles = sphere()
    points = HEAD_CENTRE + unit * HEAD_AXES
    # SOMA's haircap: the top and back, not the face.
    scalp = np.flatnonzero((unit[:, 1] > -0.1) | ((unit[:, 2] < -0.3) & (unit[:, 1] > -0.6)))
    scalp = scalp[~((unit[scalp, 2] > 0.5) & (unit[scalp, 1] < 0.3))]
    slot = np.full(count, -1)
    slot[haircap] = scalp[np.linspace(0, len(scalp) - 1, len(haircap)).astype(int)]
    free = np.flatnonzero(slot < 0)
    rest = np.setdiff1d(np.arange(len(points)), slot[haircap])
    slot[free[: len(rest)]] = rest
    slot[free[len(rest) :]] = np.arange(len(free) - len(rest)) % len(points)  # seam copies
    representative = np.empty(len(points), int)
    representative[slot[::-1]] = np.arange(count)[::-1]
    return person(points[slot], representative[triangles])


def person(positions: np.ndarray, triangles: np.ndarray) -> bytes:
    names = list(JOINTS)
    w = Writer()
    nodes: list[dict] = []
    inverse = []
    for name, (parent, at) in JOINTS.items():
        offset = np.array(at) - (np.array(JOINTS[parent][1]) if parent else 0)
        node: dict = {"name": name, "translation": offset.tolist()}
        children = [names.index(n) for n, (p, _) in JOINTS.items() if p == name]
        if children:
            node["children"] = children
        nodes.append(node)
        m = np.eye(4)
        m[:3, 3] = -np.array(at)
        inverse.append(m.T.reshape(-1))
    head = names.index("Head")
    joints = np.zeros((len(positions), 4), np.uint8)
    joints[:, 0] = head
    weights = np.zeros((len(positions), 4), np.float32)
    weights[:, 0] = 1
    attributes = {
        "POSITION": w.add(positions.astype(np.float32), "VEC3", 5126),
        "JOINTS_0": w.add(joints, "VEC4", 5121),
        "WEIGHTS_0": w.add(weights, "VEC4", 5126),
    }
    indices = w.add(triangles.reshape(-1).astype(np.uint32), "SCALAR", 5125)
    skin = {
        "joints": list(range(len(names))),
        "inverseBindMatrices": w.add(np.array(inverse, np.float32), "MAT4", 5126),
    }
    nodes.append({"name": "body", "mesh": 0, "skin": 0})
    document = {
        "asset": {"version": "2.0"},
        "scene": 0,
        "scenes": [{"nodes": [0, len(nodes) - 1]}],
        "nodes": nodes,
        "skins": [skin],
        "meshes": [
            {"name": "body", "primitives": [{"attributes": attributes, "indices": indices}]}
        ],
        "accessors": w.accessors,
        "bufferViews": w.views,
    }
    return glb(document, bytes(w.data))


def card(root: np.ndarray, rings: int, fall: float) -> np.ndarray:
    """A card's centre line on HairDeck's proxy: from root (a unit direction)
    down over the head, slowly lifting off it; or, with fall, hanging straight
    down by fall metres per ring from a root behind the head."""
    if fall:
        start = PROXY_CENTRE + root * PROXY_AXES * 1.08
        return start - np.outer(np.arange(rings), [0.0, fall, 0.0])
    line = []
    for k in range(rings):
        d = root.copy()
        d[1] -= 0.12 * k
        d /= np.linalg.norm(d)
        line.append(PROXY_CENTRE + d * PROXY_AXES * (1.08 + 0.01 * k))
    return np.array(line)


def hair() -> bytes:
    lines = []
    for elevation in np.radians([25, 45, 65, 80]):
        for azimuth in np.radians(np.arange(0, 360, 20)):
            if abs(np.degrees(azimuth) % 360 - 180) > 150 and elevation < np.radians(60):
                continue  # the face
            root = np.array(
                [
                    np.cos(elevation) * np.sin(azimuth),
                    np.sin(elevation),
                    np.cos(elevation) * np.cos(azimuth),
                ]
            )
            lines.append(card(root, RINGS, 0.0))
    for x in np.linspace(-0.01, 0.01, BRAID_CARDS):
        root = np.array([x, -0.3, -0.95])
        lines.append(card(root / np.linalg.norm(root), RINGS * 2, 0.012))
    positions, uv, triangles = [], [], []
    for line in lines:
        first = len(positions)
        n = len(line)
        for k, p in enumerate(line):
            out = (p - PROXY_CENTRE) / np.linalg.norm(p - PROXY_CENTRE)
            side = np.cross([0.0, 1.0, 0.0], out)
            side = (
                side / np.linalg.norm(side)
                if np.linalg.norm(side) > 1e-6
                else np.array([1.0, 0, 0])
            )
            for corner, u in (
                (side * 0.006, 0.0),
                (out * 0.001, 0.5),
                (-side * 0.006, 1.0),
                (-out * 0.001, 0.5),
            ):
                positions.append(p + corner)
                uv.append((u, 1 - k / (n - 1)))
        for k in range(n - 1):
            for c in range(4):
                a, b = first + 4 * k + c, first + 4 * k + (c + 1) % 4
                triangles += [(a, b, b + 4), (a, b + 4, a + 4)]
    w = Writer()
    colour = np.tile(np.array([[30, 12, 5, 255]], np.uint8), (len(positions), 1))
    attributes = {
        "POSITION": w.add(np.array(positions, np.float32), "VEC3", 5126),
        "COLOR_0": w.add(colour, "VEC4", 5121, normalized=True),
        "TEXCOORD_0": w.add(np.array(uv, np.float32), "VEC2", 5126),
    }
    indices = w.add(np.array(triangles, np.uint32).reshape(-1), "SCALAR", 5125)
    document = {
        "asset": {"version": "2.0"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"name": "hair_cards", "mesh": 0}],
        "meshes": [
            {
                "name": "hair_cards",
                "primitives": [
                    {"attributes": attributes, "indices": indices, "material": 0, "mode": 4}
                ],
            }
        ],
        "materials": [
            {
                "name": "hair",
                "pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1], "roughnessFactor": 0.75},
                "doubleSided": True,
            }
        ],
        "accessors": w.accessors,
        "bufferViews": w.views,
    }
    return glb(document, bytes(w.data))


def main() -> None:
    haircap_path, out = sys.argv[sys.argv.index("--") + 1 :]
    layout = json.loads(Path(haircap_path).read_text())
    out_dir = Path(out)
    (out_dir / "body.glb").write_bytes(body(layout["haircap"], layout["vertices"]))
    unit, triangles = sphere()
    (out_dir / "not_soma.glb").write_bytes(person(HEAD_CENTRE + unit * HEAD_AXES, triangles))
    (out_dir / "hair.glb").write_bytes(hair())
    print(f"HAIR_FIXTURE {len(unit)} head vertices")


main()
