"""Declarations, joint naming and the job's plumbing; no GPU, weights or Blender."""

from pathlib import Path

import numpy as np
import pytest
from mocapanything_model_server.naming import (
    Bone,
    NamingError,
    front_in_rig_space,
    name_quadruped,
    part,
    read_rig,
)
from mocapanything_model_server.pipeline import (
    Models,
    inference_config,
    python_wrapper,
    shadow_source,
    summary_of,
    unpin_rotations,
)
from mocapanything_model_server.server import TASK, MoCapAnythingServer

from model_server_sdk import create_app


def chain(
    names: list[str], parent: str | None, points: list[tuple[float, float, float]]
) -> list[Bone]:
    """Bones from point to point, each the parent of the next."""
    bones = []
    for name, head, tail in zip(names, points, points[1:], strict=False):
        bones.append(Bone(name, parent, head, tail))
        parent = name
    return bones


def horse() -> list[Bone]:
    """A quadruped like SkinTokens rigs one: head toward +X, Z up, left at +Y."""
    spine = chain(
        ["b0", "b1", "b2", "b3", "b4"],
        None,
        [
            (-0.5, 0, 0.2),
            (-0.2, 0, 0.2),
            (0.3, 0, 0.3),
            (0.5, 0, 0.5),
            (0.7, 0, 0.6),
            (0.8, 0, 0.5),
        ],
    )

    def leg(prefix: str, parent: str, x: float, y: float) -> list[Bone]:
        return chain(
            [f"{prefix}{i}" for i in range(3)],
            parent,
            [(x, y, 0.2), (x, y, -0.1), (x, y, -0.4), (x, y, -0.6)],
        )

    return [
        *spine,
        *leg("fl", "b2", 0.3, 0.1),
        *leg("fr", "b2", 0.3, -0.1),
        *leg("hl", "b0", -0.5, 0.1),
        *leg("hr", "b0", -0.5, -0.1),
        *chain(["t0", "t1"], "b0", [(-0.6, 0, 0.3), (-0.8, 0, 0.1), (-0.85, 0, -0.1)]),
    ]


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(MoCapAnythingServer())
    assert TASK.task == "video-to-motion"
    assert [spec.role for spec in TASK.inputs] == ["video", "rig"]
    assert TASK.output_mime == ("model/x-fbx",)


def test_a_quadruped_is_named_by_its_shape() -> None:
    names = name_quadruped(horse())
    assert names["b0"] == "quadruped pelvis"
    assert [names[b] for b in ("b1", "b2")] == ["quadruped spine 01", "quadruped spine 02"]
    assert [names[b] for b in ("b3", "b4")] == ["quadruped neck 01", "quadruped head"]
    assert [names[f"fl{i}"] for i in range(3)] == [
        "quadruped shoulder left",
        "quadruped arm left 01",
        "quadruped forearm left 01",
    ]
    assert names["fr0"] == "quadruped shoulder right"
    assert [names[f"hr{i}"] for i in range(3)] == [
        "quadruped leg right 01",
        "quadruped leg right 02",
        "quadruped leg right 03",
    ]
    assert names["hl0"] == "quadruped leg left 01"
    assert [names["t0"], names["t1"]] == ["quadruped tail 01", "quadruped tail 02"]
    assert len(set(names.values())) == len(names)


def test_a_tail_reaching_the_ground_is_not_a_leg() -> None:
    long_tail = [bone for bone in horse() if not bone.name.startswith("t")]
    long_tail += chain(
        ["t0", "t1", "t2"],
        "b0",
        [(-0.6, 0, 0.3), (-0.8, 0, 0.0), (-0.85, 0, -0.3), (-0.85, 0, -0.6)],
    )
    names = name_quadruped(long_tail)
    assert [names["t0"], names["t2"]] == ["quadruped tail 01", "quadruped tail 03"]
    assert names["hl0"] == "quadruped leg left 01"


def test_a_biped_is_not_named() -> None:
    body = chain(
        ["hips", "chest", "head"], None, [(0, 0, 1.0), (0, 0, 1.3), (0, 0, 1.6), (0, 0, 1.8)]
    )
    legs = [
        *chain(["l0", "l1"], "hips", [(0, 0.1, 1.0), (0, 0.1, 0.5), (0, 0.1, 0.0)]),
        *chain(["r0", "r1"], "hips", [(0, -0.1, 1.0), (0, -0.1, 0.5), (0, -0.1, 0.0)]),
    ]
    with pytest.raises(NamingError):
        name_quadruped([*body, *legs])


def generic(bones: list[Bone]) -> tuple[list[Bone], dict[str, str]]:
    """The skeleton with SkinTokens' names (bone_0, bone_1...), and the renaming."""
    rename = {bone.name: f"bone_{i}" for i, bone in enumerate(bones)}
    return [
        Bone(rename[b.name], rename[b.parent] if b.parent else None, b.head, b.tail) for b in bones
    ], rename


def test_a_rig_of_bone_n_is_read_as_a_quadruped_facing_its_head() -> None:
    skeleton, rename = generic(horse())
    reading = read_rig(skeleton)
    assert reading.names is not None
    assert part(reading.names, "head") == rename["b4"]
    assert reading.facing == ("axis", rename["b0"], rename["b4"])
    front = front_in_rig_space(skeleton, reading.facing)
    assert front[0] > 0.99  # world +X stays +X
    assert front[1] == 0  # Y is up there


def test_a_named_biped_keeps_its_names_and_faces_square_to_its_thighs() -> None:
    # Facing -Y (Blender's front view), left at +X, as Make-It-Animatable rigs stand.
    body = chain(
        ["mixamorig:Hips", "mixamorig:Head"], None, [(0, 0, 1.0), (0, 0, 1.6), (0, 0, 1.8)]
    )
    legs = [
        *chain(["mixamorig:LeftUpLeg"], "mixamorig:Hips", [(0.1, 0, 1.0), (0.1, 0, 0.5)]),
        *chain(["mixamorig:RightUpLeg"], "mixamorig:Hips", [(-0.1, 0, 1.0), (-0.1, 0, 0.5)]),
        *chain(["mixamorig:LeftArm"], "mixamorig:Hips", [(0.2, 0, 1.4), (0.5, 0, 1.4)]),
        *chain(["mixamorig:RightArm"], "mixamorig:Hips", [(-0.2, 0, 1.4), (-0.5, 0, 1.4)]),
    ]
    reading = read_rig([*body, *legs])
    assert reading.names is None
    assert reading.facing == ("sides", "mixamorig:LeftUpLeg", "mixamorig:RightUpLeg")
    front = front_in_rig_space([*body, *legs], reading.facing)
    assert np.allclose(front, [0, 0, 1])  # world -Y is +Z in the rig's own axes


def test_the_shadow_source_links_everything_but_zoo(tmp_path: Path) -> None:
    source = tmp_path / "source"
    (source / "preprocess").mkdir(parents=True)
    (source / "zoo").mkdir()
    (source / "README.md").write_text("x")
    work = tmp_path / "work"
    work.mkdir()
    shadow = shadow_source(source, work)
    assert (shadow / "preprocess").is_symlink()
    assert (shadow / "README.md").read_text() == "x"
    assert (shadow / "zoo").is_dir()
    assert not (shadow / "zoo").is_symlink()
    wrapper = python_wrapper(work, shadow)
    assert f"PYTHONPATH={shadow}" in wrapper.read_text()


def test_the_inference_config_points_at_the_job() -> None:
    template = {"weights": {}, "data": {"wild_mode": True}, "output": {"fps": 15}}
    models = Models(Path("/w"), Path("/r"), Path("/d"), Path("/t"), "ckpt.pt")
    config = inference_config(template, Path("/job"), models)
    assert config["weights"] == {
        "video2pose_ckpt_root": "/w",
        "rmbg_weights_dir": "/r",
        "ckpt_name": "ckpt.pt",
    }
    assert config["data"]["video_roots"] == ["/job/videos"]
    assert config["data"]["wild_mode"] is True
    assert config["output"]["blender_path"] is None
    assert template["output"] == {"fps": 15}


def test_the_motion_summary_is_read() -> None:
    output = (
        "noise\n"
        "MOCAP frames=150 bones=31/31 worst_joint_m=0.000002 largest_vertical_travel_m=0.561\n"
    )
    summary = summary_of(output)
    assert summary["frames"] == "150"
    assert float(summary["worst_joint_m"]) == 0.000002


def test_no_rotation_stays_pinned(tmp_path: Path) -> None:
    path = tmp_path / "species_info_dict.npy"
    np.save(
        path, {"Rig": {"static_joints": [0], "static_rot_joints": [1, 2, 3]}}, allow_pickle=True
    )
    unpin_rotations(path, "Rig")
    info = np.load(path, allow_pickle=True).item()
    assert info["Rig"] == {"static_joints": [0], "static_rot_joints": []}
