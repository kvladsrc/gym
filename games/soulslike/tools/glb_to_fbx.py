"""A textured GLB (Hunyuan3D + Paint) as an FBX for Unity, in Blender.

Unity does not import GLB. The mesh is joined, put with its base at the
origin and centred over it, smooth shaded, and exported with its texture
embedded as <Model>.png (PaintedImport.cs extracts it in Unity).

    exec(open(".../glb_to_fbx.py").read(), globals())
    convert("model.glb", "Model.fbx")
"""

import bpy
from mathutils import Matrix, Vector


def _scene() -> bpy.types.Scene:
    scene = bpy.data.scenes.get("Soulslike convert") or bpy.data.scenes.new(
        "Soulslike convert"
    )
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.window.scene = scene
    return scene


def convert(src: str, dst: str) -> dict:
    scene = _scene()
    bpy.ops.import_scene.gltf(filepath=src)
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    for obj in scene.objects:
        obj.select_set(obj in meshes)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = world
    for other in [o for o in scene.objects if o is not obj]:
        bpy.data.objects.remove(other, do_unlink=True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # Base at the origin, centred over it (from the vertices: bound_box is
    # stale right after a transform).
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    shift = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
    obj.data.transform(Matrix.Translation(-shift))
    bpy.ops.object.shade_smooth()
    obj.name = obj.data.name = name = dst.rsplit("/", 1)[-1].removesuffix(".fbx")
    # A packed image (from the GLB) has no file name: the FBX would embed it
    # under its media folder's name, which Unity does not read as an image.
    for material in obj.data.materials:
        for node in material.node_tree.nodes if material and material.use_nodes else ():
            if node.type == "TEX_IMAGE" and node.image and node.image.packed_file:
                node.image.name = name
                node.image.filepath = f"//{name}.png"
    bpy.ops.export_scene.fbx(
        filepath=dst,
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        path_mode="COPY",
        embed_textures=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    images = [
        n.image.name
        for m in obj.data.materials
        if m and m.use_nodes
        for n in m.node_tree.nodes
        if n.type == "TEX_IMAGE" and n.image
    ]
    return {
        "faces": len(obj.data.polygons),
        "size": [
            round(max(xs) - min(xs), 3),
            round(max(ys) - min(ys), 3),
            round(max(zs) - min(zs), 3),
        ],
        "textures": images,
    }
