"""One motion-capture job: upstream's custom-rig pipeline, then our motion transfer.

Upstream's way for a rig of one's own (examples/custom_rig) builds a small
dataset from the rig's FBX and its motions, then runs inference against it.
It was written for Truebones Zoo rigs; for a rig from SkinTokens or UniRig
this module fills the gaps, each found on a horse rigged by SkinTokens:

- prepare_rig.py (Blender): the rig in the units and axes of a Truebones rig,
  and a motion made of its rest pose (a fresh rig has none);
- naming.py: joint names T5 can read, and the facing direction, which
  upstream's guess turned almost backwards;
- no static rotations: upstream marks joints that never rotate in the rig's
  motions; with the rest pose as the only motion that was every joint, and
  the result stood still;
- stage fix_fbx skipped: it removes weightless leaf bones of 3ds Max Bipeds
  and wants an animation on the base FBX;
- a shadow of the source per job: extract_bvh_pose.py and video_to_images.py
  read zoo/ under the source whatever ZOO_ROOT says, so each job gets the
  source's entries as links beside a zoo/ of its own;
- PYTHON reset to the source alone: nix's Blender exports its Python 3.13
  packages in PYTHONPATH, which upstream's Blender scripts hand down to the
  Python they start.

Then model_server_sdk's retarget.py (Blender) puts the predicted BVH on the
input rig.
"""

import json
import os
import shlex
import shutil
import subprocess
import sys
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
from asset_processing.blender import RETARGET_SCRIPT as RETARGET

from mocapanything_model_server.naming import (
    Bone,
    RigReading,
    front_in_rig_space,
    read_rig,
)

# The rig's name in the dataset: the clip's "<name>#" prefix finds it.
RIG = "Rig"
# Upstream trained on 30 fps renders of 30 fps Truebones motions.
FPS = 30
HERE = Path(__file__).resolve().parent
PREPARE_RIG = HERE / "prepare_rig.py"
CONSISTENCY = HERE / "consistency.py"
# retarget.py's summary line.
MOCAP_PREFIX = "MOCAP "
# What prepare_rig.py prints when the input has no skeleton (a Blender script:
# it cannot import this package).
NO_ARMATURE = "NO_ARMATURE"


class StageError(RuntimeError):
    """A pipeline stage failed; the message is its name and output's end."""

    def __init__(self, stage: str, output: str, returncode: int) -> None:
        super().__init__(f"{stage} failed")
        self.stage = stage
        self.output = output
        self.returncode = returncode


@dataclass(frozen=True)
class Models:
    """Local snapshot directories of the weights the pipeline loads."""

    weights: Path  # holds the checkpoint
    rmbg: Path
    dinov2: Path
    t5: Path
    checkpoint: str


def shadow_source(source: Path, work: Path) -> Path:
    """work/src: every entry of the source as a link, and an empty zoo/."""
    shadow = work / "src"
    shadow.mkdir()
    for entry in source.iterdir():
        if entry.name in ("zoo", ".git"):
            continue
        (shadow / entry.name).symlink_to(entry)
    (shadow / "zoo").mkdir()
    return shadow


def python_wrapper(work: Path, shadow: Path) -> Path:
    """An executable for $PYTHON: this environment's Python, the shadow
    source alone on its path."""
    wrapper = work / "python.sh"
    wrapper.write_text(
        "#!/bin/sh\n"
        f"export PYTHONPATH={shlex.quote(str(shadow))}\n"
        f'exec {shlex.quote(sys.executable)} "$@"\n'
    )
    wrapper.chmod(0o755)
    return wrapper


def skeleton_of(bones_json: Path) -> list[Bone]:
    raw = json.loads(bones_json.read_text())
    return [Bone(b["name"], b["parent"], tuple(b["head"]), tuple(b["tail"])) for b in raw]


def unpin_rotations(species_info: Path, species: str) -> None:
    """No joint is known never to rotate: the rig's only motion is its rest pose."""
    info = np.load(species_info, allow_pickle=True).item()
    info[species]["static_rot_joints"] = []
    np.save(species_info, info, allow_pickle=True)


def inference_config(template: Mapping[str, Any], work: Path, models: Models) -> dict[str, Any]:
    """Upstream's custom-rig inference config on this job's directories,
    without its preview renders (they need nothing of ours and take minutes)."""
    config = json.loads(json.dumps(template))
    config["weights"].update(
        video2pose_ckpt_root=str(models.weights),
        rmbg_weights_dir=str(models.rmbg),
        ckpt_name=models.checkpoint,
    )
    config["data"].update(
        base_dir="zoo",
        character_dir="zoo/characters_face_zplus",
        scale_dict_path="zoo/cache/__mesh2pose1002_species_scale_cache.pkl",
        bvh_roots=["zoo/bvh"],
        image_roots=[str(work / "videos")],
        video_roots=[str(work / "videos")],
        frames_tmp_root=str(work / "frames"),
    )
    config["output"].update(save_dir=str(work / "out"), output_tag="job", blender_path=None)
    return config


def summary_of(output: str) -> dict[str, Any]:
    """retarget.py's summary: frames, bones, worst_joint_m, ..."""
    for line in reversed(output.splitlines()):
        if line.startswith(MOCAP_PREFIX):
            pairs = (item.split("=", 1) for item in line[len(MOCAP_PREFIX) :].split())
            return {key: value for key, value in pairs}
    raise StageError("retarget", output, 0)


class Job:
    """The directories and commands of one motion-capture job."""

    def __init__(
        self, work: Path, source: Path, blender: str, models: Models, timeout_s: float
    ) -> None:
        self.work = work
        self.source = source
        self.blender = blender
        self.models = models
        self.timeout_s = timeout_s
        self.shadow = shadow_source(source, work)
        self.python = python_wrapper(work, self.shadow)
        self.log: list[str] = []

    def env(self) -> dict[str, str]:
        env = {key: value for key, value in os.environ.items() if key != "PYTHONPATH"}
        env.update(
            PYTHON=str(self.python),
            BLENDER=self.blender,
            DATA_ROOT=str(self.work / "rigs"),
            ZOO_ROOT="zoo",
            ALIGN_REF_DIR=str(self.work / "align_ref"),
            HF_HUB_OFFLINE="1",
            PYTHONPATH=str(self.shadow),
        )
        return env

    def run(self, stage: str, command: Sequence[str], **env: str) -> str:
        try:
            done = subprocess.run(  # noqa: S603 -- fixed argv, paths are ours
                list(command),
                cwd=self.shadow,
                env={**self.env(), **env},
                capture_output=True,
                text=True,
                timeout=self.timeout_s,
                check=False,
            )
        except subprocess.TimeoutExpired as error:
            raise StageError(stage, f"timed out after {self.timeout_s:.0f} s", 1) from error
        output = done.stdout + done.stderr
        self.log.append(f"== {stage}\n{output}")
        if done.returncode != 0:
            raise StageError(stage, output, done.returncode)
        return output

    def stages(self, names: str) -> None:
        self.run(names, ["bash", "preprocess/run_pipeline.sh"], STAGES=names)

    def prepare(self, rig: Path, video: Path) -> RigReading:
        """Rig, names, facing and clip in place for upstream's pipeline."""
        rig_dir = self.work / "rigs" / RIG
        self.run(
            "prepare_rig",
            [
                self.blender,
                "-b",
                "--factory-startup",
                "-P",
                str(PREPARE_RIG),
                "--",
                str(rig),
                str(rig_dir),
                RIG,
            ],
        )
        bones_json = rig_dir / "bones.json"
        skeleton = skeleton_of(bones_json)
        shutil.move(bones_json, self.work / "bones.json")
        reading = read_rig(skeleton)
        if reading.names is not None:
            (self.work / "names.json").write_text(json.dumps({RIG: reading.names}, indent=1))
        align = self.work / "align_ref" / RIG
        align.mkdir(parents=True)
        np.save(align / "front.npy", front_in_rig_space(skeleton, reading.facing))
        videos = self.work / "videos"
        videos.mkdir()
        self.run(
            "video",
            [
                ffmpeg(),
                "-v",
                "error",
                "-i",
                str(video),
                "-an",
                "-vf",
                f"fps={FPS}",
                "-c:v",
                "libx264",
                "-crf",
                "16",
                "-pix_fmt",
                "yuv420p",
                str(videos / f"{RIG}#clip.mp4"),
            ],
        )
        return reading

    def dataset(self) -> None:
        """Upstream's custom-rig preprocessing, stage by stage (run.sh's order)."""
        self.stages("move_fbx,extract_char,align_faces")
        self.stages("rotate_bvh,extract_pose")
        # Our names when the rig's say nothing; else upstream cleans the rig's.
        names = self.work / "names.json"
        mapping = ["--joint_name_map", str(names)] if names.is_file() else []
        self.run(
            "species_info",
            [
                str(self.python),
                "preprocess/build_species_info.py",
                "--dataset_root",
                "zoo",
                *mapping,
                "--t5_model",
                str(self.models.t5),
            ],
        )
        self.stages("scale_cache,render_videos,extract_frames")
        self.run(
            "reference_features",
            [
                str(self.python),
                "preprocess/preprocess_image_only.py",
                "--dataset_root",
                "zoo",
                "--rmbg_weights_dir",
                str(self.models.rmbg),
                "--dinov2_model",
                str(self.models.dinov2),
            ],
        )
        unpin_rotations(self.shadow / "zoo" / "species_info_dict.npy", RIG)

    def infer(self, *, background_removed: bool = False) -> Path:
        import yaml

        template = yaml.safe_load((self.source / "examples/custom_rig/inference.yaml").read_text())
        config = self.work / "inference.yaml"
        settings = inference_config(template, self.work, self.models)
        settings["data"]["background_removed"] = background_removed
        config.write_text(yaml.safe_dump(settings))
        entry = (
            [str(HERE / "inference_adapter.py")]
            if background_removed
            else ["-m", "inference.video2pose2rot"]
        )
        self.run(
            "inference",
            [str(self.python), *entry, "--config", str(config)],
        )
        found = sorted((self.work / "out").rglob("*_rot6d_pred.bvh"))
        if not found:
            raise StageError("inference", "\n".join(self.log[-1:]), 0)
        return found[0]

    def consistency(self) -> dict[str, Any]:
        """How well the model's positions and rotations agree (consistency.py)."""
        output = self.run(
            "consistency", [str(self.python), str(CONSISTENCY), str(self.work / "out")]
        )
        line = next(
            line for line in reversed(output.splitlines()) if line.startswith("CONSISTENCY ")
        )
        return json.loads(line.removeprefix("CONSISTENCY "))

    def apply(
        self, rig: Path, motion: Path, facing: tuple[str, str, str], root_motion: str
    ) -> tuple[Path, dict[str, Any]]:
        out = self.work / "animated.fbx"
        output = self.run(
            "retarget",
            [
                self.blender,
                "-b",
                "--factory-startup",
                "-P",
                str(RETARGET),
                "--",
                str(rig),
                str(motion),
                str(out),
                *facing,
                root_motion,
            ],
        )
        return out, summary_of(output)


def ffmpeg() -> str:
    import imageio_ffmpeg

    return imageio_ffmpeg.get_ffmpeg_exe()


def frame_count(video: Path) -> int:
    import cv2

    capture = cv2.VideoCapture(str(video))
    try:
        return int(capture.get(cv2.CAP_PROP_FRAME_COUNT))
    finally:
        capture.release()
