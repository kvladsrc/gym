"""The game's models from the asset studio into Unity, in Blender.

Run inside Blender (its MCP, or `blender -b -P`): for each model asset in
art/models.json ({name: studio asset id}) it fetches the textured GLB
(Hunyuan3D + Paint), joins it, puts its base at the origin and centres it over
it, smooth shaded, and writes Art/Models/<name>.fbx (the mesh) and
Art/Models/<name>.png (its texture) in the Unity project, and their sizes to
Art/Models/models.json. The scene used is cleared and removed after.

    exec(open(".../art/convert_models.py").read(), globals()); convert_all()
"""

import json
import os
import tempfile
import urllib.request
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

HERE = Path("/home/myuser/Dropbox/src/games/finish_the_game/art")
OUT = Path(
    "/home/myuser/Dropbox/src/games/finish_the_game/unity/Finish the game/Assets/FinishTheGame/Art/Models"
)
STUDIO = os.environ.get("STUDIO_URL", "http://127.0.0.1:9000")


def _scene() -> bpy.types.Scene:
    scene = bpy.data.scenes.get("FinishTheGame convert") or bpy.data.scenes.new(
        "FinishTheGame convert"
    )
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.window.scene = scene
    return scene


def convert(name: str, asset: str) -> dict:
    scene = _scene()
    glb = Path(tempfile.gettempdir()) / f"ftg-{asset}.glb"
    urllib.request.urlretrieve(f"{STUDIO}/api/assets/{asset}/file", glb)
    bpy.ops.import_scene.gltf(filepath=str(glb))
    meshes = [o for o in scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector(
        (
            min(c.x for c in corners),
            min(c.y for c in corners),
            min(c.z for c in corners),
        )
    )
    hi = Vector(
        (
            max(c.x for c in corners),
            max(c.y for c in corners),
            max(c.z for c in corners),
        )
    )
    # Blender is z up: base at z 0, centred over the origin.
    obj.data.transform(
        Matrix.Translation(Vector((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z)))
    )
    for poly in obj.data.polygons:
        poly.use_smooth = True
    # The texture: the base colour's image.
    image = None
    for slot in obj.material_slots:
        if slot.material and slot.material.use_nodes:
            for node in slot.material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image:
                    image = node.image
    OUT.mkdir(parents=True, exist_ok=True)
    if image is not None:
        image.filepath_raw = str(OUT / f"{name}.png")
        image.file_format = "PNG"
        image.save()
    bpy.ops.export_scene.fbx(
        filepath=str(OUT / f"{name}.fbx"),
        use_selection=True,
        object_types={"MESH"},
        path_mode="STRIP",
        embed_textures=False,
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="FACE",
        bake_space_transform=True,
    )
    size = hi - lo
    info = {
        "asset": asset,
        "width": size.x,
        "depth": size.y,
        "height": size.z,
        "triangles": sum(len(p.vertices) - 2 for p in obj.data.polygons),
        "texture": image is not None,
    }
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.window.scene = (
        bpy.data.scenes[0] if bpy.data.scenes[0] != scene else bpy.data.scenes[-1]
    )
    bpy.data.scenes.remove(scene)
    glb.unlink(missing_ok=True)
    return info


def convert_all(only: list[str] | None = None) -> dict:
    """Converts every model, or only the named ones (the others kept as they are)."""
    models = json.loads((HERE / "models.json").read_text())
    previous = bpy.context.window.scene
    written = OUT / "models.json"
    done = json.loads(written.read_text()) if only and written.exists() else {}
    for name, asset in models.items():
        if only and name not in only:
            continue
        done[name] = convert(name, asset)
    bpy.context.window.scene = previous
    (OUT / "models.json").write_text(json.dumps(done, indent=2) + "\n")
    return done
