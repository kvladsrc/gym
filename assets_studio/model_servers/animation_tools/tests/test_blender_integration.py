import os
import subprocess
from pathlib import Path

import pytest
from animation_tools_model_server.server import (
    AnimationToolsServer,
    BakeParams,
    RetargetParams,
    RetopoParams,
    blender,
)
from asset_processing.blender import RETARGET_SCRIPT

from model_server_sdk import InputData, Job

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

    baked = server.generate(
        job("3d-bake-normals", {"high": high, "low": low}, BakeParams(texture_size=1024))
    )[0]
    length = struct.unpack("<I", baked.data[12:16])[0]
    gltf = json.loads(baked.data[20 : 20 + length])
    assert "normalTexture" in gltf["materials"][0]
