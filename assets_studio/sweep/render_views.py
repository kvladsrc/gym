"""Blender script: front, three-quarter, back and from-above views of GLB
files, for the sweep gallery (sweep.py gallery).

    blender -b --factory-startup -P sweep/render_views.py -- LIST.json

LIST.json is [[glb, out_prefix], ...]; each GLB becomes out_prefix.{0,1,2,3}.jpg.
Workbench, orthographic, framed on the mesh: textured meshes in their
colours, shapes in clay with cavity shading so their detail shows.
"""

import json
import math
import sys

import bpy  # pyright: ignore[reportMissingImports]
from mathutils import Vector  # pyright: ignore[reportMissingImports]

SIZE = 384
# (azimuth, elevation) in degrees; glTF's front (+Z) becomes -Y in Blender (Z
# up after import). The last from above: flat things (tiles, dioramas,
# steps) show only their edge at eye level, and a tilt shows.
VIEWS = ((0, 0), (45, 0), (180, 0), (35, 45))


def render(path: str, prefix: str) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    low = Vector([min(c[i] for c in corners) for i in range(3)])
    high = Vector([max(c[i] for c in corners) for i in range(3)])
    center, size = (low + high) / 2, max(high - low)
    textured = any(image.size[0] for image in bpy.data.images)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE" if textured else "SINGLE"
    shading.show_cavity = not textured
    scene.render.resolution_x = scene.render.resolution_y = SIZE
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 85
    scene.world = bpy.data.worlds.new("world")
    camera = bpy.data.objects.new("camera", bpy.data.cameras.new("camera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = size * 1.1
    camera.data.clip_end = size * 10
    for index, (azimuth, elevation) in enumerate(VIEWS):
        a, e = math.radians(azimuth), math.radians(elevation)
        direction = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
        camera.location = center + direction * size * 3
        camera.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{prefix}.{index}.jpg"
        bpy.ops.render.render(write_still=True)


def main() -> None:
    with open(sys.argv[sys.argv.index("--") + 1]) as file:
        for path, prefix in json.load(file):
            try:
                render(path, prefix)
            except Exception as error:  # one broken file must not stop the batch
                print(f"RENDER FAILED {path}: {error}", flush=True)


main()
