"""The animated head as glTF 2.0 binary.

The skin carries the actor's ARKit blendshapes and the tongue its own as
morph targets, stored sparse (each touches part of the mesh), with normals.
The lower teeth and the tongue hang under a jaw node, which the jaw transform
moves; each eyeball hangs under a node at the point it turns about. One
animation holds the blendshape weights of the skin and the tongue, the jaw's
translation and rotation and the eyes' rotations, sampled linearly.

Metres, Y up, the face towards +Z. Colours are flat; the eyeballs are
painted (sclera, iris, pupil) by the angle from straight ahead.
"""

import json
import struct
from dataclasses import dataclass

import numpy as np

from audio2face_model_server.heads import Head, Part, euler_xyz

_FLOAT, _UINT = 5126, 5125
_ARRAY_BUFFER, _ELEMENT_ARRAY_BUFFER = 34962, 34963
CENTIMETRE = 0.01

# sRGB colours.
SKIN = (0.80, 0.62, 0.53)
TEETH = (0.93, 0.91, 0.85)
TONGUE = (0.72, 0.36, 0.36)
SCLERA = (0.93, 0.92, 0.88)
IRIS = (0.30, 0.38, 0.45)
PUPIL = (0.03, 0.03, 0.03)
# Half-angles of the iris and the pupil on the eyeball, seen from its pivot.
IRIS_DEGREES, PUPIL_DEGREES, EDGE_DEGREES = 22.0, 8.0, 2.5


def linear(colour: tuple[float, float, float]) -> list[float]:
    """An sRGB colour in the linear values glTF factors are."""
    values = np.asarray(colour, dtype=np.float64)
    return np.where(values <= 0.04045, values / 12.92, ((values + 0.055) / 1.055) ** 2.4).tolist()


def vertex_normals(positions: np.ndarray, triangles: np.ndarray) -> np.ndarray:
    """Smooth normals, each the area-weighted mean of its triangles'."""
    points = positions.astype(np.float64)
    corners = triangles.astype(np.int64)
    a, b, c = (points[corners[:, k]] for k in range(3))
    faces = np.cross(b - a, c - a)
    normals = np.zeros_like(points)
    for k in range(3):
        for axis in range(3):
            normals[:, axis] += np.bincount(
                corners[:, k], weights=faces[:, axis], minlength=len(points)
            )
    length = np.linalg.norm(normals, axis=1, keepdims=True)
    normals = np.divide(normals, length, out=np.zeros_like(normals), where=length > 0)
    normals[length[:, 0] == 0] = (0.0, 1.0, 0.0)
    return normals.astype(np.float32)


def quaternions(rotations: np.ndarray) -> np.ndarray:
    """(N, 3, 3) rotation matrices (column vectors) as glTF's (x, y, z, w),
    made orthonormal first."""
    u, _, vt = np.linalg.svd(rotations.astype(np.float64))
    matrices = u @ vt
    matrices[np.linalg.det(matrices) < 0] *= -1  # never a reflection
    m = matrices
    trace = m[:, 0, 0] + m[:, 1, 1] + m[:, 2, 2]
    q = np.zeros((len(m), 4))
    candidates = np.stack([trace, m[:, 0, 0], m[:, 1, 1], m[:, 2, 2]], axis=1)
    pick = np.argmax(candidates, axis=1)
    for index in range(len(m)):
        r = m[index]
        if pick[index] == 0:
            s = 2.0 * np.sqrt(1.0 + trace[index])
            q[index] = (
                (r[2, 1] - r[1, 2]) / s,
                (r[0, 2] - r[2, 0]) / s,
                (r[1, 0] - r[0, 1]) / s,
                s / 4,
            )
        elif pick[index] == 1:
            s = 2.0 * np.sqrt(1.0 + r[0, 0] - r[1, 1] - r[2, 2])
            q[index] = (
                s / 4,
                (r[0, 1] + r[1, 0]) / s,
                (r[0, 2] + r[2, 0]) / s,
                (r[2, 1] - r[1, 2]) / s,
            )
        elif pick[index] == 2:
            s = 2.0 * np.sqrt(1.0 + r[1, 1] - r[0, 0] - r[2, 2])
            q[index] = (
                (r[0, 1] + r[1, 0]) / s,
                s / 4,
                (r[1, 2] + r[2, 1]) / s,
                (r[0, 2] - r[2, 0]) / s,
            )
        else:
            s = 2.0 * np.sqrt(1.0 + r[2, 2] - r[0, 0] - r[1, 1])
            q[index] = (
                (r[0, 2] + r[2, 0]) / s,
                (r[1, 2] + r[2, 1]) / s,
                s / 4,
                (r[1, 0] - r[0, 1]) / s,
            )
    q[q[:, 3] < 0] *= -1
    # Neighbouring keys on the same side, so that linear interpolation takes the short way.
    for index in range(1, len(q)):
        if np.dot(q[index], q[index - 1]) < 0:
            q[index] *= -1
    return q.astype(np.float32)


@dataclass(frozen=True)
class Morphs:
    """Blendshapes of a mesh: names and deltas from its neutral, in its units."""

    names: list[str]
    deltas: np.ndarray  # (K, V, 3)


@dataclass(frozen=True)
class Motion:
    """The animation: one row per frame."""

    times: np.ndarray  # (N,) seconds
    skin: np.ndarray  # (N, K skin) blendshape weights
    tongue: np.ndarray  # (N, K tongue)
    jaw: np.ndarray  # (N, 4, 4) rigid transforms, column vectors, centimetres
    eyes: np.ndarray  # (N, 2, 3) degrees, Maya's XYZ order; right, left


class _Writer:
    def __init__(self, generator: str) -> None:
        self.document: dict = {
            "asset": {"version": "2.0", "generator": generator},
            "scene": 0,
            "scenes": [{"nodes": [0]}],
            "nodes": [],
            "meshes": [],
            "materials": [],
            "accessors": [],
            "bufferViews": [],
        }
        self.binary = bytearray()

    def view(self, data: bytes, target: int | None = None) -> int:
        self.binary += b"\0" * (-len(self.binary) % 4)
        view: dict = {"buffer": 0, "byteOffset": len(self.binary), "byteLength": len(data)}
        if target is not None:
            view["target"] = target
        self.binary += data
        self.document["bufferViews"].append(view)
        return len(self.document["bufferViews"]) - 1

    def accessor(
        self, array: np.ndarray, kind: str, target: int | None = None, bounds: bool = False
    ) -> int:
        array = np.ascontiguousarray(array)
        component = _FLOAT if array.dtype == np.float32 else _UINT
        accessor: dict = {
            "bufferView": self.view(array.tobytes(), target),
            "componentType": component,
            "count": int(array.shape[0]),
            "type": kind,
        }
        if bounds:
            rows = array.reshape(array.shape[0], -1)
            accessor["min"], accessor["max"] = rows.min(0).tolist(), rows.max(0).tolist()
        self.document["accessors"].append(accessor)
        return len(self.document["accessors"]) - 1

    def sparse(self, count: int, indices: np.ndarray, values: np.ndarray) -> int:
        """A VEC3 float accessor of ``count`` elements, zero but at ``indices``."""
        if not len(indices):  # glTF wants at least one
            indices, values = np.zeros(1, np.uint32), np.zeros((1, 3), np.float32)
        bounds = np.vstack([values, np.zeros((1, 3), np.float32)])
        accessor = {
            "componentType": _FLOAT,
            "count": count,
            "type": "VEC3",
            "min": bounds.min(0).tolist(),
            "max": bounds.max(0).tolist(),
            "sparse": {
                "count": len(indices),
                "indices": {
                    "bufferView": self.view(indices.astype(np.uint32).tobytes()),
                    "componentType": _UINT,
                },
                "values": {"bufferView": self.view(values.astype(np.float32).tobytes())},
            },
        }
        self.document["accessors"].append(accessor)
        return len(self.document["accessors"]) - 1

    def material(self, name: str, colour: tuple[float, float, float], roughness: float) -> int:
        self.document["materials"].append(
            {
                "name": name,
                "pbrMetallicRoughness": {
                    "baseColorFactor": [*linear(colour), 1.0],
                    "metallicFactor": 0.0,
                    "roughnessFactor": roughness,
                },
            }
        )
        return len(self.document["materials"]) - 1

    def mesh(
        self,
        name: str,
        positions: np.ndarray,
        triangles: np.ndarray,
        material: int,
        colours: np.ndarray | None = None,
        morphs: Morphs | None = None,
    ) -> int:
        normals = vertex_normals(positions, triangles)
        attributes = {
            "POSITION": self.accessor(
                positions.astype(np.float32), "VEC3", _ARRAY_BUFFER, bounds=True
            ),
            "NORMAL": self.accessor(normals, "VEC3", _ARRAY_BUFFER),
        }
        if colours is not None:
            attributes["COLOR_0"] = self.accessor(colours.astype(np.float32), "VEC3", _ARRAY_BUFFER)
        primitive: dict = {
            "attributes": attributes,
            "indices": self.accessor(
                triangles.astype(np.uint32).reshape(-1), "SCALAR", _ELEMENT_ARRAY_BUFFER
            ),
            "material": material,
        }
        mesh: dict = {"name": name, "primitives": [primitive]}
        if morphs is not None:
            targets = []
            for delta in morphs.deltas:
                moved = vertex_normals(positions + delta, triangles) - normals
                touched = np.flatnonzero(
                    (np.abs(delta) > 1e-9).any(1) | (np.abs(moved) > 1e-5).any(1)
                )
                targets.append(
                    {
                        "POSITION": self.sparse(len(positions), touched, delta[touched]),
                        "NORMAL": self.sparse(len(positions), touched, moved[touched]),
                    }
                )
            primitive["targets"] = targets
            mesh["weights"] = [0.0] * len(targets)
            mesh["extras"] = {"targetNames": list(morphs.names)}
        self.document["meshes"].append(mesh)
        return len(self.document["meshes"]) - 1

    def node(self, name: str, **fields: object) -> int:
        self.document["nodes"].append({"name": name, **fields})
        return len(self.document["nodes"]) - 1

    def encode(self) -> bytes:
        self.document["buffers"] = [{"byteLength": len(self.binary)}]
        text = json.dumps(self.document, separators=(",", ":")).encode()
        text += b" " * (-len(text) % 4)
        binary = bytes(self.binary) + b"\0" * (-len(self.binary) % 4)
        length = 12 + 8 + len(text) + 8 + len(binary)
        return b"".join(
            [
                struct.pack("<4sII", b"glTF", 2, length),
                struct.pack("<I4s", len(text), b"JSON"),
                text,
                struct.pack("<I4s", len(binary), b"BIN\0"),
                binary,
            ]
        )


def eye_colours(ball: Part, pivot: np.ndarray) -> np.ndarray:
    """Linear vertex colours of an eyeball: pupil, iris and sclera by the
    angle from +Z, blended over a small edge."""
    directions = ball.positions.astype(np.float64) - pivot
    cosine = directions[:, 2] / np.maximum(np.linalg.norm(directions, axis=1), 1e-9)
    angle = np.degrees(np.arccos(np.clip(cosine, -1.0, 1.0)))

    def step(edge: float) -> np.ndarray:  # 0 inside, 1 outside, smooth over the edge
        t = np.clip((angle - edge + EDGE_DEGREES / 2) / EDGE_DEGREES, 0.0, 1.0)
        return (t * t * (3 - 2 * t))[:, None]

    pupil, iris, sclera = (np.asarray(linear(colour)) for colour in (PUPIL, IRIS, SCLERA))
    colours = pupil + (iris - pupil) * step(PUPIL_DEGREES)
    return colours + (sclera - colours) * step(IRIS_DEGREES)


def encode(
    head: Head,
    skin_neutral: np.ndarray,
    skin: Morphs,
    tongue_neutral: np.ndarray,
    tongue: Morphs,
    motion: Motion,
    generator: str,
) -> bytes:
    """The GLB of a head speaking. The neutrals and morph deltas are those of
    the actor's blendshape data (centimetres), on the head's topology."""
    if (
        skin_neutral.shape != head.skin.positions.shape
        or tongue_neutral.shape != head.tongue.positions.shape
    ):
        raise ValueError("the blendshape data does not match the head's meshes")
    writer = _Writer(generator)
    skin_material = writer.material("skin", SKIN, 0.55)
    teeth_material = writer.material("teeth", TEETH, 0.3)
    tongue_material = writer.material("tongue", TONGUE, 0.45)
    eye_material = writer.material("eye", (1.0, 1.0, 1.0), 0.2)

    def scaled(array: np.ndarray) -> np.ndarray:
        return (array.astype(np.float64) * CENTIMETRE).astype(np.float32)

    root = writer.node("head", children=[])
    nodes = writer.document["nodes"]

    def attach(parent: int, child: int) -> None:
        nodes[parent].setdefault("children", []).append(child)

    skin_node = writer.node(
        "skin",
        mesh=writer.mesh(
            "skin",
            scaled(skin_neutral),
            head.skin.triangles,
            skin_material,
            morphs=Morphs(skin.names, scaled(skin.deltas)),
        ),
    )
    attach(root, skin_node)

    def teeth(name: str, part: Part, parent: int) -> None:
        mesh = writer.mesh(name, scaled(part.positions), part.triangles, teeth_material)
        attach(parent, writer.node(name, mesh=mesh))

    teeth("upper_teeth", head.upper_teeth, root)
    jaw = writer.node("jaw")
    attach(root, jaw)
    teeth("lower_teeth", head.lower_teeth, jaw)
    tongue_node = writer.node(
        "tongue",
        mesh=writer.mesh(
            "tongue",
            scaled(tongue_neutral),
            head.tongue.triangles,
            tongue_material,
            morphs=Morphs(tongue.names, scaled(tongue.deltas)),
        ),
    )
    attach(jaw, tongue_node)
    eye_nodes = []
    for side, eye in zip(("right", "left"), head.eyes, strict=True):
        node = writer.node(f"{side}_eye", translation=scaled(eye.pivot).tolist())
        attach(root, node)
        ball = writer.mesh(
            f"{side}_eyeball",
            scaled(eye.ball.positions - eye.pivot),
            eye.ball.triangles,
            eye_material,
            colours=eye_colours(eye.ball, eye.pivot),
        )
        attach(node, writer.node(f"{side}_eyeball", mesh=ball))
        eye_nodes.append(node)

    _animate(writer, motion, skin_node, tongue_node, jaw, eye_nodes)
    return writer.encode()


def _animate(
    writer: _Writer, motion: Motion, skin: int, tongue: int, jaw: int, eyes: list[int]
) -> None:
    frames = len(motion.times)
    if frames == 0:
        return
    times = writer.accessor(motion.times.astype(np.float32), "SCALAR", bounds=True)
    samplers: list[dict] = []
    channels: list[dict] = []

    def channel(node: int, path: str, values: np.ndarray, kind: str) -> None:
        output = writer.accessor(values.astype(np.float32), kind)
        samplers.append({"input": times, "output": output, "interpolation": "LINEAR"})
        channels.append({"sampler": len(samplers) - 1, "target": {"node": node, "path": path}})

    channel(skin, "weights", motion.skin.reshape(-1), "SCALAR")
    channel(tongue, "weights", motion.tongue.reshape(-1), "SCALAR")
    channel(jaw, "translation", motion.jaw[:, :3, 3] * CENTIMETRE, "VEC3")
    channel(jaw, "rotation", quaternions(motion.jaw[:, :3, :3]), "VEC4")
    for index, node in enumerate(eyes):
        rotations = np.stack([euler_xyz(angles) for angles in motion.eyes[:, index]])
        channel(node, "rotation", quaternions(rotations), "VEC4")
    writer.document["animations"] = [{"name": "speech", "samplers": samplers, "channels": channels}]
