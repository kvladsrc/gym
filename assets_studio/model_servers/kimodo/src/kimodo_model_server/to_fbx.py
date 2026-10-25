"""Kimodo's BVH (SOMA skeleton) to an FBX with Mixamo bone names, through bpy;
run in a child process (bpy keeps global state).

    python -m kimodo_model_server.to_fbx motion.bvh motion.fbx

SOMA and Mixamo name the same body parts differently (SOMA's LeftLeg is
Mixamo's LeftUpLeg, its LeftShin Mixamo's LeftLeg) and SOMA has a
metacarpal joint in each finger that Mixamo has not: its fingers' joints
2–4 are Mixamo's 1–3. Joints without a Mixamo counterpart (a second neck
joint, the jaw, the eyes, the metacarpals) keep their SOMA names under a ``soma_`` prefix.
"""

import re
import sys
from pathlib import Path

_SAME = {
    "Hips", "Head", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
    "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    "LeftFoot", "LeftToeBase", "RightFoot", "RightToeBase",
}  # fmt: skip
_RENAMED = {
    "Spine1": "Spine",
    "Spine2": "Spine1",
    "Chest": "Spine2",
    "Neck1": "Neck",
    "HeadEnd": "HeadTop_End",
    "LeftLeg": "LeftUpLeg",
    "LeftShin": "LeftLeg",
    "LeftToeEnd": "LeftToe_End",
    "RightLeg": "RightUpLeg",
    "RightShin": "RightLeg",
    "RightToeEnd": "RightToe_End",
}
_FINGER = re.compile(r"(Left|Right)Hand(Thumb|Index|Middle|Ring|Pinky)(\d|End)")


def mixamo_name(soma: str) -> str | None:
    """The Mixamo name of a SOMA joint, None if Mixamo has no such joint."""
    if soma in _SAME:
        return f"mixamorig:{soma}"
    if soma in _RENAMED:
        return f"mixamorig:{_RENAMED[soma]}"
    finger = _FINGER.fullmatch(soma)
    if finger is None:
        return None
    side, name, joint = finger.groups()
    if name == "Thumb":  # three joints and an end, as Mixamo's
        number = 4 if joint == "End" else int(joint)
    else:  # a metacarpal, three joints and an end
        if joint == "1":
            return None
        number = 4 if joint == "End" else int(joint) - 1
    return f"mixamorig:{side}Hand{name}{number}"


def convert(bvh: Path, fbx: Path) -> dict[str, int]:
    import bpy  # pyright: ignore[reportMissingImports]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Kimodo writes centimetres, Y up.
    bpy.ops.import_anim.bvh(
        filepath=str(bvh), global_scale=0.01, update_scene_fps=True, update_scene_duration=True
    )
    armature = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    armature.name = "Armature"
    renamed = 0
    for bone in armature.data.bones:
        new = mixamo_name(bone.name)
        if new is not None:
            bone.name = new  # the action's F-curves follow the rename
            renamed += 1
        else:
            # SOMA metacarpal LeftHandIndex1 is not Mixamo Index1 (SOMA Index2).
            # Give extras distinct keys even when a consumer strips namespaces.
            bone.name = f"soma_{bone.name}"
    action = armature.animation_data.action
    start, end = action.frame_range
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = int(start), int(end)
    frames = int(end - start + 1)
    bpy.ops.export_scene.fbx(
        filepath=str(fbx),
        check_existing=False,
        object_types={"ARMATURE"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0,  # every frame as generated
    )
    return {"bones": len(armature.data.bones), "mixamo_bones": renamed, "frames": frames}


def main() -> None:
    from asset_processing.blender import exit_now

    bvh, fbx = (Path(arg) for arg in sys.argv[1:3])
    print(convert(bvh, fbx))
    exit_now()


if __name__ == "__main__":
    main()
