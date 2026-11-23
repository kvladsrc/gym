"""The heads the animation is shown on, read from NVIDIA's Maya scenes.

Maya-ACE (MIT) ships a scene for each Audio2Face-3D actor with the head the
networks were trained on: the skin, whose vertices are those of the actor's
blendshape data in the same order, the upper and lower teeth, the tongue and
the eyes, in centimetres, Y up, facing +Z. The scenes are Maya ASCII; only
geometry is read from them: vertex positions with their tweaks, edges, faces
and the transforms above the meshes.

The scenes were saved with the last frame of an animation on the lower jaw
(its offset parent matrix) and on the eyes (their rotation). Those are what
the animation drives, so the rest pose leaves them out.
"""

import hashlib
import re
from collections.abc import Iterator
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np

# The meshes of a head, by their shape node's name, the same in every scene.
SKIN = "c_headWatertight_hiShape"
UPPER_TEETH = "c_topDenture_hiShape"
LOWER_TEETH = "c_bottomDenture_hiShape"
TONGUE = "c_tongue_hiShape"
# Right, then left: the character's own sides, the order of the SDK's eye output.
EYES = (
    ("r_eye_grp_hi", "r_choroid_hiShape", "r_lens_hiShape"),
    ("l_eye_grp_hi", "l_choroid_hiShape", "l_lens_hiShape"),
)
# Saved animation, left out of the rest pose: (node, attribute).
ANIMATED = frozenset(
    {("c_bottomDenture_grp_hi", "opm"), ("r_eye_grp_hi", "r"), ("l_eye_grp_hi", "r")}
)

# Bumped when what read_head returns changes, so cached heads are read again.
CACHE_VERSION = 1

_STATEMENT_END = re.compile(r'"(?:[^"\\]|\\.)*"|;')
_TOKEN = re.compile(r'"((?:[^"\\]|\\.)*)"|(\S+)')
_ATTRIBUTE = re.compile(r"\.(\w+)(?:\[(\d+)(?::(\d+))?\])?")
# setAttr's flags that take a value (-av, -alteredValue, takes none).
_VALUE_FLAGS = frozenset(
    {"-s", "-k", "-l", "-ch", "-cb", "-ca", "-c", "-size", "-keyable", "-lock", "-channelBox"}
)
# Transform attributes that move what is under them, as far as read here.
_TRANSFORM = frozenset({"t", "r", "s", "rp", "sp", "rpt", "spt", "ra", "sh", "ro", "opm"})


@dataclass(frozen=True)
class Part:
    """A triangle mesh in the head's space: centimetres, Y up, facing +Z."""

    positions: np.ndarray  # (V, 3) float32
    triangles: np.ndarray  # (T, 3) uint32, counter-clockwise from outside


@dataclass(frozen=True)
class Eye:
    pivot: np.ndarray  # (3,) float32: the point the eye turns about
    ball: Part  # the choroid: sclera, iris and pupil
    lens: Part


@dataclass(frozen=True)
class Head:
    skin: Part
    upper_teeth: Part
    lower_teeth: Part
    tongue: Part
    eyes: tuple[Eye, Eye]  # right, left


@dataclass
class _Value:
    start: int | None
    end: int | None
    kind: str | None
    text: str


@dataclass
class _Node:
    kind: str
    name: str
    parent: str | None
    attributes: dict[str, list[_Value]] = field(default_factory=dict[str, list[_Value]])


def _statements(text: str) -> Iterator[str]:
    """MEL statements: up to each semicolon outside a string."""
    start = 0
    for match in _STATEMENT_END.finditer(text):
        if match.group() == ";":
            yield text[start : match.start()].strip()
            start = match.end()


def _create_node(statement: str) -> _Node:
    tokens = [
        match.group(1) if match.group(1) is not None else match.group(2)
        for match in _TOKEN.finditer(statement)
    ]
    kind, name, parent = tokens[1], None, None
    for index, token in enumerate(tokens[2:-1], start=2):
        if token in ("-n", "-name"):
            name = tokens[index + 1]
        elif token in ("-p", "-parent"):
            parent = tokens[index + 1].rsplit("|", 1)[-1]
    if name is None:
        raise ValueError(f"a node without a name: {statement[:80]}")
    return _Node(kind, name, parent)


def _set_attribute(node: _Node, statement: str) -> None:
    tokens = _TOKEN.finditer(statement, len("setAttr"))
    name = kind = None
    text = ""
    for token in tokens:
        quoted, word = token.group(1), token.group(2)
        if word == "-type":
            kind = next(tokens).group(1)
        elif name is None and word is not None and word.startswith("-"):
            if word in _VALUE_FLAGS:
                next(tokens)
        elif name is None and quoted is not None:
            name = quoted
        else:
            text = statement[token.start() :]
            break
    match = _ATTRIBUTE.fullmatch(name or "")
    if match is None:  # compound attributes (UV and colour sets) are not read
        return
    attribute, start, end = match.groups()
    first = int(start) if start is not None else None
    last = int(end) if end is not None else first
    node.attributes.setdefault(attribute, []).append(_Value(first, last, kind, text))


def read_scene(path: Path) -> dict[str, _Node]:
    """The nodes of a Maya ASCII scene by name, with the attributes it sets."""
    nodes: dict[str, _Node] = {}
    current: _Node | None = None
    for statement in _statements(path.read_text(encoding="utf-8", errors="replace")):
        if statement.startswith("createNode "):
            current = _create_node(statement)
            if current.name in nodes:
                raise ValueError(f"{path.name}: two nodes named {current.name}")
            nodes[current.name] = current
        elif statement.startswith("setAttr "):
            if current is not None:
                _set_attribute(current, statement)
        elif statement.startswith("currentUnit "):
            words = statement.split()
            units = dict(zip(words[1::2], words[2::2], strict=False))
            if (
                units.get("-l", "centimeter") != "centimeter"
                or units.get("-a", "degree") != "degree"
            ):
                raise ValueError(f"{path.name}: units {units}, not centimetres and degrees")
        elif statement.startswith("parent "):
            raise ValueError(f"{path.name}: nodes moved by `parent` are not supported")
        elif statement.startswith(("select ", "connectAttr ", "relationship ", "disconnectAttr ")):
            current = None  # later setAttr are about another node (rename and addAttr are not)
    return nodes


def _rows(
    node: _Node, attribute: str, width: int, dtype: type, count: int | None = None
) -> np.ndarray:
    """An array attribute (.vt, .pt, .ed) as rows of ``width``; rows it does
    not set are zero, or, without ``count``, an error."""
    values = [
        value
        for value in node.attributes.get(attribute, [])
        if value.start is not None and value.text
    ]
    if count is None:
        count = max((value.end + 1 for value in values if value.end is not None), default=0)
    rows = np.zeros((count, width), dtype=dtype)
    seen = np.zeros(count, dtype=bool)
    for value in values:
        assert value.start is not None
        assert value.end is not None
        data = np.array(value.text.split(), dtype=dtype)
        length = value.end - value.start + 1
        if data.size != length * width:
            raise ValueError(
                f"{node.name}.{attribute}[{value.start}:{value.end}]: {data.size} numbers"
            )
        rows[value.start : value.end + 1] = data.reshape(length, width)
        seen[value.start : value.end + 1] = True
    if attribute in ("vt", "ed") and not seen.all():
        raise ValueError(f"{node.name}.{attribute}: {int((~seen).sum())} of {count} not set")
    return rows


def _triangles(node: _Node, edges: np.ndarray) -> np.ndarray:
    """The faces (polyFaces: a loop of edges each, a negative index -k-1
    being edge k reversed), as fans of triangles."""
    triangles: list[tuple[int, int, int]] = []
    for value in sorted(node.attributes.get("fc", []), key=lambda value: value.start or 0):
        tokens = value.text.split()
        index = 0
        while index < len(tokens):
            key = tokens[index]
            if key == "f":
                count = int(tokens[index + 1])
                loop = [
                    int(edges[edge, 0]) if edge >= 0 else int(edges[-edge - 1, 1])
                    for edge in map(int, tokens[index + 2 : index + 2 + count])
                ]
                triangles.extend(
                    (loop[0], loop[corner], loop[corner + 1]) for corner in range(1, count - 1)
                )
                index += 2 + count
            elif key in ("mu", "mc"):  # UV or colour indices: set, count, indices
                index += 3 + int(tokens[index + 2])
            elif key == "fc":
                index += 2 + int(tokens[index + 1])
            elif key == "h":
                raise ValueError(f"{node.name}: faces with holes are not supported")
            else:
                raise ValueError(f"{node.name}: unknown polyFaces entry {key!r}")
    return np.asarray(triangles, dtype=np.uint32).reshape(-1, 3)


def _vector(node: _Node, attribute: str, default: float) -> np.ndarray:
    values = node.attributes.get(attribute)
    if not values:
        return np.full(3, default)
    return np.array(values[-1].text.split(), dtype=np.float64)[:3]


def _translation(offset: np.ndarray) -> np.ndarray:
    matrix = np.eye(4)
    matrix[:3, 3] = offset
    return matrix


def euler_xyz(degrees: np.ndarray) -> np.ndarray:
    """Maya's rotation in the default order (X, then Y, then Z), as a 3x3
    matrix for column vectors."""
    x, y, z = np.radians(np.asarray(degrees, dtype=np.float64))
    cx, sx, cy, sy, cz, sz = np.cos(x), np.sin(x), np.cos(y), np.sin(y), np.cos(z), np.sin(z)
    rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return rz @ ry @ rx


def _local(node: _Node) -> np.ndarray:
    """A transform node's matrix (column vectors), with its offset parent
    matrix: Maya's T·RT·RP·R·RA·RP⁻¹·ST·SP·S·SP⁻¹, without shear."""
    present = {
        name for name in node.attributes if name in _TRANSFORM and (node.name, name) not in ANIMATED
    }
    rotated = bool({"r", "ra"} & present)
    if rotated and "ro" in present and int(float(node.attributes["ro"][-1].text.split()[0])) != 0:
        raise ValueError(f"{node.name}: only the XYZ rotation order is supported")
    if "sh" in present and np.any(_vector(node, "sh", 0.0) != 0):
        raise ValueError(f"{node.name}: shear is not supported")

    def get(name: str, default: float) -> np.ndarray:
        return _vector(node, name, default) if name in present else np.full(3, default)

    rotation, axis = np.eye(4), np.eye(4)
    rotation[:3, :3] = euler_xyz(get("r", 0.0))
    axis[:3, :3] = euler_xyz(get("ra", 0.0))
    scale = np.diag([*get("s", 1.0), 1.0])
    pivot, scale_pivot = get("rp", 0.0), get("sp", 0.0)
    matrix = (
        _translation(get("t", 0.0))
        @ _translation(get("rpt", 0.0))
        @ _translation(pivot)
        @ rotation
        @ axis
        @ _translation(-pivot)
        @ _translation(get("spt", 0.0))
        @ _translation(scale_pivot)
        @ scale
        @ _translation(-scale_pivot)
    )
    if "opm" in present:
        # Maya writes matrices for row vectors, translation in the last row.
        matrix = (
            np.array(node.attributes["opm"][-1].text.split(), dtype=np.float64).reshape(4, 4).T
            @ matrix
        )
    return matrix


def _world(nodes: dict[str, _Node], name: str | None) -> np.ndarray:
    matrix = np.eye(4)
    while name is not None:
        node = nodes[name]
        matrix = _local(node) @ matrix
        name = node.parent
    return matrix


def _part(nodes: dict[str, _Node], shape: str) -> Part:
    node = nodes.get(shape)
    if node is None or node.kind != "mesh":
        raise ValueError(f"no mesh named {shape}")
    positions = _rows(node, "vt", 3, np.float64)
    positions += _rows(node, "pt", 3, np.float64, count=len(positions))
    edges = _rows(node, "ed", 3, np.int64)
    triangles = _triangles(node, edges)
    if not len(triangles) or int(triangles.max()) >= len(positions):
        raise ValueError(f"{shape}: faces do not match its {len(positions)} vertices")
    world = _world(nodes, node.parent)
    placed = positions @ world[:3, :3].T + world[:3, 3]
    return Part(placed.astype(np.float32), triangles)


def head_from_scene(nodes: dict[str, _Node]) -> Head:
    eyes = []
    for group, ball, lens in EYES:
        node = nodes[group]
        pivot = _world(nodes, node.parent) @ np.array([*_vector(node, "rp", 0.0), 1.0])
        eyes.append(Eye(pivot[:3].astype(np.float32), _part(nodes, ball), _part(nodes, lens)))
    return Head(
        skin=_part(nodes, SKIN),
        upper_teeth=_part(nodes, UPPER_TEETH),
        lower_teeth=_part(nodes, LOWER_TEETH),
        tongue=_part(nodes, TONGUE),
        eyes=(eyes[0], eyes[1]),
    )


_PARTS = ("skin", "upper_teeth", "lower_teeth", "tongue")


def _flatten(head: Head) -> dict[str, np.ndarray]:
    arrays: dict[str, np.ndarray] = {}
    for name in _PARTS:
        part: Part = getattr(head, name)
        arrays[f"{name}.positions"], arrays[f"{name}.triangles"] = part.positions, part.triangles
    for side, eye in zip("rl", head.eyes, strict=True):
        arrays[f"{side}.pivot"] = eye.pivot
        for name in ("ball", "lens"):
            part = getattr(eye, name)
            arrays[f"{side}.{name}.positions"], arrays[f"{side}.{name}.triangles"] = (
                part.positions,
                part.triangles,
            )
    return arrays


def _unflatten(arrays: dict[str, np.ndarray]) -> Head:
    def part(prefix: str) -> Part:
        return Part(arrays[f"{prefix}.positions"], arrays[f"{prefix}.triangles"])

    eyes = tuple(
        Eye(arrays[f"{side}.pivot"], part(f"{side}.ball"), part(f"{side}.lens")) for side in "rl"
    )
    return Head(*(part(name) for name in _PARTS), eyes=(eyes[0], eyes[1]))


def read_head(scene: Path, cache: Path | None = None) -> Head:
    """The head of a Maya-ACE scene; with ``cache``, kept there as .npz,
    keyed by the scene's content (reading a 70 MB scene takes a while)."""
    digest = hashlib.sha256(scene.read_bytes()).hexdigest()
    if cache is not None and cache.is_file():
        with np.load(cache) as stored:
            if str(stored["source"]) == digest and int(stored["version"]) == CACHE_VERSION:
                return _unflatten({key: stored[key] for key in stored.files})
    head = head_from_scene(read_scene(scene))
    if cache is not None:
        cache.parent.mkdir(parents=True, exist_ok=True)
        partial = cache.with_name(cache.name + ".part.npz")
        np.savez(
            partial, source=np.array(digest), version=np.array(CACHE_VERSION), **_flatten(head)
        )
        partial.replace(cache)
    return head
