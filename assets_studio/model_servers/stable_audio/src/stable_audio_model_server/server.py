"""Stable Audio Open 1.0 server: text-to-audio and audio-to-audio (contract v1).

Sound effects and short loops, 44.1 kHz stereo, up to 47 s. The whole model
(~3.5 GB in fp16) stays on the GPU.

Audio-to-audio is SDEdit, as image-to-image is for pictures: the source sound
gets noise of the level that ``strength`` picks, and all the steps denoise
from there (see schedule.py). The pipeline's own ``initial_audio_waveforms``
alone is not that: it adds the source to noise of the full level, which
drowns it.
"""

import gc
import io
import logging
import warnings
from typing import Any

import numpy as np
import soundfile
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
    is_out_of_memory,
)

logger = logging.getLogger("stable_audio_model_server")

# torchsde warns on every generation that the Brownian tree is queried at
# σ = 0 and just above σ_max (float rounding at its ends): harmless.
warnings.filterwarnings("ignore", message="Should have (ta|tb)", module="torchsde.*")

REPO = "stabilityai/stable-audio-open-1.0"
# A git revision, not a secret.
REVISION = "f21265c1e2710b3bd2386596943f0007f55f802e"  # pragma: allowlist secret
# The diffusers layout only; not the original model.ckpt / vae_model.ckpt.
FILES = [
    "model_index.json",
    "scheduler/*",
    "tokenizer/*",
    "text_encoder/*",
    "projection_model/*",
    "transformer/*",
    "vae/*",
]
SAMPLE_RATE = 44_100
MAX_SECONDS = 47.0  # the model's window (1024 latents × 2048 samples ≈ 47.5 s)
MIN_SOURCE_SECONDS = 0.5
_SILENCE_RMS = 1e-4
PEAK = 10 ** (-1 / 20)  # -1 dBFS
# Normalising never lifts by more than this: a near-silent result would
# become loud noise.
MAX_GAIN = 10 ** (40 / 20)  # +40 dB
# Strength 1: past this noise level nothing of the source is left.
SIGMA_CAP = 40.0


class Common(BaseModel):
    model_config = ConfigDict(extra="forbid")

    steps: int = Field(default=100, ge=10, le=250, description="Шаги")
    guidance: float = Field(default=7.0, ge=1, le=15, description="Следование описанию")
    negative_prompt: str = Field(default="Low quality.", description="Чего избегать")


class TextToAudioParams(Common):
    seconds: float = Field(
        default=10.0,
        ge=0.5,
        le=MAX_SECONDS,
        description="Длительность, с",
        json_schema_extra=PRIMARY,
    )
    # The model's loudness varies from -40 to 0 dBFS (and clips) between sounds.
    normalize: bool = Field(default=True, description="Выровнять громкость (пик −1 дБ)")


class AudioToAudioParams(Common):
    strength: float = Field(
        default=0.5,
        ge=0.05,
        le=1,
        description="Сила изменения (1 — исходный звук почти не учитывается)",
        json_schema_extra=PRIMARY,
    )
    # Evolution keeps the level of the sound it mutates.
    normalize: bool = Field(default=True, description="Громкость как у исходного звука")


TASKS = (
    TaskSpec(KnownTask.TEXT_TO_AUDIO, TextToAudioParams, ("audio/wav",)),
    TaskSpec(
        KnownTask.AUDIO_TO_AUDIO,
        AudioToAudioParams,
        ("audio/wav",),
        prompt="optional",
        inputs=(
            InputSpec(
                role="audio",
                mime=["audio/wav"],
                description="Исходный звук; учитываются первые 47 с",
            ),
        ),
    ),
)


def start_sigma(strength: float, sigma_min: float) -> float:
    """The noise level audio-to-audio starts from: log-linear from σ_min to
    ``SIGMA_CAP``.

    Measured (2 sources × 2 seeds, see docs/models/text-to-audio.md): the
    source is kept up to σ ≈ 1, half changed at σ ≈ 4-9, and from σ ≈ 23 the
    result is as unrelated as a new sound. On the model's own range (0.3-500)
    the top third of the slider would do nothing distinct.
    """
    return sigma_min * (SIGMA_CAP / sigma_min) ** strength


class StableAudioServer(ModelServer):
    model = ModelInfo(
        id="stable-audio-open-1",
        name="Stable Audio Open 1.0",
        revision=REVISION[:12],
        license="Stability AI Community License (non-commercial and small business)",
        source=f"https://huggingface.co/{REPO}",
    )
    tasks = TASKS

    def __init__(self) -> None:
        self._pipeline: Any = None
        self._scheduler: Any = None

    def load(self) -> None:
        import torch
        from diffusers import StableAudioPipeline
        from huggingface_hub import snapshot_download

        from stable_audio_model_server.schedule import partial_scheduler

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError(
                "Stable Audio needs a CUDA GPU (on the CPU a sound takes many minutes)"
            )
        path = snapshot_download(REPO, revision=REVISION, allow_patterns=FILES)
        pipeline = StableAudioPipeline.from_pretrained(path, torch_dtype=torch.float16)
        pipeline.scheduler = partial_scheduler(pipeline.scheduler)
        self._scheduler = pipeline.scheduler
        self._pipeline = pipeline.to("cuda")
        pipeline.set_progress_bar_config(disable=True)

    def generate(self, job: Job) -> list[Output]:
        import torch

        # Inputs first: a bad file must not cost a model pass.
        source = read_source(job.inputs["audio"].data) if "audio" in job.inputs else None
        out_of_memory: str | None = None
        if torch.cuda.is_available():
            torch.cuda.reset_peak_memory_stats()
        try:
            if isinstance(job.params, TextToAudioParams):
                return self._run(job, job.params, job.params.seconds, None, None, PEAK)
            assert isinstance(job.params, AudioToAudioParams)
            assert source is not None
            config = self._scheduler.config
            sigma = start_sigma(job.params.strength, config.sigma_min)
            seconds = min(len(source) / SAMPLE_RATE, MAX_SECONDS)
            level = min(float(np.abs(source).max()), PEAK)
            return self._run(job, job.params, seconds, source, sigma, level)
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            # Keep only the text: the traceback would pin the failed call's tensors.
            out_of_memory = str(error)
            logger.warning("out of GPU memory: %s", out_of_memory)
        finally:
            if torch.cuda.is_available():
                logger.info("GPU peak %.2f GB", torch.cuda.max_memory_allocated() / 1e9)
            # Give the allocator's cache back: other model servers share the GPU.
            torch.cuda.empty_cache()
        gc.collect()
        torch.cuda.empty_cache()
        raise GenerationError(
            "out of GPU memory; free GPU memory (other model servers) and retry", retryable=True
        )

    def _run(
        self,
        job: Job,
        params: TextToAudioParams | AudioToAudioParams,
        seconds: float,
        source: Any,
        sigma: float | None,
        level: float,
    ) -> list[Output]:
        import torch

        waveform = None
        if source is not None:
            # (1, channels, samples) on the GPU, as the pipeline expects.
            waveform = torch.from_numpy(source.T.copy()).unsqueeze(0).to("cuda", torch.float16)
        outputs: list[Output] = []
        for seed in job.seeds:
            self._scheduler.start_sigma = sigma
            audio = self._pipeline(
                prompt=job.prompt or "",
                negative_prompt=params.negative_prompt or None,
                audio_end_in_s=seconds,
                num_inference_steps=params.steps,
                guidance_scale=params.guidance,
                initial_audio_waveforms=waveform,
                initial_audio_sampling_rate=SAMPLE_RATE if waveform is not None else None,
                generator=torch.Generator(device="cpu").manual_seed(seed),
            ).audios[0]
            samples = audio.float().cpu().numpy().T[: round(seconds * SAMPLE_RATE)]
            peak = float(np.abs(samples).max())
            if params.normalize and peak > 0:
                samples = samples * min(level / peak, MAX_GAIN)
            outputs.append(
                Output(
                    "audio/wav",
                    encode_wav(samples),
                    {
                        "seconds": round(len(samples) / SAMPLE_RATE, 2),
                        "steps": params.steps,
                        "peak_before_normalizing": round(peak, 3),
                    },
                )
            )
        return outputs


def read_source(data: bytes) -> Any:
    """The source sound as float32 (samples, 2) at 44.1 kHz; the first 47 s."""
    import soxr

    try:
        info = soundfile.info(io.BytesIO(data))
        if info.channels > 2:
            raise InvalidInput(f"the sound has {info.channels} channels; use mono or stereo")
        # Only the model's window is decoded; what is decoded is what the file
        # really holds (a header can promise more).
        samples, rate = soundfile.read(
            io.BytesIO(data),
            frames=int(MAX_SECONDS * info.samplerate),
            dtype="float32",
            always_2d=True,
        )
    except (RuntimeError, soundfile.LibsndfileError) as error:
        raise InvalidInput(f"cannot read the sound: {error}") from error
    if len(samples) / rate < MIN_SOURCE_SECONDS:
        raise InvalidInput(f"the sound is shorter than {MIN_SOURCE_SECONDS} s")
    if samples.shape[1] == 1:
        samples = np.repeat(samples, 2, axis=1)
    if rate != SAMPLE_RATE:
        samples = soxr.resample(samples, rate, SAMPLE_RATE, quality="VHQ").astype(np.float32)
    if float(np.sqrt(np.mean(np.square(samples)))) < _SILENCE_RMS:
        raise InvalidInput("the sound is silent")
    return np.clip(samples, -1, 1)


def encode_wav(samples: Any) -> bytes:
    buffer = io.BytesIO()
    soundfile.write(buffer, np.clip(samples, -1, 1), SAMPLE_RATE, format="WAV", subtype="PCM_16")
    return buffer.getvalue()
