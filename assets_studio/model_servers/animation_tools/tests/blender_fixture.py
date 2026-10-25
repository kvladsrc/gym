"""Blender fixture and assertions for rigs with different rest poses and wrist twist."""

import sys
from pathlib import Path

import bpy
from mathutils import Quaternion, Vector


def skeleton(a_pose: bool) -> None:
    bpy.ops.object.armature_add()
    rig = bpy.context.object
    bpy.ops.object.mode_set(mode="EDIT")
    bones = rig.data.edit_bones
    bones.remove(bones[0])
    parts = [
        ("Hips", None, (0, 0, 1)),
        ("Spine", "Hips", (0, 0, 1.15)),
        ("Head", "Spine", (0, 0, 1.7)),
    ]
    for side, sign in [("Left", 1), ("Right", -1)]:
        parts += [
            (side + "UpLeg", "Hips", (sign * 0.1, 0, 1)),
            (side + "Leg", side + "UpLeg", (sign * 0.1, 0, 0.55)),
            (side + "Foot", side + "Leg", (sign * 0.1, 0, 0.1)),
            (side + "Arm", "Spine", (sign * 0.2, 0, 1.45)),
            (side + "ForeArm", side + "Arm", (sign * 0.5, 0, 1.2 if a_pose else 1.45)),
            (side + "Hand", side + "ForeArm", (sign * 0.8, 0, 0.95 if a_pose else 1.45)),
        ]
    for name, parent, head in parts:
        bone = bones.new(name)
        bone.head = head
        bone.tail = Vector(head) + Vector((0, 0, 0.1))
        if parent:
            bone.parent = bones[parent]
    # An unmatched SOMA-style extra may share the canonical name of a body bone.
    if a_pose:
        extra = bones.new("soma:Head")
        extra.head = (0, 0, 1.8)
        extra.tail = (0, 0, 1.9)
        extra.parent = bones["Head"]
    for bone in bones:
        children = list(bone.children)
        if children:
            bone.tail = next((c.head for c in children if c.name == "Spine"), children[0].head)
    bpy.ops.object.mode_set(mode="OBJECT")


def main() -> None:
    operation, directory = sys.argv[sys.argv.index("--") + 1 :]
    work = Path(directory)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 30
    if operation.startswith("make"):
        skeleton(False)
        rig = bpy.context.object
        bpy.ops.mesh.primitive_cube_add(size=0.2, location=(0, 0, 1))
        mesh = bpy.context.object
        mesh.parent = rig
        modifier = mesh.modifiers.new("Skin", "ARMATURE")
        modifier.object = rig
        mesh.vertex_groups.new(name="Hips").add(list(range(8)), 0.8, "REPLACE")
        # An animated target should be reset to its real bind pose by preparation.
        wrist = rig.pose.bones["LeftHand"]
        wrist.rotation_mode = "QUATERNION"
        wrist.rotation_quaternion = Quaternion((0, 1, 0), 1.3)
        wrist.keyframe_insert("rotation_quaternion", frame=1)
        bpy.ops.export_scene.fbx(
            filepath=str(work / "target.fbx"),
            add_leaf_bones=False,
            bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=False,
            bake_anim_simplify_factor=0,
        )
        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = bpy.context.scene
        scene.render.fps = 30
        skeleton(not operation.startswith("make-same"))
        rig = bpy.context.object
        for frame in range(1, 11):
            wrist = rig.pose.bones["LeftHand"]
            wrist.rotation_mode = "QUATERNION"
            wrist.rotation_quaternion = Quaternion((0, 1, 0), 0.08 * (frame - 1))
            wrist.keyframe_insert("rotation_quaternion", frame=frame)
            root = rig.pose.bones["Hips"]
            if operation.endswith("-turn"):
                root.rotation_mode = "QUATERNION"
                root.rotation_quaternion = Quaternion((0, 1, 0), 0.06 * (frame - 1))
                root.keyframe_insert("rotation_quaternion", frame=frame)
            # Hips' local Y points up: translation and vertical bob are separate.
            root.location = (frame * 0.03, frame * 0.01, 0)
            root.keyframe_insert("location", frame=frame)
        scene.frame_start, scene.frame_end = 1, 10
        bpy.ops.export_scene.fbx(
            filepath=str(work / "motion.fbx"),
            add_leaf_bones=False,
            bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=False,
            bake_anim_simplify_factor=0,
        )
    else:
        bpy.ops.import_scene.fbx(
            filepath=str(work / ("prepared.fbx" if operation == "check-rig" else "animated.fbx"))
        )
        rig = next(o for o in scene.objects if o.type == "ARMATURE")
        mesh = next(o for o in scene.objects if o.type == "MESH")
        assert all(abs(sum(g.weight for g in v.groups) - 1) < 1e-5 for v in mesh.data.vertices)
        if operation == "check-rig":
            assert not (rig.animation_data and rig.animation_data.action)
            wrist = rig.pose.bones["LeftHand"]
            assert wrist.matrix_basis.to_quaternion().angle < 1e-5
        else:
            start, end = rig.animation_data.action.frame_range
            assert round(end - start + 1) == 10, (start, end)
            scene.frame_set(round(start))
            root0 = (rig.matrix_world @ rig.pose.bones["Hips"].head).copy()
            wrist = rig.pose.bones["LeftHand"]
            wrist0 = (wrist.parent.matrix.inverted() @ wrist.matrix).to_quaternion().copy()
            hips0 = rig.pose.bones["Hips"].matrix.to_quaternion().copy()
            arm = rig.pose.bones["LeftArm"]
            forearm = rig.pose.bones["LeftForeArm"]
            direction = (forearm.head - arm.head).normalized()
            # The T-pose target follows the A-pose source, not the target's old clip.
            if "-same" in operation:
                assert abs(direction.z) < 0.01, direction
            else:
                assert direction.z < -0.4, direction
            scene.frame_set(round(end))
            root1 = rig.matrix_world @ rig.pose.bones["Hips"].head
            wrist1 = (wrist.parent.matrix.inverted() @ wrist.matrix).to_quaternion()
            hips1 = rig.pose.bones["Hips"].matrix.to_quaternion()
            if "-turn" in operation:
                expected_yaw = 0 if operation.startswith("check-in-place") else 0.54
                assert abs(hips0.rotation_difference(hips1).angle - expected_yaw) < 0.01
            assert abs(wrist0.rotation_difference(wrist1).angle - 0.72) < 0.01
            expected_travel = 0.27 if operation.startswith("check-world") else 0
            horizontal = Vector((root1.x - root0.x, root1.y - root0.y, 0)).length
            assert abs(horizontal - expected_travel) < 0.001, (root0, root1)
            assert abs(root1.z - root0.z - 0.09) < 0.001, (root0, root1)
        print("BLENDER_ASSERTIONS_OK", operation)


if __name__ == "__main__":
    main()
