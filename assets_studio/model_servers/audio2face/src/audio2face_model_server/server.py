"""Audio2Face-3D facial animation server (contract v1).

NVIDIA Audio2Face-3D turns speech into the motion of a face: the skin, the
lower jaw, the tongue and the eyes. Two kinds of network are published. v3.0
is a diffusion network trained on three actors (Claire, James, Mark), one of
whom is picked per job; it works on one-second windows and keeps state from
one window to the next. The v2.3 networks are older regression networks, one
per actor, that look at half a second of audio around each frame. Both take
ten explicit emotions (amazement, anger, ..., sadness), held here for the
whole clip, and no text: the language does not matter to them, though they
were trained on English.

The networks output vertex positions of the actor's head. NVIDIA's SDK,
built here from source with a small runner of its own (native/), does
upstream's post-processing (smoothing, strengths, eye saccades) and solves
them into the actor's 52 ARKit blendshapes and 16 tongue blendshapes; it
also gives the jaw as a rigid transform and the eyes as two rotations. The
result is a glTF of the actor's head as NVIDIA's Maya scenes (Maya-ACE) have
it, with the blendshapes as morph targets and the motion as an animation;
see glb.py. Per job the runner runs on the GPU in its own process and the
networks run as TensorRT engines, built once per GPU from the published ONNX
files (a few minutes each, the first time a model is used).

The output depends on nothing but the input: the diffusion noise comes from
a generator with a fixed seed, so a job makes one result.

Licences: networks NVIDIA Open Model License; SDK and Maya-ACE MIT.
"""

import io
import json
import logging
import os
import subprocess
import sys
import tempfile
import time
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Literal

import numpy as np
from pydantic import BaseModel, ConfigDict, Field

from audio2face_model_server import glb
from audio2face_model_server.heads import Head, read_head
from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    ui,
)

logger = logging.getLogger("audio2face_model_server")

# Git revisions and file digests, not secrets.
SOURCE_REVISION = "1ca0f02535ed774f5dbcd724a31cd486368dc783"  # pragma: allowlist secret
MAYA_ACE_REVISION = "f0dc13670952c495450dab1655c94e9056c9d7d9"  # pragma: allowlist secret
HEAD_URL = (
    "https://media.githubusercontent.com/media/NVIDIA/Maya-ACE"
    f"/{MAYA_ACE_REVISION}/sample_project/maya_geom/{{}}.ma"
)
# The scenes with the actors' heads and their SHA-256.
HEADS = {
    # pragma: allowlist nextline secret
    "claire_geom_v3": "6320bc3c07b8ab981a0f7deda3a555cc9347bfc56e1ac7461133a55a3b5046c0",
    # pragma: allowlist nextline secret
    "james_geom_v3": "f1ddb3bb719ec80f1ae4e51a6395236a35071149034b6868be481a25940fb244",
    # pragma: allowlist nextline secret
    "mark_geom_v3": "d11da6a8de60ffb26fa1ddffcce4a0764501e7c30b52ec8a97970e0440ba5373",
    # pragma: allowlist nextline secret
    "claire_geom_v2_topo1": "59fb6750f1e4ceedeeb98d5ac687ab387dbc761b6b79c5177985a5b23f7f66e9",
    # pragma: allowlist nextline secret
    "mark_geom_v2_topo1": "6c3f5725978781e3bb079cc50c4fef0e5d4f5373fb271b991ce6eb9298af02df",
}
V3_REVISION = "b74132732fd9a9d29b237bec193ded64c9745e91"  # pragma: allowlist secret

AUDIO_TO_MOTION = "audio-to-motion"
SAMPLE_RATE = 16_000  # what every network listens at
MIN_SECONDS, MAX_SECONDS = 0.25, 300.0
REGRESSION_FPS = 60  # the rate of the diffusion network, which has its own
# Engine batch sizes (max, opt). One track at a time: a diffusion network
# takes one window per track; a regression network runs many frames of the
# track at once, up to the max (upstream's defaults).
BATCH = {"diffusion": (1, 1), "regression": (128, 8)}
EMOTIONS = (
    "amazement",
    "anger",
    "cheekiness",
    "disgust",
    "fear",
    "grief",
    "joy",
    "outofbreath",
    "pain",
    "sadness",
)


@dataclass(frozen=True)
class Model:
    repository: str
    revision: str
    kind: Literal["diffusion", "regression"]
    actor: str
    identity: int | None  # diffusion: the actor's index in network_info.json's identities
    head: str  # the Maya-ACE scene with the actor's head

    @property
    def directory(self) -> Path:
        return weights_directory() / self.repository.split("/", 1)[1]

    def blendshapes(self, part: str) -> Path:
        suffix = f"_{self.actor}" if self.kind == "diffusion" else ""
        return self.directory / f"bs_{part}{suffix}.npz"


MODELS = {
    "v3.0-claire": Model(
        "nvidia/Audio2Face-3D-v3.0", V3_REVISION, "diffusion", "Claire", 0, "claire_geom_v3"
    ),
    "v3.0-james": Model(
        "nvidia/Audio2Face-3D-v3.0", V3_REVISION, "diffusion", "James", 1, "james_geom_v3"
    ),
    "v3.0-mark": Model(
        "nvidia/Audio2Face-3D-v3.0", V3_REVISION, "diffusion", "Mark", 2, "mark_geom_v3"
    ),
    "v2.3.1-claire": Model(
        "nvidia/Audio2Face-3D-v2.3.1-Claire",
        "a46eafb067adfdd7cc8f5c7941586b309004561c",  # pragma: allowlist secret
        "regression",
        "Claire",
        None,
        "claire_geom_v2_topo1",
    ),
    "v2.3.1-james": Model(
        "nvidia/Audio2Face-3D-v2.3.1-James",
        "327d000d9f76e370014a9b7467b23ea36846b680",  # pragma: allowlist secret
        "regression",
        "James",
        None,
        "james_geom_v3",
    ),
    "v2.3-mark": Model(
        "nvidia/Audio2Face-3D-v2.3-Mark",
        "5451728e07378df93b04523279e134a9993ae71b",  # pragma: allowlist secret
        "regression",
        "Mark",
        None,
        "mark_geom_v2_topo1",
    ),
}
ModelName = Literal[
    "v3.0-claire", "v3.0-james", "v3.0-mark", "v2.3.1-claire", "v2.3.1-james", "v2.3-mark"
]
DEFAULT_MODEL: ModelName = "v3.0-claire"


def _emotion(english: str, russian: str) -> Any:
    return Field(
        default=0.0,
        ge=0.0,
        le=1.0,
        description=f"{english}, 0 to 1, for the whole clip",
        json_schema_extra=ui(ru=f"{russian}, от 0 до 1, на весь ролик"),
    )


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    model: ModelName = Field(
        default=DEFAULT_MODEL,
        description=(
            "Network and actor: v3.0 is the newer diffusion network, trained on three actors;"
            " v2.3 an older regression network, one per actor"
        ),
        json_schema_extra=ui(
            ru=(
                "Сеть и актёр: v3.0 — новая диффузионная сеть, обученная на трёх актёрах;"
                " v2.3 — старая регрессионная, своя у каждого"
            ),
            primary=True,
        ),
    )
    amazement: float = _emotion("Amazement", "Изумление")
    anger: float = _emotion("Anger", "Гнев")
    cheekiness: float = _emotion("Cheekiness", "Озорство")
    disgust: float = _emotion("Disgust", "Отвращение")
    fear: float = _emotion("Fear", "Страх")
    grief: float = _emotion("Grief", "Горе")
    joy: float = _emotion("Joy", "Радость")
    outofbreath: float = _emotion("Out of breath", "Одышка")
    pain: float = _emotion("Pain", "Боль")
    sadness: float = _emotion("Sadness", "Грусть")


TASK = TaskSpec(
    AUDIO_TO_MOTION,
    Params,
    ("model/gltf-binary",),
    prompt="none",
    inputs=(
        InputSpec(
            role="audio",
            mime=["audio/wav"],
            description="Speech to animate, any language, up to 5 minutes",
            labels={"ru": "Речь для анимации, на любом языке, до 5 минут"},
        ),
    ),
    max_count=1,  # the result depends on the input only
)


def source_directory() -> Path:
    return Path(
        os.environ.get("AUDIO2FACE_SOURCE", "~/.cache/assets-studio/audio2face-source")
    ).expanduser()


def build_directory() -> Path:
    return Path(
        os.environ.get("AUDIO2FACE_BUILD", "~/.cache/assets-studio/audio2face-build")
    ).expanduser()


def weights_directory() -> Path:
    return Path(
        os.environ.get("AUDIO2FACE_WEIGHTS", "~/.cache/assets-studio/audio2face")
    ).expanduser()


def sdk_library() -> Path:
    """The SDK's shared library, as setup_native.py builds it."""
    return build_directory() / "sdk/audio2x-sdk/lib/libaudio2x.so"


def runner_path() -> Path:
    return build_directory() / "runner/a2f-runner"


def head_path(scene: str) -> Path:
    return weights_directory() / "maya-ace" / f"{scene}.ma"


def download() -> None:
    """The networks at their pinned revisions and the heads (~1.8 GB). Network only."""
    import hashlib

    from huggingface_hub import snapshot_download

    for repository, revision in sorted(
        {(model.repository, model.revision) for model in MODELS.values()}
    ):
        # A finished download leaves a marker, so that loading needs no network.
        directory = weights_directory() / repository.split("/", 1)[1]
        marker = directory / ".complete"
        if marker.is_file() and marker.read_text().strip() == revision:
            continue
        snapshot_download(repository, revision=revision, local_dir=directory)
        marker.write_text(revision + "\n")
    for scene, digest in HEADS.items():
        path = head_path(scene)
        if path.is_file() and path.stat().st_size > 0:
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        partial = path.with_name(path.name + ".part")
        url = HEAD_URL.format(scene)  # a pinned HTTPS URL
        request = urllib.request.Request(url, headers={"User-Agent": "assets-studio"})  # noqa: S310
        with (
            urllib.request.urlopen(request, timeout=60) as response,  # noqa: S310
            partial.open("wb") as file,
        ):
            while chunk := response.read(1 << 20):
                file.write(chunk)
        if hashlib.sha256(partial.read_bytes()).hexdigest() != digest:
            partial.unlink()
            raise RuntimeError(f"{scene}.ma does not have the expected content")
        partial.replace(path)


def library_path() -> str:
    """LD_LIBRARY_PATH for the runner: the SDK, and TensorRT and the CUDA
    libraries from this environment's wheels, before what is already there."""
    import nvidia  # the CUDA wheels' namespace package
    import tensorrt_libs

    directories = [sdk_library().parent, Path(tensorrt_libs.__file__).parent]
    for root in map(Path, nvidia.__path__):
        directories += [
            root / name / "lib" for name in ("cuda_runtime", "cublas", "curand", "cuda_nvrtc")
        ]
    paths = [str(directory) for directory in directories if directory.is_dir()]
    if os.environ.get("LD_LIBRARY_PATH"):
        paths.append(os.environ["LD_LIBRARY_PATH"])
    return os.pathsep.join(paths)


def read_speech(data: bytes) -> np.ndarray:
    """Mono float32 samples at 16 kHz, resampled as needed."""
    import soundfile
    import soxr

    try:
        samples, rate = soundfile.read(io.BytesIO(data), dtype="float32", always_2d=True)
    except (RuntimeError, soundfile.LibsndfileError) as error:
        raise InvalidInput(f"cannot read the audio: {error}") from error
    seconds = len(samples) / rate
    if seconds < MIN_SECONDS:
        raise InvalidInput(f"the audio is {seconds:.2f} s; give at least {MIN_SECONDS} s")
    if seconds > MAX_SECONDS:
        raise InvalidInput(
            f"the audio is {seconds:.0f} s; give at most {MAX_SECONDS / 60:.0f} minutes"
        )
    mono = samples.mean(axis=1)
    if rate != SAMPLE_RATE:
        mono = soxr.resample(mono, rate, SAMPLE_RATE, quality="VHQ")
    return np.ascontiguousarray(mono, dtype="<f4")


def _failure(stderr: str) -> str:
    lines = [line for line in stderr.strip().splitlines() if line.strip()]
    return lines[-1] if lines else "no message"


def _out_of_memory(text: str) -> bool:
    text = text.lower()
    return "out of memory" in text or "cudaerrormemoryallocation" in text or "oom" in text.split()


def ensure_engine(model: Model) -> None:
    """Builds the model's TensorRT engine unless its stamp is current (in a
    child process; minutes on the first use of a model)."""
    command = [
        sys.executable,
        "-m",
        "audio2face_model_server.engine",
        str(model.directory),
        *(str(size) for size in BATCH[model.kind]),
        model.revision,
    ]
    started = time.monotonic()
    result = subprocess.run(  # noqa: S603 -- fixed argv, no shell
        command,
        env={**os.environ, "LD_LIBRARY_PATH": library_path()},
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        if _out_of_memory(result.stderr + result.stdout):
            raise GenerationError(
                "out of GPU memory building the engine; free GPU memory and try again"
            )
        raise GenerationError(
            f"cannot build the TensorRT engine: {_failure(result.stderr or result.stdout)}"
        )
    logger.info(
        "%s: %s (%.0f s)",
        model.repository,
        result.stdout.strip().splitlines()[-1],
        time.monotonic() - started,
    )


@dataclass(frozen=True)
class Actor:
    """An actor's head and blendshapes (centimetres)."""

    head: Head
    skin_neutral: np.ndarray
    skin: dict[str, np.ndarray]
    tongue_neutral: np.ndarray
    tongue: dict[str, np.ndarray]


def _blendshapes(path: Path) -> tuple[np.ndarray, dict[str, np.ndarray]]:
    with np.load(path) as data:
        names = [
            name.decode() if isinstance(name, bytes) else str(name)
            for name in data["poseNames"].tolist()
        ]
        return data["neutral"].astype(np.float32), {
            name: data[name] for name in names if name != "neutral"
        }


def read_actor(model: Model) -> Actor:
    head = read_head(head_path(model.head), weights_directory() / "heads" / f"{model.head}.npz")
    skin_neutral, skin = _blendshapes(model.blendshapes("skin"))
    tongue_neutral, tongue = _blendshapes(model.blendshapes("tongue"))
    for name, neutral, part in (
        ("skin", skin_neutral, head.skin),
        ("tongue", tongue_neutral, head.tongue),
    ):
        # The scene's meshes are the blendshapes' topology, the vertices in the
        # same order, give or take a few tenths of a millimetre.
        if neutral.shape != part.positions.shape:
            raise RuntimeError(
                f"{model.head}: the {name} has {len(part.positions)} vertices,"
                f" the blendshapes {len(neutral)}"
            )
        offset = float(np.abs(neutral - part.positions).mean())
        if offset > 0.25:
            raise RuntimeError(
                f"{model.head}: the {name} is {offset:.2f} cm away from the blendshapes' neutral"
            )
    return Actor(head, skin_neutral, skin, tongue_neutral, tongue)


@dataclass(frozen=True)
class Solved:
    """What the runner gives: per frame weights, jaw transforms and eye rotations."""

    times: np.ndarray
    frame_rate: tuple[int, int]
    skin_names: list[str]
    tongue_names: list[str]
    skin: np.ndarray
    tongue: np.ndarray
    jaw: np.ndarray  # (N, 4, 4)
    eyes: np.ndarray  # (N, 2, 3)


def read_solved(prefix: Path) -> Solved:
    info = json.loads(prefix.with_suffix(".json").read_text())
    skin_names, tongue_names = list(info["skin_poses"]), list(info["tongue_poses"])
    widths = [len(skin_names), len(tongue_names), int(info["jaw_size"]), int(info["eyes_size"])]
    if widths[2:] != [16, 6]:
        raise GenerationError(f"unexpected jaw and eye sizes {widths[2:]}")
    frames = int(info["frames"])
    data = np.fromfile(prefix.with_suffix(".f32"), dtype="<f4")
    if data.size != frames * sum(widths):
        raise GenerationError(
            f"the runner wrote {data.size} numbers for {frames} frames of {sum(widths)}"
        )
    rows = data.reshape(frames, sum(widths))
    ends = np.cumsum(widths)
    skin, tongue, jaw, eyes = np.split(rows, ends[:-1], axis=1)
    return Solved(
        times=np.asarray(info["timestamps"], dtype=np.float64) / SAMPLE_RATE,
        frame_rate=(int(info["frame_rate"][0]), int(info["frame_rate"][1])),
        skin_names=skin_names,
        tongue_names=tongue_names,
        skin=skin,
        tongue=tongue,
        jaw=jaw.reshape(frames, 4, 4).transpose(0, 2, 1),  # written column by column
        eyes=eyes.reshape(frames, 2, 3),
    )


def run_model(model: Model, samples: np.ndarray, emotions: list[float]) -> Solved:
    with tempfile.TemporaryDirectory(prefix="audio2face-") as scratch:
        audio, prefix = Path(scratch) / "speech.f32", Path(scratch) / "result"
        samples.astype("<f4").tofile(audio)
        command = [
            str(runner_path()),
            str(model.directory / "model.json"),
            str(audio),
            str(prefix),
            "--emotion",
            ",".join(f"{value:.6g}" for value in emotions),
        ]
        if model.kind == "diffusion":
            command += ["--identity", str(model.identity)]
        else:
            command += ["--fps", str(REGRESSION_FPS)]
        seconds = len(samples) / SAMPLE_RATE
        try:
            result = subprocess.run(  # noqa: S603 -- fixed argv, no shell
                command,
                env={**os.environ, "LD_LIBRARY_PATH": library_path()},
                capture_output=True,
                text=True,
                timeout=120 + 20 * seconds,
                check=False,
            )
        except subprocess.TimeoutExpired as error:
            raise GenerationError("Audio2Face-3D took too long", retryable=True) from error
        if result.returncode != 0:
            if _out_of_memory(result.stderr):
                raise GenerationError(
                    "out of GPU memory; free GPU memory and try again", retryable=True
                )
            raise GenerationError(f"Audio2Face-3D failed: {_failure(result.stderr)}")
        return read_solved(prefix)


class Audio2FaceServer(ModelServer):
    model = ModelInfo(
        id="audio2face",
        name="Audio2Face-3D",
        revision=V3_REVISION[:12],
        license="NVIDIA Open Model License (networks); MIT (SDK, Maya-ACE heads)",
        source="https://huggingface.co/nvidia/Audio2Face-3D-v3.0",
        details={
            "kind": (
                "speech to facial motion: diffusion (v3.0, three actors)"
                " or regression (v2.3, one per actor)"
            ),
            "output": (
                "52 ARKit and 16 tongue blendshapes, jaw and eyes, on NVIDIA's sample heads (glTF)"
            ),
            "precision": "float32 TensorRT engines, built on the first use of each network",
            "runtime": f"NVIDIA's Audio2Face-3D SDK {SOURCE_REVISION[:12]}, one process per job",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._engines: set[str] = set()
        self._actors: dict[str, Actor] = {}

    def load(self) -> None:
        if not runner_path().is_file() or not sdk_library().is_file():
            raise FileNotFoundError(
                f"the Audio2Face-3D runner is not built in {build_directory()}; run `just setup`"
            )
        download()
        self._prepare(DEFAULT_MODEL)

    def _prepare(self, name: str) -> tuple[Model, Actor]:
        model = MODELS[name]
        if model.repository not in self._engines:
            ensure_engine(model)
            self._engines.add(model.repository)
        if name not in self._actors:
            self._actors[name] = read_actor(model)
        return model, self._actors[name]

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        params = job.params
        samples = read_speech(job.inputs["audio"].data)
        model, actor = self._prepare(params.model)
        emotions = [float(getattr(params, name)) for name in EMOTIONS]
        started = time.monotonic()
        solved = run_model(model, samples, emotions)
        logger.info(
            "%s: %d frames for %.1f s of speech in %.1f s",
            params.model,
            len(solved.times),
            len(samples) / SAMPLE_RATE,
            time.monotonic() - started,
        )
        missing = [name for name in solved.skin_names if name not in actor.skin]
        missing += [name for name in solved.tongue_names if name not in actor.tongue]
        if missing:
            raise GenerationError(f"blendshapes {missing} are not in the actor's data")
        data = glb.encode(
            actor.head,
            actor.skin_neutral,
            glb.Morphs(
                solved.skin_names, np.stack([actor.skin[name] for name in solved.skin_names])
            ),
            actor.tongue_neutral,
            glb.Morphs(
                solved.tongue_names, np.stack([actor.tongue[name] for name in solved.tongue_names])
            ),
            glb.Motion(solved.times, solved.skin, solved.tongue, solved.jaw, solved.eyes),
            generator=f"Assets Studio, Audio2Face-3D {params.model}",
        )
        numerator, denominator = solved.frame_rate
        meta = {
            "model": params.model,
            "network": model.kind,
            "actor": model.actor,
            "head": f"Maya-ACE {model.head}",
            "frames": len(solved.times),
            "fps": numerator / denominator,
            "duration_s": round(len(samples) / SAMPLE_RATE, 3),
            "emotions": {
                name: value for name, value in zip(EMOTIONS, emotions, strict=True) if value
            },
            "blendshapes": solved.skin_names,
            "tongue_blendshapes": solved.tongue_names,
            "animation": "speech",
            "units": "m",
            "up_axis": "Y",
            "facing": "+Z",
        }
        return [Output("model/gltf-binary", data, meta)]
