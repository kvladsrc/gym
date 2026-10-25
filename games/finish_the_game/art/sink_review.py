"""Renders each tree's foot set into the ground at a few depths (a share of
its height, LEVELS), from two sides, in Blender, in a scene of its own: to
<out>/<name>_<side>_<depth %>.png, to choose by eye where it sits as a tree
does (StorySceneBuilder's Rooted table).

    exec(open(".../art/sink_review.py").read(), globals())
    render(["oak", "pine"], "/tmp/sink")
"""

import math

import bpy
from mathutils import Vector

M = "/home/myuser/Dropbox/src/games/finish_the_game/unity/Finish the game/Assets/FinishTheGame/Art/Models/"
LEVELS = [0, 0.03, 0.06, 0.09, 0.12, 0.16]


def render(names, OUT):
    OUT = OUT.rstrip("/") + "/"
    prev = bpy.context.window.scene
    sc = bpy.data.scenes.new("sink")
    bpy.context.window.scene = sc
    eng = [
        e.identifier
        for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items
    ]
    sc.render.engine = (
        "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in eng else "BLENDER_EEVEE"
    )
    sc.render.resolution_x = sc.render.resolution_y = 360
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.6, 0.65, 0.75, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    sc.world = w
    sd = bpy.data.lights.new("sun", "SUN")
    sd.energy = 3
    sun = bpy.data.objects.new("sun", sd)
    sun.rotation_euler = (math.radians(55), 0, math.radians(30))
    sc.collection.objects.link(sun)
    gm = bpy.data.materials.new("ground")
    gm.diffuse_color = (0.35, 0.42, 0.18, 1)
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (
        0.35,
        0.42,
        0.18,
        1,
    )
    cd = bpy.data.cameras.new("cam")
    cd.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    for name in names:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=M + name + ".fbx")
        objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        img = bpy.data.images.load(M + name + ".png")
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        nt = mat.node_tree
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nt.links.new(tex.outputs[0], nt.nodes["Principled BSDF"].inputs[0])
        nt.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1
        lo = Vector((1e9,) * 3)
        hi = Vector((-1e9,) * 3)
        for o in objs:
            o.data.materials.clear()
            o.data.materials.append(mat)
            for c in o.bound_box:
                p = o.matrix_world @ Vector(c)
                lo = Vector(map(min, lo, p))
                hi = Vector(map(max, hi, p))
        H = hi.z - lo.z
        mid = (lo + hi) / 2
        bpy.ops.mesh.primitive_plane_add(size=1)
        plane = bpy.context.active_object
        plane.scale = (H * 3, H * 3, 1)
        plane.data.materials.append(gm)
        cd.ortho_scale = 0.55 * H
        el = math.radians(14)
        target = Vector((mid.x, mid.y, lo.z + 0.1 * H))
        for side, az in (("a", -35), ("b", 145)):
            d = Vector(
                (
                    math.cos(el) * math.sin(math.radians(az)),
                    -math.cos(el) * math.cos(math.radians(az)),
                    math.sin(el),
                )
            )
            cam.location = target + d * H * 4
            cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
            for k in LEVELS:
                plane.location = (mid.x, mid.y, lo.z + k * H)
                sc.render.filepath = f"{OUT}{name}_{side}_{int(k * 100):02d}.png"
                bpy.ops.render.render(write_still=True)
        for o in objs + [plane]:
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.context.window.scene = prev
    bpy.data.scenes.remove(sc)
