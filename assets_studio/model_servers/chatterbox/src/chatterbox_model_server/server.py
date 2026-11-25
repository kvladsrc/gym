"""Chatterbox Multilingual V3 text-to-speech server (contract v1).

Chatterbox (Resemble AI, June 2026) speaks 23 languages, Russian among them:
a 0.5B Llama-based model writes speech tokens from the text and the voice,
and S3Gen turns them into 24 kHz sound. The voice is cloned from a sample
(the optional "voice" input) or is the checkpoint's built-in one. Every file
carries Resemble AI's inaudible Perth watermark, as upstream adds it.

Russian text gets stress marks before tokenizing, as the V3 checkpoint was
trained: upstream calls russian-text-stresser and quietly skips the marks
when it is missing, so the server loads it with its Wiktionary dictionary
and refuses to start without them.

Sampling is random: each candidate seeds the generator with its own seed,
so a single request with that seed repeats it. Everything else is
upstream's: ChatterboxMultilingualTTS's calls and its sampling settings
apart from the four parameters the upstream demo offers.
"""

import copy
import gc
import hashlib
import io
import logging
import os
import tempfile
import unicodedata
import urllib.request
import zipfile
from pathlib import Path
from typing import Any, Literal

import numpy as np
import soundfile
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    KnownTask,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
    ui,
)

logger = logging.getLogger("chatterbox_model_server")

REPO = "ResembleAI/chatterbox"
REVISION = "5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18"  # pragma: allowlist secret
T3_CHECKPOINT = "t3_mtl23ls_v3.safetensors"
# What ChatterboxMultilingualTTS.from_pretrained(t3_model="v3") takes from the repository.
FILES = (
    "ve.pt",
    T3_CHECKPOINT,
    "s3gen.pt",
    "grapheme_mtl_merged_expanded_v1.json",
    "conds.pt",
    "Cangjie5_TC.json",
)
SAMPLE_RATE = 24_000  # S3GEN_SR

# russian-text-stresser's Wiktionary dictionary, a release asset of its repository.
DICTIONARY_URL = (
    "https://github.com/Vuizur/add-stress-to-epub/releases/download/v1.0.1/russian_dict.zip"
)
DICTIONARY_SHA256 = (
    "833a256704b3fe849234799f2d15780bc5b56551e6c92e2e4e46e10630f5df3d"  # pragma: allowlist secret
)
DICTIONARY_FILES = ("russian_dict.db", "simple_cases.pkl")
# The health check of upstream's V3 demo: a correct dictionary stresses this.
STRESS_CHECK = "твои слова ничего не значат."

Language = Literal[
    "ru", "en", "ar", "da", "de", "el", "es", "fi", "fr", "he", "hi", "it", "ja", "ko", "ms",
    "nl", "no", "pl", "pt", "sv", "sw", "tr", "zh",
]  # fmt: skip

MAX_TEXT_LENGTH = 500
MIN_SAMPLE_SECONDS = 1.0
MAX_SAMPLE_SECONDS = 300.0  # upstream uses the first 10 s
_SILENCE_RMS = 1e-4


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    language: Language = Field(
        default="ru", description="Language", json_schema_extra=ui(ru="Язык", primary=True)
    )
    exaggeration: float = Field(
        default=0.5,
        ge=0.25,
        le=2.0,
        description="Expressiveness: 0.5 is neutral, higher is more emotional and faster",
        json_schema_extra=ui(
            ru="Выразительность: 0,5 — нейтрально, выше — эмоциональнее и быстрее",
            primary=True,
        ),
    )
    cfg_weight: float = Field(
        default=0.5,
        ge=0.0,
        le=1.0,
        description=(
            "Pace and guidance: lower is slower and more deliberate;"
            " 0 when the sample's language differs from the text's"
        ),
        json_schema_extra=ui(
            ru="Темп и следование: ниже — медленнее и размереннее; 0, если образец на другом языке"
        ),
    )
    temperature: float = Field(
        default=0.8,
        ge=0.05,
        le=5.0,
        description="Variability",
        json_schema_extra=ui(ru="Вариативность"),
    )


TASK = TaskSpec(
    KnownTask.TEXT_TO_SPEECH,
    Params,
    ("audio/wav",),
    inputs=(
        InputSpec(
            role="voice",
            mime=["audio/wav"],
            required=False,
            description=(
                "Voice sample: clean speech in the language of the text;"
                " the first 10 s are used. Without it, the built-in voice speaks"
            ),
            labels={
                "ru": "Образец голоса: чистая речь на языке текста; учитываются первые 10 с."
                " Без него говорит встроенный голос"
            },
        ),
    ),
)


def cache_root() -> Path:
    xdg = os.environ.get("XDG_CACHE_HOME") or str(Path.home() / ".cache")
    return Path(xdg) / "assets-studio"


def dictionary_dir() -> Path:
    configured = os.environ.get("CHATTERBOX_RUSSIAN_DICTIONARY")
    return Path(configured) if configured else cache_root() / "chatterbox-russian-dictionary"


def dictionary_ready(directory: Path) -> bool:
    return all((directory / name).is_file() for name in DICTIONARY_FILES)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as file:
        for block in iter(lambda: file.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def download_dictionary(directory: Path) -> Path:
    """The stresser's dictionary (~360 MB, 1.5 GB unpacked), downloaded and checked once."""
    if dictionary_ready(directory):
        return directory
    directory.mkdir(parents=True, exist_ok=True)
    archive = directory / "russian_dict.zip.part"
    # An archive left by an interrupted unpacking is reused when it is whole.
    if not archive.is_file() or sha256(archive) != DICTIONARY_SHA256:
        logger.info("downloading the Russian stress dictionary (~360 MB)")
        urllib.request.urlretrieve(DICTIONARY_URL, archive)
        if (digest := sha256(archive)) != DICTIONARY_SHA256:
            archive.unlink()
            raise RuntimeError(
                f"the Russian stress dictionary's checksum is {digest},"
                f" expected {DICTIONARY_SHA256}"
            )
    with zipfile.ZipFile(archive) as zipped:
        for name in DICTIONARY_FILES:
            target = directory / name
            with zipped.open(name) as source, open(f"{target}.part", "wb") as sink:
                while block := source.read(1 << 20):
                    sink.write(block)
            os.replace(f"{target}.part", target)
    archive.unlink()
    return directory


def stresser_class() -> Any:
    """RussianTextStresser without its optional LLM disambiguation, whose module
    creates a debug database, llmdebug.db, in the working directory on import."""
    import sys

    # A None entry makes the stresser's guarded import fail, as without the module.
    sys.modules.setdefault("russian_text_stresser.gpt3_WSD", None)
    from russian_text_stresser.text_stresser import RussianTextStresser

    return RussianTextStresser


def make_stresser(directory: Path) -> Any:
    """russian-text-stresser on the given dictionary, checked as upstream's demo does."""
    RussianTextStresser = stresser_class()
    if not dictionary_ready(directory):
        raise RuntimeError(f"the Russian stress dictionary is missing in {directory}")
    # Absolute paths replace the stresser's own directory (pathlib joins them so).
    stresser = RussianTextStresser(
        db_file=str(directory / "russian_dict.db"),
        simple_cases_file=str(directory / "simple_cases.pkl"),
    )
    stressed = stresser.stress_text(unicodedata.normalize("NFKD", STRESS_CHECK))
    if "́" not in stressed:
        raise RuntimeError(f"Russian stress marking does not work: {stressed!r}")
    return stresser


def cached() -> str | None:
    """The checkpoint's directory in the Hugging Face cache when every file of
    it is there, else None."""
    from huggingface_hub import try_to_load_from_cache

    paths = [try_to_load_from_cache(REPO, name, revision=REVISION) for name in FILES]
    if not all(isinstance(path, str) for path in paths):
        return None
    return os.path.dirname(paths[0])


def weights() -> str:
    """The checkpoint's directory: a complete one from the cache without the
    network, else downloaded at the pinned revision."""
    from huggingface_hub import snapshot_download

    return cached() or snapshot_download(REPO, revision=REVISION, allow_patterns=list(FILES))


def encode_wav(samples: np.ndarray, rate: int = SAMPLE_RATE) -> bytes:
    buffer = io.BytesIO()
    soundfile.write(buffer, samples, rate, format="WAV", subtype="PCM_16")
    return buffer.getvalue()


def check_sample(data: bytes) -> bytes:
    """A voice sample must decode completely, last 1 s to 5 min and not be silent."""
    try:
        info = soundfile.info(io.BytesIO(data))
        if info.duration > MAX_SAMPLE_SECONDS:
            raise InvalidInput(
                f"the voice sample is {info.duration:.0f} s; use at most {MAX_SAMPLE_SECONDS:.0f} s"
            )
        # Decode everything: a header can promise more audio than the file holds.
        samples, rate = soundfile.read(io.BytesIO(data), dtype="float32", always_2d=True)
    except (RuntimeError, soundfile.LibsndfileError) as error:
        raise InvalidInput(f"cannot read the voice sample: {error}") from error
    seconds = len(samples) / rate
    if seconds < MIN_SAMPLE_SECONDS:
        raise InvalidInput(
            f"the voice sample is {seconds:.1f} s; give at least a few seconds of speech"
        )
    if float(np.sqrt(np.mean(np.square(samples)))) < _SILENCE_RMS:
        raise InvalidInput("the voice sample is silent")
    return data


class ChatterboxServer(ModelServer):
    model = ModelInfo(
        id="chatterbox-multilingual-v3",
        name="Chatterbox Multilingual V3",
        revision=REVISION[:12],
        license="MIT; outputs carry Resemble AI's Perth watermark",
        source=f"https://huggingface.co/{REPO}",
        details={
            "parameters": "0.5 B",
            "languages": "23, Russian with stress marks (russian-text-stresser, GPL-3.0)",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._model: Any = None
        self._built_in: Any = None  # the checkpoint's own voice (conds.pt)

    def load(self) -> None:
        import torch
        from chatterbox.models.tokenizers import tokenizer as upstream_tokenizer
        from chatterbox.mtl_tts import ChatterboxMultilingualTTS

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("Chatterbox needs a CUDA GPU")
        # Upstream's add_russian_stress() uses this module-level instance when it is set.
        upstream_tokenizer._russian_stresser = make_stresser(dictionary_dir())
        self._model = ChatterboxMultilingualTTS.from_local(weights(), "cuda", t3_model="v3")
        self._built_in = copy.deepcopy(self._model.conds)

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, Params)
        text = (job.prompt or "").strip()
        if not text:
            raise InvalidInput("give the text to speak")
        if len(text) > MAX_TEXT_LENGTH:
            raise InvalidInput(
                f"the text is {len(text)} characters; the limit is {MAX_TEXT_LENGTH}:"
                " the model speaks at most about 40 s at a time, split the text"
            )
        out_of_memory: str | None = None
        try:
            with tempfile.TemporaryDirectory() as directory:
                sample = job.inputs.get("voice")
                voice: str | None = None
                if sample is not None:
                    voice = str(Path(directory) / "voice.wav")
                    Path(voice).write_bytes(check_sample(sample.data))
                return [self._say(text, job.params, voice, seed) for seed in job.seeds]
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            # Keep only the text: the traceback would pin the failed call's tensors.
            out_of_memory = str(error)
            logger.warning("out of GPU memory: %s", out_of_memory)
        gc.collect()
        torch.cuda.empty_cache()
        raise GenerationError("out of GPU memory; free GPU memory and retry", retryable=True)

    def _say(self, text: str, params: Params, voice: str | None, seed: int) -> Output:
        import torch

        torch.manual_seed(seed)  # seeds the CPU and every CUDA device
        # Upstream keeps the last voice as the model's state: a sample is
        # conditioned anew with every call, the built-in voice is put back.
        if voice is None:
            self._model.conds = copy.deepcopy(self._built_in)
        wave = self._model.generate(
            text,
            language_id=params.language,
            audio_prompt_path=voice,
            exaggeration=params.exaggeration,
            cfg_weight=params.cfg_weight,
            temperature=params.temperature,
        )
        samples = wave.squeeze(0).numpy().astype(np.float32)
        if not samples.size:
            raise GenerationError("the model produced no sound", retryable=True)
        return Output(
            "audio/wav",
            encode_wav(samples),
            {
                "seconds": round(len(samples) / SAMPLE_RATE, 2),
                "language": params.language,
                "voice": "sample" if voice else "built-in",
            },
        )
