# pyright: basic
# A Blender script: bpy has no types.
"""Blender script: a motion onto a rig with the same bone names.

The motion is a BVH (MoCapAnything's, on the rig's own skeleton; GVHMR's,
SMPL's skeleton under Mixamo's names) or another rig's FBX animation
(Kimodo's). Servers run it with Blender (asset_processing.blender's
RETARGET_SCRIPT gives its path); it prints one line,
``MOCAP key=value ...``.

    blender -b --factory-startup -P retarget.py -- \
        RIG.fbx MOTION.bvh|MOTION.fbx OUT.fbx KIND A B MODE [METHOD]

The BVH has the rig's bone names, but its own space: Y up, metres, facing
+Z (the pipeline turns every rig that way). Each bone's rotation from its
rest pose in the BVH is turned to face the way the rig does (about the
vertical, found in both from bones A and B as naming.RigReading.facing says:
KIND "axis" from A to B, "sides" square to the line from B to A) and put on the rig's
own rest pose; the root also moves as in the BVH. Bones the BVH lacks keep
their rest pose.

MODE "world" keeps the root's motion as predicted: the heading and the path
over the ground (a horse trotting in an arc around a panning camera turns).
"in-place" keeps the root's bob, pitch and roll but not its heading or its
travel over the ground: a gait for a game, which moves and steers the body
itself.
"stationary" removes horizontal travel while preserving heading, so attacks
and turns keep their original spin without moving the game object.

Bones are matched by name without the rigs' prefixes (mixamorig:Hips is
Hips). A "auto" with KIND "sides" finds the rig's thighs by name.

METHOD "rotations" maps each rest bone direction to its source counterpart,
then transfers full source rotations, including twist and terminal joints.
It needs humanoid LeftUpLeg/RightUpLeg names. Translation is scaled by rest
leg lengths. The source and target can have different rest poses.

METHOD "smpl" transfers full rotations but keeps the target's neutral pelvis,
spine, neck, head, clavicles and feet. SMPL joint offsets describe body shape, not a bend to
apply to another skin. Limb rest directions are still aligned. The pelvis
uses its actual source rotation, not a frame inferred from joint positions.

METHOD "smpl-neutral" also preserves limb rest directions: use only when the
target mesh is already authored in the same neutral body pose as SMPL. Bone
placement differences must not rotate an already matching neutral surface.

METHOD "delta" (the default) puts each bone's rotation from its rest pose
in the source on the rig's rest pose: right for MoCapAnything's BVH, which
has the rig's own skeleton and rest pose. "aim" turns each bone to point
where the source's same bone points (at its child joint), the root set by
the hips and spine: for motion whose joint positions are useful but whose
rotations are unreliable. Aim needs KIND
"sides"; the twist about a bone stays as at rest.

Writes OUT.fbx (mesh, skeleton, animation) and prints the
largest distance between a joint of the result and of the BVH, turned the
same way, in metres: the check that nothing was lost on the way.
"""

import contextlib
import math
import sys
from pathlib import Path

# The script is launched directly by Blender, without importing the model-server runtime.
sys.path.insert(0, str(Path(__file__).parent))
import bpy  # pyright: ignore[reportMissingImports]
from mathutils import Matrix, Vector  # pyright: ignore[reportMissingImports]
from rigging import RigError, reset_pose, validate  # pyright: ignore[reportMissingImports]


def facing(armature, kind: str, first: str, second: str) -> Vector:  # noqa: ANN001
    """The horizontal way the armature faces, in world space (naming.forward_of)."""
    world = armature.matrix_world
    bones = armature.data.bones
    a, b = world @ bones[first].head_local, world @ bones[second].head_local
    if kind == "axis":
        direction = b - a
    else:
        left = a - b
        direction = Vector((left.y, -left.x, 0.0))
    direction.z = 0
    return direction.normalized()


def aim_motion(
    rig,  # noqa: ANN001 -- bpy objects
    source,  # noqa: ANN001
    shared: list[str],
    turn: Matrix,
    left: str,
    right: str,
    mode: str,
    lowest: dict[str, list[float]],
    *,
    rotations: bool = False,
    preserve_axial_rest: bool = False,
    preserve_all_rest: bool = False,
) -> None:
    """Key the rig so each bone points where the source's same bone points."""
    scene = bpy.context.scene
    rig_world = rig.matrix_world
    to_rig = rig_world.inverted().to_3x3()  # world vectors into armature space
    bones = rig.data.bones
    names = [bone.name for bone in bones]
    root = next(bone.name for bone in bones if bone.parent is None)

    def in_line(name: str) -> str | None:
        """The child a bone points at: of those the source has, the one most
        along the bone at rest (the hips' spine, not a thigh; the chest's
        neck, not a collarbone)."""
        bone = bones[name]
        axis = (bone.tail_local - bone.head_local).normalized()
        children = [child for child in bone.children if child.name in shared]
        if not children:
            return None
        return max(
            children, key=lambda c: (c.head_local - bone.head_local).normalized().dot(axis)
        ).name

    aim = {name: in_line(name) for name in names}
    spine = aim[root]
    if spine is None:
        raise SystemExit(f"{root} has no child bone in the motion to set the hips by")

    def frame_of(heads: dict[str, Vector]) -> Matrix:
        """Hips' axes: across from right to left thigh, up along the spine."""
        across = (heads[left] - heads[right]).normalized()
        up = heads[spine] - heads[root]
        up = (up - up.dot(across) * across).normalized()
        return Matrix((across, up.cross(across), up)).transposed()

    rest_heads = {name: rig_world @ bones[name].head_local for name in names}
    rest_frame = frame_of(rest_heads)
    scene.frame_set(scene.frame_start)
    start = {name: source.matrix_world @ source.pose.bones[name].head for name in shared}
    lowest_rig = min(head.z for head in rest_heads.values())
    lowest_source = min(head.z for head in start.values())
    scale = (rest_heads[root].z - lowest_rig) / max(start[root].z - lowest_source, 1e-6)
    # A constant anatomical alignment maps the target's bone direction to the
    # source's. Full source rotations then preserve twist, including at wrists
    # and other terminal joints; position-only aiming cannot recover that twist.
    corrections = {}
    # Ankle-to-toe slope describes foot anatomy/heel height, not a sole
    # rotation to impose on another mesh (the guard differs by about 23 degrees).
    preserved_rest = {
        "hips",
        "spine",
        "spine1",
        "spine2",
        "neck",
        "head",
        "leftshoulder",
        "rightshoulder",
        "leftfoot",
        "rightfoot",
        "lefttoebase",
        "righttoebase",
    }
    for name in names:
        child = aim[name]
        parent = bones[name].parent
        if preserve_all_rest or (preserve_axial_rest and key(name) in preserved_rest):
            corrections[name] = Matrix.Identity(3)
        elif child and name in shared:
            ours = rest_heads[child] - rest_heads[name]
            theirs = turn @ (
                source.matrix_world @ source.data.bones[child].head_local
                - source.matrix_world @ source.data.bones[name].head_local
            )
            corrections[name] = ours.rotation_difference(theirs).to_matrix()
        else:
            corrections[name] = corrections[parent.name] if parent else Matrix.Identity(3)
    if rotations:
        # Scale travel by rest leg length, independent of how crouched frame 1 is.
        def leg_length(heads: dict[str, Vector]) -> float:
            pairs = [
                ("LeftUpLeg", "LeftLeg"),
                ("LeftLeg", "LeftFoot"),
                ("RightUpLeg", "RightLeg"),
                ("RightLeg", "RightFoot"),
            ]
            keyed = {key(n): v for n, v in heads.items()}
            return sum((keyed[key(a)] - keyed[key(b)]).length for a, b in pairs)

        source_rest = {n: source.matrix_world @ source.data.bones[n].head_local for n in shared}
        # Non-humanoids use the height ratio above.
        with contextlib.suppress(KeyError):
            scale = leg_length(rest_heads) / max(leg_length(source_rest), 1e-6)
    for frame in range(scene.frame_start, scene.frame_end + 1):
        scene.frame_set(frame)
        heads = {name: source.matrix_world @ source.pose.bones[name].head for name in shared}
        # The source's world turned to face as the rig does; in place, its
        # heading (the hips' turn about the vertical) taken off as well.
        unspin = turn.copy()
        source_root = source.pose.bones[root]
        root_change = (source.matrix_world @ source_root.matrix).to_3x3().normalized() @ (
            source.matrix_world @ source_root.bone.matrix_local
        ).to_3x3().normalized().inverted()
        if mode == "in-place":
            if preserve_axial_rest:
                forward = facing(rig, "sides", left, right)
                ahead = turn @ root_change @ turn.inverted() @ forward
                yaw = math.atan2(
                    forward.x * ahead.y - forward.y * ahead.x,
                    forward.x * ahead.x + forward.y * ahead.y,
                )
            else:
                ahead = turn @ frame_of(heads) @ rest_frame.inverted() @ Vector((0, 1, 0))
                yaw = math.atan2(-ahead.x, ahead.y)
            unspin = Matrix.Rotation(-yaw, 3, "Z") @ turn
        spin = unspin @ frame_of(heads) @ rest_frame.inverted()
        if preserve_axial_rest:
            spin = unspin @ root_change @ turn.inverted()
        posed: dict[str, Matrix] = {}
        for name in names:
            bone = bones[name]
            rest = bone.matrix_local
            parent = bone.parent
            carried = (
                posed[parent.name] @ parent.matrix_local.inverted() @ rest
                if parent
                else rest.copy()
            )
            if parent is None:
                rotation = (
                    to_rig.normalized() @ spin @ (rig_world.to_3x3().normalized() @ rest.to_3x3())
                ).normalized()
                moved = unspin @ (heads[name] - start[name]) * scale
                if mode != "world":
                    moved = Vector((0, 0, moved.z))
                target = Matrix.LocRotScale(rest.to_translation() + to_rig @ moved, rotation, None)
            elif rotations and name in shared:
                source_bone = source.pose.bones[name]
                source_pose = (source.matrix_world @ source_bone.matrix).to_3x3().normalized()
                source_rest = (
                    (source.matrix_world @ source_bone.bone.matrix_local).to_3x3().normalized()
                )
                change = unspin @ source_pose @ source_rest.inverted() @ turn.inverted()
                world_rotation = (
                    change @ corrections[name] @ (rig_world @ rest).to_3x3().normalized()
                )
                target = Matrix.LocRotScale(
                    carried.to_translation(), to_rig.normalized() @ world_rotation, None
                )
            elif (toward := aim[name]) is not None and name in heads:
                child = bones[toward].head_local
                now = (carried @ (rest.inverted() @ child)) - carried.to_translation()
                wanted = to_rig @ (unspin @ (heads[toward] - heads[name]))
                swing = now.rotation_difference(wanted).to_matrix()
                target = Matrix.LocRotScale(
                    carried.to_translation(), (swing @ carried.to_3x3()).normalized(), None
                )
            else:
                target = carried
            posed[name] = target
            pose_bone = rig.pose.bones[name]
            pose_bone.matrix_basis = carried.inverted() @ target
            pose_bone.keyframe_insert("location", frame=frame)
            pose_bone.keyframe_insert("rotation_quaternion", frame=frame)
        bpy.context.view_layer.update()
        for name in shared:
            lowest[name].append((rig_world @ rig.pose.bones[name].head).z)


def key(name: str) -> str:
    """A bone's name without its rig's prefix: mixamorig:LeftUpLeg -> leftupleg."""
    return name.rsplit(":", 1)[-1].lower()


def match_names(source, rig) -> None:  # noqa: ANN001
    """The source's bones renamed as the rig's same bones (GVHMR's Hips is
    mixamorig:Hips); Blender renames the animation's channels along."""
    # Legacy Kimodo leaves SOMA metacarpals unprefixed while mapping the
    # next joint to mixamorig:LeftHandIndex1. Those extras must not steal
    # the mapped Mixamo joint or make unrelated body matches ambiguous.
    groups = {}
    for bone in source.data.bones:
        groups.setdefault(key(bone.name), []).append(bone)
    for target in rig.data.bones:
        candidates = groups.get(key(target.name), [])
        if not candidates:
            continue
        mixamo = [bone for bone in candidates if bone.name.lower().startswith("mixamorig:")]
        exact = [bone for bone in candidates if bone.name == target.name]
        chosen = mixamo or exact or candidates
        if len(chosen) != 1:
            raise RigError(f"motion has ambiguous candidates for {target.name}")
        if chosen[0].name != target.name:
            # A bare target name may currently belong to a legacy SOMA extra.
            # Free it before renaming, otherwise Blender silently adds .001.
            for other in candidates:
                if other != chosen[0] and other.name == target.name:
                    other.name = f"unmapped_{other.name}"
            chosen[0].name = target.name


def thighs(rig) -> tuple[str, str]:  # noqa: ANN001
    """The rig's left and right upper legs, for KIND "sides" with A "auto"."""
    found = {}
    for bone in rig.data.bones:
        name = key(bone.name)
        if "upleg" in name or "thigh" in name:
            side = "left" if "left" in name or name.endswith("_l") else "right"
            found.setdefault(side, bone.name)
    if set(found) != {"left", "right"}:
        raise RigError(
            "cannot find humanoid thighs (LeftUpLeg/RightUpLeg); use a Mixamo humanoid rig"
        )
    return found["left"], found["right"]


def main() -> None:
    args = sys.argv[sys.argv.index("--") + 1 :]
    rig_path, motion_path, out_path, kind, first, second, mode = args[:7]
    method = args[7] if len(args) > 7 else "delta"
    if method not in ("delta", "aim", "rotations", "smpl", "smpl-neutral") or (
        method in ("aim", "rotations", "smpl", "smpl-neutral") and kind != "sides"
    ):
        raise SystemExit(f"method {method!r} with facing {kind!r} is not supported")
    if mode not in ("world", "stationary", "in-place"):
        raise SystemExit(f"unknown mode {mode!r}")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=rig_path)
    rig = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    validate(rig)
    reset_pose(rig)
    before = set(bpy.context.scene.objects)
    if motion_path.lower().endswith(".glb"):
        bpy.ops.import_scene.gltf(filepath=motion_path)
    elif motion_path.lower().endswith(".fbx"):
        # Another rig's animation (Kimodo's): the same bone names, its own rest pose.
        bpy.ops.import_scene.fbx(filepath=motion_path)
    else:
        bpy.ops.import_anim.bvh(
            filepath=motion_path,
            rotate_mode="NATIVE",
            update_scene_fps=True,
            update_scene_duration=True,
        )
    imported = [o for o in bpy.context.scene.objects if o not in before]
    sources = [o for o in imported if o.type == "ARMATURE"]
    if len(sources) != 1:
        raise RigError("motion must contain exactly one armature")
    source = sources[0]
    if source.animation_data is None or source.animation_data.action is None:
        raise RigError("motion has no animation; choose an animated FBX")
    match_names(source, rig)
    if first == "auto":
        first, second = thighs(rig)
    required = {first, second}
    required.update(b.name for b in rig.data.bones if b.parent is None)
    missing = sorted(required - set(source.pose.bones.keys()))
    if missing:
        raise RigError("motion is missing required matching bones: " + ", ".join(missing))
    scene = bpy.context.scene
    # The importer lengthens the scene to the motion but never shortens it.
    start, end = source.animation_data.action.frame_range
    scene.frame_start, scene.frame_end = round(start), round(end)

    turn = (
        facing(source, kind, first, second)
        .rotation_difference(facing(rig, kind, first, second))
        .to_matrix()
    )
    turn4 = turn.to_4x4()
    forward = facing(rig, kind, first, second)
    root = next(bone.name for bone in rig.data.bones if bone.parent is None)
    rig_world = rig.matrix_world
    source_world = source.matrix_world
    names = [bone.name for bone in rig.data.bones]  # parents before children
    shared = [name for name in names if name in source.pose.bones]

    rig.animation_data_create()
    rig.animation_data.action = bpy.data.actions.new("Mocap")
    for pose_bone in rig.pose.bones:
        pose_bone.rotation_mode = "QUATERNION"
    worst = 0.0
    lowest = {name: [] for name in shared}  # each joint's height per frame
    if method in ("aim", "rotations", "smpl", "smpl-neutral"):
        aim_motion(
            rig,
            source,
            shared,
            turn,
            first,
            second,
            mode,
            lowest,
            rotations=method in ("rotations", "smpl", "smpl-neutral"),
            preserve_axial_rest=method in ("smpl", "smpl-neutral"),
            preserve_all_rest=method == "smpl-neutral",
        )
        worst = float("nan")
    for frame in range(scene.frame_start, scene.frame_end + 1) if method == "delta" else ():
        scene.frame_set(frame)
        # In place: the root's heading is taken off every bone alike.
        unyaw = Matrix.Identity(3)
        if mode == "in-place":
            root_pose = source_world @ source.pose.bones[root].matrix
            root_rest = source_world @ source.data.bones[root].matrix_local
            heading = (
                turn
                @ (root_pose.to_3x3() @ root_rest.to_3x3().inverted())
                @ turn.inverted()
                @ forward
            )
            yaw = math.atan2(
                forward.x * heading.y - forward.y * heading.x,
                forward.x * heading.x + forward.y * heading.y,
            )
            unyaw = Matrix.Rotation(-yaw, 3, "Z")
        unyaw4 = unyaw.to_4x4()
        posed: dict[str, Matrix] = {}  # armature space of the rig
        for name in names:
            bone = rig.data.bones[name]
            rest = bone.matrix_local
            parent = bone.parent
            # Where the bone would be with no rotation of its own.
            carried = (
                posed[parent.name] @ parent.matrix_local.inverted() @ rest
                if parent
                else rest.copy()
            )
            if name in shared:
                source_bone = source.pose.bones[name]
                world_pose = source_world @ source_bone.matrix
                world_rest = source_world @ source.data.bones[name].matrix_local
                change = (
                    unyaw4
                    @ turn4
                    @ (world_pose.to_3x3() @ world_rest.to_3x3().inverted()).to_4x4()
                    @ turn4.inverted()
                )
                rotation = (rig_world.inverted() @ change @ rig_world @ rest).to_3x3().normalized()
                location = carried.to_translation()
                if parent is None:
                    moved = turn @ (world_pose.to_translation() - world_rest.to_translation())
                    if mode != "world":
                        moved = Vector((0, 0, moved.z))
                    location = location + (rig_world.inverted().to_3x3() @ moved)
                target = Matrix.LocRotScale(location, rotation, None)
            else:
                target = carried
            posed[name] = target
            basis = carried.inverted() @ target
            pose_bone = rig.pose.bones[name]
            pose_bone.matrix_basis = basis
            pose_bone.keyframe_insert("location", frame=frame)
            pose_bone.keyframe_insert("rotation_quaternion", frame=frame)
        # The check: rig joints against the BVH's, turned and moved alike.
        bpy.context.view_layer.update()
        # Relative to the root, which in place no longer travels.
        our_root = rig_world @ rig.pose.bones[root].head
        their_root = source_world @ source.pose.bones[root].head
        for name in shared:
            ours = rig_world @ rig.pose.bones[name].head
            theirs = unyaw @ (turn @ (source_world @ source.pose.bones[name].head - their_root))
            worst = max(worst, (ours - our_root - theirs).length)
            lowest[name].append(ours.z)

    for obj in imported:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=True,
        bake_anim=True,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0,
    )
    # How far the joints travel up and down: zero would mean nothing moves.
    travel = max(max(heights) - min(heights) for heights in lowest.values())
    print(
        f"MOCAP frames={scene.frame_end - scene.frame_start + 1} "
        f"fps={scene.render.fps / scene.render.fps_base:.6f} bones={len(shared)}/{len(names)} "
        f"worst_joint_m={worst:.6f} largest_vertical_travel_m={travel:.3f}"
    )


if __name__ == "__main__":
    try:
        main()
    except RigError as error:
        print(f"RIG_INVALID {error}", flush=True)
        sys.exit(3)
