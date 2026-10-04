"""Deterministic, GPU-free model server for testing the studio.

Declares every known task. Each output is a pure function of the request
(task, prompt, inputs, parameters and seed), so tests can compare bytes.
Every task accepts ``delay_s`` and ``fail`` to simulate slow models and
errors.
"""

import hashlib
import json
import struct
import time
from collections.abc import Sequence
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    PRIMARY,
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    KnownTask,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    media,
    ui,
)

Failure = Literal["none", "generation", "retryable", "invalid_input", "internal"]


class Controls(BaseModel):
    model_config = ConfigDict(extra="forbid")

    delay_s: float = Field(
        default=0.0,
        ge=0,
        le=60,
        description="Artificial delay, s",
        json_schema_extra=ui(ru="Искусственная задержка, с"),
    )
    fail: Failure = Field(
        default="none", description="Simulated failure", json_schema_extra=ui(ru="Имитация ошибки")
    )


class ImageParams(Controls):
    width: int = Field(default=256, ge=16, le=1024)
    height: int = Field(default=256, ge=16, le=1024)


class ImageToImageParams(ImageParams):
    strength: float = Field(
        default=0.5,
        ge=0,
        le=1,
        description="Change strength",
        json_schema_extra=ui(ru="Сила изменения", primary=True),
    )


class MeshParams(Controls):
    scale: float = Field(default=0.5, gt=0, le=10)


class RigParams(Controls):
    pass


class PaintParams(Controls):
    pass


class SpeechParams(Controls):
    voice: Literal["alpha", "beta"] = "alpha"
    speed: float = Field(default=1.0, ge=0.5, le=2.0)


class AudioParams(Controls):
    duration_s: float = Field(default=1.0, ge=0.1, le=10, json_schema_extra=PRIMARY)


class AudioToAudioParams(AudioParams):
    strength: float = Field(default=0.5, ge=0, le=1)


class SongParams(Controls):
    style: str = Field(
        default="folk",
        min_length=1,
        description="Style",
        json_schema_extra=ui(ru="Стиль", primary=True),
    )


class TextParams(Controls):
    words: int = Field(
        default=12, ge=1, le=200, description="Words", json_schema_extra=ui(ru="Слов", primary=True)
    )


class VideoParams(Controls):
    motion: float = Field(
        default=0.5, ge=0, le=1, description="Motion", json_schema_extra=ui(ru="Движение")
    )


_IMAGE_INPUT = InputSpec(role="image", mime=["image/*"])
_AUDIO_INPUT = InputSpec(role="audio", mime=["audio/*"])

ALL_TASKS: tuple[TaskSpec, ...] = (
    TaskSpec(KnownTask.TEXT_TO_IMAGE, ImageParams, ("image/png",)),
    TaskSpec(
        KnownTask.IMAGE_TO_IMAGE,
        ImageToImageParams,
        ("image/png",),
        prompt="optional",
        inputs=(_IMAGE_INPUT,),
    ),
    TaskSpec(
        KnownTask.IMAGE_TO_3D,
        MeshParams,
        ("model/gltf-binary",),
        prompt="none",
        inputs=(_IMAGE_INPUT,),
    ),
    TaskSpec(KnownTask.TEXT_TO_SPEECH, SpeechParams, ("audio/wav",)),
    TaskSpec(KnownTask.TEXT_TO_AUDIO, AudioParams, ("audio/wav",)),
    TaskSpec(
        KnownTask.AUDIO_TO_AUDIO,
        AudioToAudioParams,
        ("audio/wav",),
        prompt="optional",
        inputs=(_AUDIO_INPUT,),
    ),
    TaskSpec(
        KnownTask.TEXT_TO_TEXT,
        TextParams,
        ("text/plain",),
        inputs=(InputSpec(role="text", mime=["text/plain"], required=False),),
    ),
    TaskSpec(
        KnownTask.IMAGE_TO_VIDEO,
        VideoParams,
        ("video/mp4",),
        prompt="optional",
        inputs=(_IMAGE_INPUT,),
    ),
    TaskSpec(KnownTask.TEXT_TO_3D, MeshParams, ("model/gltf-binary",)),
    TaskSpec(KnownTask.TEXT_TO_SONG, SongParams, ("audio/wav",)),
    TaskSpec(
        KnownTask.RIG_3D,
        RigParams,
        ("model/x-fbx",),
        prompt="none",
        inputs=(InputSpec(role="mesh", mime=["model/gltf-binary"]),),
    ),
    TaskSpec(
        KnownTask.PAINT_3D,
        PaintParams,
        ("model/gltf-binary",),
        prompt="none",
        inputs=(InputSpec(role="mesh", mime=["model/gltf-binary"]), _IMAGE_INPUT),
    ),
)

_WORDS = (
    "мох", "камень", "свет", "тень", "ветер", "вода", "лист", "корень", "искра",
    "туман", "песок", "звезда", "дым", "лёд", "пламя", "эхо", "шёпот", "тропа",
)  # fmt: skip


class FakeModelServer(ModelServer):
    def __init__(
        self,
        *,
        model_id: str = "fake",
        tasks: Sequence[str] | None = None,
        load_delay_s: float = 0.0,
        load_error: str | None = None,
    ) -> None:
        self.model = ModelInfo(
            id=model_id,
            name=f"Fake model ({model_id})",
            revision="1",
            license="MIT",
            source="assets_studio/model_servers/fake",
        )
        known = {spec.task: spec for spec in ALL_TASKS}
        selected = list(known) if tasks is None else list(tasks)
        unknown = [task for task in selected if task not in known]
        if unknown:
            raise ValueError(f"unknown tasks: {', '.join(unknown)}")
        self.tasks = [known[task] for task in selected]
        self._load_delay_s = load_delay_s
        self._load_error = load_error

    def load(self) -> None:
        time.sleep(self._load_delay_s)
        if self._load_error is not None:
            raise RuntimeError(self._load_error)

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Controls)
        time.sleep(job.params.delay_s)
        _simulate_failure(job.params.fail)
        return [self._render(job, _digest(job, seed)) for seed in job.seeds]

    def _render(self, job: Job, digest: bytes) -> Output:
        params = job.params
        meta = {"digest": digest.hex()[:16]}
        if isinstance(params, ImageParams):
            red, green, blue = digest[0], digest[1], digest[2]

            def pixel(x: int, y: int) -> tuple[int, int, int]:
                return (red, (green + x) % 256, (blue + y) % 256)

            return Output("image/png", media.encode_png(params.width, params.height, pixel), meta)
        if isinstance(params, MeshParams):
            color = (digest[0] / 255, digest[1] / 255, digest[2] / 255, 1.0)
            points, triangles = media.tetrahedron(params.scale)
            return Output("model/gltf-binary", media.encode_glb(points, triangles, color), meta)
        if isinstance(params, SpeechParams):
            base = 180 if params.voice == "alpha" else 260
            length = 0.3 + 0.02 * len(job.prompt or "") / params.speed
            return Output("audio/wav", media.encode_wav(media.tone(base + digest[0], length)), meta)
        if isinstance(params, AudioParams):
            frequency = 200 + 4 * digest[0]
            return Output(
                "audio/wav", media.encode_wav(media.tone(frequency, params.duration_s)), meta
            )
        if isinstance(params, SongParams):
            # A line of the lyrics takes a quarter of a second.
            length = 0.5 + 0.25 * len((job.prompt or "").splitlines())
            return Output("audio/wav", media.encode_wav(media.tone(300 + digest[0], length)), meta)
        if isinstance(params, TextParams):
            words = [_WORDS[digest[i % len(digest)] % len(_WORDS)] for i in range(params.words)]
            source = job.inputs.get("text")
            prefix = source.data.decode().strip() + "\n" if source is not None else ""
            return Output("text/plain", (prefix + " ".join(words) + "\n").encode(), meta)
        if isinstance(params, PaintParams):
            color = (digest[0] / 255, digest[1] / 255, digest[2] / 255, 1.0)
            points, triangles = media.tetrahedron(0.5)
            return Output("model/gltf-binary", media.encode_glb(points, triangles, color), meta)
        if isinstance(params, RigParams):
            # The FBX header and the digest: enough for signature checks.
            return Output("model/x-fbx", media.FBX_MAGIC + b"\x34\x1d\x00\x00" + digest, meta)
        if isinstance(params, VideoParams):
            # The sample clip plus a "free" box with the digest: players skip it.
            box = struct.pack(">I4s", 8 + len(digest), b"free") + digest
            return Output("video/mp4", media.SAMPLE_MP4 + box, meta)
        raise AssertionError(f"no renderer for {type(params).__name__}")


def _simulate_failure(fail: Failure) -> None:
    match fail:
        case "none":
            return
        case "generation":
            raise GenerationError("simulated generation failure")
        case "retryable":
            raise GenerationError("simulated transient failure", retryable=True)
        case "invalid_input":
            raise InvalidInput("simulated rejection of the input")
        case "internal":
            raise RuntimeError("simulated crash")


def _digest(job: Job, seed: int) -> bytes:
    """Stable fingerprint of everything that determines one output."""
    description = {
        "task": job.task,
        "prompt": job.prompt,
        "inputs": {
            role: [item.mime, hashlib.sha256(item.data).hexdigest()]
            for role, item in sorted(job.inputs.items())
        },
        "params": job.params.model_dump(mode="json", exclude={"delay_s", "fail"}),
        "seed": seed,
    }
    return hashlib.sha256(json.dumps(description, sort_keys=True).encode()).digest()
