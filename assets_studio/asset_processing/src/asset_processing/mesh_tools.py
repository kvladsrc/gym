"""Retopology and normal baking, a Blender script (blender -b -P mesh_tools.py -- ...).

    retopo IN.glb OUT.glb FACES METHOD        METHOD: quadriflow | decimate
    bake HIGH.glb LOW.glb OUT.glb SIZE RAY    RAY: ray distance, a share of the size

Prints ``MESH_REPORT {json}`` when done, ``MESH_INVALID <reason>`` for an input
it cannot use.

- Retopology makes a game mesh from a detailed one (Hi3D's million
  triangles): QuadriFlow lays quads along the shape's curvature, decimation
  only collapses edges (triangles, any input). QuadriFlow needs a closed
  manifold surface with consistent normals; a generated mesh rarely is (Hi3D's
  passed an edge check yet failed QuadriFlow's), so it is always
  voxel-remeshed first, at 1/256 of its largest side if that passes, finer than any detail the
  game mesh keeps (that goes to the baked normals); coarser when QuadriFlow
  refuses (see quadriflow()). glTF stores triangles: the quads arrive as
  pairs, with their edge flow.
- Baking moves the detailed mesh's surface into a tangent-space normal map
  on the painted game mesh (its UVs, as the painter unwrapped it) and wires
  it into the material, so the GLB carries base colour, metal-roughness and
  normals. The painter may have rescaled the mesh: the detailed one is put
  over it by their bounding boxes (centre, largest side) first.
"""

import json
import sys
from pathlib import Path
from typing import Any

import bmesh  # pyright: ignore[reportMissingImports]
import bpy  # pyright: ignore[reportMissingImports]
from mathutils import Vector  # pyright: ignore[reportMissingImports]

# Voxel remesh resolutions tried for QuadriFlow, finest first.
VOXELS_ACROSS = (256, 128, 64)


def invalid(reason: str) -> None:
    print("MESH_INVALID " + reason, flush=True)
    sys.exit(1)


def report(**data: Any) -> None:
    print("MESH_REPORT " + json.dumps(data), flush=True)


def triangles(obj: Any) -> int:
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def activate(*objects: Any) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[-1]


def import_joined(path: Path, what: str) -> Any:
    """The GLB's meshes as one object, transforms applied."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    if not meshes:
        invalid(f"the {what} has no mesh")
    activate(*meshes)
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for other in [o for o in bpy.data.objects if o not in before and o is not obj]:
        if other.type != "MESH":
            bpy.data.objects.remove(other, do_unlink=True)
    return obj


def weld(obj: Any) -> None:
    work = bmesh.new()
    work.from_mesh(obj.data)
    bmesh.ops.remove_doubles(work, verts=work.verts, dist=1e-6 * max(obj.dimensions))
    work.to_mesh(obj.data)
    work.free()


def export(obj: Any, path: Path, *, materials: bool) -> None:
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    activate(obj)
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_materials="EXPORT" if materials else "NONE",
        export_texcoords=materials,
        export_normals=True,
        export_yup=True,
    )


def drop_specks(obj: Any) -> int:
    """Remove connected pieces under 0.1 % of the faces (a voxel remesh leaves
    a few closed specks of 6-10 faces); returns how many faces went."""
    work = bmesh.new()
    work.from_mesh(obj.data)
    seen: set[Any] = set()
    specks: list[Any] = []
    limit = 0.001 * len(work.faces)
    for face in work.faces:
        if face in seen:
            continue
        piece, stack = [face], [face]
        seen.add(face)
        while stack:
            for edge in stack.pop().edges:
                for other in edge.link_faces:
                    if other not in seen:
                        seen.add(other)
                        stack.append(other)
                        piece.append(other)
        if len(piece) < limit:
            specks += piece
    bmesh.ops.delete(work, geom=specks, context="FACES")
    bmesh.ops.delete(work, geom=[v for v in work.verts if not v.link_faces], context="VERTS")
    work.to_mesh(obj.data)
    work.free()
    obj.data.update()
    return len(specks)


def edit(obj: Any, *operations: Any) -> None:
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    for operation in operations:
        operation()
    bpy.ops.object.mode_set(mode="OBJECT")


def quadriflow(obj: Any, faces: int) -> int:
    """QuadriFlow over a voxel remesh of ``obj``; returns the voxels across
    the largest side that worked. QuadriFlow's check rejects a voxel remesh as
    it is (even of the monkey primitive, Blender 5.1): it is welded (doubled
    vertices), cleared of specks, made consistent and triangulated first. On
    Hi3D's troll 1/256 still failed (thin claws and chains), 1/128 worked: the
    finest that passes is used, a 20k-face game mesh holds no finer detail."""
    original = obj.data.copy()
    for across in VOXELS_ACROSS:
        obj.data = original.copy()
        voxel = max(obj.dimensions) / across
        obj.data.remesh_voxel_size = voxel
        activate(obj)
        bpy.ops.object.voxel_remesh()
        edit(obj, lambda t=voxel * 0.01: bpy.ops.mesh.remove_doubles(threshold=t))
        drop_specks(obj)
        edit(
            obj,
            lambda: bpy.ops.mesh.normals_make_consistent(inside=False),
            lambda: bpy.ops.mesh.quads_convert_to_tris(),
        )
        activate(obj)
        result = bpy.ops.object.quadriflow_remesh(
            mode="FACES", target_faces=faces, use_mesh_symmetry=False, seed=0
        )
        if "FINISHED" in result and len(obj.data.polygons):
            return across
    invalid("QuadriFlow found no quad layout for this mesh; try method decimate")
    return 0


def retopo(source: Path, target: Path, faces: int, method: str) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    obj = import_joined(source, "mesh")
    weld(obj)
    source_triangles = triangles(obj)
    across = None
    if method == "quadriflow":
        across = quadriflow(obj, faces)
    else:
        modifier = obj.modifiers.new("decimate", "DECIMATE")
        modifier.ratio = min(1.0, faces / max(source_triangles, 1))
        activate(obj)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    report(
        method=method,
        source_triangles=source_triangles,
        voxels_across=across,
        faces=len(obj.data.polygons),
        quads=sum(1 for p in obj.data.polygons if len(p.vertices) == 4),
        triangles=triangles(obj),
    )
    export(obj, target, materials=False)


def put_over(obj: Any, reference: Any) -> None:
    """``obj`` moved and scaled so its bounding box has ``reference``'s centre
    and largest side."""

    def box(o: Any) -> tuple[Vector, Vector]:
        corners = [o.matrix_world @ Vector(c) for c in o.bound_box]
        lo = Vector(
            (min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners))
        )
        hi = Vector(
            (max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners))
        )
        return lo, hi

    lo, hi = box(obj)
    ref_lo, ref_hi = box(reference)
    scale = max(ref_hi - ref_lo) / max(max(hi - lo), 1e-12)
    centre, ref_centre = (lo + hi) / 2, (ref_lo + ref_hi) / 2
    for vertex in obj.data.vertices:
        vertex.co = (vertex.co - centre) * scale + ref_centre
    obj.data.update()


def bake(high_path: Path, low_path: Path, target: Path, size: int, ray: float) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    high = import_joined(high_path, "detailed mesh")
    low = import_joined(low_path, "painted mesh")
    if not low.data.uv_layers:
        invalid("the painted mesh has no UVs")
    if not low.data.materials:
        invalid("the painted mesh has no material")
    put_over(high, low)
    for polygon in low.data.polygons:
        polygon.use_smooth = True

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    normals = bpy.data.images.new("normals", size, size, alpha=False)
    normals.colorspace_settings.name = "Non-Color"
    normals.generated_color = (0.5, 0.5, 1.0, 1.0)  # flat outside the UV islands
    for material in low.data.materials:
        material.use_nodes = True
        nodes = material.node_tree.nodes
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = normals
        nodes.active = texture  # the bake target

    diagonal = (Vector(low.dimensions)).length
    activate(high, low)  # selected to active: from the detailed onto the painted
    bpy.ops.object.bake(
        type="NORMAL",
        normal_space="TANGENT",
        use_selected_to_active=True,
        cage_extrusion=ray * diagonal,
        max_ray_distance=2 * ray * diagonal,
        margin=16,
    )
    normals.pack()

    for material in low.data.materials:
        tree = material.node_tree
        principled = next((n for n in tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        texture = next(n for n in tree.nodes if n.type == "TEX_IMAGE" and n.image == normals)
        if principled is None:
            continue
        normal_map = tree.nodes.new("ShaderNodeNormalMap")
        tree.links.new(texture.outputs["Color"], normal_map.inputs["Color"])
        tree.links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])

    bpy.data.objects.remove(high, do_unlink=True)
    report(
        texture_size=size,
        ray_share=ray,
        triangles=triangles(low),
        materials=len(low.data.materials),
    )
    export(low, target, materials=True)


def main() -> None:
    args = sys.argv[sys.argv.index("--") + 1 :]
    if args[0] == "retopo":
        retopo(Path(args[1]), Path(args[2]), int(args[3]), args[4])
    elif args[0] == "bake":
        bake(Path(args[1]), Path(args[2]), Path(args[3]), int(args[4]), float(args[5]))
    else:
        invalid(f"unknown operation {args[0]}")


if __name__ == "__main__":
    main()
