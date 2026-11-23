"""The person as glTF 2.0 binary: skinned surfaces (the body and the eyeballs) on one skeleton
in its bind pose, sharing one texture; a material per surface (its roughness).

Joint nodes carry translations and rotations only; the inverse bind matrices are computed from
those, so the file shows the bind pose exactly. Metres, Y up, the face towards +Z.
"""

import json
import struct
from collections.abc import Sequence
from dataclasses import dataclass

import numpy as np

_BYTE, _UBYTE, _USHORT, _UINT, _FLOAT = 5120, 5121, 5123, 5125, 5126
_ARRAY_BUFFER, _ELEMENT_ARRAY_BUFFER = 34962, 34963
_LINEAR, _LINEAR_MIPMAP_LINEAR, _CLAMP = 9729, 9987, 33071


@dataclass(frozen=True)
class Skeleton:
    names: list[str]
    parents: list[int]  # -1 for the root; a parent comes before its children
    world: np.ndarray  # (J, 4, 4) bind transforms in the world, column vectors


@dataclass(frozen=True)
class Surface:
    name: str
    positions: np.ndarray  # (N, 3)
    normals: np.ndarray  # (N, 3)
    uv: np.ndarray  # (N, 2), glTF's: the texture's top left at (0, 0)
    joints: np.ndarray  # (N, 4) joint indices
    weights: np.ndarray  # (N, 4), each row sums to 1
    triangles: np.ndarray  # (T, 3), counter-clockwise seen from outside
    roughness: float


def quaternion(m: np.ndarray) -> np.ndarray:
    """A rotation matrix (made orthonormal) as glTF's (x, y, z, w)."""
    u, _, vt = np.linalg.svd(m)
    r = u @ vt
    if np.linalg.det(r) < 0:
        r = -r
    t = np.trace(r)
    if t > 0:
        s = 2.0 * np.sqrt(1.0 + t)
        q = ((r[2, 1] - r[1, 2]) / s, (r[0, 2] - r[2, 0]) / s, (r[1, 0] - r[0, 1]) / s, s / 4)
    elif r[0, 0] > r[1, 1] and r[0, 0] > r[2, 2]:
        s = 2.0 * np.sqrt(1.0 + r[0, 0] - r[1, 1] - r[2, 2])
        q = (s / 4, (r[0, 1] + r[1, 0]) / s, (r[0, 2] + r[2, 0]) / s, (r[2, 1] - r[1, 2]) / s)
    elif r[1, 1] > r[2, 2]:
        s = 2.0 * np.sqrt(1.0 + r[1, 1] - r[0, 0] - r[2, 2])
        q = ((r[0, 1] + r[1, 0]) / s, s / 4, (r[1, 2] + r[2, 1]) / s, (r[0, 2] - r[2, 0]) / s)
    else:
        s = 2.0 * np.sqrt(1.0 + r[2, 2] - r[0, 0] - r[1, 1])
        q = ((r[0, 2] + r[2, 0]) / s, (r[1, 2] + r[2, 1]) / s, s / 4, (r[1, 0] - r[0, 1]) / s)
    q = np.asarray(q)
    return q / np.linalg.norm(q) * (1 if q[3] >= 0 else -1)


def matrix(translation: np.ndarray, q: np.ndarray) -> np.ndarray:
    x, y, z, w = q
    m = np.eye(4)
    m[:3, :3] = [
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ]
    m[:3, 3] = translation
    return m


def joint_transforms(skeleton: Skeleton) -> tuple[list[tuple[np.ndarray, np.ndarray]], np.ndarray]:
    """Each joint's local (translation, rotation) and the world matrices they compose to."""
    local, world = [], np.zeros((len(skeleton.names), 4, 4))
    for j, parent in enumerate(skeleton.parents):
        m = (
            skeleton.world[j]
            if parent < 0
            else np.linalg.inv(skeleton.world[parent]) @ skeleton.world[j]
        )
        t, q = m[:3, 3].copy(), quaternion(m[:3, :3])
        local.append((t, q))
        world[j] = matrix(t, q) if parent < 0 else world[parent] @ matrix(t, q)
    return local, world


class _Writer:
    def __init__(self) -> None:
        self.data = bytearray()
        self.views: list[dict] = []
        self.accessors: list[dict] = []

    def view(self, raw: bytes, target: int | None = None) -> int:
        while len(self.data) % 4:
            self.data.append(0)
        view = {"buffer": 0, "byteOffset": len(self.data), "byteLength": len(raw)}
        if target is not None:
            view["target"] = target
        self.data.extend(raw)
        self.views.append(view)
        return len(self.views) - 1

    def accessor(
        self,
        array: np.ndarray,
        component: int,
        kind: str,
        target: int | None = None,
        bounds: bool = False,
    ) -> int:
        dtype = {_UBYTE: np.uint8, _USHORT: np.uint16, _UINT: np.uint32, _FLOAT: np.float32}[
            component
        ]
        values = np.ascontiguousarray(array, dtype=dtype)
        entry = {
            "bufferView": self.view(values.tobytes(), target),
            "componentType": component,
            "count": int(values.shape[0]),
            "type": kind,
        }
        if bounds:
            flat = values.reshape(len(values), -1)
            entry["min"], entry["max"] = flat.min(0).tolist(), flat.max(0).tolist()
        self.accessors.append(entry)
        return len(self.accessors) - 1


def build_glb(
    surfaces: Sequence[Surface],
    skeleton: Skeleton,
    texture: bytes,
    mime: str = "image/png",
    generator: str = "fromsoma model server",
) -> bytes:
    w = _Writer()
    J = len(skeleton.names)
    local, world = joint_transforms(skeleton)
    nodes: list[dict] = []
    for j, name in enumerate(skeleton.names):
        t, q = local[j]
        node = {
            "name": name,
            "translation": [float(x) for x in t],
            "rotation": [float(x) for x in q],
        }
        children = [k for k, p in enumerate(skeleton.parents) if p == j]
        if children:
            node["children"] = children
        nodes.append(node)
    inverse = np.stack([np.linalg.inv(m).T for m in world]).astype(np.float32)  # column-major
    skin = {
        "name": "skeleton",
        "joints": list(range(J)),
        "skeleton": 0,
        "inverseBindMatrices": w.accessor(inverse.reshape(J, 16), _FLOAT, "MAT4"),
    }
    joint_type = _UBYTE if J <= 255 else _USHORT
    meshes, materials, roughness = [], [], {}
    for surface in surfaces:
        if surface.roughness not in roughness:
            roughness[surface.roughness] = len(materials)
            materials.append(
                {
                    "name": f"albedo_r{surface.roughness:g}",
                    "pbrMetallicRoughness": {
                        "baseColorTexture": {"index": 0},
                        "metallicFactor": 0.0,
                        "roughnessFactor": float(surface.roughness),
                    },
                }
            )
        attributes = {
            "POSITION": w.accessor(surface.positions, _FLOAT, "VEC3", _ARRAY_BUFFER, bounds=True),
            "NORMAL": w.accessor(surface.normals, _FLOAT, "VEC3", _ARRAY_BUFFER),
            "TEXCOORD_0": w.accessor(surface.uv, _FLOAT, "VEC2", _ARRAY_BUFFER),
            "JOINTS_0": w.accessor(surface.joints, joint_type, "VEC4", _ARRAY_BUFFER),
            "WEIGHTS_0": w.accessor(surface.weights, _FLOAT, "VEC4", _ARRAY_BUFFER),
        }
        indices = w.accessor(surface.triangles.reshape(-1), _UINT, "SCALAR", _ELEMENT_ARRAY_BUFFER)
        meshes.append(
            {
                "name": surface.name,
                "primitives": [
                    {
                        "attributes": attributes,
                        "indices": indices,
                        "material": roughness[surface.roughness],
                    }
                ],
            }
        )
        nodes.append({"name": surface.name, "mesh": len(meshes) - 1, "skin": 0})
    image = w.view(texture)
    document = {
        "asset": {"version": "2.0", "generator": generator},
        "scene": 0,
        "scenes": [{"nodes": [0, *range(J, len(nodes))]}],
        "nodes": nodes,
        "skins": [skin],
        "meshes": meshes,
        "materials": materials,
        "textures": [{"sampler": 0, "source": 0}],
        "samplers": [
            {
                "magFilter": _LINEAR,
                "minFilter": _LINEAR_MIPMAP_LINEAR,
                "wrapS": _CLAMP,
                "wrapT": _CLAMP,
            }
        ],
        "images": [{"bufferView": image, "mimeType": mime}],
        "accessors": w.accessors,
        "bufferViews": w.views,
        "buffers": [{"byteLength": len(w.data)}],
    }
    while len(w.data) % 4:
        w.data.append(0)
    document["buffers"][0]["byteLength"] = len(w.data)
    text = json.dumps(document, separators=(",", ":")).encode()
    text += b" " * (-len(text) % 4)
    body = bytes(w.data)
    total = 12 + 8 + len(text) + 8 + len(body)
    return (
        struct.pack("<4sII", b"glTF", 2, total)
        + struct.pack("<I4s", len(text), b"JSON")
        + text
        + struct.pack("<I4s", len(body), b"BIN\x00")
        + body
    )
