"""XTTS-v2 text-to-speech server (contract v1).

Speaks 14 languages, Russian by default, in one of the 58 built-in voices or
in a voice cloned from a short sample (the optional "voice" input, a few
seconds of clean speech). Sampling is random: each candidate seeds the
generator with its own seed, so a single request with that seed repeats it.
"""

import gc
import io
import logging
import tempfile
from pathlib import Path
from typing import Any, Literal

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

logger = logging.getLogger("xtts_model_server")

REPO = "coqui/XTTS-v2"
REVISION = "6c2b0d75eae4b7047358e3b6bd9325f857d43f77"  # pragma: allowlist secret
FILES = ["config.json", "model.pth", "vocab.json", "speakers_xtts.pth", "mel_stats.pth", "dvae.pth"]
SAMPLE_RATE = 24_000

# Left out: Chinese and Japanese (their text front-ends need pypinyin, cutlet
# and fugashi, not installed) and Hindi (coqui cannot expand digits in Hindi).
Language = Literal[
    "ru", "en", "pl", "de", "fr", "es", "it", "pt", "tr", "nl", "cs", "ar", "hu", "ko"
]
# The voices shipped in speakers_xtts.pth at REVISION.
Speaker = Literal[
    "Claribel Dervla", "Daisy Studious", "Gracie Wise", "Tammie Ema", "Alison Dietlinde",
    "Ana Florence", "Annmarie Nele", "Asya Anara", "Brenda Stern", "Gitta Nikolina",
    "Henriette Usha", "Sofia Hellen", "Tammy Grit", "Tanja Adelina", "Vjollca Johnnie",
    "Andrew Chipper", "Badr Odhiambo", "Dionisio Schuyler", "Royston Min", "Viktor Eka",
    "Abrahan Mack", "Adde Michal", "Baldur Sanjin", "Craig Gutsy", "Damien Black",
    "Gilberto Mathias", "Ilkin Urbano", "Kazuhiko Atallah", "Ludvig Milivoj", "Suad Qasim",
    "Torcull Diarmuid", "Viktor Menelaos", "Zacharie Aimilios", "Nova Hogarth", "Maja Ruoho",
    "Uta Obando", "Lidiya Szekeres", "Chandra MacFarland", "Szofi Granger", "Camilla Holmström",
    "Lilya Stainthorpe", "Zofija Kendrick", "Narelle Moon", "Barbora MacLean", "Alexandra Hisakawa",
    "Alma María", "Rosemary Okafor", "Ige Behringer", "Filip Traverse", "Damjan Chapman",
    "Wulf Carlevaro", "Aaron Dreschner", "Kumar Dahl", "Eugenio Mataracı", "Ferran Simen",
    "Xavier Hayasaka", "Luis Moray", "Marcos Rudaski",
]  # fmt: skip


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    language: Language = Field(
        default="ru", description="Language", json_schema_extra=ui(ru="Язык", primary=True)
    )
    speaker: Speaker = Field(
        default="Claribel Dervla",
        description="Voice (when no voice sample is given)",
        json_schema_extra=ui(ru="Голос (если не задан образец голоса)", primary=True),
    )
    temperature: float = Field(
        default=0.75,
        ge=0.1,
        le=1.0,
        description="Variability",
        json_schema_extra=ui(ru="Вариативность"),
    )
    speed: float = Field(
        default=1.0, ge=0.5, le=2.0, description="Speed", json_schema_extra=ui(ru="Скорость")
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
            description="Voice sample: at least 3 s of clean speech; the first 30 s are used",
            labels={"ru": "Образец голоса: не меньше 3 с чистой речи, учитываются первые 30 с"},
        ),
    ),
)


class XttsServer(ModelServer):
    model = ModelInfo(
        id="xtts-v2",
        name="XTTS-v2",
        revision=REVISION[:12],
        license="Coqui Public Model License 1.0.0 (non-commercial)",
        source=f"https://huggingface.co/{REPO}",
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._model: Any = None

    def load(self) -> None:
        import torch
        from huggingface_hub import snapshot_download
        from TTS.tts.configs.xtts_config import XttsConfig
        from TTS.tts.models.xtts import Xtts

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        path = snapshot_download(REPO, revision=REVISION, allow_patterns=FILES)
        config = XttsConfig()
        config.load_json(f"{path}/config.json")
        model = Xtts.init_from_config(config)
        model.load_checkpoint(config, checkpoint_dir=path, use_deepspeed=False)
        if torch.cuda.is_available():
            model.cuda()
        else:
            logger.warning("CUDA is not available: speech is synthesised on the CPU, slowly")
        model.eval()  # coqui's Xtts.eval() returns None, unlike nn.Module.eval()
        self._model = model

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, Params)
        if len(job.prompt or "") > MAX_TEXT_LENGTH:
            raise InvalidInput(
                f"the text is {len(job.prompt or '')} characters; the limit is {MAX_TEXT_LENGTH}"
            )
        out_of_memory: str | None = None
        try:
            latent, embedding = self._voice(job)
            return [self._say(job, job.params, latent, embedding, seed) for seed in job.seeds]
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            # Keep only the text: the traceback would pin the failed call's tensors.
            out_of_memory = str(error)
            logger.warning("out of GPU memory: %s", out_of_memory)
        gc.collect()
        torch.cuda.empty_cache()
        raise GenerationError("out of GPU memory; free GPU memory and retry", retryable=True)

    def _voice(self, job: Job) -> tuple[Any, Any]:
        """Conditioning of the voice: from the sample if given, else a built-in one."""
        assert isinstance(job.params, Params)
        sample = job.inputs.get("voice")
        if sample is None:
            voice = self._model.speaker_manager.speakers[job.params.speaker]
            return voice["gpt_cond_latent"], voice["speaker_embedding"]
        config = self._model.config
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "voice.wav"
            path.write_bytes(_check_sample(sample.data))
            # The model's tuned settings (as coqui's own synthesize() uses them):
            # up to 30 s of the sample shape the voice, not just the first 6 s.
            return self._model.get_conditioning_latents(
                audio_path=[str(path)],
                gpt_cond_len=config.gpt_cond_len,
                gpt_cond_chunk_len=config.gpt_cond_chunk_len,
                max_ref_length=config.max_ref_len,
                sound_norm_refs=config.sound_norm_refs,
            )

    def _say(self, job: Job, params: Params, latent: Any, embedding: Any, seed: int) -> Output:
        import numpy as np
        import torch

        config = self._model.config
        torch.manual_seed(seed)  # seeds the CPU and every CUDA device
        try:
            result = self._inference(job, params, latent, embedding, config)
        except AssertionError as error:
            # XTTS asserts that each sentence fits into 400 text tokens.
            if "400 tokens" not in str(error):
                raise
            raise InvalidInput(f"a sentence is too long for the model: {error}") from error
        samples = np.asarray(result["wav"], dtype=np.float32)
        return Output(
            "audio/wav",
            encode_wav(samples),
            {"seconds": round(len(samples) / SAMPLE_RATE, 2), "language": params.language},
        )

    def _inference(
        self, job: Job, params: Params, latent: Any, embedding: Any, config: Any
    ) -> dict[str, Any]:
        return self._model.inference(
            job.prompt,
            params.language,
            latent,
            embedding,
            temperature=params.temperature,
            speed=params.speed,
            # The model's tuned sampling settings, as coqui's synthesize() uses them.
            length_penalty=config.length_penalty,
            repetition_penalty=config.repetition_penalty,
            top_k=config.top_k,
            top_p=config.top_p,
            enable_text_splitting=True,  # long texts are spoken sentence by sentence
        )


def encode_wav(samples: Any) -> bytes:
    buffer = io.BytesIO()
    soundfile.write(buffer, samples, SAMPLE_RATE, format="WAV", subtype="PCM_16")
    return buffer.getvalue()


MAX_TEXT_LENGTH = 5000
MIN_SAMPLE_SECONDS = 1.0
MAX_SAMPLE_SECONDS = 300.0  # only the first 30 s shape the voice
_SILENCE_RMS = 1e-4


def _check_sample(data: bytes) -> bytes:
    """A voice sample must decode completely, last 1 s to 5 min and not be silent."""
    import numpy as np

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
