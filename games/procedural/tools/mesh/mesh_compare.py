"""Compare GLB mesh variants against a reference inside headless Blender.

Example:
    blender -b --factory-startup -P mesh_compare.py -- \
        --reference reference.glb --out /tmp/mesh-out \
        --variant hi3d=hi3d.glb --variant decimate=decimate.glb

The script imports the reference and each variant into an empty scene, joins
mesh objects, applies transforms, records geometry metrics, aligns each
variant to the reference by bounding-box centre / largest side, measures
two-way surface deviations with mathutils BVHTree, and renders four Cycles CPU
views.  Results are written to OUT_DIR/metrics.json and OUT_DIR/*.png.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import sys
import traceback
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

DEV_SAMPLES = 20_000
VIEW_NAMES = ("front", "three_quarter", "top", "closeup")


# ---------------------------------------------------------------------------
# CLI


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Compare GLB mesh variants against a reference in Blender."
    )
    parser.add_argument("--reference", required=True, help="reference GLB")
    parser.add_argument("--out", required=True, help="output directory")
    parser.add_argument(
        "--variant",
        action="append",
        default=[],
        metavar="LABEL=PATH",
        help="variant GLB (repeatable)",
    )
    parser.add_argument("--size", type=int, default=384, help="square render size")
    parser.add_argument(
        "--samples", type=int, default=16, help="Cycles CPU samples per view"
    )
    args = parser.parse_args(argv)

    if args.size < 32:
        parser.error("--size must be at least 32")
    if args.samples < 1:
        parser.error("--samples must be at least 1")

    variants: list[tuple[str, Path]] = []
    labels = {"reference"}
    for item in args.variant:
        if "=" not in item:
            parser.error(f"--variant must be LABEL=PATH, got {item!r}")
        label, raw_path = item.split("=", 1)
        label = label.strip()
        if not label:
            parser.error(f"empty label in --variant {item!r}")
        if label == "reference":
            parser.error("'reference' is reserved for --reference")
        if label in labels:
            parser.error(f"duplicate label {label!r}")
        path = Path(raw_path).expanduser()
        if not path.is_file():
            parser.error(f"variant path does not exist: {path}")
        labels.add(label)
        variants.append((label, path.resolve()))

    ref = Path(args.reference).expanduser()
    if not ref.is_file():
        parser.error(f"reference path does not exist: {ref}")
    out = Path(args.out).expanduser().resolve()

    return argparse.Namespace(
        reference=ref.resolve(),
        out=out,
        variants=variants,
        size=args.size,
        samples=args.samples,
    )


# ---------------------------------------------------------------------------
# Scene / import helpers


def clear_scene() -> None:
    """Start from a genuinely empty factory scene for every input."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def delete_non_mesh_objects() -> None:
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)


def import_and_join(path: Path):
    """Import a GLB, remove non-mesh scene objects, join meshes, apply xform."""
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(path))

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"{path}: GLB contains no mesh objects")

    # glTF nodes are often parented to empties.  Preserve the world transform
    # before removing those non-mesh helpers.
    bpy.context.view_layer.update()
    for obj in meshes:
        if obj.parent is not None:
            world = obj.matrix_world.copy()
            obj.parent = None
            obj.matrix_world = world

    delete_non_mesh_objects()

    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    active = meshes[0]
    bpy.context.view_layer.objects.active = active
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"{path}: joining mesh objects failed")

    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # transform_apply should leave an identity matrix.  Make that guaranteed:
    # BVHTree.FromObject builds in object space, while samples live in world
    # space.
    if not obj.matrix_world.is_identity:
        obj.data.transform(obj.matrix_world)
        obj.matrix_world = Matrix.Identity(4)

    obj.name = "MeshCompareObject"
    obj.data.name = "MeshCompareMesh"
    weld_coincident_vertices(obj)
    return obj


def weld_coincident_vertices(obj) -> None:
    """Merge glTF's normal/UV-split vertices by position.

    glTF stores a vertex per distinct normal/UV, so even a perfectly closed
    sphere arrives as a triangle soup.  Welding coincident positions restores
    meaningful connected-component, boundary-edge and manifold counts.  The
    tolerance is tiny relative to the object size.
    """
    mesh = obj.data
    if not len(mesh.vertices):
        return
    coords = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
    mesh.vertices.foreach_get("co", coords)
    coords = coords.reshape(-1, 3)
    diagonal = float(np.linalg.norm(np.ptp(coords, axis=0)))
    distance = max(diagonal * 1e-6, 1e-9)

    before = len(mesh.vertices)
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=distance)
        bm.to_mesh(mesh)
    finally:
        bm.free()
    mesh.update()
    obj["mesh_compare_weld_distance"] = distance
    obj["mesh_compare_vertices_welded"] = before - len(mesh.vertices)


def object_bbox_from_verts(verts: np.ndarray) -> dict:
    if verts.size == 0:
        raise RuntimeError("mesh has no vertices")
    lo = np.min(verts, axis=0)
    hi = np.max(verts, axis=0)
    size = hi - lo
    center = (hi + lo) * 0.5
    return {
        "min": [float(v) for v in lo],
        "max": [float(v) for v in hi],
        "center": [float(v) for v in center],
        "size": [float(v) for v in size],
        "diagonal": float(np.linalg.norm(size)),
        "largest_side": float(np.max(size)),
    }


# ---------------------------------------------------------------------------
# Evaluated geometry and metrics


def evaluated_geometry(obj, build_bvh: bool = True, compute_stats: bool = True):
    """Return world-space vertices/triangles and optional BVH/statistics.

    The imported object has an identity transform after import_and_join(), so
    BVHTree.FromObject() is in the same coordinate space as the arrays.
    """
    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = obj.evaluated_get(depsgraph)
    mesh = eval_obj.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
    if mesh is None:
        raise RuntimeError("could not evaluate mesh")

    try:
        mesh.calc_loop_triangles()

        coords = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
        mesh.vertices.foreach_get("co", coords)
        coords = coords.reshape(-1, 3)

        matrix = np.array(obj.matrix_world, dtype=np.float64)
        if not np.allclose(matrix, np.eye(4), atol=1e-9):
            coords = coords @ matrix[:3, :3].T + matrix[:3, 3]

        tri_flat = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int64)
        mesh.loop_triangles.foreach_get("vertices", tri_flat)
        triangles = tri_flat.reshape(-1, 3)

        bvh = BVHTree.FromObject(obj, depsgraph) if build_bvh else None

        if compute_stats:
            loops = np.empty(len(mesh.loops), dtype=np.int64)
            mesh.loops.foreach_get("vertex_index", loops)
            starts = np.empty(len(mesh.polygons), dtype=np.int64)
            totals = np.empty(len(mesh.polygons), dtype=np.int64)
            mesh.polygons.foreach_get("loop_start", starts)
            mesh.polygons.foreach_get("loop_total", totals)

            # One pair per polygon side, using loop connectivity so n-gons are
            # represented by their real boundary edges (not triangulation fans).
            if len(loops):
                next_loop = np.arange(1, len(loops) + 1, dtype=np.int64)
                ends = starts + totals - 1
                next_loop[ends] = starts
                pairs = np.column_stack((loops, loops[next_loop]))
                pairs.sort(axis=1)
                unique_edges, edge_faces = np.unique(pairs, axis=0, return_counts=True)
            else:
                unique_edges = np.empty((0, 2), dtype=np.int64)
                edge_faces = np.empty((0,), dtype=np.int64)

            stats = mesh_statistics(coords, triangles, unique_edges, edge_faces)
        else:
            stats = None
        return coords, triangles, bvh, stats
    finally:
        eval_obj.to_mesh_clear()


def mesh_statistics(coords, triangles, unique_edges, edge_faces) -> dict:
    bbox = object_bbox_from_verts(coords)

    areas = np.zeros(len(triangles), dtype=np.float64)
    if len(triangles):
        a = coords[triangles[:, 0]]
        b = coords[triangles[:, 1]]
        c = coords[triangles[:, 2]]
        areas = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
    surface_area = float(np.sum(areas))

    components = count_components(len(coords), unique_edges)
    boundary_edges = int(np.count_nonzero(edge_faces == 1))
    non_manifold_edges = int(np.count_nonzero(edge_faces > 2))

    return {
        "triangles": len(triangles),
        "vertices": len(coords),
        "connected_components": int(components),
        "non_manifold_edges": non_manifold_edges,
        "boundary_edges": boundary_edges,
        "surface_area": surface_area,
        "bbox_size": bbox["size"],
        "bbox_center": bbox["center"],
        "bbox_diagonal": bbox["diagonal"],
    }


def count_components(vertex_count: int, edges: np.ndarray) -> int:
    """Connected components over mesh edges (union-find)."""
    if vertex_count == 0:
        return 0
    parent = list(range(vertex_count))
    rank = bytearray(vertex_count)

    for a, b in edges.tolist():
        # Find with path halving.
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        while parent[b] != b:
            parent[b] = parent[parent[b]]
            b = parent[b]
        if a == b:
            continue
        if rank[a] < rank[b]:
            a, b = b, a
        parent[b] = a
        if rank[a] == rank[b]:
            rank[a] += 1

    roots = set()
    for i in range(vertex_count):
        j = i
        while parent[j] != j:
            parent[j] = parent[parent[j]]
            j = parent[j]
        roots.add(j)
    return len(roots)


def sample_surface(verts, triangles, count: int, seed: int) -> np.ndarray:
    """Area-uniform barycentric samples on a triangle soup."""
    if not len(triangles):
        raise RuntimeError("cannot sample a mesh with no triangles")

    a = verts[triangles[:, 0]]
    b = verts[triangles[:, 1]]
    c = verts[triangles[:, 2]]
    areas = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
    valid = areas > 0.0
    if not np.any(valid):
        raise RuntimeError("cannot sample a mesh with zero surface area")

    a, b, c, areas = a[valid], b[valid], c[valid], areas[valid]
    cumulative = np.cumsum(areas, dtype=np.float64)
    total = float(cumulative[-1])
    rng = np.random.default_rng(seed)

    positions = rng.random(count) * total
    index = np.searchsorted(cumulative, positions, side="right")
    index = np.clip(index, 0, len(areas) - 1)

    u = rng.random(count)
    v = rng.random(count)
    flip = (u + v) > 1.0
    u[flip] = 1.0 - u[flip]
    v[flip] = 1.0 - v[flip]

    return (
        a[index]
        + u[:, None] * (b[index] - a[index])
        + v[:, None] * (c[index] - a[index])
    )


def nearest_distances(points: np.ndarray, bvh: BVHTree) -> np.ndarray:
    distances = np.empty(len(points), dtype=np.float64)
    for i, point in enumerate(points):
        hit = bvh.find_nearest(Vector(point))
        if hit is None:
            raise RuntimeError("BVH find_nearest returned no hit")
        distances[i] = abs(float(hit[3]))
    return distances


def distance_stats(distances: np.ndarray, diagonal: float) -> dict:
    if diagonal <= 0.0:
        raise RuntimeError("reference bounding-box diagonal is zero")
    if not len(distances):
        raise RuntimeError("no deviation samples")
    return {
        "mean": float(np.mean(distances) / diagonal),
        "p95": float(np.percentile(distances, 95) / diagonal),
        "max": float(np.max(distances) / diagonal),
    }


# ---------------------------------------------------------------------------
# Alignment


def align_variant_to_reference(obj, src_bbox: dict, ref_bbox: dict) -> dict:
    """Uniformly scale by largest bbox side, then match bbox centres."""
    src_largest = src_bbox["largest_side"]
    ref_largest = ref_bbox["largest_side"]
    if src_largest <= 0.0 or ref_largest <= 0.0:
        raise RuntimeError("cannot align a zero-sized bounding box")

    scale = ref_largest / src_largest
    src_center = np.asarray(src_bbox["center"], dtype=np.float64)
    ref_center = np.asarray(ref_bbox["center"], dtype=np.float64)
    translation = ref_center - scale * src_center

    mesh = obj.data
    for vert in mesh.vertices:
        co = vert.co
        vert.co = (
            float(co.x * scale + translation[0]),
            float(co.y * scale + translation[1]),
            float(co.z * scale + translation[2]),
        )
    mesh.update()

    aligned_size = [float(v * scale) for v in src_bbox["size"]]
    return {
        "method": "uniform scale by largest bbox side; bbox centres matched",
        "scale": float(scale),
        "translation": [float(v) for v in translation],
        "aligned_bbox_size": aligned_size,
        "aligned_bbox_center": [float(v) for v in ref_center],
    }


# ---------------------------------------------------------------------------
# Rendering


def add_grey_material(obj) -> None:
    material = bpy.data.materials.new("MeshCompareGrey")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (0.56, 0.58, 0.62, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.68
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.0
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.35
    obj.data.materials.clear()
    obj.data.materials.append(material)


def add_sun(name: str, energy: float, color, direction: Vector, center: Vector):
    data = bpy.data.lights.new(name, type="SUN")
    data.energy = energy
    data.color = color
    data.angle = math.radians(6.0)
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = center - direction.normalized() * 10.0
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.normalized().to_track_quat("-Z", "Y")
    return obj


def camera_views(bbox: dict) -> dict:
    center = Vector(bbox["center"])
    diagonal = max(float(bbox["diagonal"]), 1e-6)
    distance = max(diagonal * 2.5, 1e-3)
    corners = [
        Vector((x, y, z))
        for x in (bbox["min"][0], bbox["max"][0])
        for y in (bbox["min"][1], bbox["max"][1])
        for z in (bbox["min"][2], bbox["max"][2])
    ]

    def view(offset: Vector) -> dict:
        offset = offset.normalized()
        location = center + offset * distance
        direction = (center - location).normalized()
        rotation = direction.to_track_quat("-Z", "Y")
        rot_matrix = rotation.to_matrix()
        right = rot_matrix @ Vector((1.0, 0.0, 0.0))
        up = rot_matrix @ Vector((0.0, 1.0, 0.0))
        xs = [(corner - center).dot(right) for corner in corners]
        ys = [(corner - center).dot(up) for corner in corners]
        extent = max(max(xs) - min(xs), max(ys) - min(ys))
        return {
            "location": location,
            "rotation": rotation,
            "ortho_scale": max(extent * 1.08, diagonal * 0.01),
        }

    yaw = math.radians(45.0)
    down = math.radians(20.0)
    views = {
        "front": view(Vector((0.0, -1.0, 0.0))),
        "three_quarter": view(
            Vector(
                (
                    math.sin(yaw) * math.cos(down),
                    -math.cos(yaw) * math.cos(down),
                    math.sin(down),
                )
            )
        ),
        "top": view(Vector((0.0, 0.0, 1.0))),
    }
    closeup = dict(views["three_quarter"])
    closeup["ortho_scale"] = views["three_quarter"]["ortho_scale"] / 2.5
    views["closeup"] = closeup
    return views


def setup_studio(scene, obj, bbox: dict, size: int):
    scene.render.engine = "CYCLES"
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.render.use_file_extension = True

    scene.render.threads_mode = "FIXED"
    scene.render.threads = max(1, (os.cpu_count() or 2) // 2)

    world = bpy.data.worlds.new("MeshCompareWorld")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.75, 0.80, 0.90, 1.0)
        background.inputs["Strength"].default_value = 0.15
    scene.world = world

    add_grey_material(obj)

    center = Vector(bbox["center"])
    key = add_sun(
        "Key",
        2.7,
        (1.0, 0.95, 0.88),
        Vector((1.0, 1.0, -1.0)),
        center,
    )
    fill = add_sun(
        "Fill",
        0.85,
        (0.82, 0.90, 1.0),
        Vector((-1.0, 1.0, -0.3)),
        center,
    )
    rim = add_sun(
        "Rim",
        2.6,
        (1.0, 1.0, 1.0),
        Vector((-0.35, -0.9, -0.45)),
        center,
    )

    camera_data = bpy.data.cameras.new("MeshCompareCamera")
    camera_data.type = "ORTHO"
    camera_data.clip_start = max(float(bbox["diagonal"]) * 0.01, 1e-5)
    camera_data.clip_end = max(float(bbox["diagonal"]) * 20.0, 10.0)
    camera = bpy.data.objects.new("MeshCompareCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    return camera, (key, fill, rim)


def aim_lights(lights, view, center: Vector) -> None:
    key, fill, rim = lights
    rotation = view["rotation"]
    right = rotation @ Vector((1.0, 0.0, 0.0))
    up = rotation @ Vector((0.0, 1.0, 0.0))
    forward = rotation @ Vector((0.0, 0.0, -1.0))

    key_direction = right * 0.7 - up * 0.8 + forward * 0.8
    fill_direction = -right * 0.8 + up * 0.25 + forward * 0.6
    rim_direction = -right * 0.35 - up * 0.4 - forward * 0.9
    for light, direction in (
        (key, key_direction),
        (fill, fill_direction),
        (rim, rim_direction),
    ):
        light.rotation_quaternion = direction.normalized().to_track_quat("-Z", "Y")
        light.location = center - direction.normalized() * 10.0


def configure_engine(scene, samples: int, engine: str, denoise: bool) -> None:
    if engine == "CYCLES":
        scene.render.engine = "CYCLES"
        scene.cycles.device = "CPU"
        scene.cycles.samples = samples
        scene.cycles.use_denoising = bool(denoise)
        if denoise:
            try:
                scene.cycles.denoiser = "OPENIMAGEDENOISE"
            except (TypeError, ValueError):
                pass
    elif engine == "BLENDER_EEVEE":
        try:
            scene.render.engine = "BLENDER_EEVEE"
        except TypeError:
            scene.render.engine = "BLENDER_EEVEE_NEXT"
        if hasattr(scene, "eevee") and hasattr(scene.eevee, "taa_render_samples"):
            scene.eevee.taa_render_samples = samples
    elif engine == "BLENDER_WORKBENCH":
        scene.render.engine = "BLENDER_WORKBENCH"
        shading = scene.display.shading
        shading.light = "STUDIO"
        shading.color_type = "MATERIAL"
    else:
        raise RuntimeError(f"unknown render engine {engine}")


def render_all_views(
    scene,
    obj,
    bbox: dict,
    size: int,
    samples: int,
    out_dir: Path,
    filename_stem: str,
    state: dict,
) -> str:
    camera, lights = setup_studio(scene, obj, bbox, size)
    views = camera_views(bbox)
    center = Vector(bbox["center"])

    attempts = 0
    while True:
        configure_engine(scene, samples, state["engine"], state.get("denoise", True))
        try:
            for view_name in VIEW_NAMES:
                view = views[view_name]
                camera.location = view["location"]
                camera.rotation_mode = "QUATERNION"
                camera.rotation_quaternion = view["rotation"]
                camera.data.ortho_scale = view["ortho_scale"]
                aim_lights(lights, view, center)
                scene.render.filepath = str(
                    out_dir / f"{filename_stem}_{view_name}.png"
                )
                bpy.ops.render.render(write_still=True)
            return state["engine"]
        except Exception as exc:
            attempts += 1
            if attempts > 4:
                raise
            print(
                f"[mesh_compare] render failed with {state['engine']}: {exc}",
                file=sys.stderr,
                flush=True,
            )
            if state["engine"] == "CYCLES" and state.get("denoise", True):
                print(
                    "[mesh_compare] retrying Cycles with denoising disabled",
                    file=sys.stderr,
                    flush=True,
                )
                state["denoise"] = False
                continue
            if state["engine"] == "CYCLES":
                print(
                    "[mesh_compare] falling back to BLENDER_EEVEE",
                    file=sys.stderr,
                    flush=True,
                )
                state["engine"] = "BLENDER_EEVEE"
                continue
            if state["engine"] == "BLENDER_EEVEE":
                print(
                    "[mesh_compare] falling back to BLENDER_WORKBENCH",
                    file=sys.stderr,
                    flush=True,
                )
                state["engine"] = "BLENDER_WORKBENCH"
                continue
            raise


# ---------------------------------------------------------------------------
# Main flow


def safe_filename_stem(label: str) -> str:
    cleaned = re.sub(r'[<>:"/\\|?*\x00-\x1f]', "_", label).strip()
    return cleaned or "variant"


def read_raw_metrics(path: Path, build_bvh: bool = True) -> tuple:
    obj = import_and_join(path)
    coords, triangles, bvh, stats = evaluated_geometry(obj, build_bvh=build_bvh)
    stats = {
        **stats,
        "vertices_welded": int(obj.get("mesh_compare_vertices_welded", 0)),
        "vertex_weld_distance": float(obj.get("mesh_compare_weld_distance", 0.0)),
    }
    bbox = object_bbox_from_verts(coords)
    return obj, stats, bbox, coords, triangles, bvh


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    out_dir: Path = args.out
    out_dir.mkdir(parents=True, exist_ok=True)

    # Make sure filenames do not silently collide after sanitising.
    stems: dict[str, str] = {}
    all_labels = ["reference"] + [label for label, _ in args.variants]
    for label in all_labels:
        stem = safe_filename_stem(label)
        if stem in stems:
            raise RuntimeError(
                f"labels {stems[stem]!r} and {label!r} map to the same "
                f"filename stem {stem!r}"
            )
        stems[stem] = label

    print(
        f"[mesh_compare] reference={args.reference} variants={len(args.variants)} "
        f"size={args.size} samples={args.samples}",
        flush=True,
    )

    # Reference ------------------------------------------------------------
    (
        ref_obj,
        ref_stats,
        ref_bbox,
        ref_coords,
        ref_triangles,
        ref_bvh,
    ) = read_raw_metrics(args.reference)
    ref_samples = sample_surface(ref_coords, ref_triangles, DEV_SAMPLES, seed=0x51A7)
    ref_diag = ref_bbox["diagonal"]
    if ref_diag <= 0.0:
        raise RuntimeError("reference has a zero-size bounding box")

    reference_entry = {
        "label": "reference",
        "path": str(args.reference),
        "metrics": {
            **ref_stats,
            "alignment": {
                "method": "reference",
                "scale": 1.0,
                "translation": [0.0, 0.0, 0.0],
                "aligned_bbox_size": ref_stats["bbox_size"],
                "aligned_bbox_center": ref_stats["bbox_center"],
            },
            "deviation": {
                "samples_per_direction": DEV_SAMPLES,
                "normalization": "fraction of reference bbox diagonal",
                "reference_bbox_diagonal": ref_diag,
                "variant_to_reference": {"mean": 0.0, "p95": 0.0, "max": 0.0},
                "reference_to_variant": {"mean": 0.0, "p95": 0.0, "max": 0.0},
            },
        },
        "images": {name: f"{stems['reference']}_{name}.png" for name in VIEW_NAMES},
    }

    render_state = {"engine": "CYCLES", "denoise": True}
    print("[mesh_compare] rendering reference", flush=True)
    reference_entry["render_engine"] = render_all_views(
        bpy.context.scene,
        ref_obj,
        ref_bbox,
        args.size,
        args.samples,
        out_dir,
        stems["reference"],
        render_state,
    )

    entries = [reference_entry]

    # Variants -------------------------------------------------------------
    for label, path in args.variants:
        print(f"[mesh_compare] loading variant {label!r}: {path}", flush=True)
        obj, stats, src_bbox, _, _, _ = read_raw_metrics(path, build_bvh=False)

        alignment = align_variant_to_reference(obj, src_bbox, ref_bbox)
        coords, triangles, variant_bvh, _ = evaluated_geometry(obj, compute_stats=False)

        variant_samples = sample_surface(coords, triangles, DEV_SAMPLES, seed=0x51A7)
        v2r = nearest_distances(variant_samples, ref_bvh)
        r2v = nearest_distances(ref_samples, variant_bvh)

        metrics = {
            **stats,
            "alignment": alignment,
            "deviation": {
                "samples_per_direction": DEV_SAMPLES,
                "normalization": "fraction of reference bbox diagonal",
                "reference_bbox_diagonal": ref_diag,
                "variant_to_reference": distance_stats(v2r, ref_diag),
                "reference_to_variant": distance_stats(r2v, ref_diag),
            },
        }
        entry = {
            "label": label,
            "path": str(path),
            "metrics": metrics,
            "images": {name: f"{stems[label]}_{name}.png" for name in VIEW_NAMES},
        }

        print(
            f"[mesh_compare] rendering {label}: "
            f"{metrics['triangles']} tris, "
            f"R->V p95={metrics['deviation']['reference_to_variant']['p95']:.5f}",
            flush=True,
        )
        entry["render_engine"] = render_all_views(
            bpy.context.scene,
            obj,
            ref_bbox,
            args.size,
            args.samples,
            out_dir,
            stems[label],
            render_state,
        )
        entries.append(entry)

    metrics_path = out_dir / "metrics.json"
    tmp_path = out_dir / ".metrics.json.tmp"
    with tmp_path.open("w", encoding="utf-8") as handle:
        json.dump(entries, handle, indent=2, allow_nan=False)
        handle.write("\n")
    tmp_path.replace(metrics_path)

    print(f"[mesh_compare] wrote {metrics_path}", flush=True)
    print(f"[mesh_compare] render engine(s): {render_state['engine']}", flush=True)
    return 0


def _script_args() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


if __name__ == "__main__":
    try:
        raise SystemExit(main(_script_args()))
    except Exception:  # noqa: BLE001 - Blender exits 0 after an uncaught script error
        traceback.print_exc()
        raise SystemExit(1)
