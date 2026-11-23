"""Qwen3-TTS text-to-speech server (contract v1).

Qwen3-TTS (Alibaba Qwen, 2026) speaks ten languages, Russian among them: a
1.7B language model writes speech as codes of Qwen's 12 Hz speech tokenizer,
which turns them into sound. Three checkpoints are published, and each runs
in its own server process (``--model``):

- ``design``: a voice made from a description in words, which also steers
  the manner, the mood and the pace (VoiceDesign);
- ``voice``: one of nine built-in voices, steered by an optional instruction
  (CustomVoice);
- ``clone``: the voice of a sample, a few seconds of clean speech, best with
  its transcript (Base).

Sampling is random: each candidate seeds the generator with its own seed, so
a single request with that seed repeats it. Everything else is upstream's:
the qwen-tts package's calls, the checkpoint's sampling settings (only the
temperature is a parameter), bfloat16 weights. Attention runs through
PyTorch's scaled-dot-product kernels rather than FlashAttention 2, which
upstream recommends for less memory but would need building from source.
"""

import gc
import io
import logging
import os
from dataclasses import dataclass
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

logger = logging.getLogger("qwen3_tts_model_server")


@dataclass(frozen=True)
class Variant:
    """One published checkpoint and how the server offers it."""

    repository: str
    revision: str
    kind: str  # upstream's tts_model_type
    name: str


VARIANTS = {
    "design": Variant(
        "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign",
        "5ecdb67327fd37bb2e042aab12ff7391903235d3",  # pragma: allowlist secret
        "voice_design",
        "Qwen3-TTS 1.7B VoiceDesign",
    ),
    "voice": Variant(
        "Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice",
        "0c0e3051f131929182e2c023b9537f8b1c68adfe",  # pragma: allowlist secret
        "custom_voice",
        "Qwen3-TTS 1.7B CustomVoice",
    ),
    "clone": Variant(
        "Qwen/Qwen3-TTS-12Hz-1.7B-Base",
        "fd4b254389122332181a7c3db7f27e918eec64e3",  # pragma: allowlist secret
        "base",
        "Qwen3-TTS 1.7B Base",
    ),
}
DEFAULT_VARIANT = "design"
# What each checkpoint consists of; the three repositories hold the same files.
FILES = (
    "config.json",
    "generation_config.json",
    "merges.txt",
    "model.safetensors",
    "preprocessor_config.json",
    "tokenizer_config.json",
    "vocab.json",
    "speech_tokenizer/config.json",
    "speech_tokenizer/configuration.json",
    "speech_tokenizer/model.safetensors",
    "speech_tokenizer/preprocessor_config.json",
)

Language = Literal["ru", "en", "de", "fr", "es", "it", "pt", "ja", "ko", "zh"]
# The checkpoints' own names of the languages (talker_config.codec_language_id).
LANGUAGES: dict[str, str] = {
    "ru": "russian",
    "en": "english",
    "de": "german",
    "fr": "french",
    "es": "spanish",
    "it": "italian",
    "pt": "portuguese",
    "ja": "japanese",
    "ko": "korean",
    "zh": "chinese",
}
# The CustomVoice checkpoint's voices (talker_config.spk_id) and the native
# language upstream names for each; every voice speaks every language.
Speaker = Literal[
    "serena", "vivian", "ono_anna", "sohee", "uncle_fu", "ryan", "aiden", "dylan", "eric"
]  # fmt: skip

MAX_TEXT_LENGTH = 5000
MAX_DESCRIPTION_LENGTH = 1000
MIN_SAMPLE_SECONDS = 1.0
MAX_SAMPLE_SECONDS = 30.0
_SILENCE_RMS = 1e-4


def _language() -> Any:
    return Field(
        default="ru", description="Language", json_schema_extra=ui(ru="Язык", primary=True)
    )


def _temperature() -> Any:
    return Field(
        default=0.9,  # the checkpoints' generate_config.json
        ge=0.1,
        le=1.5,
        description="Variability",
        json_schema_extra=ui(ru="Вариативность"),
    )


class DesignParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    voice_description: str = Field(
        default="",
        max_length=MAX_DESCRIPTION_LENGTH,
        description=(
            "Who speaks and how, in words: sex, age, timbre, mood, pace, manner;"
            " empty leaves it to the model"
        ),
        json_schema_extra=ui(
            ru=(
                "Кто и как говорит, словами: пол, возраст, тембр, настроение, темп, манера;"
                " пусто — на усмотрение модели"
            ),
            primary=True,
        ),
    )
    language: Language = _language()
    temperature: float = _temperature()


class VoiceParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    speaker: Speaker = Field(
        default="serena",
        description=(
            "Voice: serena, vivian (young women), ono_anna (Japanese woman), sohee (Korean"
            " woman), uncle_fu (older man), ryan, aiden (English-speaking men), dylan, eric"
            " (young men with Beijing and Sichuan Chinese)"
        ),
        json_schema_extra=ui(
            ru=(
                "Голос: serena, vivian (молодые женщины), ono_anna (японка), sohee (кореянка),"
                " uncle_fu (пожилой мужчина), ryan, aiden (англоязычные мужчины), dylan, eric"
                " (молодые мужчины с пекинским и сычуаньским китайским)"
            ),
            primary=True,
        ),
    )
    instruction: str = Field(
        default="",
        max_length=MAX_DESCRIPTION_LENGTH,
        description="How to speak: emotion, pace, manner (optional)",
        json_schema_extra=ui(ru="Как говорить: эмоция, темп, манера (необязательно)"),
    )
    language: Language = _language()
    temperature: float = _temperature()


class CloneParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    voice_text: str = Field(
        default="",
        max_length=MAX_TEXT_LENGTH,
        description=(
            "What the voice sample says, word for word; without it only the timbre is"
            " taken, less faithfully"
        ),
        json_schema_extra=ui(
            ru=(
                "Что сказано в образце голоса, дословно;"
                " без этого берётся только тембр, менее точно"
            ),
            primary=True,
        ),
    )
    language: Language = _language()
    temperature: float = _temperature()


VOICE_SAMPLE = InputSpec(
    role="voice",
    mime=["audio/wav"],
    description=(
        f"Voice sample: {MIN_SAMPLE_SECONDS:.0f} to {MAX_SAMPLE_SECONDS:.0f} s of clean speech"
    ),
    labels={
        "ru": f"Образец голоса: {MIN_SAMPLE_SECONDS:.0f}–{MAX_SAMPLE_SECONDS:.0f} с чистой речи"
    },
)
TASKS = {
    "design": TaskSpec(KnownTask.TEXT_TO_SPEECH, DesignParams, ("audio/wav",)),
    "voice": TaskSpec(KnownTask.TEXT_TO_SPEECH, VoiceParams, ("audio/wav",)),
    "clone": TaskSpec(
        KnownTask.TEXT_TO_SPEECH, CloneParams, ("audio/wav",), inputs=(VOICE_SAMPLE,)
    ),
}


def encode_wav(samples: np.ndarray, rate: int) -> bytes:
    buffer = io.BytesIO()
    soundfile.write(buffer, samples, rate, format="WAV", subtype="PCM_16")
    return buffer.getvalue()


def read_sample(data: bytes) -> tuple[np.ndarray, int]:
    """A voice sample as mono float32 and its rate: it must decode completely,
    last 1 to 30 s and not be silent."""
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
    mono = samples.mean(axis=1)  # upstream averages channels too, but fails on them
    if float(np.sqrt(np.mean(np.square(mono)))) < _SILENCE_RMS:
        raise InvalidInput("the voice sample is silent")
    return np.ascontiguousarray(mono, dtype=np.float32), int(rate)


def cached(variant: Variant) -> str | None:
    """The checkpoint's directory in the Hugging Face cache when every file of
    it is there, else None (a snapshot directory alone proves nothing: a
    single downloaded file creates it)."""
    from huggingface_hub import try_to_load_from_cache

    paths = [
        try_to_load_from_cache(variant.repository, name, revision=variant.revision)
        for name in FILES
    ]
    if not all(isinstance(path, str) for path in paths):
        return None
    return os.path.dirname(paths[0])


def weights(variant: Variant) -> str:
    """The checkpoint's directory: a complete one from the cache without the
    network, else downloaded at the pinned revision."""
    from huggingface_hub import snapshot_download

    return cached(variant) or snapshot_download(
        variant.repository, revision=variant.revision, allow_patterns=list(FILES)
    )


class Qwen3TtsServer(ModelServer):
    def __init__(self, variant: str = DEFAULT_VARIANT) -> None:
        self.variant_name = variant
        self.variant = VARIANTS[variant]
        self.model = ModelInfo(
            id=f"qwen3-tts-{variant}",
            name=self.variant.name,
            revision=self.variant.revision[:12],
            license="Apache-2.0",
            source=f"https://huggingface.co/{self.variant.repository}",
            details={
                "parameters": "1.7 B",
                "precision": "bfloat16",
                "attention": "PyTorch scaled-dot-product",
                "languages": "Russian, English, German, French, Spanish, Italian, Portuguese,"
                " Japanese, Korean, Chinese",
            },
        )
        self.tasks = (TASKS[variant],)
        self._model: Any = None

    def load(self) -> None:
        import torch
        from qwen_tts import Qwen3TTSModel

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("Qwen3-TTS needs a CUDA GPU")
        model = Qwen3TTSModel.from_pretrained(
            weights(self.variant),
            device_map="cuda:0",
            dtype=torch.bfloat16,
            attn_implementation="sdpa",
        )
        if model.model.tts_model_type != self.variant.kind:
            raise RuntimeError(
                f"{self.variant.repository} is a {model.model.tts_model_type} model,"
                f" not {self.variant.kind}"
            )
        self._model = model

    def generate(self, job: Job) -> list[Output]:
        import torch

        text = (job.prompt or "").strip()
        if not text:
            raise InvalidInput("give the text to speak")
        if len(text) > MAX_TEXT_LENGTH:
            raise InvalidInput(
                f"the text is {len(text)} characters; the limit is {MAX_TEXT_LENGTH}"
            )
        out_of_memory: str | None = None
        try:
            prompt = self._voice_prompt(job)
            return [self._say(job, text, prompt, seed) for seed in job.seeds]
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            # Keep only the text: the traceback would pin the failed call's tensors.
            out_of_memory = str(error)
            logger.warning("out of GPU memory: %s", out_of_memory)
        gc.collect()
        torch.cuda.empty_cache()
        raise GenerationError("out of GPU memory; free GPU memory and retry", retryable=True)

    def _voice_prompt(self, job: Job) -> Any:
        """The cloned voice, worked out once for all the job's candidates."""
        if not isinstance(job.params, CloneParams):
            return None
        sample = read_sample(job.inputs["voice"].data)
        transcript = job.params.voice_text.strip()
        return self._model.create_voice_clone_prompt(
            ref_audio=sample,
            ref_text=transcript or None,
            # Without a transcript upstream can only take the speaker embedding.
            x_vector_only_mode=not transcript,
        )

    def _say(self, job: Job, text: str, prompt: Any, seed: int) -> Output:
        import torch

        params = job.params
        language = LANGUAGES[params.language]
        torch.manual_seed(seed)  # seeds the CPU and every CUDA device
        meta: dict[str, Any] = {"language": params.language, "model": self.variant_name}
        if isinstance(params, DesignParams):
            waves, rate = self._model.generate_voice_design(
                text=text,
                instruct=params.voice_description.strip(),
                language=language,
                temperature=params.temperature,
            )
        elif isinstance(params, VoiceParams):
            waves, rate = self._model.generate_custom_voice(
                text=text,
                speaker=params.speaker,
                language=language,
                instruct=params.instruction.strip() or None,
                temperature=params.temperature,
            )
            meta["speaker"] = params.speaker
        else:
            assert isinstance(params, CloneParams)
            waves, rate = self._model.generate_voice_clone(
                text=text,
                language=language,
                voice_clone_prompt=prompt,
                temperature=params.temperature,
            )
            meta["clone"] = (
                "speaker embedding only"
                if not params.voice_text.strip()
                else "sample and transcript"
            )
        samples = np.asarray(waves[0], dtype=np.float32).reshape(-1)
        if not samples.size:
            raise GenerationError("the model produced no sound", retryable=True)
        meta["seconds"] = round(len(samples) / rate, 2)
        return Output("audio/wav", encode_wav(samples, int(rate)), meta)
