"""Upstream's app_blender.py with its textures named; run in its directory.

The mesh reaches Blender as a GLB, so its texture is a packed image without
a file path, and the FBX export embeds it under the name of the export's
media folder ("rigged.fbm"): Unity does not recognise it as an image and the
model imports untextured. Before the export each packed image gets a file
name from its content (``texture-<hash>.png``), unique across models, which
is what Unity matches extracted textures by.

The mesh also reaches Blender unwelded (every triangle its own three
vertices: 144k vertices for 60k triangles) and flat: in a game it looks
faceted, with dark seams along the edges. Before the export the vertices at
one position are merged (their bone weights are the same, predicted from the
position) and the faces smooth shaded.

    python blender_export.py <app_blender.py arguments>
"""

import hashlib
import os
import runpy
import sys

import bpy


def name_packed_images() -> None:
    for image in bpy.data.images:
        if image.packed_file is not None and not image.filepath:
            digest = hashlib.sha1(image.packed_file.data, usedforsecurity=False).hexdigest()[:12]
            image.filepath = f"//texture-{digest}.png"


def weld_and_smooth() -> None:
    # bpy as a module puts bmesh on the path: imported after it, not at the top.
    import bmesh

    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        work = bmesh.new()
        work.from_mesh(mesh)
        bmesh.ops.remove_doubles(work, verts=work.verts, dist=1e-5)
        work.to_mesh(mesh)
        work.free()
        for polygon in mesh.polygons:
            polygon.use_smooth = True
        mesh.update()


class _ExportScene:
    """bpy.ops.export_scene with the images named before an FBX export."""

    def __init__(self, original: object) -> None:
        self._original = original

    def fbx(self, **kwargs: object) -> object:
        name_packed_images()
        weld_and_smooth()
        return self._original.fbx(**kwargs)  # type: ignore[attr-defined]

    def __getattr__(self, name: str) -> object:
        return getattr(self._original, name)


def main() -> None:
    bpy.ops.export_scene = _ExportScene(bpy.ops.export_scene)
    sys.path.insert(0, os.getcwd())
    sys.argv = ["app_blender.py", *sys.argv[1:]]
    runpy.run_path("app_blender.py", run_name="__main__")


if __name__ == "__main__":
    main()
