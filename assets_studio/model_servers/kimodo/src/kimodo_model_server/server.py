"""Kimodo text-to-motion server (contract v1, task text-to-motion).

A description in English in, a humanoid animation out: an FBX with a
skeleton in the T-pose and its motion, at 30 frames a second. NVIDIA's
Kimodo (2026) is a kinematic motion diffusion model trained on ~700 hours of
motion capture (Bones Rigplay); sentences separated by periods become
consecutive motions with transitions.

Kimodo's skeleton is SOMA's (77 joints). Its bones are renamed to Mixamo's
(``mixamorig:Hips``…; to_fbx.py): Unity maps such a skeleton to a Humanoid
avatar by itself, and a Humanoid clip plays on any Humanoid character, the
Make-It-Animatable rigs (server ``mia``) among them.

The text encoder is LLM2Vec on Llama-3-8B-Instruct: ~16 GB in bfloat16, run
on the CPU (one pass per prompt) so the GPU holds only Kimodo (~1 GB, under
3 GB while generating). Llama 3 is gated: the Hugging Face account needs
Meta's approval.

Licences: Kimodo, NVIDIA Open Model License (commercial use allowed, the
outputs are not NVIDIA's); LLM2Vec adapters, MIT; Llama 3, Meta Llama 3
Community License.

``--model smplx`` serves Kimodo-SMPLX-RP-v1 instead (gated; NVIDIA R&D model
licence): the 22 SMPL-X body joints of GVHMR and HTD-Refine. It returns the
studio's native body motion (``studio.smplx-motion.v1``, text) for later
``retarget-native-motion``, never an FBX, and adds in-betweening of native clips
(native.py converts to and from Kimodo's canonical frame).
"""

import hashlib
import json
import logging
import os
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any

from model_server_sdk.media import SMPLX_MOTION, SOMA_MOTION
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
    is_out_of_memory,
    ui,
)

from . import native, soma
from .constraints import read_constraints

logger = logging.getLogger("kimodo_model_server")

# Weights per served variant: Studio runs one model per server.
VARIANTS = {
    "soma": {
        "name": "Kimodo-SOMA-RP-v1.2",
        "revision": "0e3c89024d896064b6851201373f7834784a386b",  # pragma: allowlist secret
        "id": "kimodo",
        "license": "NVIDIA Open Model License; Llama 3 Community License (text encoder)",
    },
    "smplx": {
        "name": "Kimodo-SMPLX-RP-v1",
        "revision": "1419ba56b734c48bbafb41fefa84088ca94583b5",  # pragma: allowlist secret
        "id": "kimodo-smplx",
        "license": "NVIDIA R&D Model License; Llama 3 Community License (text encoder)",
    },
}
MODEL_NAME = VARIANTS["soma"]["name"]
WEIGHTS_REPO = f"nvidia/{MODEL_NAME}"
ENCODER_REPO = "McGill-NLP/LLM2Vec-Meta-Llama-3-8B-Instruct-mntp"
ENCODER_PEFT_REPO = "McGill-NLP/LLM2Vec-Meta-Llama-3-8B-Instruct-mntp-supervised"
LLAMA_REPO = "meta-llama/Meta-Llama-3-8B-Instruct"
# Git revisions, not secrets.
WEIGHTS_REVISION = VARIANTS["soma"]["revision"]
ENCODER_REVISION = "31474e395ada192e8ed1586db6be79fb3b70c9c0"  # pragma: allowlist secret
ENCODER_PEFT_REVISION = "baa8ebf04a1c2500e61288e7dad65e8ae42601a7"  # pragma: allowlist secret
SOURCE_REVISION = "e1c219c99b4b94ba48be1fb6a1cbc64ecee4c954"  # pragma: allowlist secret

FBX_TIMEOUT_S = 300
MAX_SECONDS = 10.0  # Kimodo's limit per prompt (its docs)


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    seconds: float = Field(
        default=5.0,
        ge=1.0,
        le=MAX_SECONDS,
        description="Length of each sentence's motion, in seconds",
        json_schema_extra=ui(
            ru="Длительность движения на каждое предложение, секунд", primary=True
        ),
    )
    steps: int = Field(
        default=100,
        ge=10,
        le=200,
        description="Diffusion steps",
        json_schema_extra=ui(ru="Шаги диффузии"),
    )
    fix_feet: bool = Field(
        default=True,
        description="Post-processing against foot sliding",
        json_schema_extra=ui(ru="Убрать проскальзывание ступней"),
    )


TASK = TaskSpec(
    "text-to-motion",
    Params,
    ("model/x-fbx",),
    prompt="required",
    inputs=(
        InputSpec(
            role="constraints",
            mime=["text/plain"],
            required=False,
            description="Optional Kimodo constraints JSON: SOMA 30/77 poses, Y-up metres, "
            "0-based frames. One sentence only when constrained.",
            labels={"ru": "Опорные позы Kimodo (JSON, необязательно)"},
        ),
    ),
    max_count=4,
)


class InbetweenParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    seconds: float = Field(
        default=2.0,
        ge=1.0,
        le=MAX_SECONDS,
        description="Length of the result, including the keyed context frames, seconds",
        json_schema_extra=ui(
            ru="Длительность результата вместе с опорными кадрами, секунд", primary=True
        ),
    )
    context_frames: int = Field(
        default=3,
        ge=1,
        le=9,
        description="Frames taken as keys from the end of the motion and the start of the "
        "continuation (velocity as well as pose)",
        json_schema_extra=ui(ru="Опорных кадров с конца движения и с начала продолжения"),
    )
    forward_m: float = Field(
        default=0.0,
        ge=-3.0,
        le=3.0,
        description="Where the continuation starts: metres ahead of the motion's last frame",
        json_schema_extra=ui(ru="Насколько дальше вперёд начинается продолжение, метров"),
    )
    turn_deg: float = Field(
        default=0.0,
        ge=-180.0,
        le=180.0,
        description="Turn of the continuation's heading relative to the motion's last frame",
        json_schema_extra=ui(ru="Поворот продолжения относительно последнего кадра, градусов"),
    )
    steps: int = Field(
        default=100,
        ge=10,
        le=200,
        description="Diffusion steps",
        json_schema_extra=ui(ru="Шаги диффузии"),
    )
    fix_feet: bool = Field(
        default=True,
        description="Post-processing: contacts and keys",
        json_schema_extra=ui(ru="Постобработка: стопы и опорные кадры"),
    )
    hold_hands: bool = Field(
        default=False,
        description="Keep both wrists where the motion's last frame has them, on evenly spaced "
        "inner frames (a held weapon stays put while the body moves)",
        json_schema_extra=ui(ru="Держать кисти на месте последнего кадра движения"),
    )
    hand_keys: int = Field(
        default=6,
        ge=1,
        le=18,
        description="Inner frames with wrist (and foot) constraints when holding",
        json_schema_extra=ui(ru="Сколько кадров середины с опорой на кисти"),
    )
    hold_feet: bool = Field(
        default=False,
        description="Keep both feet where the motion's last frame has them, on the same inner "
        "frames (planted stance)",
        json_schema_extra=ui(ru="Держать стопы на месте последнего кадра движения"),
    )


NATIVE_INPUT = "studio.smplx-motion.v1 (GVHMR, HTD-Refine, joined or Kimodo native motion)"


class KeyframesParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    frames: str = Field(
        default="1",
        pattern=r"^\s*\d+(\s*,\s*\d+)*\s*$",
        description="Key frames of the motion, 1-based, comma-separated (e.g. 1,40,95); their "
        "spacing is kept and the first one starts the result",
        json_schema_extra=ui(ru="Опорные кадры движения через запятую, с 1", primary=True),
    )
    seconds: float = Field(
        default=4.0,
        ge=1.0,
        le=MAX_SECONDS,
        description="Length of the result, seconds; at least the span of the key frames",
        json_schema_extra=ui(ru="Длительность результата, секунд"),
    )
    steps: int = Field(
        default=100,
        ge=10,
        le=200,
        description="Diffusion steps",
        json_schema_extra=ui(ru="Шаги диффузии"),
    )
    fix_feet: bool = Field(
        default=True,
        description="Post-processing: contacts and keys",
        json_schema_extra=ui(ru="Постобработка: стопы и опорные кадры"),
    )
    loop: bool = Field(
        default=False,
        description="End where it begins: the first key again on the last frame (a hold loop)",
        json_schema_extra=ui(ru="Цикл: последний кадр снова первый опорный (удержание)"),
    )


NATIVE_TEXT_TASK = TaskSpec(
    "text-to-native-motion",
    Params,
    (SMPLX_MOTION,),
    prompt="required",
    inputs=(
        InputSpec(
            role="constraints",
            mime=["text/plain"],
            required=False,
            description="Optional Kimodo constraints JSON: SMPL-X 22-joint poses, Y-up metres, "
            "0-based frames. One sentence only when constrained.",
            labels={"ru": "Опорные позы Kimodo SMPL-X (JSON, необязательно)"},
        ),
    ),
    max_count=4,
)
KEYFRAMES_TASK = TaskSpec(
    "keyframes-to-native-motion",
    KeyframesParams,
    (SMPLX_MOTION,),
    prompt="required",
    inputs=(
        InputSpec(
            role="motion",
            mime=[SMPLX_MOTION],
            required=True,
            description=f"Motion whose key frames the result passes through: {NATIVE_INPUT}",
            labels={"ru": "Движение, через опорные кадры которого пройдёт результат"},
        ),
    ),
    max_count=4,
)


class SomaKeyframesParams(KeyframesParams):
    hold_hands: bool = Field(
        default=False,
        description="Keep both hands where the first key has them, on evenly spaced inner "
        "frames (a held sword stays put)",
        json_schema_extra=ui(ru="Держать кисти как в первом опорном кадре"),
    )
    hold_feet: bool = Field(
        default=False,
        description="Keep both feet where the first key has them, on the same inner frames "
        "(a planted stance); Kimodo then also holds the pelvis there",
        json_schema_extra=ui(ru="Держать стопы как в первом опорном кадре"),
    )
    hold_keys: int = Field(
        default=12,
        ge=1,
        le=60,
        description="Inner frames with hand and foot constraints when holding",
        json_schema_extra=ui(ru="Сколько внутренних кадров с ограничениями кистей и стоп"),
    )


SOMA_KEYFRAMES_TASK = TaskSpec(
    "keyframes-to-soma-motion",
    SomaKeyframesParams,
    (SOMA_MOTION,),
    prompt="required",
    inputs=(
        InputSpec(
            role="motion",
            mime=[SOMA_MOTION],
            required=True,
            description="studio.soma-motion.v1 (a GEM-X pose or motion) whose key frames the "
            "result passes through; a pose is its frame 1",
            labels={"ru": "Поза или движение SOMA, через опорные кадры которого пройдёт результат"},
        ),
    ),
    max_count=4,
)
INBETWEEN_TASK = TaskSpec(
    "inbetween-native-motion",
    InbetweenParams,
    (SMPLX_MOTION,),
    prompt="required",
    inputs=(
        InputSpec(
            role="motion",
            mime=[SMPLX_MOTION],
            required=True,
            description=f"Motion to continue from its last frames: {NATIVE_INPUT}",
            labels={"ru": "Движение, от последних кадров которого идёт переход"},
        ),
        InputSpec(
            role="continuation",
            mime=[SMPLX_MOTION],
            required=False,
            description=f"Optional motion to reach at its first frames: {NATIVE_INPUT}",
            labels={"ru": "Движение, к первым кадрам которого ведёт переход"},
        ),
    ),
    max_count=4,
)


def data_directory() -> Path:
    return Path(os.environ.get("KIMODO_DATA", "~/.cache/assets-studio/kimodo")).expanduser()


def download(variant: str = "soma") -> None:
    """Kimodo and the adapters, pinned, into the data directory; Llama 3 into
    the Hub cache (loaded from the adapters' config). Network only."""
    from huggingface_hub import snapshot_download

    data = data_directory()
    weights = VARIANTS[variant]
    snapshot_download(
        f"nvidia/{weights['name']}",
        revision=weights["revision"],
        local_dir=data / "checkpoints" / weights["name"],
    )
    snapshot_download(ENCODER_REPO, revision=ENCODER_REVISION, local_dir=data / "encoder")
    snapshot_download(
        ENCODER_PEFT_REPO, revision=ENCODER_PEFT_REVISION, local_dir=data / "encoder-peft"
    )
    try:
        snapshot_download(LLAMA_REPO, allow_patterns=["*.json", "*.safetensors"])
    except Exception as error:
        raise RuntimeError(
            f"cannot download {LLAMA_REPO}: request access at "
            f"https://huggingface.co/{LLAMA_REPO} and set HF_TOKEN ({error})"
        ) from error


def key_frames(params: KeyframesParams, frames: int) -> tuple[list[int], list[int], int]:
    """The chosen frames (0-based), where they go in the result (the first at 0) and its
    length in frames. A loop also puts the first one on the last frame."""
    chosen = sorted({int(value) - 1 for value in params.frames.split(",")})
    if chosen[0] < 0 or chosen[-1] >= frames:
        raise InvalidInput(f"key frames must be within 1..{frames}")
    at = [index - chosen[0] for index in chosen]
    total = round(params.seconds * 30)
    if at[-1] >= total - (1 if params.loop else 0):
        raise InvalidInput(
            f"the key frames span {at[-1] / 30:.2f} s: make the result longer than that"
        )
    if params.loop:
        chosen, at = [*chosen, chosen[0]], [*at, total - 1]
    return chosen, at, total


def sentences(prompt: str) -> list[str]:
    """Kimodo's multi-prompt form: one motion per sentence."""
    texts = [text.strip() for text in prompt.split(".")]
    return [f"{text}." for text in texts if text]


class KimodoServer(ModelServer):
    model = ModelInfo(
        id="kimodo",
        name=f"Kimodo ({MODEL_NAME})",
        revision=WEIGHTS_REVISION[:12] + ".soma-keys.3",
        license=VARIANTS["soma"]["license"],
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
        details={
            "text encoder": "LLM2Vec on Llama-3-8B-Instruct, bf16 on the CPU (~16 GB RAM)",
            "VRAM": "~1 GB for Kimodo, under 3 GB while generating",
            "skeleton": "SOMA 77 joints (Mixamo names in the FBX)",
        },
    )
    tasks = (TASK, SOMA_KEYFRAMES_TASK)

    def __init__(self, variant: str = "soma") -> None:
        self._model: Any = None
        self.variant = variant
        if variant != "soma":
            weights = VARIANTS[variant]
            self.model = ModelInfo(
                id=weights["id"],
                name=f"Kimodo ({weights['name']})",
                revision=weights["revision"][:12] + ".native.2",
                license=weights["license"],
                source=f"https://huggingface.co/nvidia/{weights['name']}",
                details={
                    "text encoder": "LLM2Vec on Llama-3-8B-Instruct, bf16 on the CPU (~16 GB RAM)",
                    "VRAM": "~1 GB for Kimodo, under 3 GB while generating",
                    "skeleton": "SMPL-X 22 body joints (studio native motion)",
                },
            )
            self.tasks = (NATIVE_TEXT_TASK, INBETWEEN_TASK, KEYFRAMES_TASK)

    def load(self) -> None:
        import torch

        if not torch.cuda.is_available():
            raise RuntimeError("Kimodo needs a CUDA GPU")
        download(self.variant)
        os.environ["CHECKPOINT_DIR"] = str(data_directory() / "checkpoints")
        from kimodo import load_model  # pyright: ignore[reportMissingImports]
        from kimodo.model import LLM2VecEncoder  # pyright: ignore[reportMissingImports]

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        encoder = LLM2VecEncoder(
            base_model_name_or_path=str(data_directory() / "encoder"),
            peft_model_name_or_path=str(data_directory() / "encoder-peft"),
            dtype="bfloat16",
            llm_dim=4096,
            device="cpu",
        )
        self._model = load_model(
            VARIANTS[self.variant]["name"], device="cuda", text_encoder=encoder
        )
        logger.info("Kimodo ready")

    def generate(self, job: Job) -> list[Output]:
        assert job.prompt is not None
        texts = sentences(job.prompt)
        if not texts:
            raise GenerationError("the description has no words")
        if job.task == "inbetween-native-motion":
            assert isinstance(job.params, InbetweenParams)
            if len(texts) != 1:
                raise InvalidInput("in-betweening takes exactly one motion description")
            return [self._inbetween(texts, job, seed) for seed in job.seeds]
        if job.task in ("keyframes-to-native-motion", "keyframes-to-soma-motion"):
            assert isinstance(job.params, KeyframesParams)
            if len(texts) != 1:
                raise InvalidInput("key frames take exactly one motion description")
            make = self._soma_keyframes if job.task.endswith("soma-motion") else self._keyframes
            return [make(texts, job, seed) for seed in job.seeds]
        assert isinstance(job.params, Params)
        constraints = None
        joints = (22,) if self.variant == "smplx" else (30, 77)
        if "constraints" in job.inputs:
            if len(texts) != 1:
                raise InvalidInput("constraints require exactly one motion description")
            constraints = read_constraints(
                job.inputs["constraints"].data,
                round(job.params.seconds * self._model.fps),
                joints=joints,
            )
        if job.task == "text-to-native-motion":
            return [self._native(texts, job.params, seed, constraints) for seed in job.seeds]
        return [self._one(texts, job.params, seed, constraints) for seed in job.seeds]

    def _run(
        self,
        texts: list[str],
        frames: list[int],
        constraints_data: list[dict] | None,
        steps: int,
        fix_feet: bool,
        seed: int,
        constraint_sets: list[Any] | None = None,
    ) -> dict:
        """Kimodo's raw output for one sample (numpy, batch dimension removed). Constraints come
        as Kimodo's JSON dicts or, ``constraint_sets``, as its constraint objects."""
        import torch
        from kimodo.constraints import load_constraints_lst
        from kimodo.tools import seed_everything  # pyright: ignore[reportMissingImports]

        # As Kimodo's own CLI: tensors follow the skeleton's device. A dtype here
        # would also turn the frame indices into floats, which post-processing
        # then fails to index with.
        constraints = (
            load_constraints_lst(constraints_data, self._model.skeleton) if constraints_data else []
        ) + list(constraint_sets or [])
        seed_everything(seed)
        try:
            output = self._model(
                texts,
                frames,
                constraint_lst=constraints,
                num_denoising_steps=steps,
                num_samples=1,
                multi_prompt=True,
                num_transition_frames=5,
                post_processing=fix_feet,
                return_numpy=True,
            )
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            torch.cuda.empty_cache()
            raise GenerationError("out of GPU memory; free GPU memory", retryable=True) from error
        return {key: value[0] for key, value in output.items()}

    def _kimodo_rest(self) -> Any:
        return self._model.skeleton.neutral_joints.detach().cpu().numpy()

    def _native(
        self, texts: list[str], params: Params, seed: int, constraints: list[dict] | None
    ) -> Output:
        import numpy as np

        frames = [round(params.seconds * self._model.fps)] * len(texts)
        out = self._run(texts, frames, constraints, params.steps, params.fix_feet, seed)
        identity = native.Frame(np.zeros(3), 0.0, 1.0)
        data = native.package(
            out["local_rot_mats"],
            out["root_positions"],
            self._kimodo_rest(),
            identity,
            "kimodo-smplx",
            {
                "model": VARIANTS[self.variant]["name"],
                "texts": texts,
                "seed": seed,
                "coordinate_frame": "kimodo canonical",
            },
        )
        meta = {
            "frames": len(out["root_positions"]),
            "fps": 30,
            "sentences": len(texts),
            "constraint_groups": len(constraints or []),
        }
        return Output(SMPLX_MOTION, data, meta)

    def _keyframes(self, texts: list[str], job: Job, seed: int) -> Output:
        """A new motion through chosen frames of a motion, their spacing kept (the studio's
        frame viewer picks them): full-body keys, the first one at frame 0."""
        params = job.params
        assert isinstance(params, KeyframesParams)
        rest = self._kimodo_rest()
        try:
            clip = native.read_clip(job.inputs["motion"].data)
        except ValueError as error:
            raise InvalidInput(str(error)) from error
        chosen, at, total = key_frames(params, clip.frames)
        frame = native.canonical_frame(clip, chosen[0], rest)
        constraints = [native.fullbody_keys(clip, chosen, frame, at)]
        out = self._run(texts, [total], constraints, params.steps, params.fix_feet, seed)
        data = native.package(
            out["local_rot_mats"],
            out["root_positions"],
            rest,
            frame,
            "kimodo-smplx-keyframes",
            {
                "model": VARIANTS[self.variant]["name"],
                "texts": texts,
                "seed": seed,
                "key_frames": [index + 1 for index in chosen],
                "key_frames_at": at,
                "coordinate_frame": "the motion input's world",
            },
        )
        result = native.read_clip(data)
        meta: dict[str, Any] = {
            "frames": total,
            "fps": 30,
            "key_frames": [index + 1 for index in chosen],
            "key_pose_error_deg": round(
                max(
                    native.pose_difference_deg(result.rotations[a][1:], clip.rotations[c][1:])
                    for a, c in zip(at, chosen, strict=True)
                ),
                2,
            ),
        }
        return Output(SMPLX_MOTION, data, meta)

    def _soma_keyframes(self, texts: list[str], job: Job, seed: int) -> Output:
        """A SOMA motion through chosen frames of a SOMA pose or motion (GEM-X): full-body keys
        of joint positions on Kimodo's body, the first one at frame 0."""
        import numpy as np
        import torch
        from kimodo.constraints import (
            FullBodyConstraintSet,
            LeftFootConstraintSet,
            LeftHandConstraintSet,
            RightFootConstraintSet,
            RightHandConstraintSet,
        )
        from kimodo.skeleton import SOMASkeleton30  # pyright: ignore[reportMissingImports]

        params = job.params
        assert isinstance(params, SomaKeyframesParams)
        body = self._model.skeleton
        skeleton = soma.Skeleton(
            list(body.bone_order_names),
            [int(parent) for parent in body.joint_parents.tolist()],
            body.neutral_joints.detach().cpu().numpy(),
        )
        try:
            clip = soma.read_clip(job.inputs["motion"].data)
        except (ValueError, KeyError, TypeError) as error:
            raise InvalidInput(str(error)) from error
        missing = [name for name in skeleton.names if name not in clip.names]
        if missing:
            raise InvalidInput(f"the motion has no SOMA joints {', '.join(missing[:4])}")
        chosen, at, total = key_frames(params, clip.frames)
        frame = soma.canonical_frame(clip, chosen[0], skeleton)
        wanted = soma.keys(clip, chosen, skeleton, frame)
        device = body.neutral_joints.device
        turns = soma.key_rotations(clip, chosen, skeleton, frame, wanted)
        tensor = lambda value: torch.tensor(value, dtype=torch.float32, device=device)  # noqa: E731
        sets = [
            # generation follows the positions; post-processing pins the rotations
            FullBodyConstraintSet(body, torch.tensor(at), tensor(wanted), tensor(turns))
        ]
        inner: list[int] = []
        if params.hold_hands or params.hold_feet:
            span = at[-1] - at[0]
            inner = sorted(
                {at[0] + round(span * (i + 1) / (params.hold_keys + 1))
                 for i in range(params.hold_keys)} - set(at)
            )  # fmt: skip
            kinds = (LeftHandConstraintSet, RightHandConstraintSet) if params.hold_hands else ()
            kinds += (LeftFootConstraintSet, RightFootConstraintSet) if params.hold_feet else ()
            held = len(inner)
            for kind in kinds:
                sets.append(
                    kind(
                        body,
                        frame_indices=torch.tensor(inner),
                        global_joints_positions=tensor(np.repeat(wanted[:1], held, axis=0)),
                        global_joints_rots=tensor(np.repeat(turns[:1], held, axis=0)),
                        smooth_root_2d=None,
                    )
                )
        out = self._run(
            texts, [total], None, params.steps, params.fix_feet, seed, constraint_sets=sets
        )
        full = body.somaskel77 if isinstance(body, SOMASkeleton30) else body
        names = list(full.bone_order_names)
        posed = out["posed_joints"]  # (T, 77, 3): the output is on the 77 joints
        subset = [names.index(name) for name in skeleton.names]
        data = soma.package(
            posed,
            names,
            [int(parent) for parent in full.joint_parents.tolist()],
            frame,
            "kimodo-soma-keyframes",
            {
                "model": MODEL_NAME,
                "texts": texts,
                "seed": seed,
                "key_frames": [index + 1 for index in chosen],
                "key_frames_at": at,
                "held_frames": inner,
                "hold_hands": params.hold_hands,
                "hold_feet": params.hold_feet,
                "key_rotations": "the input's" if clip.rotations is not None else "fitted",
                "input_sha256": hashlib.sha256(job.inputs["motion"].data).hexdigest(),
            },
            rotations=out["global_rot_mats"],
        )
        meta: dict[str, Any] = {
            "frames": total,
            "fps": 30,
            "key_frames": [index + 1 for index in chosen],
            # on Kimodo's body in its canonical frame, before scaling back
            "key_error_cm": round(100 * soma.key_error_m(posed[at][:, subset], wanted), 1),
        }
        return Output(SOMA_MOTION, data, meta)

    def _inbetween(self, texts: list[str], job: Job, seed: int) -> Output:
        import numpy as np

        params = job.params
        assert isinstance(params, InbetweenParams)
        rest = self._kimodo_rest()
        try:
            before = native.read_clip(job.inputs["motion"].data)
            after = (
                native.read_clip(job.inputs["continuation"].data)
                if "continuation" in job.inputs
                else None
            )
        except ValueError as error:
            raise InvalidInput(str(error)) from error
        total = round(params.seconds * 30)
        k = params.context_frames
        if before.frames < k or (after is not None and after.frames < k):
            raise InvalidInput(f"inputs need at least {k} frames")
        if total < 2 * k + 4:
            raise InvalidInput("too short: leave at least 4 free frames between the keys")
        last = before.frames - 1
        frame = native.canonical_frame(before, last, rest)
        tail = list(range(before.frames - k, before.frames))
        constraints = [native.fullbody_keys(before, tail, frame, list(range(k)))]
        if after is not None:
            # The continuation keeps its own shape of motion; it is placed by
            # forward_m/turn_deg relative to the motion's last frame.
            own = native.canonical_frame(after, 0, rest)
            turn = np.radians(params.turn_deg)
            placed = native.Frame(own.origin, own.yaw - turn, own.scale)
            keys = native.fullbody_keys(
                after, list(range(k)), placed, list(range(total - k, total))
            )
            keys["root_positions"] = [
                [x, y, z + params.forward_m * frame.scale] for x, y, z in keys["root_positions"]
            ]
            constraints.append(keys)
        if params.hold_hands or params.hold_feet:
            inner = sorted(
                {
                    round(k + (total - 2 * k - 1) * (i + 1) / (params.hand_keys + 1))
                    for i in range(params.hand_keys)
                }
            )
            held = native.fullbody_keys(before, [last] * len(inner), frame, inner)
            kinds = ("left-hand", "right-hand") if params.hold_hands else ()
            kinds += ("left-foot", "right-foot") if params.hold_feet else ()
            for kind in kinds:
                constraints.append({**held, "type": kind})
        out = self._run(texts, [total], constraints, params.steps, params.fix_feet, seed)
        data = native.package(
            out["local_rot_mats"],
            out["root_positions"],
            rest,
            frame,
            "kimodo-smplx-inbetween",
            {
                "model": VARIANTS[self.variant]["name"],
                "texts": texts,
                "seed": seed,
                "context_frames": k,
                "forward_m": params.forward_m,
                "turn_deg": params.turn_deg,
                "hold_hands": params.hold_hands,
                "hand_keys": params.hand_keys if params.hold_hands or params.hold_feet else 0,
                "hold_feet": params.hold_feet,
                "coordinate_frame": "the motion input's world",
                "layout": f"frames 0..{k - 1} = motion's last {k}; "
                + (
                    f"frames {total - k}..{total - 1} = continuation's first {k}"
                    if after
                    else "no continuation"
                ),
            },
        )
        result = native.read_clip(data)
        meta: dict[str, Any] = {
            "frames": total,
            "fps": 30,
            "context_frames": k,
            "start_key_pose_error_deg": round(
                max(
                    native.pose_difference_deg(result.rotations[i], before.rotations[tail[i]])
                    for i in range(k)
                ),
                2,
            ),
            "start_key_root_error_m": round(
                float(np.abs(result.pelvis[:k] - before.pelvis[tail]).max()), 4
            ),
        }
        if after is not None:
            meta["end_key_pose_error_deg"] = round(
                max(
                    native.pose_difference_deg(
                        result.rotations[total - k + i][1:], after.rotations[i][1:]
                    )
                    for i in range(k)
                ),
                2,
            )
        return Output(SMPLX_MOTION, data, meta)

    def _one(
        self,
        texts: list[str],
        params: Params,
        seed: int,
        constraints_data: list[dict] | None = None,
    ) -> Output:
        import torch
        from kimodo.exports.bvh import save_motion_bvh  # pyright: ignore[reportMissingImports]
        from kimodo.skeleton import (  # pyright: ignore[reportMissingImports]
            SOMASkeleton30,
            global_rots_to_local_rots,
        )

        model = self._model
        frames = [round(params.seconds * model.fps)] * len(texts)
        output = self._run(texts, frames, constraints_data, params.steps, params.fix_feet, seed)
        skeleton = model.skeleton
        if isinstance(skeleton, SOMASkeleton30):  # the output is already on the 77 joints
            skeleton = skeleton.somaskel77.to("cuda")
        joints = torch.from_numpy(output["posed_joints"]).to("cuda")
        rotations = torch.from_numpy(output["global_rot_mats"]).to("cuda")
        local = global_rots_to_local_rots(rotations, skeleton)
        with tempfile.TemporaryDirectory(prefix="kimodo-") as directory:
            work = Path(directory)
            bvh, fbx = work / "motion.bvh", work / "motion.fbx"
            save_motion_bvh(
                bvh,
                local,
                joints[:, skeleton.root_idx, :],
                skeleton=skeleton,
                fps=model.fps,
                standard_tpose=True,  # the rest pose is the T-pose: what Unity's avatar wants
            )
            self._to_fbx(bvh, fbx)
            meta = {
                "frames": int(joints.shape[0]),
                "fps": float(model.fps),
                "seconds": round(int(joints.shape[0]) / float(model.fps), 2),
                "skeleton": "mixamo names (SOMA 77)",
                "rest_pose": "t-pose",
                "sentences": len(texts),
                "constraint_groups": len(constraints_data or []),
                "constraint_frames": sorted(
                    {i for c in (constraints_data or []) for i in c["frame_indices"]}
                ),
                "constraints_sha256": hashlib.sha256(
                    json.dumps(constraints_data, sort_keys=True).encode()
                ).hexdigest()
                if constraints_data
                else None,
                "constraint_space": "SOMA, Y-up metres; human skeleton constraints",
            }
            return Output("model/x-fbx", fbx.read_bytes(), meta)

    def _to_fbx(self, bvh: Path, fbx: Path) -> None:
        command = [sys.executable, "-m", "kimodo_model_server.to_fbx", str(bvh), str(fbx)]
        try:
            done = subprocess.run(  # noqa: S603 -- fixed argv, paths are ours
                command, capture_output=True, text=True, timeout=FBX_TIMEOUT_S, check=False
            )
        except subprocess.TimeoutExpired as error:
            raise GenerationError(f"the FBX export took over {FBX_TIMEOUT_S} s") from error
        if done.returncode != 0 or not fbx.is_file():
            tail = "\n".join((done.stderr or done.stdout).strip().splitlines()[-5:])
            logger.error("FBX export failed (%s):\n%s", done.returncode, done.stderr)
            raise GenerationError(f"the FBX export failed: {tail}")
