"""Turn a raw TripoSR mesh into a Y-up glTF binary for the studio and games."""

from typing import Any

import numpy as np
import trimesh

# TripoSR: X depth, Y horizontal, Z up. glTF: X horizontal, Y up, Z depth.
Z_UP_TO_Y_UP = np.array([[0, 1, 0, 0], [0, 0, 1, 0], [1, 0, 0, 0], [0, 0, 0, 1]], dtype=float)


class EmptyMesh(ValueError):
    pass


def linear_vertex_colors(colors: np.ndarray) -> np.ndarray:
    """Decode sRGB vertex colours to linear glTF ``COLOR_0``; keep alpha.

    TripoSR predicts image-space sRGB, and trimesh stores the bytes as they
    are, while glTF ``COLOR_0`` is a linear multiplier.
    """
    result = np.array(colors, dtype=np.uint8, copy=True)
    rgb = result[:, :3].astype(np.float64) / 255
    linear = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    result[:, :3] = np.rint(linear * 255).astype(np.uint8)
    return result


def to_glb(mesh: trimesh.Trimesh) -> tuple[bytes, dict[str, Any]]:
    """Return the GLB bytes and a JSON-serialisable description of the mesh."""
    if not len(mesh.faces) or not np.isfinite(mesh.vertices).all():
        raise EmptyMesh("TripoSR returned an empty or invalid mesh")
    mesh = mesh.copy()
    mesh.apply_transform(Z_UP_TO_Y_UP)
    if mesh.visual.kind == "vertex":
        mesh.visual.vertex_colors = linear_vertex_colors(mesh.visual.vertex_colors)
    data = mesh.export(file_type="glb")
    meta = {
        "vertices": len(mesh.vertices),
        "triangles": len(mesh.faces),
        "watertight": bool(mesh.is_watertight),
        "bounds": [[float(value) for value in corner] for corner in mesh.bounds],
        "up_axis": "Y",
        "color": str(mesh.visual.kind),
    }
    return data, meta
