# pyright: basic
"""Blender-only rig validation and preparation, also callable as a script.

    blender -b --factory-startup -P rigging.py -- INPUT.fbx|INPUT.glb OUT.fbx

Keeps the skeleton, rest pose, mesh and world transforms. Removes existing
animation and resets pose channels; normalizes deform weights. It does not
invent a humanoid skeleton or turn an arbitrary rest pose into a T-pose.
"""

import json
import math
import runpy
import sys
from pathlib import Path

import bpy  # pyright: ignore[reportMissingImports]


class RigError(ValueError):
    pass


def reset_pose(rig) -> None:  # noqa: ANN001 -- Blender objects
    rig.animation_data_clear()
    for bone in rig.pose.bones:
        if bone.constraints:
            raise RigError(f"bake constraints before using this rig: {bone.name}")
        bone.location = (0, 0, 0)
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = (1, 0, 0, 0)
        bone.scale = (1, 1, 1)
    bpy.context.view_layer.update()


def validate(rig, *, require_skin: bool = True) -> dict:  # noqa: ANN001
    bones = rig.data.bones
    if not bones:
        raise RigError("the armature has no bones")
    if len([b for b in bones if b.parent is None]) != 1:
        raise RigError("expected one skeleton root")
    canonical = [b.name.rsplit(":", 1)[-1].lower() for b in bones]
    if len(set(canonical)) != len(canonical):
        raise RigError("bone names are ambiguous after removing namespace prefixes")
    for bone in bones:
        if not all(math.isfinite(x) for x in (*bone.head_local, *bone.tail_local)):
            raise RigError(f"non-finite bone coordinates: {bone.name}")
        if bone.length < 1e-8:
            raise RigError(f"zero-length bone: {bone.name}")
    meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and any(m.type == "ARMATURE" and m.object == rig for m in obj.modifiers)
    ]
    if require_skin and not meshes:
        raise RigError("the rig has no mesh bound to its armature")
    vertices = 0
    normalized = 0
    for mesh in meshes:
        groups = {g.index: g for g in mesh.vertex_groups if g.name in bones}
        for vertex in mesh.data.vertices:
            weights = [(groups[g.group], g.weight) for g in vertex.groups if g.group in groups]
            if any(not math.isfinite(w) or w < 0 for _, w in weights):
                raise RigError(f"invalid skin weight on {mesh.name}, vertex {vertex.index}")
            total = sum(weight for _, weight in weights)
            if total <= 1e-8:
                raise RigError(
                    f"unweighted vertex {vertex.index} on {mesh.name}; rig the mesh again"
                )
            if abs(total - 1) > 1e-6:
                for group, weight in weights:
                    group.add([vertex.index], weight / total, "REPLACE")
                normalized += 1
            vertices += 1
    return {
        "bones": len(bones),
        "skinned_meshes": len(meshes),
        "vertices": vertices,
        "normalized_vertices": normalized,
    }


def prepare(source: str, target: str) -> dict:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if Path(source).suffix.lower() == ".glb":
        bpy.ops.import_scene.gltf(filepath=source)
    else:
        bpy.ops.import_scene.fbx(filepath=source)
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(rigs) != 1:
        raise RigError(f"expected one armature, got {len(rigs)}; rig the mesh first")
    rig = rigs[0]
    reset_pose(rig)
    report = validate(rig)
    # No animation from previously animated input objects should survive preparation.
    for obj in bpy.context.scene.objects:
        obj.animation_data_clear()
    helpers = runpy.run_path(str(Path(__file__).with_name("blender.py")))
    helpers["name_packed_images"]()
    bpy.ops.export_scene.fbx(
        filepath=target,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=True,
    )
    return report


def main() -> None:
    source, target = sys.argv[sys.argv.index("--") + 1 :]
    try:
        report = prepare(source, target)
    except (RigError, RuntimeError) as error:
        print(f"RIG_INVALID {error}", flush=True)
        sys.exit(3)
    print("RIG_REPORT " + json.dumps(report), flush=True)


if __name__ == "__main__":
    main()
