"""Renders the studio's models for review, in Blender, in a scene of its own
(the open scene is left as it is): each model asset from two sides on a grey
ground, to <out>/<name>_<side>.png.

    exec(open(".../art/review_render.py").read(), globals())
    render({"house": "<asset id>", ...}, "/tmp/review")
"""

import math
import os
import tempfile
import urllib.request
from pathlib import Path

import bpy
from mathutils import Vector

STUDIO = os.environ.get("STUDIO_URL", "http://127.0.0.1:9000")


def render(
    models: dict, out: str, size=(800, 640), sides=(("front", -30), ("back", 150))
) -> dict:
    out_dir = Path(out)
    out_dir.mkdir(parents=True, exist_ok=True)
    previous = bpy.context.window.scene
    scene = bpy.data.scenes.new("FinishTheGame review")
    bpy.context.window.scene = scene
    engines = [
        e.identifier
        for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items
    ]
    scene.render.engine = (
        "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    )
    scene.render.resolution_x, scene.render.resolution_y = size
    world = bpy.data.worlds.new("review")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.57, 0.6, 1)
    scene.world = world
    sun_data = bpy.data.lights.new("sun", "SUN")
    sun_data.energy = 3.0
    sun = bpy.data.objects.new("sun", sun_data)
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    scene.collection.objects.link(sun)
    facts = {}
    for name, asset in models.items():
        glb = Path(tempfile.gettempdir()) / f"ftg-review-{asset}.glb"
        urllib.request.urlretrieve(f"{STUDIO}/api/assets/{asset}/file", glb)
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=str(glb))
        new = [o for o in bpy.data.objects if o not in before]
        meshes = [o for o in new if o.type == "MESH"]
        corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
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
        middle, extent = (lo + hi) / 2, max(hi - lo)
        facts[name] = {
            "size": [round(v, 3) for v in (hi - lo)],
            "triangles": sum(len(o.data.polygons) for o in meshes),
        }
        for side, angle in sides:
            cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
            scene.collection.objects.link(cam)
            a = math.radians(angle)
            r = extent * 2.1
            cam.location = (
                middle.x + r * math.sin(a),
                middle.y - r * math.cos(a),
                middle.z + extent * 0.35,
            )
            cam.rotation_euler = (
                (middle - cam.location).to_track_quat("-Z", "Y").to_euler()
            )
            scene.camera = cam
            scene.render.filepath = str(out_dir / f"{name}_{side}.png")
            bpy.ops.render.render(write_still=True)
            bpy.data.objects.remove(cam)
        for o in new:
            bpy.data.objects.remove(o, do_unlink=True)
        glb.unlink(missing_ok=True)
    bpy.data.objects.remove(sun)
    bpy.context.window.scene = previous
    bpy.data.scenes.remove(scene)
    return facts
