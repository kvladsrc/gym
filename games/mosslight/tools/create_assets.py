"""Create Mosslight's local art in a dedicated Blender scene; export FBX."""

import math
import random
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "unity/Assets/Generated"
ART = ROOT / "art"
OUT.mkdir(parents=True, exist_ok=True)
ART.mkdir(parents=True, exist_ok=True)

# Preserve unrelated scenes. Only replace the explicitly owned prototype scene.
previous = bpy.data.scenes.get("Mosslight")
if previous:
    for obj in list(previous.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(previous)
scene = bpy.data.scenes.new("Mosslight")
bpy.context.window.scene = scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1


def material(name, color):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (*color, 1)
    return mat


MATS = {
    "Moss": material("Moss", (0.24, 0.42, 0.29)),
    "Stone": material("Stone", (0.19, 0.29, 0.32)),
    "Path": material("Path", (0.66, 0.69, 0.53)),
    "Trunk": material("Trunk", (0.30, 0.22, 0.17)),
    "Leaf": material("Leaf", (0.12, 0.32, 0.26)),
    "LeafLight": material("LeafLight", (0.27, 0.51, 0.34)),
    "Suit": material("Suit", (0.94, 0.48, 0.18)),
    "Visor": material("Visor", (0.07, 0.20, 0.25)),
    "Cream": material("Cream", (0.91, 0.90, 0.72)),
    "Glow": material("Glow", (0.40, 0.94, 0.81)),
}


def finish(obj, name, mat):
    obj.name = name
    obj.data.materials.append(MATS[mat])
    return obj


def cube(name, pos, scale, mat, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(obj, name, mat)
    if bevel:
        mod = obj.modifiers.new("Soft edges", "BEVEL")
        mod.width, mod.segments = bevel, 2
        obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")
    return obj


def cone(name, pos, r1, r2, depth, mat, vertices=8):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices, radius1=r1, radius2=r2, depth=depth, location=pos
    )
    return finish(bpy.context.object, name, mat)


def sphere(name, pos, radius, mat, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=radius, location=pos)
    obj = finish(bpy.context.object, name, mat)
    obj.scale = scale
    return obj


def export(name, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=str(OUT / f"{name}.fbx"), use_selection=True,
        object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, bake_anim=False, add_leaf_bones=False,
        use_mesh_modifiers=True,
    )


# The complete traversable level is authored here, not as Unity primitives.
cone("Island", (0, 0, -0.45), 16, 16, 0.9, "Moss", 16)
cone("FloatingRock", (0, 0, -2.6), 8, 15.9, 3.6, "Stone", 12)
for i in range(32):
    a = i * math.tau / 32
    tile = cube("PathTile", (9 * math.cos(a), 9 * math.sin(a), 0.045),
                (1.5, 1.35, 0.09), "Path", 0.1)
    tile.rotation_euler.z = a
for y in range(-7, 8, 2):
    cube("CenterPath", (0, y, 0.05), (1.9, 1.65, 0.1), "Path", 0.12)
cone("SanctuaryBase", (0, 0, 0.15), 2.7, 2.7, 0.3, "Stone", 12)
cone("SanctuaryTop", (0, 0, 0.34), 2.2, 2.2, 0.1, "Path", 12)
for x in (-2, 2):
    cube("PortalPillar", (x, 1, 2), (0.65, 0.65, 4), "Stone", 0.1)
    cube("PortalLight", (x, 0.64, 2.6), (0.18, 0.08, 1.2), "Glow", 0.02)
cube("PortalLintel", (0, 1, 4), (4.8, 0.9, 0.7), "Path", 0.15)
for step in range(4):
    cube("Stair", (-7, -3 + step, (step + 1) * 0.14),
         (3, 1, (step + 1) * 0.28), "Stone", 0.03)
cube("Overlook", (-7, 2, 0.55), (4, 5, 1.1), "Path", 0.12)
rng = random.Random(17)
for i in range(22):
    a = i * math.tau / 22
    radius = rng.uniform(12.5, 14.5)
    x, y = radius * math.cos(a), radius * math.sin(a)
    height = rng.uniform(2.2, 4.2)
    cone("TreeTrunk", (x, y, height * 0.32), 0.18, 0.12, height * 0.64, "Trunk")
    cone("TreeCrown", (x, y, height * 0.7), 1.05, 0, height, "Leaf")
    cone("TreeTip", (x, y, height), 0.75, 0, height * 0.65, "LeafLight")
for i in range(27):
    a, radius = rng.uniform(0, math.tau), rng.uniform(10.5, 15)
    sphere("Rock", (radius * math.cos(a), radius * math.sin(a), 0.3),
           rng.uniform(0.3, 0.7), "Stone", (1.2, 0.8, 1))
level_objects = list(scene.objects)
export("Level", level_objects)

# Toy robot: separate named limbs allow a lightweight procedural walk cycle.
before = set(scene.objects)
cube("Body", (0, 0, 0.94), (0.65, 0.40, 0.66), "Suit", 0.10)
cube("Head", (0, -0.01, 1.52), (0.69, 0.53, 0.5), "Cream", 0.10)
cube("Visor", (0, -0.29, 1.54), (0.53, 0.08, 0.24), "Visor", 0.05)
for x in (-0.14, 0.14):
    cube("Eye", (x, -0.342, 1.56), (0.07, 0.025, 0.055), "Glow", 0.015)
for x, side in ((-0.46, "Left"), (0.46, "Right")):
    cube(f"Arm{side}", (x, 0, 0.92), (0.21, 0.28, 0.58), "Suit", 0.07)
for x, side in ((-0.20, "Left"), (0.20, "Right")):
    cube(f"Leg{side}", (x, 0, 0.31), (0.27, 0.30, 0.55), "Visor", 0.05)
    cube(f"Boot{side}", (x, -0.08, 0.10), (0.31, 0.43, 0.20), "Cream", 0.05)
cube("Backpack", (0, 0.31, 0.99), (0.46, 0.22, 0.49), "Visor", 0.06)
cone("Antenna", (0.21, 0.02, 1.90), 0.025, 0.025, 0.28, "Stone")
sphere("AntennaLight", (0.21, 0.02, 2.07), 0.10, "Glow")
character_objects = list(set(scene.objects) - before)
export("Character", character_objects)

before = set(scene.objects)
cone("CrystalTop", (0, 0, 0.3), 0.3, 0, 0.6, "Glow", 5)
cone("CrystalBottom", (0, 0, -0.2), 0, 0.3, 0.4, "Glow", 5)
export("Crystal", list(set(scene.objects) - before))
# Show the character separately in the source scene after export.
for obj in character_objects:
    obj.location.x += 4
scene.world = bpy.data.worlds.new("MosslightWorld")
scene.world.color = (0.13, 0.20, 0.22)
bpy.ops.wm.save_as_mainfile(filepath=str(ART / "mosslight.blend"))
print(f"Mosslight: exported Level, Character, Crystal to {OUT}")
