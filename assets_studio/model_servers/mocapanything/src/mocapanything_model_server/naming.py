"""Readable joint names for a quadruped skeleton from its shape.

MoCapAnything reads each joint's name (T5 embeddings of the name) to tell
what the joint is. SkinTokens and UniRig name their bones bone_0, bone_1...,
which say nothing, and the pipeline strips trailing digits, so all would be
"bone". The names here follow the zoo1030 training data ("quadruped leg left
01"): the part is found from the skeleton's shape.

- Legs: the chains that end near the ground.
- Spine: from the root to the head. The head is the end of the chain the
  front legs branch from; the other chain from the root without legs is the
  tail.
- Front and hind: where a leg branches, toward the head or not; left and
  right: the side of the head-to-tail axis it stands on.

Axes: Z up, as Blender has them.
"""

import re
from collections.abc import Mapping, Sequence
from dataclasses import dataclass

import numpy as np

# Leg chains as the zoo1030 horse names them, from the body down.
FRONT_LEG = ("shoulder", "arm 01", "forearm 01", "hand 01", "finger 01")
HIND_LEG = ("leg 01", "leg 02", "leg 03", "foot 01", "toe 01")
# A chain ends at a leg's foot when its end is this low (share of the height).
GROUND_SHARE = 0.15
PREFIX = "quadruped"


@dataclass(frozen=True)
class Bone:
    name: str
    parent: str | None
    head: tuple[float, float, float]
    tail: tuple[float, float, float]


class NamingError(ValueError):
    """The skeleton does not look like a quadruped's."""


def _chain_to(bones: Mapping[str, Bone], name: str) -> list[str]:
    """Bones from the root down to ``name``."""
    chain: list[str] = []
    current: str | None = name
    while current is not None:
        chain.append(current)
        current = bones[current].parent
    return chain[::-1]


def _lateral(skeleton: Sequence[Bone], chain: Sequence[str]) -> float:
    """How far to the side of the body's long axis (seen from above) a chain lies."""
    heads = np.array([bone.head[:2] for bone in skeleton])
    centre = heads.mean(0)
    long_axis = np.linalg.svd(heads - centre)[2][0]
    side = np.array([-long_axis[1], long_axis[0]])
    by_name = {bone.name: bone for bone in skeleton}
    middle = np.mean([by_name[name].head[:2] for name in chain], axis=0)
    return float(np.dot(middle - centre, side))


def name_quadruped(skeleton: Sequence[Bone]) -> dict[str, str]:
    """{bone name: readable name} for every bone."""
    bones = {bone.name: bone for bone in skeleton}
    children: dict[str, list[str]] = {name: [] for name in bones}
    roots = []
    for bone in skeleton:
        if bone.parent is None:
            roots.append(bone.name)
        else:
            children[bone.parent].append(bone.name)
    if len(roots) != 1:
        raise NamingError(f"expected one root bone, found {len(roots)}")
    leaves = [name for name, below in children.items() if not below]
    low = min(bone.tail[2] for bone in skeleton)
    high = max(max(bone.head[2], bone.tail[2]) for bone in skeleton)
    near_ground = low + GROUND_SHARE * (high - low)

    def branch(leaf: str) -> list[str]:
        """The leaf's own chain, up to (not including) the nearest fork."""
        chain = [leaf]
        while True:
            parent = bones[chain[-1]].parent
            if parent is None or len(children[parent]) > 1:
                return chain[::-1]
            chain.append(parent)

    legs = [leaf for leaf in leaves if bones[leaf].tail[2] <= near_ground]
    if len(legs) > 4:
        # A long tail reaches the ground too (UniRig's horse): the legs are
        # the four chains farthest to the sides of the body's long axis, the
        # tail hangs in the middle.
        legs = sorted(legs, key=lambda leaf: -abs(_lateral(skeleton, branch(leaf))))[:4]
    if len(legs) != 4:
        raise NamingError(f"expected 4 legs reaching the ground, found {len(legs)}")
    leg_chains = {leaf: branch(leaf) for leaf in legs}
    leg_bones = {name for chain in leg_chains.values() for name in chain}
    # Where each leg joins the body.
    hips = {leaf: bones[chain[0]].parent for leaf, chain in leg_chains.items()}
    others = [leaf for leaf in leaves if leaf not in legs]

    # The head: the non-leg end whose path passes the most leg forks, farthest.
    def score(leaf: str) -> tuple[int, int]:
        path = set(_chain_to(bones, leaf))
        return sum(hip in path for hip in hips.values()), len(path)

    if not others:
        raise NamingError("no head: every chain ends at the ground")
    head = max(others, key=score)
    spine = [name for name in _chain_to(bones, head) if name not in leg_bones]
    root = spine[0]
    forward = np.subtract(bones[head].tail, bones[root].head)[:2]
    if not np.linalg.norm(forward):
        raise NamingError("the head is straight above the root")
    forward = forward / np.linalg.norm(forward)
    left = np.array([-forward[1], forward[0]])  # Z up: left = up × forward

    names = {root: f"{PREFIX} pelvis"}
    # The spine up to the last leg fork, then the neck, then the head.
    fork = max(spine.index(hip) for hip in hips.values() if hip in spine)
    names.update({name: f"{PREFIX} spine {i:02d}" for i, name in enumerate(spine[1 : fork + 1], 1)})
    neck = spine[fork + 1 : -1]
    names.update({name: f"{PREFIX} neck {i:02d}" for i, name in enumerate(neck, 1)})
    names[head] = f"{PREFIX} head"

    # The two legs that join the body nearest the head are the front ones.
    along = {
        leaf: float(np.dot(np.subtract(bones[chain[0]].head[:2], bones[root].head[:2]), forward))
        for leaf, chain in leg_chains.items()
    }
    front_legs = sorted(along, key=along.__getitem__)[2:]
    for leaf, chain in leg_chains.items():
        front = leaf in front_legs
        # The mean of the leg's joints: a rigger's leaf tails point anywhere.
        middle = np.mean([bones[name].head[:2] for name in chain], axis=0)
        lateral = np.dot(middle - np.asarray(bones[root].head[:2]), left)
        side = "left" if lateral > 0 else "right"
        parts = FRONT_LEG if front else HIND_LEG
        for i, name in enumerate(chain):
            part = parts[min(i, len(parts) - 1)]
            word, _, number = part.partition(" ")
            names[name] = f"{PREFIX} {word} {side}" + (f" {number}" if number else "")

    # Everything else: chains off the root away from the head are the tail;
    # the rest (ears, jaw) are named by where they hang.
    for leaf in others:
        if leaf == head:
            continue
        chain = [name for name in branch(leaf) if name not in names]
        start = bones[chain[0]].head[:2] if chain else None
        toward_head = (
            start is not None and np.dot(np.subtract(start, bones[root].head[:2]), forward) > 0
        )
        label = "head part" if toward_head else "tail"
        names.update({name: f"{PREFIX} {label} {i:02d}" for i, name in enumerate(chain, 1)})
    for name in bones:
        names.setdefault(name, f"{PREFIX} body")
    return names


def part(names: Mapping[str, str], suffix: str) -> str:
    """The bone named ``PREFIX suffix`` (e.g. "pelvis", "head")."""
    return next(bone for bone, name in names.items() if name == f"{PREFIX} {suffix}")


# Bone names that say nothing (SkinTokens, UniRig): the rig is read by shape.
GENERIC_NAME = re.compile(r"^bone_\d+$")


@dataclass(frozen=True)
class RigReading:
    """What the pipeline needs to know about a rig."""

    # Readable joint names for T5, or None: the rig's own names will do
    # (Mixamo's "mixamorig:LeftUpLeg"; upstream cleans those itself).
    names: dict[str, str] | None
    # How to find the way it faces: ("axis", from, to) horizontally from one
    # bone to the other (a quadruped's pelvis to head), or ("sides", left,
    # right): square to the line between a left and a right bone (a biped's
    # thighs; its head is above its pelvis).
    facing: tuple[str, str, str]


def read_rig(skeleton: Sequence[Bone]) -> RigReading:
    if all(GENERIC_NAME.match(bone.name) for bone in skeleton):
        names = name_quadruped(skeleton)
        return RigReading(names, ("axis", part(names, "pelvis"), part(names, "head")))
    left, right = side_pair(skeleton)
    return RigReading(None, ("sides", left, right))


def side_pair(skeleton: Sequence[Bone]) -> tuple[str, str]:
    """A left and a right bone named alike, the upper legs if there are any."""
    names = {bone.name for bone in skeleton}
    pairs = []
    for name in names:
        position = name.lower().find("left")
        if position < 0:
            continue
        right = (
            name[:position]
            + ("Right" if name[position].isupper() else "right")
            + name[position + 4 :]
        )
        if right in names:
            pairs.append((name, right))
    if not pairs:
        raise NamingError("bones are neither bone_N nor named Left…/Right…")
    legs = [pair for pair in pairs if any(word in pair[0].lower() for word in ("upleg", "thigh"))]
    return min(legs or pairs, key=lambda pair: (len(pair[0]), pair[0]))


def forward_of(skeleton: Sequence[Bone], facing: tuple[str, str, str]) -> np.ndarray:
    """The horizontal way the rig faces, in world space (Z up), unit length."""
    kind, first, second = facing
    bones = {bone.name: bone for bone in skeleton}
    if kind == "axis":
        forward = np.subtract(bones[second].tail, bones[first].head)[:2]
    else:
        left = np.subtract(bones[first].head, bones[second].head)[:2]
        forward = np.array([left[1], -left[0]])  # left × up
    return forward / np.linalg.norm(forward)


def front_in_rig_space(skeleton: Sequence[Bone], facing: tuple[str, str, str]) -> np.ndarray:
    """The way the rig faces in the armature's space after prepare_rig (Y up:
    world (x, y, z) is (x, z, -y) there), for the pipeline's front.npy. Its
    own guess turned one of our horses almost backwards."""
    forward = forward_of(skeleton, facing)
    return np.array([forward[0], 0.0, -forward[1]])
