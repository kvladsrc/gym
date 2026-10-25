"""Blender script: a rigged FBX as MoCapAnything's custom-rig pipeline wants it.

    blender -b --factory-startup -P prepare_rig.py -- RIG.fbx|RIG.glb OUT_DIR NAME

Writes OUT_DIR/NAME.fbx (the rig as it is: the base), OUT_DIR/NAME-Rest.fbx
(the rig holding its rest pose for REST_FRAMES frames: the pipeline builds
the reference frame from a motion, and a fresh rig has none) and
OUT_DIR/bones.json (name, parent, head and tail in world space, Z up), for
naming the joints.
"""

import json
import math
import sys
from pathlib import Path

import bpy  # pyright: ignore[reportMissingImports]
from mathutils import Matrix  # pyright: ignore[reportMissingImports]

# Only frame 0 is used as the reference; a few more keep the stages that
# render and sample the motion from meeting a one-frame clip.
REST_FRAMES = 8
# Printed (exit status 3) when the file has no skeleton: the input's fault.
NO_ARMATURE = "NO_ARMATURE"


def as_truebones(armature) -> None:  # noqa: ANN001 (a bpy object)
    """The armature's bones in centimetres and Y up, its object scaled by
    0.01 and turned back to Z up: the world stays as it was.

    The pipeline was written for Truebones Zoo rigs from 3ds Max: their BVH
    (the armature's own space) is in centimetres with Y up, which it scales
    by 0.01 and turns to face +Z about Y. A rig in metres shrank a hundred
    times and the reference camera ended up inside the mesh.
    """
    meshes = [child for child in armature.children if child.type == "MESH"]
    for mesh in meshes:
        world = mesh.matrix_world.copy()
        mesh.parent = None
        mesh.matrix_world = world
    target = Matrix.Rotation(math.radians(90), 4, "X") @ Matrix.Scale(0.01, 4)
    change = target.inverted() @ armature.matrix_world
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    # Connected bones move with their parent's tail: unconnected, each is
    # transformed once.
    connected = {bone.name for bone in armature.data.edit_bones if bone.use_connect}
    for bone in armature.data.edit_bones:
        bone.use_connect = False
    for bone in armature.data.edit_bones:
        bone.transform(change)
    for bone in armature.data.edit_bones:
        bone.use_connect = bone.name in connected
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.matrix_world = target
    for mesh in meshes:
        world = mesh.matrix_world.copy()
        mesh.parent = armature
        mesh.matrix_parent_inverse = armature.matrix_world.inverted()
        mesh.matrix_world = world


def main() -> None:
    rig, out, name = sys.argv[sys.argv.index("--") + 1 :]
    out_dir = Path(out)
    out_dir.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if rig.lower().endswith((".glb", ".gltf")):
        bpy.ops.import_scene.gltf(filepath=rig)
    else:
        bpy.ops.import_scene.fbx(filepath=rig)
    armature = next((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), None)
    if armature is None:
        print(NO_ARMATURE)
        sys.exit(3)

    world = armature.matrix_world
    bones = [
        {
            "name": bone.name,
            "parent": bone.parent.name if bone.parent else None,
            "head": list(world @ bone.head_local),
            "tail": list(world @ bone.tail_local),
        }
        for bone in armature.data.bones
    ]
    (out_dir / "bones.json").write_text(json.dumps(bones, indent=1))
    as_truebones(armature)

    export = {
        "use_selection": False,
        "add_leaf_bones": False,
        "path_mode": "COPY",
        "embed_textures": True,
    }
    bpy.ops.export_scene.fbx(filepath=str(out_dir / f"{name}.fbx"), bake_anim=False, **export)

    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="POSE")
    action = bpy.data.actions.new(f"{name}-Rest")
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame in range(1, REST_FRAMES + 1):
        for pose_bone in armature.pose.bones:
            pose_bone.location = (0, 0, 0)
            pose_bone.rotation_mode = "QUATERNION"
            pose_bone.rotation_quaternion = (1, 0, 0, 0)
            pose_bone.scale = (1, 1, 1)
            for path in ("location", "rotation_quaternion", "scale"):
                pose_bone.keyframe_insert(path, frame=frame)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, REST_FRAMES
    bpy.ops.export_scene.fbx(
        filepath=str(out_dir / f"{name}-Rest.fbx"),
        bake_anim=True,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        **export,
    )


if __name__ == "__main__":
    main()
