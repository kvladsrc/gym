# pyright: basic
"""Retopology and normal baking, a Blender script (blender -b -P mesh_tools.py -- ...).

    retopo IN.glb OUT.glb FACES METHOD [OPTIONS]   METHOD: quadriflow | decimate | autoremesher
                                              OPTIONS: AutoRemesher settings, JSON
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
- Decimation keeps a painted mesh's UVs and material (Blender's collapse
  interpolates face-corner UVs), so painting can come before it. The remeshing
  methods rebuild the surface and drop them. Edge collapse cannot pass
  non-manifold edges (a Hi3D tree has thousands): when it stops above the
  target the job fails instead of returning a mesh several times heavier. Every method first removes
  specks: loose pieces under 0.1 % of the faces.
- Every retopology reports how far it strayed from the detailed surface
  (see deviation()): a lost finger or horn shows as source points far from
  the result, and where they are.
- Baking moves the detailed mesh's surface into a tangent-space normal map
  on the painted game mesh (its UVs, as the painter unwrapped it) and wires
  it into the material, so the GLB carries base colour, metal-roughness and
  normals. The painter may have rescaled the mesh: the detailed one is put
  over it by their bounding boxes (centre, largest side) first.
"""

import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any

import bmesh  # pyright: ignore[reportMissingImports]
import bpy  # pyright: ignore[reportMissingImports]
from mathutils import Vector  # pyright: ignore[reportMissingImports]
from mathutils.bvhtree import BVHTree  # pyright: ignore[reportMissingImports]

# Voxel remesh resolutions tried for QuadriFlow, finest first.
VOXELS_ACROSS = (256, 128, 64)
# Surface points compared between the detailed mesh and the result.
DEVIATION_SAMPLES = 50_000
# A source point this far from the result (share of the bounding box
# diagonal) counts as lost shape.
LOST_SHARE = 0.005
# Decimation may end this far above its target before the job fails.
DECIMATE_SLACK = 1.1
# AutoRemesher's own defaults.
AUTOREMESHER_DEFAULTS = {
    "edge_scaling": 1.0,
    "sharp_edge": 90.0,
    "smooth_normal": 0.0,
    "adaptivity": 1.0,
    "anisotropy": 1.0,
}


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


def nonmanifold_edges(obj: Any) -> int:
    work = bmesh.new()
    work.from_mesh(obj.data)
    count = sum(1 for edge in work.edges if len(edge.link_faces) > 2)
    work.free()
    return count


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


def triangulate_clean(obj: Any) -> int:
    """Triangulate a remeshed ``obj`` as the GLB will store it, merging
    doubled vertices and dissolving zero-area triangles; returns how many
    triangles went. AutoRemesher can leave a doubled vertex and a few
    zero-area triangles; TRELLIS.2's UV unwrap drops those, so painting would
    change the triangle count (and fail its surface check)."""
    work = bmesh.new()
    work.from_mesh(obj.data)
    bmesh.ops.triangulate(work, faces=work.faces, quad_method="BEAUTY")
    before = len(work.faces)
    dist = 1e-6 * max(obj.dimensions)
    bmesh.ops.remove_doubles(work, verts=work.verts, dist=dist)
    bmesh.ops.dissolve_degenerate(work, dist=dist, edges=work.edges)
    work.to_mesh(obj.data)
    work.free()
    obj.data.update()
    return before - len(obj.data.polygons)


def surface(obj: Any) -> tuple[BVHTree, list[Vector]]:
    """A BVH of ``obj``'s surface and up to DEVIATION_SAMPLES of its
    vertices (a fixed stride, so the same mesh gives the same points)."""
    vertices = [v.co.copy() for v in obj.data.vertices]
    tree = BVHTree.FromPolygons(vertices, [tuple(p.vertices) for p in obj.data.polygons])
    stride = max(1, len(vertices) // DEVIATION_SAMPLES)
    return tree, vertices[::stride]


def deviation(source: tuple[BVHTree, list[Vector]], obj: Any, diagonal: float) -> dict:
    """Distances between the detailed surface and the result, as shares of
    the bounding box diagonal: ``lost`` from source points to the result
    (missing shape: fingers, horns), ``added`` from the result's points to
    the source (bulges, webbing). ``lost_box`` bounds the source points
    further than LOST_SHARE (Blender axes, Z up), to tell where shape went."""
    source_tree, source_points = source
    result_tree, result_points = surface(obj)
    lost = [result_tree.find_nearest(p)[3] / diagonal for p in source_points]
    added = [source_tree.find_nearest(p)[3] / diagonal for p in result_points]
    far = [p for p, d in zip(source_points, lost, strict=True) if d > LOST_SHARE]

    def summary(values: list[float]) -> dict:
        values = sorted(values)
        return {
            "mean": round(sum(values) / len(values), 6),
            "p99": round(values[int(0.99 * (len(values) - 1))], 6),
            "max": round(values[-1], 6),
        }

    box = None
    if far:
        box = [[round(f(p[i] for p in far), 4) for i in range(3)] for f in (min, max)]
    return {
        "lost": summary(lost),
        "added": summary(added),
        "lost_points_share": round(len(far) / len(source_points), 6),
        "lost_box": box,
    }


def retopo(source: Path, target: Path, faces: int, method: str, options: dict) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    obj = import_joined(source, "mesh")
    weld(obj)
    painted = bool(obj.data.uv_layers) and bool(obj.data.materials)
    specks = drop_specks(obj)
    source_triangles = triangles(obj)
    detailed = surface(obj)
    diagonal = Vector(obj.dimensions).length
    across = None
    settings = None
    if method == "quadriflow":
        across = quadriflow(obj, faces)
    elif method == "autoremesher":
        settings = {**AUTOREMESHER_DEFAULTS, **options}
        obj = autoremesher(obj, faces, settings)
    else:
        modifier = obj.modifiers.new("decimate", "DECIMATE")
        modifier.ratio = min(1.0, faces / max(source_triangles, 1))
        activate(obj)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        if triangles(obj) > DECIMATE_SLACK * faces:
            invalid(
                f"decimation stopped at {triangles(obj)} triangles for a target of {faces}:"
                f" edge collapse cannot pass non-manifold edges ({nonmanifold_edges(obj)} here);"
                " use quadriflow, or Hi3D's max_faces for a Hi3D mesh"
            )
    kept_material = painted and method == "decimate"
    faces_made = len(obj.data.polygons)
    quads = sum(1 for p in obj.data.polygons if len(p.vertices) == 4)
    degenerate = 0 if method == "decimate" else triangulate_clean(obj)
    report(
        method=method,
        source_triangles=source_triangles,
        speck_faces_removed=specks,
        kept_material=kept_material,
        material_dropped=painted and not kept_material,
        voxels_across=across,
        autoremesher=settings,
        faces=faces_made,
        quads=quads,
        degenerate_triangles_removed=degenerate,
        triangles=triangles(obj),
        deviation=deviation(detailed, obj, diagonal),
    )
    export(obj, target, materials=kept_material)


def autoremesher(obj: Any, faces: int, settings: dict) -> Any:
    """The standalone CPU AutoRemesher, keeping its quad layout until GLB export."""
    binary = Path(
        os.environ.get("AUTOREMESHER", "~/.cache/assets-studio/autoremesher/squashfs-root/AppRun")
    ).expanduser()
    if not binary.is_file():
        invalid("AutoRemesher is not installed; set AUTOREMESHER to its executable")
    with tempfile.TemporaryDirectory(prefix="autoremesher-") as directory:
        source, target = Path(directory) / "input.obj", Path(directory) / "output.obj"
        activate(obj)
        bpy.ops.wm.obj_export(
            filepath=str(source),
            export_selected_objects=True,
            export_materials=False,
            export_normals=False,
            forward_axis="NEGATIVE_Z",
            up_axis="Y",
        )
        environment = {**os.environ, "QT_QPA_PLATFORM": "xcb"}
        done = subprocess.run(  # noqa: S603 -- configured binary, fixed argv, no shell
            [
                str(binary),
                "--input",
                str(source),
                "--output",
                str(target),
                "--target-quads",
                str(faces),
                "--edge-scaling",
                str(settings["edge_scaling"]),
                "--sharp-edge",
                str(settings["sharp_edge"]),
                "--smooth-normal",
                str(settings["smooth_normal"]),
                "--adaptivity",
                str(settings["adaptivity"]),
                "--anisotropy",
                str(settings["anisotropy"]),
            ],
            env=environment,
            capture_output=True,
            text=True,
            timeout=540,
            check=False,
        )
        if done.returncode or not target.is_file():
            raise RuntimeError(f"AutoRemesher failed ({done.returncode}): {done.stderr[-2000:]}")
        bpy.data.objects.remove(obj, do_unlink=True)
        bpy.ops.wm.obj_import(filepath=str(target), forward_axis="NEGATIVE_Z", up_axis="Y")
        meshes = [o for o in bpy.context.selected_objects if o.type == "MESH"]
        if not meshes:
            invalid("AutoRemesher produced no mesh")
        activate(*meshes)
        if len(meshes) > 1:
            bpy.ops.object.join()
        result = bpy.context.view_layer.objects.active
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        return result


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
        options = json.loads(args[5]) if len(args) > 5 else {}
        retopo(Path(args[1]), Path(args[2]), int(args[3]), args[4], options)
    elif args[0] == "bake":
        bake(Path(args[1]), Path(args[2]), Path(args[3]), int(args[4]), float(args[5]))
    else:
        invalid(f"unknown operation {args[0]}")


if __name__ == "__main__":
    main()
