"""A level assembled in Blender from the studio's models, exported for Unity.

    blender -b workspace/The_Last_Vigil_Woodland_Final.blend --python games/soulslike/tools/export_level.py -- \
        --scene "The Last Vigil | Living Woodland" \
        --out games/soulslike/unity/soulslike/Assets/Soulslike/Art/Levels/LastVigil

The .blend is not changed (nothing is saved). Out come <out>/<Name>.fbx, its
textures in <out>/Textures/ and <out>/<Name>.json (the lights). Unity's
Soulslike → Build Last Vigil puts the game on it.

- The studio's models come back whole: a model in a .blend may be a reduced
  copy (the Pilgrims Reach had trees of 4,200 triangles torn at almost every
  edge: reduced without welding its UV seams). Each textured mesh is matched
  to the studio's painted model by its texture's bytes, the original (from
  the studio, cached in ~/.cache/assets-studio/originals) is welded, smooth
  shaded and fitted to the copy's box, and swapped in for all its copies.
- Every unique mesh is exported once and shared by its copies, reduced to a
  triangle budget by its size, welded first so the reduction opens no
  cracks: a level of 3,500 studio models is over 100 M triangles. Unity
  adds distance LODs on import (PaintedImport.cs).
- Blender-only materials (noise, colour ramps: the ground, the road, the
  steps, the water) are baked to textures; objects without UVs get them.
  Such a surface facing mostly down gets its normals flipped: Blender draws
  both sides of a face, a game only the front, so a ground sculpted upside
  down is invisible and cannot be stood on.
- Textures get the names of the objects that use them, saved as PNG.
- Collections become empties, the objects' parents: Unity tells the ground
  from the undergrowth by them.
- Lights become empties of the same name; their kind, colour and power go to
  the JSON. A sun gets a "<name> target" empty along its direction.
- The JSON also names the ground (objects painted in Blender alone, water
  aside): what the game stands props and actors on.
- A ground painted from tiled images by masks in colour attributes (a
  material reading "Attribute" nodes) has its masks baked too, each to
  "<Object>_<Attribute>.png" in the atlas UV: the game blends the images
  at their own scale, as a bake of 12 cm a pixel cannot (stones, twigs and
  leaves come out one colour). The JSON lists them ("masks").
- Route curves ("GUIDE" in the name) become empties every ROUTE_STEP metres
  along them, "<curve> 000", "<curve> 001", ...: the game places enemies by
  them, by name.
"""

import argparse
import hashlib
import json
import re
import struct
import sys
import urllib.request
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

# Triangle budgets by the size of an object (its largest dimension, metres,
# the largest copy): grass and pebbles need little, buildings more. A level
# of 3,500 objects at the studio's 40-60k triangles each was 100 M+.
BUDGETS = ((1.2, 3000), (3.0, 6000), (8.0, 10000), (20.0, 12000), (60.0, 30000))
LARGEST = 60000
STUDIO = "http://127.0.0.1:9000"
ROUTE_STEP = 6.0
ORIGINALS = Path.home() / ".cache" / "assets-studio" / "originals"
# Baked texture size by the object's largest dimension, metres; a simple
# shape (a box of water, a slab) gets SIMPLE whatever its size.
# At most 4096: an 8192 bake holds gigabytes of float buffers, and was killed
# for memory beside Unity and an image model (4096 on 300 m is ~7 cm a pixel).
BAKE_SIZES = ((40.0, 4096), (8.0, 2048), (2.0, 1024), (0.0, 512))
SIMPLE = (50, 512)


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--scene", required=True)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--name", default="LastVigil")
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(argv)


def base(name: str) -> str:
    """'Fir.012' → 'Fir', '01 Terrain and water.004' → '01 Terrain and water'."""
    return re.sub(r"\.\d{3}$", "", name)


def only_scene(name: str) -> bpy.types.Scene:
    scene = bpy.data.scenes[name]
    for other in list(bpy.data.scenes):
        if other != scene:
            bpy.data.scenes.remove(other)
    return scene


def select_only(scene: bpy.types.Scene, obj: bpy.types.Object) -> None:
    for o in scene.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def budget(size: float) -> int:
    return next((b for limit, b in BUDGETS if size < limit), LARGEST)


def file_name(name: str) -> str:
    """A name safe for a file and a Unity asset ("Sanctuary | Stair.003" →
    "Sanctuary_Stair_003"); LevelBuilder.Vigil compares with the same rule."""
    return re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_")


def reduce_meshes(scene: bpy.types.Scene) -> dict:
    """Each mesh shared by objects reduced once (collapse) and swapped in for all."""
    report = {}
    by_mesh: dict = {}
    for o in scene.objects:
        if o.type == "MESH":
            by_mesh.setdefault(o.data, []).append(o)
    for mesh, users in by_mesh.items():
        target = budget(max(max(o.dimensions) for o in users))
        faces = len(mesh.polygons)
        if faces <= target * 1.2:
            continue
        probe = bpy.data.objects.new("probe", mesh)
        scene.collection.objects.link(probe)
        decimate = probe.modifiers.new("reduce", "DECIMATE")
        decimate.ratio = target / faces
        evaluated = probe.evaluated_get(bpy.context.evaluated_depsgraph_get())
        reduced = bpy.data.meshes.new_from_object(evaluated)
        reduced.name = base(users[0].name)
        bpy.data.objects.remove(probe)
        mesh.user_remap(reduced)
        report[reduced.name] = (faces, len(reduced.polygons), len(users))
    return report


def originals_index() -> dict:
    """The studio's painted models (fetched once into ORIGINALS) by the SHA-1
    of their embedded texture."""
    ORIGINALS.mkdir(parents=True, exist_ok=True)
    try:
        with urllib.request.urlopen(f"{STUDIO}/api/assets?limit=1000") as response:
            assets = json.load(response)
    except OSError as error:
        print(f"studio unreachable ({error}): models stay as in the .blend", flush=True)
        return {}
    index = {}
    for asset in assets:
        if asset["mime"] != "model/gltf-binary" or "server:paint" not in asset["tags"]:
            continue
        path = ORIGINALS / f"{asset['id']}.glb"
        if not path.exists():
            with urllib.request.urlopen(
                f"{STUDIO}/api/assets/{asset['id']}/file"
            ) as response:
                path.write_bytes(response.read())
        data = path.read_bytes()
        length = struct.unpack_from("<I", data, 12)[0]
        document = json.loads(data[20 : 20 + length])
        binary = data[20 + length + 8 :]
        for image in document.get("images", []):
            view = document["bufferViews"][image["bufferView"]]
            start = view.get("byteOffset", 0)
            raw = binary[start : start + view["byteLength"]]
            index[hashlib.sha1(raw, usedforsecurity=False).hexdigest()] = path
    return index


def weld_and_smooth(mesh: bpy.types.Mesh) -> None:
    work = bmesh.new()
    work.from_mesh(mesh)
    bmesh.ops.remove_doubles(work, verts=work.verts, dist=1e-5)
    work.to_mesh(mesh)
    work.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    mesh.update()


def box(mesh: bpy.types.Mesh) -> tuple:
    xs = [v.co for v in mesh.vertices]
    lo = Vector((min(v.x for v in xs), min(v.y for v in xs), min(v.z for v in xs)))
    hi = Vector((max(v.x for v in xs), max(v.y for v in xs), max(v.z for v in xs)))
    return lo, hi


def restore_originals(scene: bpy.types.Scene, index: dict) -> dict:
    """Each textured mesh swapped for its studio original, welded, smooth and
    fitted to the copy's box (copies are re-centred, sometimes scaled)."""
    restored = {}
    meshes: dict = {}
    for o in scene.objects:
        if o.type == "MESH":
            meshes.setdefault(o.data, o)
    for mesh, user in meshes.items():
        images = [
            n.image
            for m in mesh.materials
            if m and m.node_tree
            for n in m.node_tree.nodes
            if n.type == "TEX_IMAGE" and n.image and n.image.packed_file
        ]
        if not images or not mesh.vertices:
            continue
        path = index.get(
            hashlib.sha1(images[0].packed_file.data, usedforsecurity=False).hexdigest()
        )
        if path is None:
            continue
        before = set(bpy.data.meshes)
        bpy.ops.import_scene.gltf(filepath=str(path))
        original = next(m for m in bpy.data.meshes if m not in before)
        for o in list(bpy.context.selected_objects):
            bpy.data.objects.remove(o)
        weld_and_smooth(original)
        (clo, chi), (olo, ohi) = box(mesh), box(original)
        scale = Vector(
            (c / o if o > 1e-6 else 1.0)
            for c, o in zip(chi - clo, ohi - olo, strict=True)
        )
        original.transform(
            Matrix.Translation((clo + chi) / 2)
            @ Matrix.Diagonal(scale).to_4x4()
            @ Matrix.Translation(-(olo + ohi) / 2)
        )
        original.materials.clear()
        for material in mesh.materials:
            original.materials.append(material)
        original.name = base(user.name)
        restored[original.name] = (len(mesh.polygons), len(original.polygons))
        mesh.user_remap(original)
    return restored


def needs_bake(material: bpy.types.Material) -> bool:
    """A base colour FBX cannot carry: anything but an image wired straight
    in (noise and colour ramps, an image tinted through a mix node). A plain
    colour or an image goes as it is."""
    if not material.node_tree:
        return False
    for n in material.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED":
            links = n.inputs["Base Color"].links
            return bool(links) and links[0].from_node.type != "TEX_IMAGE"
    return False


def is_emissive(material: bpy.types.Material) -> bool:
    if not material.node_tree:
        return False
    for n in material.node_tree.nodes:
        if n.type == "EMISSION":
            return True
        if (
            n.type == "BSDF_PRINCIPLED"
            and n.inputs["Emission Strength"].default_value > 0
        ):
            return True
    return False


def is_procedural(material: bpy.types.Material) -> bool:
    """Painted in Blender, no image at all: the ground, the road, the steps."""
    return bool(material.node_tree) and not any(
        n.type == "TEX_IMAGE" for n in material.node_tree.nodes
    )


def ground_of(scene: bpy.types.Scene) -> list:
    """The objects to walk on: those painted in Blender, procedurally or
    from images mixed by nodes (a ground of several layers): the studio's
    models carry an image wired straight in. Not water."""
    names = []
    for o in scene.objects:
        materials = [m for m in o.data.materials if m] if o.type == "MESH" else []
        painted = materials and all(
            (is_procedural(m) or needs_bake(m)) and not is_emissive(m) for m in materials
        )
        water = any(
            w in m.name.lower() for m in materials for w in ("water", "lake", "stream")
        )
        if painted and not water:
            names.append(o.name)
    return names


MASK_SIZE = 1024


def bake_masks(scene: bpy.types.Scene, textures: Path) -> dict:
    """Each colour attribute a ground's material reads (an Attribute node), as
    an image in the object's atlas UV: an emission of it, baked."""
    masks: dict = {}
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    for obj in [o for o in scene.objects if o.type == "MESH"]:
        names = {
            n.attribute_name
            for m in obj.data.materials if m and m.node_tree
            for n in m.node_tree.nodes if n.type == "ATTRIBUTE"
        } & {a.name for a in obj.data.color_attributes}
        atlas = next((uv for uv in obj.data.uv_layers if "atlas" in uv.name.lower()), None)
        if not names or atlas is None:
            continue
        select_only(scene, obj)
        obj.data.uv_layers.active = atlas
        kept = [slot.material for slot in obj.material_slots]
        dx, dy = obj.dimensions.x, obj.dimensions.y
        width, height = (MASK_SIZE, round(MASK_SIZE * dy / dx)) if dx >= dy else (round(MASK_SIZE * dx / dy), MASK_SIZE)
        for name in sorted(names):
            image = bpy.data.images.new(f"{file_name(obj.name)}_{file_name(name)}", width, height)
            image.colorspace_settings.name = "Non-Color"  # data, not a colour
            material = bpy.data.materials.new("Mask bake")
            material.use_nodes = True
            tree = material.node_tree
            tree.nodes.clear()
            attribute = tree.nodes.new("ShaderNodeAttribute")
            attribute.attribute_name = name
            emission = tree.nodes.new("ShaderNodeEmission")
            out = tree.nodes.new("ShaderNodeOutputMaterial")
            target = tree.nodes.new("ShaderNodeTexImage")
            target.image = image
            tree.nodes.active = target
            tree.links.new(attribute.outputs["Color"], emission.inputs["Color"])
            tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])
            for slot in obj.material_slots:
                slot.material = material
            print(f"baking mask {image.name} at {width}x{height}", flush=True)
            bpy.ops.object.bake(type="EMIT", margin=8)
            image.filepath_raw = str(textures / f"{image.name}.png")
            image.file_format = "PNG"
            image.save()
            masks.setdefault(obj.name, []).append(image.name)
        for slot, material in zip(obj.material_slots, kept, strict=True):
            slot.material = material
    return masks


def bake_procedural(scene: bpy.types.Scene, textures: Path) -> list:
    """Bake the base colour of materials FBX cannot carry, once per mesh
    (its materials belong to the mesh, so its copies share the bake)."""
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    baked = []
    done = set()
    for obj in [o for o in scene.objects if o.type == "MESH"]:
        materials = [m for m in obj.data.materials if m]
        if obj.data in done or not materials or any(is_emissive(m) for m in materials):
            continue
        if not any(needs_bake(m) for m in materials):
            continue
        done.add(obj.data)
        up = sum(
            (obj.matrix_world.to_3x3() @ p.normal).z * p.area for p in obj.data.polygons
        )
        if up < -0.3 * sum(p.area for p in obj.data.polygons):
            obj.data.flip_normals()
            print(f"flipped normals: {obj.name} faced down")
        select_only(scene, obj)
        # A UV map made for this ("… atlas") is the bake's: the first one may
        # be a tiling layer's (a ground of 4 m tiles), the bake through it a mush.
        atlas = next((uv for uv in obj.data.uv_layers if "atlas" in uv.name.lower()), None)
        if atlas is not None:
            obj.data.uv_layers.active = atlas
            atlas.active_render = True
        if not obj.data.uv_layers:
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.uv.smart_project(island_margin=0.002)
            bpy.ops.object.mode_set(mode="OBJECT")
        size = next(s for limit, s in BAKE_SIZES if max(obj.dimensions) >= limit)
        if len(obj.data.polygons) < SIMPLE[0]:
            size = min(size, SIMPLE[1])
        # Each object its own bake, named after it.
        name = file_name(obj.name)
        # The image in the object's proportions (300 x 500 m: 2458 x 4096).
        dx, dy = obj.dimensions.x, obj.dimensions.y
        width, height = (size, size) if atlas is None or dx <= 0 or dy <= 0 else (
            (size, round(size * dy / dx)) if dx >= dy else (round(size * dx / dy), size))
        image = bpy.data.images.new(name, width, height)
        for slot in obj.material_slots:
            slot.material = slot.material.copy()
            node = slot.material.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = image
            slot.material.node_tree.nodes.active = node
        print(f"baking {name} at {size}", flush=True)
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, margin=8)
        # The baked image as a plain textured material.
        for slot in obj.material_slots:
            tree = slot.material.node_tree
            tree.nodes.clear()
            out = tree.nodes.new("ShaderNodeOutputMaterial")
            bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
            tex = tree.nodes.new("ShaderNodeTexImage")
            tex.image = image
            bsdf.inputs["Roughness"].default_value = 0.9
            tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
            tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
            slot.material.name = name
        image.filepath_raw = str(textures / f"{name}.png")
        image.file_format = "PNG"
        image.save()
        # The game gets the atlas alone: the other maps are the tiling layers'.
        if atlas is not None:
            for uv in [uv for uv in obj.data.uv_layers if uv.name != atlas.name]:
                obj.data.uv_layers.remove(uv)
        baked.append((name, size))
    return baked


def save_textures(scene: bpy.types.Scene, textures: Path) -> int:
    """The models' images, named after the first object using them."""
    named = {}
    for obj in scene.objects:
        if obj.type != "MESH":
            continue
        for m in obj.data.materials:
            if not m or not m.node_tree:
                continue
            for n in m.node_tree.nodes:
                if n.type != "TEX_IMAGE" or not n.image or n.image in named:
                    continue
                if Path(n.image.filepath_raw).parent != textures:  # not baked here
                    name = file_name(base(obj.name))
                    while name in named.values() or (textures / f"{name}.png").exists():
                        name += "_"
                    named[n.image] = name
    for image, name in named.items():
        image.name = name
        image.filepath_raw = str(textures / f"{name}.png")
        image.file_format = "PNG"
        image.save()
    return len(named)


def parent_by_collection(scene: bpy.types.Scene) -> None:
    for c in scene.collection.children:
        group = bpy.data.objects.new(base(c.name), None)
        scene.collection.objects.link(group)
        for o in list(c.all_objects):
            if o.parent is None and o != group:
                world = o.matrix_world.copy()
                o.parent = group
                o.matrix_world = world


def routes_to_empties(scene: bpy.types.Scene) -> dict:
    """Each route curve as empties along it, ROUTE_STEP apart (by length)."""
    routes = {}
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for curve in [o for o in scene.objects if o.type == "CURVE" and "GUIDE" in o.name]:
        evaluated = curve.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        points = [curve.matrix_world @ v.co for v in mesh.vertices]
        evaluated.to_mesh_clear()
        placed, walked, last = [], ROUTE_STEP, None
        for point in points:
            if last is not None:
                walked += (point - last).length
            last = point
            if walked >= ROUTE_STEP:
                empty = bpy.data.objects.new(f"{curve.name} {len(placed):03d}", None)
                scene.collection.objects.link(empty)
                empty.location = point
                placed.append(empty.name)
                walked = 0.0
        routes[curve.name] = len(placed)
    return routes


def lights_to_empties(scene: bpy.types.Scene) -> list:
    lights = []
    for o in [o for o in scene.objects if o.type == "LIGHT"]:
        light = o.data
        entry = {
            "name": o.name,
            "type": light.type,
            "color": list(light.color),
            "energy": light.energy,
            "radius": getattr(light, "shadow_soft_size", 0.0),
        }
        empty = bpy.data.objects.new(o.name, None)
        scene.collection.objects.link(empty)
        empty.parent = o.parent
        empty.matrix_world = o.matrix_world.copy()
        if light.type == "SUN":
            # A sun shines along its local -Z.
            direction = o.matrix_world.to_quaternion() @ Vector((0.0, 0.0, -1.0))
            target = bpy.data.objects.new(o.name + " target", None)
            scene.collection.objects.link(target)
            target.location = o.matrix_world.translation + direction * 10.0
            entry["target"] = target.name
        name = o.name
        bpy.data.objects.remove(o)
        empty.name = name
        lights.append(entry)
    return lights


def main() -> None:
    args = arguments()
    out: Path = args.out.resolve()
    textures = out / "Textures"
    textures.mkdir(parents=True, exist_ok=True)
    scene = only_scene(args.scene)
    for o in [o for o in scene.objects if o.type in ("CAMERA", "FONT")]:
        bpy.data.objects.remove(o)
    print("restoring the studio's originals", flush=True)
    restored = restore_originals(scene, originals_index())
    print("reducing meshes", flush=True)
    reduced = reduce_meshes(scene)
    ground = ground_of(scene)
    masks = bake_masks(scene, textures)
    baked = bake_procedural(scene, textures)
    saved = save_textures(scene, textures)
    print("saving textures, exporting", flush=True)
    lights = lights_to_empties(scene)
    routes = routes_to_empties(scene)
    parent_by_collection(scene)
    for o in scene.objects:
        o.select_set(o.type in ("MESH", "EMPTY"))
    bpy.ops.export_scene.fbx(
        filepath=str(out / f"{args.name}.fbx"),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        use_mesh_modifiers=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        path_mode="STRIP",
        embed_textures=False,
        add_leaf_bones=False,
        bake_anim=False,
    )
    (out / f"{args.name}.json").write_text(
        json.dumps({"lights": lights, "ground": ground, "routes": routes, "masks": masks}, indent=1)
    )
    total = sum(len(o.data.polygons) for o in scene.objects if o.type == "MESH")
    print(
        json.dumps(
            {
                "restored": restored,
                "reduced": reduced,
                "baked": baked,
                "textures": saved,
                "lights": len(lights),
                "objects": len(scene.objects),
                "faces": total,
            }
        )
    )


if __name__ == "__main__":
    main()
