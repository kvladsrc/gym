#!/usr/bin/env python3
"""Render procedural eight-direction character animation frames in Blender."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


def parse_args() -> argparse.Namespace:
    """Parse arguments passed to Blender after its `--` separator."""
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--animation", choices=("walk",), required=True)
    return parser.parse_args(blender_args)


def load_config(project_root: Path) -> dict:
    """Load the versioned character and render configuration."""
    config_path = project_root / "tools/sprite_pipeline/character.json"
    with config_path.open(encoding="utf-8") as config_file:
        return json.load(config_file)


def clear_scene() -> None:
    """Remove the default Blender scene contents."""
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def import_character(model_path: Path) -> tuple[bpy.types.Object, bpy.types.Object, list]:
    """Import the GLB and parent its roots to one rotation pivot."""
    bpy.ops.import_scene.gltf(filepath=str(model_path))
    imported_objects = list(bpy.context.scene.objects)
    armature = next((obj for obj in imported_objects if obj.type == "ARMATURE"), None)
    if armature is None:
        raise RuntimeError(f"No armature found in {model_path}")

    pivot = bpy.data.objects.new("SpritePivot", None)
    bpy.context.scene.collection.objects.link(pivot)
    for obj in imported_objects:
        if obj.parent is None:
            obj.parent = pivot
    return pivot, armature, imported_objects


def mesh_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    """Return world-space bounds for all imported mesh objects."""
    corners = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        if obj.type == "MESH"
        for corner in obj.bound_box
    ]
    if not corners:
        raise RuntimeError("Imported character has no meshes")
    minimum = Vector(tuple(min(point[axis] for point in corners) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in corners) for axis in range(3)))
    return minimum, maximum


def center_character(pivot: bpy.types.Object, objects: list[bpy.types.Object]) -> float:
    """Place the model origin at the ground below its horizontal center."""
    bpy.context.view_layer.update()
    minimum, maximum = mesh_bounds(objects)
    pivot.location = Vector(
        (-(minimum.x + maximum.x) * 0.5, -(minimum.y + maximum.y) * 0.5, -minimum.z)
    )
    bpy.context.view_layer.update()
    return maximum.z - minimum.z


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    """Point an object local -Z axis towards a target."""
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_camera(character_height: float) -> bpy.types.Object:
    """Create the fixed orthographic sprite camera."""
    camera_data = bpy.data.cameras.new("SpriteCamera")
    camera = bpy.data.objects.new("SpriteCamera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = Vector((0.0, -6.0, 5.2))
    look_at(camera, Vector((0.0, 0.0, character_height * 0.48)))
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = character_height
    bpy.context.scene.camera = camera
    return camera


def add_area_light(name: str, location: Vector, energy: float, size: float) -> None:
    """Add a soft area light aimed at the character."""
    light_data = bpy.data.lights.new(name=name, type="AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    light.location = location
    look_at(light, Vector((0.0, 0.0, 1.0)))
    bpy.context.scene.collection.objects.link(light)


def configure_render(output_size: int) -> None:
    """Configure transparent, color-managed sprite rendering."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = output_size
    scene.render.resolution_y = output_size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 30
    scene.world.color = (0.06, 0.07, 0.09)
    add_area_light("KeyLight", Vector((-3.5, -4.0, 6.0)), 900.0, 4.0)
    add_area_light("FillLight", Vector((4.0, 1.0, 3.5)), 500.0, 5.0)


def rotation(axis: str, angle: float) -> Matrix:
    """Create a local-axis rotation matrix."""
    return Matrix.Rotation(angle, 4, axis)


def apply_pose(
    armature: bpy.types.Object,
    base_matrices: dict[str, Matrix],
    phase: float,
) -> float:
    """Apply one simple procedural movement pose and return vertical bob."""
    for bone_name, matrix in base_matrices.items():
        armature.pose.bones[bone_name].matrix_basis = matrix.copy()

    leg_amplitude = 0.38
    arm_amplitude = 0.32
    knee_amplitude = 0.42
    stride = math.cos(phase)
    lift = math.sin(phase)
    left_lift = max(0.0, lift)
    right_lift = max(0.0, -lift)

    transforms = {
        "upperleg.l": rotation("X", leg_amplitude * stride),
        "upperleg.r": rotation("X", -leg_amplitude * stride),
        "lowerleg.l": rotation("X", left_lift * knee_amplitude),
        "lowerleg.r": rotation("X", right_lift * knee_amplitude),
        "upperarm.l": rotation("Z", -0.9) @ rotation("X", -arm_amplitude * stride),
        "upperarm.r": rotation("Z", 0.9) @ rotation("X", arm_amplitude * stride),
        "lowerarm.l": rotation("X", -0.18 - left_lift * 0.12),
        "lowerarm.r": rotation("X", -0.18 - right_lift * 0.12),
        "spine": rotation("X", -0.035),
        "chest": rotation("Z", lift * 0.04),
    }
    for bone_name, transform in transforms.items():
        bone = armature.pose.bones.get(bone_name)
        if bone is not None:
            bone.matrix_basis = base_matrices[bone_name] @ transform

    return 0.032 * (1.0 - math.cos(phase * 2.0))


def render_animation(
    project_root: Path,
    config: dict,
    animation: str,
    pivot: bpy.types.Object,
    armature: bpy.types.Object,
) -> None:
    """Render every configured direction and animation phase."""
    frame_count = int(config["frames"][animation])
    output_root = project_root / "tmp/sprites" / config["name"] / animation
    base_location = pivot.location.copy()
    base_matrices = {bone.name: bone.matrix_basis.copy() for bone in armature.pose.bones}

    for direction_index, (direction_name, angle_degrees) in enumerate(config["directions"]):
        direction_dir = output_root / f"{direction_index:02d}_{direction_name}"
        direction_dir.mkdir(parents=True, exist_ok=True)
        for frame_index in range(frame_count):
            phase = math.tau * frame_index / frame_count
            bob = apply_pose(armature, base_matrices, phase)
            pivot.location = base_location + Vector((0.0, 0.0, bob))
            pivot.rotation_euler.z = math.radians(angle_degrees)
            bpy.context.view_layer.update()
            bpy.context.scene.render.filepath = str(direction_dir / f"frame_{frame_index:02d}.png")
            bpy.ops.render.render(write_still=True)

    print(f"Rendered {animation} frames to {output_root}")


def main() -> None:
    """Load the configured character and render the requested animation."""
    args = parse_args()
    project_root = args.project_root.resolve()
    config = load_config(project_root)
    clear_scene()
    pivot, armature, imported_objects = import_character(project_root / config["model"])
    character_height = center_character(pivot, imported_objects)
    add_camera(character_height)
    configure_render(int(config["output_size"]))
    render_animation(project_root, config, args.animation, pivot, armature)


if __name__ == "__main__":
    main()
