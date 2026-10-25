"""Native SOMA packages (GEM-X) <-> Kimodo-SOMA keys; no GPU, weights or Kimodo."""

import json

import numpy as np
import pytest
from kimodo_model_server import soma
from kimodo_model_server.server import SOMA_KEYFRAMES_TASK, KimodoServer

from model_server_sdk import create_app

# A small SOMA-like body: Hips, both legs and a head; parents before children.
NAMES = [
    "Hips", "LeftLeg", "LeftShin", "LeftFoot", "LeftToeBase",
    "RightLeg", "RightShin", "RightFoot", "RightToeBase", "Head",
]  # fmt: skip
PARENTS = [-1, 0, 1, 2, 3, 0, 5, 6, 7, 0]
KIMODO_REST = np.array(
    [
        [0, 0, 0], [0.1, -0.05, 0], [0.1, -0.5, 0], [0.1, -0.9, 0], [0.1, -0.95, 0.12],
        [-0.1, -0.05, 0], [-0.1, -0.5, 0], [-0.1, -0.9, 0], [-0.1, -0.95, 0.12], [0, 0.6, 0],
    ]
)  # fmt: skip


def person(scale: float, yaw: float, at: tuple[float, float]) -> np.ndarray:
    """Kimodo's rest body standing on y = 0, scaled, turned about +Y and moved on the floor."""
    body = KIMODO_REST * scale
    body[:, 1] -= body[:, 1].min()
    c, s = np.cos(yaw), np.sin(yaw)
    turn = np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return body @ turn.T + np.array([at[0], 0, at[1]])


def gemx(frames: list[np.ndarray]) -> bytes:
    return json.dumps(
        {
            "format": soma.FORMAT,
            "joint_names": ["Root", *NAMES],
            "world": {"joints": [f.tolist() for f in frames]},
        }
    ).encode()


SKELETON = soma.Skeleton(NAMES, PARENTS, KIMODO_REST)


def native_frame(to: dict) -> soma.Frame:
    return soma.Frame(np.array(to["origin"]), to["yaw"], to["scale"])


def test_a_pose_lands_on_kimodos_body_facing_plus_z_at_the_origin() -> None:
    clip = soma.read_clip(gemx([person(1.2, 0.7, (2.0, -1.0))]))
    frame = soma.canonical_frame(clip, 0, SKELETON)
    assert frame.scale == pytest.approx(1 / 1.2)
    key = soma.onto(SKELETON, clip.joints[0], clip.names, frame)
    # Kimodo's own lengths and the source directions: its rest body, standing on the floor
    expected = KIMODO_REST - [0, KIMODO_REST[:, 1].min(), 0]
    assert np.allclose(key, expected, atol=1e-6)
    assert soma.heading(key, NAMES) == pytest.approx(0, abs=1e-6)


def test_the_result_returns_to_the_source_world_and_size() -> None:
    source = person(1.2, 0.7, (2.0, -1.0))
    clip = soma.read_clip(gemx([source]))
    frame = soma.canonical_frame(clip, 0, SKELETON)
    key = soma.keys(clip, [0], SKELETON, frame)
    turns = soma.global_rotations(SKELETON, key[0])[None]
    data = soma.package(key, NAMES, PARENTS, frame, "test", {}, rotations=turns)
    back = soma.read_clip(data)
    assert np.allclose(back.joints[0], source, atol=1e-5)
    package = json.loads(data)
    # Kimodo's own frame for its skin, and the way back to the world
    kimodo = package["kimodo"]
    assert np.allclose(kimodo["canonical"]["joints"], key, atol=1e-6)
    to = kimodo["to_world"]
    assert np.allclose(
        native_frame(to).from_canonical(np.array(kimodo["canonical"]["joints"])), source, atol=1e-5
    )
    # the rig, as GEM-X's: Root first and its own parent; Hips under it
    assert package["rig"]["joint_parent_ids"][:3] == [0, 0, 1]


def test_other_packages_are_refused() -> None:
    with pytest.raises(ValueError, match="soma-motion"):
        soma.read_clip(json.dumps({"format": "studio.smplx-motion.v1"}).encode())
    with pytest.raises(ValueError, match="world joints"):
        soma.read_clip(
            json.dumps(
                {"format": soma.FORMAT, "joint_names": NAMES, "world": {"joints": [[[0, 0]]]}}
            ).encode()
        )


def test_soma_variant_declares_the_key_frame_task() -> None:
    server = KimodoServer()
    create_app(server)
    assert [t.task for t in server.tasks] == ["text-to-motion", "keyframes-to-soma-motion"]
    assert SOMA_KEYFRAMES_TASK.output_mime == ("application/vnd.studio.soma-motion+json",)
    assert SOMA_KEYFRAMES_TASK.inputs[0].mime == ["application/vnd.studio.soma-motion+json"]


def test_a_loop_ends_on_its_first_key() -> None:
    from kimodo_model_server.server import KeyframesParams, key_frames

    chosen, at, total = key_frames(KeyframesParams(frames="1", seconds=4, loop=True), 1)
    assert (chosen, at, total) == ([0, 0], [0, 119], 120)
    assert key_frames(KeyframesParams(frames="1,31", seconds=4), 40) == ([0, 30], [0, 30], 120)


def test_key_rotations_give_the_key_back() -> None:
    """A pose the body can take comes back exactly (one the rest offsets cannot make, such as
    a head turned apart from the hips it shares a joint with here, gets the best rigid fit)."""
    turned = person(1.2, 0.7, (2.0, -1.0))
    turned[2] = turned[1] + [0.3, -0.3, 0.0]  # left thigh raised forward and out
    turned[3] = turned[2] + [0.0, -0.4, 0.0]
    turned[4] = turned[3] + [0.0, -0.05, 0.12]
    clip = soma.read_clip(gemx([turned]))
    frame = soma.canonical_frame(clip, 0, SKELETON)
    key = soma.onto(SKELETON, clip.joints[0], clip.names, frame)
    turns = soma.global_rotations(SKELETON, key)
    assert np.allclose(np.einsum("jab,jcb->jac", turns, turns), np.eye(3), atol=1e-6)
    back = np.zeros_like(key)
    for joint, parent in enumerate(PARENTS):
        back[joint] = (
            key[0]
            if parent < 0
            else (back[parent] + turns[parent] @ (KIMODO_REST[joint] - KIMODO_REST[parent]))
        )
    assert np.allclose(back, key, atol=1e-6)


def test_a_package_with_world_rotations_keys_them() -> None:
    source = person(1.2, 0.7, (2.0, -1.0))
    clip = soma.read_clip(gemx([source]))
    frame = soma.canonical_frame(clip, 0, SKELETON)
    key = soma.keys(clip, [0], SKELETON, frame)
    turns = soma.global_rotations(SKELETON, key[0])[None]
    data = soma.package(key, NAMES, PARENTS, frame, "test", {}, rotations=turns)
    again = soma.read_clip(data)  # as a fitted pose comes in: world joints and rotations
    assert again.rotations is not None
    keyed = soma.key_rotations(again, [0], SKELETON, soma.canonical_frame(again, 0, SKELETON), key)
    assert np.allclose(keyed, turns, atol=1e-5)


def test_holding_is_declared_for_soma_keys() -> None:
    from kimodo_model_server.server import SomaKeyframesParams

    params = SomaKeyframesParams(frames="1", seconds=2, loop=True, hold_hands=True, hold_feet=True)
    assert params.hold_keys == 12
