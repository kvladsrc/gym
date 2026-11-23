import os
import subprocess
from pathlib import Path

import pytest
from animation_tools_model_server.server import (
    HAIR_SCRIPT,
    AnimationToolsServer,
    BakeColourParams,
    BakeParams,
    FitHairParams,
    RetargetParams,
    RetopoParams,
    blender,
)
from asset_processing.blender import RETARGET_SCRIPT

from model_server_sdk import InputData, InvalidInput, Job

pytestmark = pytest.mark.skipif(
    os.environ.get("RUN_BLENDER_TESTS") != "1",
    reason="run just integration for real Blender checks",
)


def invoke(binary, fixture, operation, work):
    done = subprocess.run(
        [
            binary,
            "-b",
            "--factory-startup",
            "--python-exit-code",
            "1",
            "-P",
            str(fixture),
            "--",
            operation,
            str(work),
        ],
        capture_output=True,
        text=True,
        timeout=120,
    )
    assert done.returncode == 0, done.stdout + done.stderr
    if not operation.startswith("make"):
        assert "BLENDER_ASSERTIONS_OK" in done.stdout


@pytest.mark.parametrize("mode", ["world", "stationary", "in-place"])
@pytest.mark.parametrize("turn", [False, True])
def test_bind_pose_weights_terminal_twist_and_root_motion(tmp_path, mode, turn):
    binary = blender()
    assert binary, "Blender is required"
    fixture = Path(__file__).with_name("blender_fixture.py")
    invoke(binary, fixture, "make-turn" if turn else "make", tmp_path)
    model = AnimationToolsServer()
    model.load()
    inputs = {"rig": InputData("model/x-fbx", (tmp_path / "target.fbx").read_bytes())}
    prepared = model.generate(Job("prepare-rig", None, inputs, RetargetParams(), (42,)))[0]
    (tmp_path / "prepared.fbx").write_bytes(prepared.data)
    invoke(binary, fixture, "check-rig", tmp_path)
    inputs["motion"] = InputData("model/x-fbx", (tmp_path / "motion.fbx").read_bytes())
    animated = model.generate(
        Job("retarget-motion", None, inputs, RetargetParams(root_motion=mode), (42,))
    )[0]
    (tmp_path / "animated.fbx").write_bytes(animated.data)
    invoke(binary, fixture, "check-" + mode + ("-turn" if turn else ""), tmp_path)


@pytest.mark.parametrize("mode", ["stationary", "in-place"])
def test_same_skeleton_delta_preserves_bob_and_optional_heading(tmp_path, mode):
    binary = blender()
    assert binary
    fixture = Path(__file__).with_name("blender_fixture.py")
    invoke(binary, fixture, "make-same-turn", tmp_path)
    done = subprocess.run(
        [
            binary,
            "-b",
            "--factory-startup",
            "--python-exit-code",
            "1",
            "-P",
            str(RETARGET_SCRIPT),
            "--",
            str(tmp_path / "target.fbx"),
            str(tmp_path / "motion.fbx"),
            str(tmp_path / "animated.fbx"),
            "sides",
            "auto",
            "auto",
            mode,
            "delta",
        ],
        capture_output=True,
        text=True,
        timeout=120,
    )
    assert done.returncode == 0, done.stdout + done.stderr
    invoke(binary, fixture, "check-" + mode + "-turn-same", tmp_path)


@pytest.mark.parametrize("mode", ["world", "stationary", "in-place"])
@pytest.mark.parametrize("alignment", ["bone-directions", "matched-neutral"])
def test_smpl_shape_does_not_bend_neutral_torso_and_keeps_head_motion(tmp_path, mode, alignment):
    binary = blender()
    assert binary
    fixture = Path(__file__).with_name("smpl_fixture.py")
    invoke(binary, fixture, "make", tmp_path)
    done = subprocess.run(
        [
            binary,
            "-b",
            "--factory-startup",
            "--python-exit-code",
            "1",
            "-P",
            str(RETARGET_SCRIPT),
            "--",
            str(tmp_path / "target.fbx"),
            str(tmp_path / "motion.fbx"),
            str(tmp_path / "animated.fbx"),
            "sides",
            "auto",
            "auto",
            mode,
            "smpl-neutral" if alignment == "matched-neutral" else "smpl",
        ],
        capture_output=True,
        text=True,
        timeout=120,
    )
    assert done.returncode == 0, done.stdout + done.stderr
    invoke(
        binary,
        fixture,
        ("check-neutral-" if alignment == "matched-neutral" else "check-") + mode,
        tmp_path,
    )


MAKE_MESHES = """
import sys, bpy
work = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
# Detailed: a sphere with bumps; game mesh: a coarse sphere, unwrapped, with a material.
bpy.ops.mesh.primitive_uv_sphere_add(segments=128, ring_count=64)
high = bpy.context.active_object
texture = bpy.data.textures.new("bumps", "CLOUDS")
displace = high.modifiers.new("d", "DISPLACE")
displace.texture, displace.strength = texture, 0.05
bpy.ops.object.modifier_apply(modifier="d")
high.data.materials.append(bpy.data.materials.new("detailed"))
bpy.ops.export_scene.gltf(filepath=work + "/high.glb", export_format="GLB", use_selection=True)
bpy.ops.object.delete()
bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12)
low = bpy.context.active_object
low.data.materials.append(bpy.data.materials.new("painted"))
bpy.ops.export_scene.gltf(filepath=work + "/low.glb", export_format="GLB", use_selection=True)
"""


def test_retopology_and_normal_baking(tmp_path):
    import json
    import struct

    binary = blender()
    assert binary, "Blender is required"
    subprocess.run(
        [binary, "-b", "--factory-startup", "--python-expr", MAKE_MESHES, "--", str(tmp_path)],
        check=True,
        capture_output=True,
        timeout=120,
    )
    server = AnimationToolsServer()
    server.load()
    high = (tmp_path / "high.glb").read_bytes()
    low = (tmp_path / "low.glb").read_bytes()

    def job(task, inputs, params):
        glbs = {role: InputData("model/gltf-binary", data) for role, data in inputs.items()}
        return Job(task, None, glbs, params, (0,))

    retopo = server.generate(job("3d-retopo", {"mesh": high}, RetopoParams(faces=2000)))[0]
    assert retopo.meta["quads"] > 1000
    assert retopo.meta["voxels_across"] in (256, 128, 64)
    assert retopo.meta["material_dropped"]

    # A painted mesh keeps its UVs and material through decimation.
    decimated = server.generate(
        job("3d-retopo", {"mesh": high}, RetopoParams(faces=2000, method="decimate"))
    )[0]
    assert decimated.meta["kept_material"]
    assert not decimated.meta["material_dropped"]
    length = struct.unpack("<I", decimated.data[12:16])[0]
    gltf = json.loads(decimated.data[20 : 20 + length])
    assert gltf["materials"][0]["name"] == "detailed"
    assert "TEXCOORD_0" in gltf["meshes"][0]["primitives"][0]["attributes"]
    assert decimated.meta["triangles"] <= 2100

    baked = server.generate(
        job("3d-bake-normals", {"high": high, "low": low}, BakeParams(texture_size=1024))
    )[0]
    length = struct.unpack("<I", baked.data[12:16])[0]
    gltf = json.loads(baked.data[20 : 20 + length])
    assert "normalTexture" in gltf["materials"][0]


# A sphere painted red above its equator and blue below (one UV point per
# face), and a coarse icosphere without UVs or material to take the paint.
# The paint is metallic, as TRELLIS.2 marks whole tree crowns: its base
# colour still has to carry over (a diffuse bake turned it black).
MAKE_PAINTED = """
import sys, bpy
work = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32)
painted = bpy.context.active_object
image = bpy.data.images.new("paint", 4, 4)
image.pixels = [c for y in range(4) for x in range(4)
                for c in ((0.8, 0.1, 0.1, 1.0) if y >= 2 else (0.1, 0.1, 0.8, 1.0))]
image.pack()
uv = painted.data.uv_layers.active.data
for polygon in painted.data.polygons:
    for i in polygon.loop_indices:
        uv[i].uv = (0.5, 0.75 if polygon.center.z > 0 else 0.25)
material = bpy.data.materials.new("paint")
material.use_nodes = True
texture = material.node_tree.nodes.new("ShaderNodeTexImage")
texture.image = image
principled = material.node_tree.nodes["Principled BSDF"]
material.node_tree.links.new(texture.outputs["Color"], principled.inputs["Base Color"])
# Metal above (roughness 0.3), matte below (0.9), packed as glTF keeps them.
metal = bpy.data.images.new("metal", 4, 4)
metal.colorspace_settings.name = "Non-Color"
metal.pixels = [c for y in range(4) for x in range(4)
                for c in ((1.0, 0.3, 1.0, 1.0) if y >= 2 else (1.0, 0.9, 0.0, 1.0))]
metal.pack()
metal_texture = material.node_tree.nodes.new("ShaderNodeTexImage")
metal_texture.image = metal
split = material.node_tree.nodes.new("ShaderNodeSeparateColor")
material.node_tree.links.new(metal_texture.outputs["Color"], split.inputs["Color"])
material.node_tree.links.new(split.outputs["Green"], principled.inputs["Roughness"])
material.node_tree.links.new(split.outputs["Blue"], principled.inputs["Metallic"])
painted.data.materials.append(material)
bpy.ops.export_scene.gltf(filepath=work + "/painted.glb", export_format="GLB", use_selection=True)
bpy.ops.object.delete()
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3)
coarse = bpy.context.active_object
while coarse.data.uv_layers:
    coarse.data.uv_layers.remove(coarse.data.uv_layers[0])
bpy.ops.export_scene.gltf(
    filepath=work + "/coarse.glb", export_format="GLB", use_selection=True,
    export_materials="NONE", export_texcoords=False,
)
"""

# Faces well above or below the equator whose baked colour (at their UV
# centre) is not the paint's there, and those whose paint channel came out
# darker than the paint's 0.8.
CHECK_PAINT = """
import sys, bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[sys.argv.index("--") + 1])
obj = next(o for o in bpy.data.objects if o.type == "MESH")
tree = obj.data.materials[0].node_tree
principled = next(n for n in tree.nodes if n.type == "BSDF_PRINCIPLED")
def feeding(name):
    node = principled.inputs[name].links[0].from_node
    while node.type != "TEX_IMAGE":
        node = next(i for i in node.inputs if i.is_linked).links[0].from_node
    return node.image
image = feeding("Base Color")
metal = feeding("Metallic")
w, h = image.size
pixels = image.pixels[:]
metal_pixels = metal.pixels[:]
uv = obj.data.uv_layers.active.data
checked = wrong = dim = wrong_metal = 0
for polygon in obj.data.polygons:
    z = (obj.matrix_world @ polygon.center).z
    if abs(z) < 0.2:
        continue
    u = sum(uv[i].uv.x for i in polygon.loop_indices) / polygon.loop_total
    v = sum(uv[i].uv.y for i in polygon.loop_indices) / polygon.loop_total
    at = 4 * (min(int(v * h), h - 1) * w + min(int(u * w), w - 1))
    red, blue = pixels[at], pixels[at + 2]
    checked += 1
    wrong += (red > blue) != (z > 0)
    dim += max(red, blue) < 0.7
    rough, metallic = metal_pixels[at + 1], metal_pixels[at + 2]
    wanted = (0.3, 1.0) if z > 0 else (0.9, 0.0)
    wrong_metal += abs(rough - wanted[0]) > 0.08 or abs(metallic - wanted[1]) > 0.08
print("PAINT_CHECK", checked, wrong, dim, wrong_metal)
"""


def test_colour_baking_carries_the_paint_and_metal_to_an_unwrapped_mesh(tmp_path):
    binary = blender()
    assert binary, "Blender is required"
    subprocess.run(
        [binary, "-b", "--factory-startup", "--python-expr", MAKE_PAINTED, "--", str(tmp_path)],
        check=True,
        capture_output=True,
        timeout=120,
    )
    server = AnimationToolsServer()
    server.load()
    painted = InputData("model/gltf-binary", (tmp_path / "painted.glb").read_bytes())
    coarse = InputData("model/gltf-binary", (tmp_path / "coarse.glb").read_bytes())
    params = BakeColourParams(texture_size=1024)

    baked = server.generate(
        Job("3d-bake-colour", None, {"painted": painted, "mesh": coarse}, params, (0,))
    )[0]
    assert baked.meta["unwrapped"]
    assert baked.meta["triangles"] == 320
    baked_path = tmp_path / "baked.glb"
    baked_path.write_bytes(baked.data)
    done = subprocess.run(
        [binary, "-b", "--factory-startup", "--python-expr", CHECK_PAINT, "--", str(baked_path)],
        check=True,
        capture_output=True,
        text=True,
        timeout=120,
    )
    line = next(line for line in done.stdout.splitlines() if line.startswith("PAINT_CHECK"))
    checked, wrong, dim, wrong_metal = map(int, line.split()[1:])
    assert checked > 200
    assert wrong == 0
    assert dim == 0
    assert baked.meta["metallic_roughness"]
    assert wrong_metal == 0

    # The paint has to come from a mesh that has some.
    swapped = Job("3d-bake-colour", None, {"painted": coarse, "mesh": painted}, params, (0,))
    with pytest.raises(InvalidInput, match="no UVs or material"):
        server.generate(swapped)


# Six thin sheets meeting at one long spine, like a book: every spine edge has
# six faces (non-manifold), which edge collapse cannot pass, so 18,000
# triangles stop far above a 500-triangle target.
MAKE_BOOK = """
import math, sys, bmesh, bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
work = bmesh.new()
along, across = 150, 10
spine = [work.verts.new((0.0, v / along, 0.0)) for v in range(along + 1)]
for sheet in range(6):
    dx, dz = math.cos(sheet * math.pi / 3), math.sin(sheet * math.pi / 3)
    rows = [spine]
    for u in range(1, across + 1):
        x, z = 0.1 * dx * u / across, 0.1 * dz * u / across
        rows.append([work.verts.new((x, v / along, z)) for v in range(along + 1)])
    for u in range(across):
        for v in range(along):
            work.faces.new((rows[u][v], rows[u + 1][v], rows[u + 1][v + 1], rows[u][v + 1]))
mesh = bpy.data.meshes.new("book")
work.to_mesh(mesh)
bpy.context.collection.objects.link(bpy.data.objects.new("book", mesh))
bpy.ops.export_scene.gltf(filepath=sys.argv[-1], export_format="GLB")
"""


def test_decimation_stopping_above_its_target_fails(tmp_path):
    binary = blender()
    assert binary, "Blender is required"
    book = tmp_path / "book.glb"
    subprocess.run(
        [binary, "-b", "--factory-startup", "--python-expr", MAKE_BOOK, "--", str(book)],
        check=True,
        capture_output=True,
        timeout=120,
    )
    server = AnimationToolsServer()
    server.load()
    mesh = {"mesh": InputData("model/gltf-binary", book.read_bytes())}
    job = Job("3d-retopo", None, mesh, RetopoParams(faces=500, method="decimate"), (0,))
    with pytest.raises(InvalidInput, match="non-manifold edges"):
        server.generate(job)


def read_glb(data):
    """(document, accessor reader) of a GLB, without numpy."""
    import json
    import struct

    length = struct.unpack_from("<I", data, 12)[0]
    document = json.loads(data[20 : 20 + length])
    binary = data[28 + length :]
    formats = {5121: "B", 5123: "H", 5125: "I", 5126: "f"}
    widths = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}

    def accessor(index):
        a = document["accessors"][index]
        view = document["bufferViews"][a["bufferView"]]
        width = widths[a["type"]]
        start = view.get("byteOffset", 0) + a.get("byteOffset", 0)
        values = struct.unpack_from(
            f"<{a['count'] * width}{formats[a['componentType']]}", binary, start
        )
        return [values[k : k + width] for k in range(0, len(values), width)]

    return document, accessor


def test_hair_fitting_puts_hairdeck_cards_on_a_soma_head(tmp_path):
    binary = blender()
    assert binary, "Blender is required"
    fixture = Path(__file__).with_name("hair_fixture.py")
    haircap = HAIR_SCRIPT.with_name("soma_haircap.json")
    subprocess.run(
        [binary, "-b", "--factory-startup", "-P", str(fixture), "--", str(haircap), str(tmp_path)],
        check=True,
        capture_output=True,
        timeout=120,
    )
    server = AnimationToolsServer()
    server.load()
    body = (tmp_path / "body.glb").read_bytes()
    hair = (tmp_path / "hair.glb").read_bytes()

    def fit(params, body=body, hair=hair):
        inputs = {
            "body": InputData("model/gltf-binary", body),
            "hair": InputData("model/gltf-binary", hair),
        }
        return server.generate(Job("3d-fit-hair", None, inputs, params, (0,)))[0]

    fitted = fit(FitHairParams())
    report = fitted.meta
    assert report["cards"] == 69  # 66 on the crown and sides, 3 braid cards
    assert not report["proxy_in_file"]
    assert report["vertices_inside_after"] == 0
    # The roots land on the skin plus the clearance, not on HairDeck's shell.
    assert 0.5 < report["root_gap_mm"]["median"] < 4
    # The whole hairstyle moved as one piece: the cards bent only with the
    # field (real HairDeck hair: p95 0.17-0.37 of a card's length).
    assert report["card_shape_change"]["p95"] < 0.15
    assert report["braid"] is None

    document, accessor = read_glb(fitted.data)
    source, source_accessor = read_glb(body)
    assert len(document["meshes"]) == len(source["meshes"]) + 1
    for name in ("POSITION", "JOINTS_0", "WEIGHTS_0"):  # the body as it came
        index = source["meshes"][0]["primitives"][0]["attributes"][name]
        assert accessor(index) == source_accessor(index)
    names = [node.get("name") for node in document["nodes"]]
    node = document["nodes"][names.index("hair")]
    assert node["skin"] == 0
    assert names.index("hair") in document["scenes"][0]["nodes"]
    primitive = document["meshes"][node["mesh"]]["primitives"][0]
    assert {"POSITION", "NORMAL", "TEXCOORD_0", "COLOR_0", "JOINTS_0", "WEIGHTS_0"} <= set(
        primitive["attributes"]
    )
    head = [document["nodes"][j]["name"] for j in document["skins"][0]["joints"]].index("Head")
    assert {j[0] for j in accessor(primitive["attributes"]["JOINTS_0"])} == {head}
    assert {w[0] for w in accessor(primitive["attributes"]["WEIGHTS_0"])} == {1.0}
    assert document["materials"][primitive["material"]]["doubleSided"]

    # A braid follows the chest down the back; the crown stays on the head.
    braided = fit(FitHairParams(braid=True, thickness=0.8))
    assert braided.meta["braid"]["vertices_following_chest"] > 0
    document, accessor = read_glb(braided.data)
    names = [node.get("name") for node in document["nodes"]]
    primitive = document["meshes"][document["nodes"][names.index("hair")]["mesh"]]["primitives"][0]
    chest = [w[1] for w in accessor(primitive["attributes"]["WEIGHTS_0"])]
    assert max(chest) > 0.9
    assert sum(1 for w in chest if w > 0) < len(chest) / 3

    with pytest.raises(InvalidInput, match="not a SOMA person"):
        fit(FitHairParams(), body=(tmp_path / "not_soma.glb").read_bytes())
    with pytest.raises(InvalidInput, match="hair_cards"):
        fit(FitHairParams(), hair=body)
