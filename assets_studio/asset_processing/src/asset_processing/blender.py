"""A rigged GLB to an FBX for Unity (ADR-006), through bpy.

For model servers that have bpy (Blender as a Python module) in their
environment; the processing package does not depend on it. Run in a child process:
bpy keeps global state and must not be used from the model thread.

    python -m asset_processing.blender rigged.glb rigged.fbx

What the export needs, learned on Make-It-Animatable (its blender_export.py):

- A GLB's textures reach Blender as packed images without file names, and
  the FBX export embeds them under the media folder's name: Unity does not
  recognise them as images and imports the model untextured. Each gets a
  name from its content (``texture-<hash>.png``), unique across models.
- Generated meshes come unwelded (three vertices per triangle) and flat: in
  a game they look faceted, with dark seams. Vertices at one position are
  merged (their bone weights agree: predicted or transferred by position)
  and the faces smooth shaded.
- No leaf bones: Unity does not need them, and they add a bone per chain end.
"""

import hashlib
import importlib
import sys
from pathlib import Path
from typing import Any

# A Blender script (blender -b -P RETARGET_SCRIPT -- ...): a motion onto a rig
# with the same bone names; see the script for its arguments.
RETARGET_SCRIPT = Path(__file__).with_name("retarget.py")


def _module(name: str) -> Any:
    """bpy and bmesh, untyped: imported only where they exist."""
    return importlib.import_module(name)


def name_packed_images() -> None:
    bpy = _module("bpy")

    for image in bpy.data.images:
        if image.packed_file is not None and not image.filepath:
            digest = hashlib.sha1(image.packed_file.data, usedforsecurity=False).hexdigest()[:12]
            image.filepath = f"//texture-{digest}.png"


def weld_and_smooth() -> None:
    bpy = _module("bpy")
    bmesh = _module("bmesh")  # bpy as a module puts bmesh on the path: after it

    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        work = bmesh.new()
        work.from_mesh(mesh)
        bmesh.ops.remove_doubles(work, verts=work.verts, dist=1e-5)
        work.to_mesh(mesh)
        work.free()
        for polygon in mesh.polygons:
            polygon.use_smooth = True
        mesh.update()


def glb_to_fbx(glb: Path, fbx: Path) -> dict[str, int]:
    """Convert; returns the counts of bones and triangles written."""
    bpy = _module("bpy")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    # glTF keeps joints, not bone tails: the importer guesses them. FORTUNE
    # points bones along their limbs (on SkinTokens' rigs 71 % of the tails
    # inside the body, against 51 % with the default BLENDER): what Blender
    # shows and what the bones' axes are, for whoever animates them.
    bpy.ops.import_scene.gltf(filepath=str(glb), bone_heuristic="FORTUNE")
    # glTF import keeps nodes it cannot place in this collection; not ours.
    leftover = bpy.data.collections.get("glTF_not_exported")
    if leftover is not None:
        for obj in list(leftover.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
    name_packed_images()
    weld_and_smooth()
    bpy.ops.export_scene.fbx(
        filepath=str(fbx),
        check_existing=False,
        use_selection=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=True,
    )
    bones = sum(len(o.data.bones) for o in bpy.data.objects if o.type == "ARMATURE")
    triangles = sum(
        sum(len(p.vertices) - 2 for p in o.data.polygons)
        for o in bpy.data.objects
        if o.type == "MESH"
    )
    return {"bones": bones, "triangles": triangles}


def exit_now() -> None:
    """End the process, skipping bpy's teardown: as a module, bpy (4.2, 4.3)
    crashes with a segmentation fault at exit, after the work is done."""
    import os

    sys.stdout.flush()
    sys.stderr.flush()
    os._exit(0)


def main() -> None:
    glb, fbx = (Path(arg) for arg in sys.argv[1:3])
    print(glb_to_fbx(glb, fbx))
    exit_now()


if __name__ == "__main__":
    main()
