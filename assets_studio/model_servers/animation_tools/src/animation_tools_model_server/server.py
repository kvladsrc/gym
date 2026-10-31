"""Rig preparation, animation transfer, retopology and normal baking: Blender
scripts, without weights or a GPU model."""

import json
import os
import shutil
import subprocess
import tempfile
from pathlib import Path
from typing import Literal

from asset_processing.blender import RETARGET_SCRIPT
from asset_processing.native_motion import read_native_motion
from model_server_sdk.media import SMPLX_MOTION
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    explain_child_failure,
    ui,
)

PREPARE_SCRIPT = RETARGET_SCRIPT.with_name("rigging.py")
MESH_SCRIPT = RETARGET_SCRIPT.with_name("mesh_tools.py")
TIMEOUT_S = 600


class PrepareParams(BaseModel):
    model_config = ConfigDict(extra="forbid")


class RetargetParams(PrepareParams):
    root_motion: Literal["in-place", "stationary", "world"] = Field(
        default="in-place",
        description="in-place: no path/heading; stationary: keep turns; world: keep the path",
        json_schema_extra=ui(
            ru="in-place — без пути и поворотов; stationary — на месте, с поворотами; "
            "world — ещё и с перемещением",
            primary=True,
        ),
    )
    method: Literal["auto", "positions"] = Field(
        default="auto",
        description="Auto transfers humanoid rotations and twist; "
        "positions transfers bone directions only",
        json_schema_extra=ui(
            ru="auto — перенос вращений, включая кисти; positions — перенос по направлениям костей"
        ),
    )


class NativeRetargetParams(PrepareParams):
    rest_alignment: Literal["bone-directions", "matched-neutral"] = Field(
        default="bone-directions",
        description="matched-neutral requires the target surface to share SMPL’s neutral pose",
        json_schema_extra=ui(ru="matched-neutral — исходная поза поверхности уже совпадает с SMPL"),
    )

    root_motion: Literal["in-place", "stationary", "world"] = Field(
        default="stationary",
        description="Keep turns for attacks; world also preserves the path",
        json_schema_extra=ui(ru="stationary — с поворотами; world — ещё и с перемещением"),
    )


class RetopoParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    faces: int = Field(
        default=20_000,
        ge=500,
        le=200_000,
        description="Target faces: quads for QuadriFlow/AutoRemesher, triangles for Decimate",
        json_schema_extra=ui(
            ru="Цель: квады у QuadriFlow/AutoRemesher, треугольники у Decimate",
            primary=True,
        ),
    )
    method: Literal["quadriflow", "decimate", "autoremesher"] = Field(
        default="quadriflow",
        description="quadriflow: quads along the shape (over a voxel remesh); "
        "decimate: collapse edges; autoremesher: AutoRemesher quad layout without voxel remesh",
        json_schema_extra=ui(
            ru="quadriflow — квады по форме (через воксельный ремеш); "
            "decimate — схлопывание рёбер; autoremesher — квады без воксельного ремеша"
        ),
    )
    edge_scaling: float = Field(
        default=1.0,
        ge=1.0,
        le=4.0,
        description="AutoRemesher only: edge scaling factor",
        json_schema_extra=ui(ru="Только AutoRemesher: масштаб рёбер"),
    )
    sharp_edge: float = Field(
        default=90.0,
        ge=30.0,
        le=180.0,
        description="AutoRemesher only: dihedral angle, degrees, above which an edge is kept sharp",
        json_schema_extra=ui(ru="Только AutoRemesher: двугранный угол острого ребра, градусы"),
    )
    smooth_normal: float = Field(
        default=0.0,
        ge=0.0,
        le=180.0,
        description="AutoRemesher only: smooth normal angle threshold, degrees",
        json_schema_extra=ui(ru="Только AutoRemesher: порог сглаживания нормалей, градусы"),
    )
    adaptivity: float = Field(
        default=1.0,
        ge=0.0,
        le=1.0,
        description="AutoRemesher only: smaller quads where the surface bends more",
        json_schema_extra=ui(ru="Только AutoRemesher: мельче квады на изгибах"),
    )
    anisotropy: float = Field(
        default=1.0,
        ge=0.0,
        le=1.0,
        description="AutoRemesher only: quads stretched along the curvature",
        json_schema_extra=ui(ru="Только AutoRemesher: вытягивание квадов вдоль кривизны"),
    )


class BakeParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    texture_size: Literal[1024, 2048, 4096] = Field(
        default=2048,
        description="Normal map size, pixels",
        json_schema_extra=ui(ru="Размер карты нормалей, пиксели", primary=True),
    )
    ray_distance: float = Field(
        default=0.015,
        ge=0.001,
        le=0.1,
        description="How far rays look for the detailed surface, as a share of the model's size",
        json_schema_extra=ui(ru="Как далеко лучи ищут детальную поверхность, доля размера модели"),
    )


GLB = ["model/gltf-binary"]
RETOPO = TaskSpec(
    "3d-retopo",
    RetopoParams,
    ("model/gltf-binary",),
    prompt="none",
    inputs=(
        InputSpec(
            role="mesh",
            mime=GLB,
            description="A detailed mesh (Hi3D's million triangles); "
            "decimate keeps a painted mesh's UVs and material",
            labels={
                "ru": "Детальная сетка (миллион треугольников Hi3D); "
                "decimate сохраняет UV и материал покрашенной"
            },
        ),
    ),
    max_count=1,
)
BAKE = TaskSpec(
    "3d-bake-normals",
    BakeParams,
    ("model/gltf-binary",),
    prompt="none",
    inputs=(
        InputSpec(
            role="high",
            mime=GLB,
            description="The detailed mesh the normals come from",
            labels={"ru": "Детальная сетка, откуда берутся нормали"},
        ),
        InputSpec(
            role="low",
            mime=GLB,
            description="The painted game mesh (UVs and material) that gets them",
            labels={"ru": "Покрашенная игровая сетка (UV и материал), куда они запекаются"},
        ),
    ),
    max_count=1,
)


RIG = InputSpec(
    role="rig",
    mime=["model/x-fbx", "model/gltf-binary"],
    description="Character with a skeleton and skin weights",
    labels={"ru": "Персонаж со скелетом и весами кожи"},
)
PREPARE = TaskSpec(
    "prepare-rig", PrepareParams, ("model/x-fbx",), prompt="none", inputs=(RIG,), max_count=1
)
RETARGET = TaskSpec(
    "retarget-motion",
    RetargetParams,
    ("model/x-fbx",),
    prompt="none",
    inputs=(
        RIG,
        InputSpec(
            role="motion",
            mime=["model/x-fbx", "model/gltf-binary"],
            description="Humanoid motion with Mixamo bone names "
            "(Kimodo, Mixamo or studio motion capture)",
            labels={
                "ru": "Анимация с именами костей Mixamo: Kimodo, Mixamo или захват движения студии"
            },
        ),
    ),
    max_count=1,
)
NATIVE_RETARGET = TaskSpec(
    "retarget-native-motion",
    NativeRetargetParams,
    ("model/x-fbx",),
    prompt="none",
    inputs=(
        RIG,
        InputSpec(
            role="motion",
            mime=[SMPLX_MOTION],
            description="studio.smplx-motion.v1: native body motion, before target-rig transfer",
        ),
    ),
    max_count=1,
)


def blender() -> str | None:
    found = os.environ.get("BLENDER") or shutil.which("blender")
    profile = Path.home() / ".nix-profile/bin/blender"
    return found or (str(profile) if profile.is_file() else None)


def run(binary: str, script: Path, *args: str) -> str:
    try:
        done = subprocess.run(  # noqa: S603 -- fixed Blender argv, no shell
            [
                binary,
                "-b",
                "--factory-startup",
                "--python-exit-code",
                "1",
                "-P",
                str(script),
                "--",
                *args,
            ],
            capture_output=True,
            text=True,
            timeout=TIMEOUT_S,
            check=False,
        )
    except subprocess.TimeoutExpired as error:
        raise GenerationError(f"animation processing exceeded {TIMEOUT_S} s") from error
    output = done.stdout + done.stderr
    invalid = next(
        (
            line.split(" ", 1)[1]
            for line in output.splitlines()
            if line.startswith(("RIG_INVALID ", "MESH_INVALID "))
        ),
        None,
    )
    if invalid:
        raise InvalidInput(invalid)
    if done.returncode:
        raise GenerationError(explain_child_failure(output, done.returncode))
    return output


def mesh_report(output: str) -> dict:
    line = next((line for line in output.splitlines() if line.startswith("MESH_REPORT ")), None)
    if line is None:
        raise GenerationError("Blender did not report the mesh")
    return json.loads(line.removeprefix("MESH_REPORT "))


def prepare_report(output: str) -> dict:
    line = next((line for line in output.splitlines() if line.startswith("RIG_REPORT ")), None)
    if line is None:
        raise GenerationError("Blender did not report a prepared rig")
    return json.loads(line.removeprefix("RIG_REPORT "))


class AnimationToolsServer(ModelServer):
    model = ModelInfo(
        id="animation-tools",
        name="Animation tools (Blender)",
        revision="18",
        license="Repository code; Blender: GPL-3.0-or-later",
    )
    tasks = (PREPARE, RETARGET, NATIVE_RETARGET, RETOPO, BAKE)

    def __init__(self) -> None:
        self._blender: str | None = None

    def load(self) -> None:
        self._blender = blender()
        if self._blender is None:
            raise FileNotFoundError("Blender is required; put it on PATH or set BLENDER")

    def generate(self, job: Job) -> list[Output]:
        assert self._blender
        if job.task in (RETOPO.task, BAKE.task):
            return [self._mesh(job)]
        with tempfile.TemporaryDirectory(prefix="animation-tools-") as directory:
            work = Path(directory)
            rig_input = job.inputs["rig"]
            rig = work / ("input.fbx" if rig_input.mime == "model/x-fbx" else "input.glb")
            rig.write_bytes(rig_input.data)
            prepared = work / "prepared.fbx"
            report = prepare_report(run(self._blender, PREPARE_SCRIPT, str(rig), str(prepared)))
            if not prepared.is_file():
                raise GenerationError("Blender did not write the prepared FBX")
            if job.task == "prepare-rig":
                return [Output("model/x-fbx", prepared.read_bytes(), report)]
            assert isinstance(job.params, (RetargetParams, NativeRetargetParams))
            native = None
            if job.task == "retarget-native-motion":
                try:
                    native = read_native_motion(job.inputs["motion"].data)
                except ValueError as error:
                    raise InvalidInput(str(error)) from error
                motion = work / "motion.bvh"
                motion.write_text(native.bvh)
                assert isinstance(job.params, NativeRetargetParams)
                method = (
                    "smpl-neutral" if job.params.rest_alignment == "matched-neutral" else "smpl"
                )
            else:
                assert isinstance(job.params, RetargetParams)
                motion = work / (
                    "motion.glb"
                    if job.inputs["motion"].mime == "model/gltf-binary"
                    else "motion.fbx"
                )
                motion.write_bytes(job.inputs["motion"].data)
                method = "aim" if job.params.method == "positions" else "rotations"
            target = work / "animated.fbx"
            output = run(
                self._blender,
                RETARGET_SCRIPT,
                str(prepared),
                str(motion),
                str(target),
                "sides",
                "auto",
                "auto",
                job.params.root_motion,
                method,
            )
            if not target.is_file():
                raise GenerationError("Blender did not write the animated FBX")
            summary = next((line for line in output.splitlines() if line.startswith("MOCAP ")), "")
            if not summary:
                raise GenerationError("Blender did not report transferred animation")
            metadata = {
                **report,
                "method": method,
                "root_motion": job.params.root_motion,
                "summary": summary,
            }
            if native is not None:
                metadata.update(frames=native.frames, native_model=native.source.get("model"))
            return [Output("model/x-fbx", target.read_bytes(), metadata)]

    def _mesh(self, job: Job) -> Output:
        assert self._blender
        with tempfile.TemporaryDirectory(prefix="mesh-tools-") as directory:
            work = Path(directory)
            target = work / "result.glb"
            if job.task == RETOPO.task:
                assert isinstance(job.params, RetopoParams)
                source = work / "mesh.glb"
                source.write_bytes(job.inputs["mesh"].data)
                args = [
                    "retopo",
                    str(source),
                    str(target),
                    str(job.params.faces),
                    job.params.method,
                    json.dumps(
                        {
                            "edge_scaling": job.params.edge_scaling,
                            "sharp_edge": job.params.sharp_edge,
                            "smooth_normal": job.params.smooth_normal,
                            "adaptivity": job.params.adaptivity,
                            "anisotropy": job.params.anisotropy,
                        }
                    ),
                ]
            else:
                assert isinstance(job.params, BakeParams)
                high, low = work / "high.glb", work / "low.glb"
                high.write_bytes(job.inputs["high"].data)
                low.write_bytes(job.inputs["low"].data)
                args = [
                    "bake",
                    str(high),
                    str(low),
                    str(target),
                    str(job.params.texture_size),
                    str(job.params.ray_distance),
                ]
            report = mesh_report(run(self._blender, MESH_SCRIPT, *args))
            if not target.is_file():
                raise GenerationError("Blender did not write the GLB")
            return Output("model/gltf-binary", target.read_bytes(), report)
