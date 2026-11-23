# pyright: basic
"""HairDeck's hair on a SOMA person, a Blender script (blender -b -P hair_fit.py -- ...).

    BODY.glb HAIR.glb OUT.glb OPTIONS   OPTIONS: JSON {"thickness", "clearance", "braid"}

Prints ``MESH_REPORT {json}`` when done, ``MESH_INVALID <reason>`` for an input
it cannot use. Blender lends its numpy and mathutils' BVH trees; no scene is
built. The body's glTF is written back as it came, with the hair added as one
more mesh on its skin: weighted to the Head joint (a braid partly to Chest),
with HairDeck's UVs, colours and material and computed normals.

The body is a SOMA person as fromsoma and frommhr write it: SOMA's mesh in the
st UV layout (19,631 vertices, 18,056 once the seams are welded, closed) on
SOMA's skeleton. Where hair grows is SOMA's own haircap segment
(soma_haircap.json, SOMA-X, Apache-2.0). The hair is HairDeck's: ``hair_cards``
(tubes of four-vertex rings, the root ring stored with v = 1) in HairDeck's
head frame, whose head proxy is a fixed ellipsoid; the file carries it as
``head_proxy`` only when asked to.

HairDeck's roots do not lie on its proxy: they float on a shell 2-15 % outside
it, and the cards are layered (outer ones start on inner ones). Seating every
root on the skin flattened the layers into each other, and pushing cards one
by one bent them. The hairstyle therefore moves as one piece:

1. The proxy onto an ellipsoid fitted to the haircap (a shift and a scale per
   axis; a real head is about 1.5 times the proxy).
2. The hairstyle's own scalp, direction by direction from the ellipsoid's
   centre: the 5th percentile of the normalised radius t (1 on the ellipsoid)
   of its hair within 20 degrees. HairDeck lays the crown nearer its proxy
   than the sides, so one number for the whole head sank the crown and floated
   the sides.
3. One smooth radial field over directions moves that scalp onto the skin plus
   the clearance. The skin's radius is measured by rays on the haircap and two
   rings of vertices around it (the jaw and neck, nearer the centre, pulled
   the nape's hair into the skin); the face below the brows is left out and
   the ears are pressed onto the ellipsoid. Every point moves along its own
   direction, so layers keep their order and cards their shape. ``thickness``
   then scales each point's height above that scalp, up to 5 cm; farther
   points (a braid) follow the squeeze of those 5 cm.
4. ``braid``: HairDeck has no body, so a braid hangs straight down through the
   back. Hair behind and below the nape swings back as a whole about the
   nape, bending over 4 cm below it, by the angle past which 4 degrees more
   free less than 15 % of what is still inside; it follows the Chest joint
   over 15 cm going down.
5. The few rings still nearer the skin than the clearance (roots below the
   scalp, hair on the neck and shoulders) are lifted along the skin's normal,
   the lift fading over neighbouring rings.

Inside or outside the skin is decided by rays (the welded body is closed), not
by the nearest face's normal, which an edge or a concave spot can flip.
"""

import json
import struct
import sys
from pathlib import Path
from typing import Any

import numpy as np  # pyright: ignore[reportMissingImports] -- Blender bundles numpy
from mathutils import Vector  # pyright: ignore[reportMissingImports]
from mathutils.bvhtree import BVHTree  # pyright: ignore[reportMissingImports]

HAIRCAP = Path(__file__).with_name("soma_haircap.json")
HEAD_BONES = ("Head", "Jaw", "LeftEye", "RightEye")
# HairDeck's head proxy (hairdeck.vis.head_proxy at the training frame), as
# its GLB bounds: centre and semi-axes, metres.
PROXY_CENTRE = np.array([0.0, 0.42202, 0.00028])
PROXY_AXES = np.array([0.051677, 0.063799, 0.063799])
DIRECTIONS = 8000
SHELL_PERCENTILE = 5.0
SHELL_DEGREES = 20.0
SHELL_MIN_POINTS = 12
# Cards rooted farther out than this (in t) belong to a braid or ponytail.
ON_HEAD_T = 1.4
MARGIN_RINGS = 2
THICKNESS_REACH = 0.05
BRAID_BEND = 0.04
BRAID_FOLLOW = 0.15
LIFT_ROUNDS = 6
LIFT_SPREAD = np.array([0.15, 0.35, 0.6, 1.0, 0.6, 0.35, 0.15])
# Farther from the skin than this a point is not inside it.
INSIDE_REACH = 0.05
INSIDE_RAYS = tuple(
    Vector(v).normalized()
    for v in ((0.213, 0.937, 0.276), (-0.611, 0.302, 0.731), (0.5, -0.2, -0.84))
)

_COMPONENTS = {
    5120: np.int8,
    5121: np.uint8,
    5122: np.int16,
    5123: np.uint16,
    5125: np.uint32,
    5126: np.float32,
}
_CODES = {np.dtype(v): k for k, v in _COMPONENTS.items()}
_WIDTHS = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}
_TYPES = {v: k for k, v in _WIDTHS.items()}
_ARRAY_BUFFER, _ELEMENT_ARRAY_BUFFER = 34962, 34963


def invalid(reason: str) -> None:
    print("MESH_INVALID " + reason, flush=True)
    sys.exit(1)


class Glb:
    """A glTF binary's document and its one embedded buffer; accessors read as
    arrays, new data appended at the end."""

    def __init__(self, data: bytes, what: str) -> None:
        if data[:4] != b"glTF" or len(data) < 28:
            invalid(f"the {what} is not a glTF binary")
        length = struct.unpack_from("<I", data, 12)[0]
        self.doc: dict[str, Any] = json.loads(data[20 : 20 + length])
        at = 20 + length
        size = struct.unpack_from("<I", data, at)[0] if at + 8 <= len(data) else 0
        self.binary = bytearray(data[at + 8 : at + 8 + size])
        if any("uri" in b for b in self.doc.get("buffers", [])):
            invalid(f"the {what} refers to external buffers")

    def accessor(self, index: int) -> np.ndarray:
        a = self.doc["accessors"][index]
        if "sparse" in a or "bufferView" not in a:
            invalid("sparse or empty accessors are not supported")
        view = self.doc["bufferViews"][a["bufferView"]]
        dtype = np.dtype(_COMPONENTS[a["componentType"]])
        width = _WIDTHS[a["type"]]
        start = view.get("byteOffset", 0) + a.get("byteOffset", 0)
        stride = view.get("byteStride", width * dtype.itemsize)
        rows = np.ndarray(
            (a["count"], width), dtype, self.binary, start, (stride, dtype.itemsize)
        ).copy()
        return rows if width > 1 else rows[:, 0]

    def normalized(self, index: int) -> bool:
        return bool(self.doc["accessors"][index].get("normalized"))

    def add(self, rows: np.ndarray, target: int | None = None, *, normalized: bool = False) -> int:
        """Append an accessor over a new buffer view; returns its index."""
        rows = np.ascontiguousarray(rows)
        while len(self.binary) % 4:
            self.binary.append(0)
        view: dict[str, Any] = {
            "buffer": 0,
            "byteOffset": len(self.binary),
            "byteLength": rows.nbytes,
        }
        if target is not None:
            view["target"] = target
        self.binary += rows.tobytes()
        views = self.doc.setdefault("bufferViews", [])
        views.append(view)
        width = 1 if rows.ndim == 1 else rows.shape[1]
        accessor: dict[str, Any] = {
            "bufferView": len(views) - 1,
            "componentType": _CODES[rows.dtype],
            "count": len(rows),
            "type": _TYPES[width],
        }
        if normalized:
            accessor["normalized"] = True
        if target == _ARRAY_BUFFER and rows.dtype == np.float32 and width == 3:
            accessor["min"] = rows.min(0).tolist()
            accessor["max"] = rows.max(0).tolist()
        accessors = self.doc.setdefault("accessors", [])
        accessors.append(accessor)
        return len(accessors) - 1

    def node_matrix(self, index: int) -> np.ndarray:
        """The node's world matrix (column vectors)."""
        nodes = self.doc["nodes"]
        parent = {c: i for i, n in enumerate(nodes) for c in n.get("children", [])}
        m = np.eye(4)
        while True:
            m = local_matrix(nodes[index]) @ m
            if index not in parent:
                return m
            index = parent[index]

    def to_bytes(self) -> bytes:
        while len(self.binary) % 4:
            self.binary.append(0)
        self.doc.setdefault("buffers", [{}])[0]["byteLength"] = len(self.binary)
        text = json.dumps(self.doc, separators=(",", ":")).encode()
        text += b" " * (-len(text) % 4)
        total = 12 + 8 + len(text) + 8 + len(self.binary)
        return (
            struct.pack("<4sII", b"glTF", 2, total)
            + struct.pack("<I4s", len(text), b"JSON")
            + text
            + struct.pack("<I4s", len(self.binary), b"BIN\x00")
            + bytes(self.binary)
        )


def local_matrix(node: dict) -> np.ndarray:
    if "matrix" in node:
        return np.array(node["matrix"], float).reshape(4, 4).T
    x, y, z, w = node.get("rotation", (0.0, 0.0, 0.0, 1.0))
    rotation = np.array(
        [
            [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
        ]
    )
    m = np.eye(4)
    m[:3, :3] = rotation * np.array(node.get("scale", (1.0, 1.0, 1.0)))
    m[:3, 3] = node.get("translation", (0.0, 0.0, 0.0))
    return m


def transform(m: np.ndarray, points: np.ndarray) -> np.ndarray:
    return points @ m[:3, :3].T + m[:3, 3]


# --- the body ---------------------------------------------------------------


class Body:
    """The SOMA person: the skinned body mesh welded at its seams, its joints'
    bind positions, the haircap and the head's vertices."""

    def __init__(self, glb: Glb) -> None:
        doc = glb.doc
        if not doc.get("skins"):
            invalid("the body has no skeleton: give a SOMA person (fromsoma, frommhr)")
        skinned = [n for n in doc.get("nodes", []) if "mesh" in n and "skin" in n]
        if not skinned:
            invalid("the body has no skinned mesh")

        def vertices(node: dict) -> int:
            primitive = doc["meshes"][node["mesh"]]["primitives"][0]
            return doc["accessors"][primitive["attributes"]["POSITION"]]["count"]

        node = max(skinned, key=vertices)
        layout = json.loads(HAIRCAP.read_text())
        if vertices(node) != layout["vertices"]:
            invalid(
                f"the body is not a SOMA person as fromsoma or frommhr write it: its mesh has "
                f"{vertices(node)} vertices, SOMA's {layout['vertices']}"
            )
        self.skin = node["skin"]
        primitive = doc["meshes"][node["mesh"]]["primitives"][0]
        attributes = primitive["attributes"]
        if (
            "JOINTS_0" not in attributes
            or "WEIGHTS_0" not in attributes
            or "indices" not in primitive
        ):
            invalid("the body mesh lacks joints, weights or indices")
        positions = glb.accessor(attributes["POSITION"]).astype(np.float64)
        triangles = glb.accessor(primitive["indices"]).astype(np.int64).reshape(-1, 3)
        skin = doc["skins"][self.skin]
        joint_nodes: list[int] = skin["joints"]
        self.names = [doc["nodes"][j].get("name", "") for j in joint_nodes]
        if "inverseBindMatrices" in skin:
            inverse = glb.accessor(skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)
            world = np.linalg.inv(inverse)
        else:
            world = np.stack([glb.node_matrix(j) for j in joint_nodes])
        self.joints = {name: world[k][:3, 3] for k, name in enumerate(self.names)}
        missing = [
            n for n in ("Head", "HeadEnd", "LeftEye", "RightEye", "Chest") if n not in self.joints
        ]
        if missing:
            invalid(f"the body's skeleton lacks SOMA's joints {', '.join(missing)}")

        unique, inverse_index = np.unique(positions, axis=0, return_inverse=True)
        inverse_index = inverse_index.reshape(-1)
        self.vertices = unique
        self.triangles = inverse_index[triangles]
        edges = np.sort(
            np.concatenate(
                [self.triangles[:, [0, 1]], self.triangles[:, [1, 2]], self.triangles[:, [2, 0]]]
            ),
            axis=1,
        )
        _, uses = np.unique(edges, axis=0, return_counts=True)
        if (uses != 2).any():
            invalid("the body's welded mesh is not closed, so inside and outside are undefined")
        joints = glb.accessor(attributes["JOINTS_0"])
        weights = glb.accessor(attributes["WEIGHTS_0"])
        main = np.array(self.names)[joints[np.arange(len(joints)), weights.argmax(1)]]
        self.head = np.zeros(len(unique), bool)
        self.head[inverse_index[np.isin(main, HEAD_BONES)]] = True
        self.haircap = np.unique(inverse_index[np.array(layout["haircap"])])
        self.eye = (self.joints["LeftEye"] + self.joints["RightEye"]) / 2


# --- geometry ---------------------------------------------------------------


def fit_ellipsoid(points: np.ndarray) -> tuple[np.ndarray, np.ndarray, float]:
    """Axis-aligned ellipsoid (centre, semi-axes, rms metres) by the radial
    distance of each point: Levenberg-Marquardt on six numbers."""

    def residual(x: np.ndarray) -> np.ndarray:
        c, a = x[:3], x[3:]
        q = (points - c) / a
        r = np.linalg.norm(q, axis=1)
        return (r - 1) * np.linalg.norm(points - c, axis=1) / np.maximum(r, 1e-9)

    x = np.concatenate([points.mean(0), np.ptp(points, axis=0) / 2])
    f = residual(x)
    cost, damping = f @ f, 1e-3
    for _ in range(200):
        step_size = 1e-7 * np.maximum(1.0, np.abs(x))
        jacobian = np.stack(
            [(residual(x + np.eye(6)[k] * step_size[k]) - f) / step_size[k] for k in range(6)], 1
        )
        a = jacobian.T @ jacobian
        g = jacobian.T @ f
        step = np.linalg.solve(a + damping * np.diag(np.diag(a)), -g)
        trial = residual(x + step)
        if trial @ trial < cost:
            x, f = x + step, trial
            done = cost - trial @ trial < 1e-14 * max(cost, 1e-30)
            cost, damping = trial @ trial, damping * 0.3
            if done:
                break
        else:
            damping *= 10
            if damping > 1e12:
                break
    return x[:3], np.abs(x[3:]), float(np.sqrt(np.mean(f**2)))


def ellipsoid_radius(directions: np.ndarray, axes: np.ndarray) -> np.ndarray:
    return 1 / np.linalg.norm(directions / axes, axis=1)


def ears(
    vertices: np.ndarray, head: np.ndarray, centre: np.ndarray, axes: np.ndarray
) -> np.ndarray:
    """The ears: head vertices out of the scalp ellipsoid at its sides, at ear
    height and behind the cheek (SOMA has no ear segment)."""
    q = (vertices - centre) / axes
    r = np.linalg.norm(q, axis=1)
    return (
        head
        & (r > 1.05)
        & (np.abs(q[:, 0]) > 0.75)
        & (q[:, 2] > -0.55)
        & (q[:, 2] < 0.05)
        & (q[:, 1] > -0.75)
        & (q[:, 1] < 0.15)
    )


def fibonacci(n: int) -> np.ndarray:
    """n nearly even directions on the sphere."""
    i = np.arange(n) + 0.5
    y = 1 - 2 * i / n
    r = np.sqrt(1 - y * y)
    phi = np.pi * (3 - np.sqrt(5)) * i
    return np.stack([r * np.cos(phi), y, r * np.sin(phi)], 1)


def spherical_mean(
    directions: np.ndarray, values: np.ndarray, at: np.ndarray, sigma_degrees: float
) -> tuple[np.ndarray, np.ndarray]:
    """Gaussian-weighted mean of values given at unit directions, at unit
    directions ``at``: (mean, weight sum); the weight sum is 0 where no
    direction lies within 3 sigma."""
    sigma = np.radians(sigma_degrees)
    reach = min(3 * sigma, np.pi)
    mean, weight = np.zeros(len(at)), np.zeros(len(at))
    for start in range(0, len(at), 512):
        angle = np.arccos(np.clip(at[start : start + 512] @ directions.T, -1, 1))
        w = np.where(angle <= reach, np.exp(-0.5 * (angle / sigma) ** 2), 0.0)
        total = w.sum(1)
        weight[start : start + 512] = total
        mean[start : start + 512] = np.where(total > 0, w @ values / np.maximum(total, 1e-300), 0)
    return mean, weight


def radial_field(directions: np.ndarray, valid: np.ndarray, values: np.ndarray) -> np.ndarray:
    """Values on the valid directions over all of them: smoothed where
    measured, filled from ever wider neighbourhoods elsewhere, then smoothed
    once more across the seams."""
    field = np.full(len(directions), np.nan)
    for sigma in (4.0, 12.0, 30.0, 70.0):
        mean, weight = spherical_mean(directions[valid], values[valid], directions, sigma)
        enough = np.isnan(field) & (weight > 0.5 * np.exp(-0.5) * (1 if sigma == 4.0 else 0.2))
        field[enough] = mean[enough]
    field[np.isnan(field)] = np.median(values[valid])
    return spherical_mean(directions, field, directions, 3.0)[0]


class Skin:
    """Distances to a closed surface and inside tests by ray parity."""

    def __init__(self, vertices: np.ndarray, triangles: np.ndarray) -> None:
        self.tree = BVHTree.FromPolygons(vertices.tolist(), triangles.tolist(), all_triangles=True)

    def inside(self, point: np.ndarray) -> bool:
        votes = 0
        for direction in INSIDE_RAYS:
            origin, hits = Vector(point), 0
            while True:
                location = self.tree.ray_cast(origin, direction)[0]
                if location is None:
                    break
                hits += 1
                origin = location + direction * 1e-6
            votes += hits % 2
        return votes >= 2

    def distance(self, points: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
        """Signed distance (positive outside) and the outward face normal at
        the closest point."""
        distance = np.empty(len(points))
        normal = np.empty((len(points), 3))
        for k, p in enumerate(points):
            _, n, _, d = self.tree.find_nearest(Vector(p))
            distance[k] = d
            normal[k] = n
            if d < INSIDE_REACH and self.inside(p):
                distance[k] = -d
        return distance, normal

    def contains(self, points: np.ndarray) -> np.ndarray:
        return np.array([self.inside(p) for p in points], bool)


def cards(uv: np.ndarray) -> list[tuple[int, int]]:
    """Ring ranges per card, [(first ring, ring count)]: a card's root ring is
    stored with v = 1, its tip with v = 0."""
    v = 1 - uv[::4, 1].astype(np.float64)
    starts = np.concatenate([[0], np.flatnonzero((v[:-1] == 1) & (v[1:] == 0)) + 1])
    ends = np.concatenate([starts[1:], [len(v)]])
    return [(int(s), int(e - s)) for s, e in zip(starts, ends, strict=True)]


def card_shape_change(
    points: np.ndarray, reference: np.ndarray, ranges: list[tuple[int, int]]
) -> np.ndarray:
    """Per card: the largest change of a ring's offset from the root ring,
    relative to the card's length (0: the card only moved)."""
    p, q = points.reshape(-1, 4, 3).mean(1), reference.reshape(-1, 4, 3).mean(1)
    change = []
    for first, count in ranges:
        if count < 2:
            continue
        a = p[first : first + count] - p[first]
        b = q[first : first + count] - q[first]
        length = np.linalg.norm(np.diff(q[first : first + count], axis=0), axis=1).sum()
        change.append(np.linalg.norm(a - b, axis=1).max() / max(length, 1e-6))
    return np.array(change or [0.0])


def smoothstep(x: np.ndarray) -> np.ndarray:
    return x * x * (3 - 2 * x)


def braid_weight(
    body: Body, centre: np.ndarray, points: np.ndarray
) -> tuple[np.ndarray, np.ndarray]:
    """The nape anchor (the skin point farthest back between Head - 0.30 and
    Head + 0.40 of the head's height, within 5 cm of the middle) and, per
    point, how much it belongs to the braid: from 0 at the anchor's height to 1
    at BRAID_BEND below it, behind the head's middle."""
    head_y = body.joints["Head"][1]
    tall = body.joints["HeadEnd"][1] - head_y
    v = body.vertices
    near = (
        (v[:, 1] > head_y - 0.30 * tall)
        & (v[:, 1] < head_y + 0.40 * tall)
        & (np.abs(v[:, 0] - centre[0]) < 0.05)
    )
    anchor = v[near][np.argmin(v[near][:, 2])]
    down = np.clip((anchor[1] - points[:, 1]) / BRAID_BEND, 0, 1)
    back = np.clip((centre[2] + 0.02 - points[:, 2]) / 0.04, 0, 1)
    return anchor, smoothstep(down) * smoothstep(back)


def vertex_normals(points: np.ndarray, triangles: np.ndarray) -> np.ndarray:
    corners = points[triangles]
    faces = np.cross(corners[:, 1] - corners[:, 0], corners[:, 2] - corners[:, 0])
    normals = np.zeros_like(points)
    for k in range(3):
        np.add.at(normals, triangles[:, k], faces)
    length = np.linalg.norm(normals, axis=1, keepdims=True)
    return np.where(length > 1e-12, normals / np.maximum(length, 1e-12), [0.0, 1.0, 0.0])


# --- the fit ----------------------------------------------------------------


def fit(body_path: Path, hair_path: Path, target: Path, options: dict) -> None:
    thickness = float(options.get("thickness", 1.0))
    clearance = float(options.get("clearance", 0.0015))
    braid = bool(options.get("braid", False))

    out = Glb(body_path.read_bytes(), "body")
    body = Body(out)
    hair_glb = Glb(hair_path.read_bytes(), "hair")
    nodes = hair_glb.doc.get("nodes", [])
    names = [n.get("name") for n in nodes]
    if "hair_cards" not in names:
        invalid("the hair has no hair_cards node: give HairDeck's result")
    hair_node = names.index("hair_cards")
    primitive = hair_glb.doc["meshes"][nodes[hair_node]["mesh"]]["primitives"][0]
    attributes = primitive["attributes"]
    if (
        primitive.get("mode", 4) != 4
        or "indices" not in primitive
        or "TEXCOORD_0" not in attributes
    ):
        invalid("HairDeck's cards come as indexed triangles with UVs")
    original = transform(
        hair_glb.node_matrix(hair_node),
        hair_glb.accessor(attributes["POSITION"]).astype(np.float64),
    )
    if len(original) % 4:
        invalid("HairDeck's cards come as rings of four vertices")
    uv = hair_glb.accessor(attributes["TEXCOORD_0"])
    faces = hair_glb.accessor(primitive["indices"]).astype(np.int64).reshape(-1, 3)
    proxy_centre, proxy_axes = PROXY_CENTRE, PROXY_AXES
    if "head_proxy" in names:
        k = names.index("head_proxy")
        proxy_mesh = hair_glb.doc["meshes"][nodes[k]["mesh"]]["primitives"][0]
        proxy = transform(
            hair_glb.node_matrix(k),
            hair_glb.accessor(proxy_mesh["attributes"]["POSITION"]).astype(np.float64),
        )
        proxy_centre = (proxy.min(0) + proxy.max(0)) / 2
        proxy_axes = (proxy.max(0) - proxy.min(0)) / 2

    vertices, triangles = body.vertices, body.triangles
    centre, axes, rms = fit_ellipsoid(vertices[body.haircap])
    ear = ears(vertices, body.head, centre, axes)
    pressed = vertices.copy()
    q = (vertices[ear] - centre) / axes
    pressed[ear] = centre + q / np.linalg.norm(q, axis=1, keepdims=True) * axes
    skin = Skin(pressed, triangles)

    # 1. The proxy onto the haircap's ellipsoid.
    mapped = centre + (original - proxy_centre) * (axes / proxy_axes)
    ranges = cards(uv)
    t = np.linalg.norm((mapped - centre) / axes, axis=1)
    ring_t = t.reshape(-1, 4).mean(1)
    root_t = np.array([ring_t[first] for first, _ in ranges])
    on_head = root_t < ON_HEAD_T
    if not on_head.any():
        invalid("no card is rooted on HairDeck's head")

    # 2. The hairstyle's own scalp per direction.
    offset = mapped - centre
    direction = offset / np.linalg.norm(offset, axis=1, keepdims=True)
    dirs = fibonacci(DIRECTIONS)
    near_head = t < ON_HEAD_T
    around, near_t = direction[near_head], t[near_head]
    shell = np.full(len(dirs), np.nan)
    cos_shell = np.cos(np.radians(SHELL_DEGREES))
    for k, d in enumerate(dirs):
        values = near_t[around @ d >= cos_shell]
        if len(values) >= SHELL_MIN_POINTS:
            shell[k] = np.percentile(values, SHELL_PERCENTILE)

    # 3. The skin's radius by rays on the scalp, and one field moving the
    # hairstyle's scalp onto it.
    scalp = np.zeros(len(vertices), bool)
    scalp[body.haircap] = True
    for _ in range(MARGIN_RINGS):
        scalp[triangles[scalp[triangles].any(1)].ravel()] = True
    head_tree = BVHTree.FromPolygons(
        pressed.tolist(), triangles[scalp[triangles].all(1)].tolist(), all_triangles=True
    )
    radius = np.full(len(dirs), np.nan)
    hit = np.full((len(dirs), 3), np.nan)
    origin = Vector(centre)
    for k, d in enumerate(dirs):
        location, _, _, distance = head_tree.ray_cast(origin, Vector(d))
        if location is not None:
            radius[k], hit[k] = distance, location
    with np.errstate(invalid="ignore"):
        face = (hit[:, 1] < body.eye[1] + 0.02) & (hit[:, 2] > body.eye[2] - 0.03)
    valid = np.isfinite(radius) & ~face & np.isfinite(shell)
    if valid.sum() < 100:
        invalid("the hair and the scalp share too few directions to fit")
    measured = np.where(valid, radius + clearance - shell * ellipsoid_radius(dirs, axes), np.nan)
    delta = radial_field(dirs, valid, np.nan_to_num(measured))
    moved_by = spherical_mean(dirs, delta, direction, 2.5)[0]
    warped = mapped + direction * moved_by[:, None]
    shell_field = radial_field(dirs, np.isfinite(shell), np.nan_to_num(shell))
    shell_at = spherical_mean(dirs, shell_field, direction, 2.5)[0]
    height = (t - shell_at) * ellipsoid_radius(direction, axes)  # above the hairstyle's scalp
    squeezed = np.where(
        height < THICKNESS_REACH,
        thickness * height,
        thickness * THICKNESS_REACH + height - THICKNESS_REACH,
    )
    keep = braid_weight(body, centre, warped)[1] if braid else np.zeros(len(warped))
    # The braid keeps its place: squeezing it towards the centre pushed it into the back.
    warped = warped + direction * ((squeezed - height) * (1 - keep))[:, None]
    near = height < THICKNESS_REACH

    # 4. The braid swings back about the nape.
    swing = None
    anchor = None
    if braid:
        anchor, weight = braid_weight(body, centre, warped)
        moving = weight > 0

        def swung(degrees: float) -> np.ndarray:
            a = np.radians(degrees) * weight
            rel = warped - anchor
            return anchor + np.stack(
                [
                    rel[:, 0],
                    rel[:, 1] * np.cos(a) - rel[:, 2] * np.sin(a),
                    rel[:, 1] * np.sin(a) + rel[:, 2] * np.cos(a),
                ],
                1,
            )

        curve = {}
        for degrees in range(0, 32, 2):
            d, _ = skin.distance(swung(degrees)[moving])
            curve[degrees] = int((d < 0).sum())
        chosen = next(
            (
                a
                for a in curve
                if curve[a] <= 0.002 * moving.sum() or curve.get(a + 4, curve[a]) > 0.85 * curve[a]
            ),
            max(curve),
        )
        warped = swung(chosen)
        swing = {
            "anchor": np.round(anchor, 4).tolist(),
            "vertices": int(moving.sum()),
            "degrees": chosen,
            "inside_by_degrees": sorted(curve.items()),
        }

    # 5. Lift what is still too near the skin, ring by ring, fading over the
    # neighbours.
    rings = warped.reshape(-1, 4, 3).copy()
    lifted_rings, lift_max, inside_before = 0, 0.0, None
    for _ in range(LIFT_ROUNDS):
        d, n = skin.distance(rings.reshape(-1, 3))
        d, n = d.reshape(-1, 4), n.reshape(-1, 4, 3)
        if inside_before is None:
            inside_before = int((d < 0).sum())
        need = np.maximum(clearance - d, 0).max(1)
        if not (need > 2e-4).any():
            break
        lifted_rings = max(lifted_rings, int((need > 2e-4).sum()))
        lift_max = max(lift_max, float(need.max()))
        push = need[:, None] * n.mean(1)
        total = np.zeros_like(push)
        for first, count in ranges:
            for k in np.flatnonzero(need[first : first + count] > 2e-4):
                size = np.linalg.norm(push[first + k])
                for s, w in enumerate(LIFT_SPREAD):
                    j = k + s - len(LIFT_SPREAD) // 2
                    if 0 <= j < count and np.linalg.norm(total[first + j]) < w * size:
                        total[first + j] = w * push[first + k]
        rings += total[:, None, :]
    fitted = rings.reshape(-1, 3)

    d_after, _ = skin.distance(fitted)
    real = Skin(vertices, triangles)
    near_ears = np.linalg.norm(fitted - centre, axis=1) < 1.6 * axes.max()
    in_real = int(real.contains(fitted[near_ears]).sum())
    root_gap = np.array([d_after[first * 4 : first * 4 + 4].mean() for first, _ in ranges])[on_head]

    # The hair on the body's skin: the Head joint, a braid partly the Chest.
    head_joint = body.names.index("Head")
    chest_joint = body.names.index("Chest")
    chest = np.zeros(len(fitted))
    if braid and anchor is not None:
        follow = np.clip((anchor[1] - fitted[:, 1]) / BRAID_FOLLOW, 0, 1)
        behind = np.clip((centre[2] + 0.02 - fitted[:, 2]) / 0.04, 0, 1)
        chest = smoothstep(follow) * smoothstep(behind)
        if swing is not None:
            swing["vertices_following_chest"] = int((chest > 0).sum())
    joint_type = np.uint8 if len(body.names) <= 255 else np.uint16
    joints = np.zeros((len(fitted), 4), joint_type)
    joints[:, 0], joints[:, 1] = head_joint, chest_joint
    weights = np.zeros((len(fitted), 4), np.float32)
    weights[:, 0], weights[:, 1] = 1 - chest, chest
    new_attributes = {
        "POSITION": out.add(fitted.astype(np.float32), _ARRAY_BUFFER),
        "NORMAL": out.add(vertex_normals(fitted, faces).astype(np.float32), _ARRAY_BUFFER),
        "TEXCOORD_0": out.add(uv.astype(np.float32), _ARRAY_BUFFER),
        "JOINTS_0": out.add(joints, _ARRAY_BUFFER),
        "WEIGHTS_0": out.add(weights, _ARRAY_BUFFER),
    }
    if "COLOR_0" in attributes:
        colour = hair_glb.accessor(attributes["COLOR_0"])
        new_attributes["COLOR_0"] = out.add(
            colour, _ARRAY_BUFFER, normalized=hair_glb.normalized(attributes["COLOR_0"])
        )
    material: dict[str, Any] = {
        "name": "hair",
        "pbrMetallicRoughness": {"metallicFactor": 0.0, "roughnessFactor": 0.75},
        "doubleSided": True,
    }
    if "material" in primitive:
        source = hair_glb.doc["materials"][primitive["material"]]
        pbr = source.get("pbrMetallicRoughness", {})
        material["pbrMetallicRoughness"] = {
            k: pbr[k] for k in ("baseColorFactor", "metallicFactor", "roughnessFactor") if k in pbr
        }
        material["doubleSided"] = source.get("doubleSided", True)
    doc = out.doc
    doc.setdefault("materials", []).append(material)
    doc.setdefault("meshes", []).append(
        {
            "name": "hair",
            "primitives": [
                {
                    "attributes": new_attributes,
                    "indices": out.add(faces.reshape(-1).astype(np.uint32), _ELEMENT_ARRAY_BUFFER),
                    "material": len(doc["materials"]) - 1,
                    "mode": 4,
                }
            ],
        }
    )
    doc["nodes"].append({"name": "hair", "mesh": len(doc["meshes"]) - 1, "skin": body.skin})
    doc["scenes"][doc.get("scene", 0)]["nodes"].append(len(doc["nodes"]) - 1)
    target.write_bytes(out.to_bytes())

    shape = card_shape_change(fitted, mapped, ranges)
    moved = np.linalg.norm(fitted - mapped, axis=1)

    def mm(x: float) -> float:
        return round(float(x) * 1000, 1)

    print(
        "MESH_REPORT "
        + json.dumps(
            {
                "cards": len(ranges),
                "cards_on_head": int(on_head.sum()),
                "hair_vertices": len(fitted),
                "hair_triangles": len(faces),
                "proxy_in_file": "head_proxy" in names,
                "scalp_ellipsoid": {
                    "centre": np.round(centre, 4).tolist(),
                    "semi_axes_mm": [mm(a) for a in axes],
                    "rms_mm": round(rms * 1000, 2),
                },
                "scale_from_hairdeck": np.round(axes / proxy_axes, 3).tolist(),
                "shell_t": {
                    "min": round(float(np.nanmin(shell)), 3),
                    "median": round(float(np.nanmedian(shell)), 3),
                    "max": round(float(np.nanmax(shell)), 3),
                },
                "thickness": {
                    "factor": thickness,
                    "height_mm_before": {
                        "p50": mm(np.percentile(height[near], 50)),
                        "p95": mm(np.percentile(height[near], 95)),
                    },
                    "height_mm_after": {
                        "p50": mm(np.percentile(squeezed[near], 50)),
                        "p95": mm(np.percentile(squeezed[near], 95)),
                    },
                },
                "clearance_mm": mm(clearance),
                "braid": swing,
                "skull_rays": int(valid.sum()),
                "delta_mm": {
                    "min": mm(delta.min()),
                    "median": mm(np.median(delta)),
                    "max": mm(delta.max()),
                },
                "vertices_inside_before_lift": inside_before,
                "rings_lifted": lifted_rings,
                "lift_max_mm": mm(lift_max),
                "vertices_inside_after": int((d_after < 0).sum()),
                "nearest_after_mm": round(float(d_after.min()) * 1000, 2),
                "vertices_in_real_ears": in_real,
                "root_gap_mm": {
                    "p5": mm(np.percentile(root_gap, 5)),
                    "median": mm(np.median(root_gap)),
                    "p95": mm(np.percentile(root_gap, 95)),
                },
                "moved_from_ellipsoid_fit_mm": {
                    "median": mm(np.median(moved)),
                    "max": mm(moved.max()),
                },
                "card_shape_change": {
                    "p95": round(float(np.percentile(shape, 95)), 4),
                    "max": round(float(shape.max()), 4),
                },
            }
        ),
        flush=True,
    )


def main() -> None:
    args = sys.argv[sys.argv.index("--") + 1 :]
    if len(args) != 4:
        invalid("expected BODY.glb HAIR.glb OUT.glb OPTIONS")
    fit(Path(args[0]), Path(args[1]), Path(args[2]), json.loads(args[3]))


if __name__ == "__main__":
    main()
