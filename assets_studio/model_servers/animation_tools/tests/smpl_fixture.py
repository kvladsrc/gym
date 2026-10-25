"""Blender regression: shape offsets must not become torso or head motion."""

import math
import runpy
import sys
from itertools import pairwise
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

AXIAL = ("Hips", "Spine", "Spine1", "Spine2", "Neck", "Head")


def make_skeleton(curved):
    helper = runpy.run_path(str(Path(__file__).with_name("blender_fixture.py")))
    helper["skeleton"](curved)
    rig = bpy.context.object
    bpy.ops.object.mode_set(mode="EDIT")
    bones = rig.data.edit_bones
    offsets = [(0.04, 0.06), (-0.04, -0.02), (0.02, -0.09), (-0.03, 0.04), (0.01, -0.02)]
    for i, name in enumerate(AXIAL[1:]):
        bone = bones.get(name) or bones.new(name)
        x, y = offsets[i] if curved else (0, 0)
        bone.head = (x, y, 1.15 + i * 0.14)
        bone.tail = bone.head + Vector((0, 0, 0.1))
        bone.parent = bones[AXIAL[i]]
    for a, b in pairwise(AXIAL):
        bones[a].tail = bones[b].head
    for side, sign in (("Left", 1), ("Right", -1)):
        shoulder = bones.new(side + "Shoulder")
        shoulder.head = (
            sign * (0.04 if curved else 0.08),
            0.06 if curved else 0,
            1.60 if curved else 1.45,
        )
        shoulder.tail = bones[side + "Arm"].head
        shoulder.parent = bones["Spine2"]
        bones[side + "Arm"].parent = shoulder
        foot = bones[side + "Foot"]
        toe = bones.new(side + "ToeBase")
        toe.head = foot.head + Vector((0, -0.10 if curved else -0.18, -0.13 if curved else -0.07))
        toe.tail = toe.head + Vector((0, -0.08, 0))
        toe.parent = foot
        foot.tail = toe.head
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def load(path):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return next(o for o in bpy.context.scene.objects if o not in before and o.type == "ARMATURE")


def world_delta(rig, name):
    bone = rig.pose.bones[name]
    pose = (rig.matrix_world @ bone.matrix).to_3x3().normalized()
    rest = (rig.matrix_world @ bone.bone.matrix_local).to_3x3().normalized()
    return pose @ rest.inverted()


def main():
    operation, directory = sys.argv[sys.argv.index("--") + 1 :]
    work = Path(directory)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 30
    if operation == "make":
        rig = make_skeleton(False)
        bpy.ops.mesh.primitive_cube_add(size=0.2, location=(0, 0, 1))
        mesh = bpy.context.object
        mesh.parent = rig
        mesh.modifiers.new("Skin", "ARMATURE").object = rig
        mesh.vertex_groups.new(name="Hips").add(list(range(8)), 1, "REPLACE")
        bpy.ops.export_scene.fbx(
            filepath=str(work / "target.fbx"), add_leaf_bones=False, bake_anim=False
        )
        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = bpy.context.scene
        scene.render.fps = 30
        rig = make_skeleton(True)
        for frame, factor in ((1, 0), (10, 1)):
            for name, axis, angle in (
                ("Hips", (0, 1, 0), 0.6),
                ("Spine", (1, 0, 0), 0.15),
                ("Neck", (1, 0, 0), -0.11),
                ("Head", (1, 0, 0), 0.23),
                ("LeftShoulder", (0, 1, 0), 0.19),
                ("RightShoulder", (0, 1, 0), -0.17),
                ("LeftFoot", (1, 0, 0), 0.21),
                ("RightFoot", (1, 0, 0), -0.16),
                ("LeftToeBase", (1, 0, 0), 0.15),
                ("RightToeBase", (1, 0, 0), 0.13),
                ("LeftHand", (0, 1, 0), 0.72),
            ):
                bone = rig.pose.bones[name]
                bone.rotation_mode = "QUATERNION"
                bone.rotation_quaternion = Quaternion(axis, angle * factor)
                bone.keyframe_insert("rotation_quaternion", frame=frame)
        scene.frame_start, scene.frame_end = 1, 10
        bpy.ops.export_scene.fbx(
            filepath=str(work / "motion.fbx"),
            add_leaf_bones=False,
            bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=False,
            bake_anim_simplify_factor=0,
        )
    else:
        check_all = operation.startswith("check-neutral-")
        mode = (
            operation.removeprefix("check-neutral-")
            if check_all
            else operation.removeprefix("check-")
        )
        target = load(work / "animated.fbx")
        source = load(work / "motion.fbx")
        forward = Vector((0, -1, 0))  # both fixtures face -Y
        target_start, target_end = map(round, target.animation_data.action.frame_range)
        source_start, source_end = map(round, source.animation_data.action.frame_range)
        assert target_end - target_start == source_end - source_start == 9
        # FBX imports add an animation offset; compare corresponding samples.
        checked = (
            AXIAL
            + (
                "LeftShoulder",
                "RightShoulder",
                "LeftFoot",
                "RightFoot",
                "LeftToeBase",
                "RightToeBase",
            )
            + (
                ("LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand")
                if check_all
                else ()
            )
        )
        for sample in range(10):
            scene.frame_set(target_start + sample)
            actual_rotations = {name: world_delta(target, name).to_quaternion() for name in checked}
            wrist = target.pose.bones["LeftHand"].matrix_basis.to_quaternion().copy()
            scene.frame_set(source_start + sample)
            correction = Matrix.Identity(3)
            if mode == "in-place":
                ahead = world_delta(source, "Hips") @ forward
                yaw = math.atan2(
                    forward.x * ahead.y - forward.y * ahead.x,
                    forward.x * ahead.x + forward.y * ahead.y,
                )
                correction = Matrix.Rotation(-yaw, 3, "Z")
            for name in checked:
                actual = actual_rotations[name]
                expected = (correction @ world_delta(source, name)).to_quaternion()
                angle = actual.rotation_difference(expected).angle
                assert min(angle, 2 * math.pi - angle) < 0.002, (sample, name, angle)
                if sample == 0:
                    assert actual.angle < 0.002, (name, actual.angle)
            if sample == 9:
                assert actual_rotations["Head"].angle > 0.15
                assert wrist.angle > 0.5, wrist.angle
        print("BLENDER_ASSERTIONS_OK", operation)


if __name__ == "__main__":
    main()
