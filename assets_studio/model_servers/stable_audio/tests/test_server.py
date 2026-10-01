"""Declarations, the partial schedule, source decoding and failure handling;
no GPU or weights required."""

import io
import types
from collections.abc import Sequence
from typing import Any

import numpy as np
import pytest
import soundfile
import torch
from stable_audio_model_server import server
from stable_audio_model_server.schedule import PartialCosineScheduler
from stable_audio_model_server.server import (
    SAMPLE_RATE,
    TASKS,
    AudioToAudioParams,
    StableAudioServer,
    TextToAudioParams,
    read_source,
    start_sigma,
)

from model_server_sdk import GenerationError, InputData, InvalidInput, Job, create_app

# Stable Audio Open 1.0's scheduler_config.json at the pinned revision.
CONFIG = {
    "sigma_min": 0.3,
    "sigma_max": 500,
    "sigma_data": 1.0,
    "sigma_schedule": "exponential",
    "solver_order": 2,
    "prediction_type": "v_prediction",
    "rho": 7.0,
    "final_sigmas_type": "zero",
    "solver_type": "midpoint",
    "lower_order_final": True,
    "euler_at_final": False,
    "num_train_timesteps": 1000,
}


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(StableAudioServer())
    assert [task.task for task in TASKS] == ["text-to-audio", "audio-to-audio"]


def test_strength_spreads_the_noise_levels() -> None:
    levels = [start_sigma(k / 10, 0.3) for k in range(11)]
    assert levels == sorted(levels)
    assert levels[0] == pytest.approx(0.3)
    assert levels[-1] == pytest.approx(40)
    # Measured: kept to σ ≈ 1, half changed at σ ≈ 4-9, a new sound from σ ≈ 23.
    assert start_sigma(0.3, 0.3) < 1.5
    assert 3 < start_sigma(0.5, 0.3) < 10 < start_sigma(0.9, 0.3)


# The partial schedule


def _scheduler(start: float | None = None) -> PartialCosineScheduler:
    scheduler = PartialCosineScheduler.from_config(CONFIG)
    scheduler.start_sigma = start
    return scheduler


def test_full_schedule_is_the_models() -> None:
    from diffusers import CosineDPMSolverMultistepScheduler

    ours, theirs = _scheduler(), CosineDPMSolverMultistepScheduler.from_config(CONFIG)
    ours.set_timesteps(20)
    theirs.set_timesteps(20)
    assert torch.equal(ours.timesteps, theirs.timesteps)
    assert torch.equal(ours.sigmas, theirs.sigmas)
    assert ours.init_noise_sigma == theirs.init_noise_sigma


def test_partial_schedule_runs_every_step_from_the_start_level() -> None:
    scheduler = _scheduler(2.0)
    scheduler.set_timesteps(20)
    assert len(scheduler.timesteps) == 20
    assert float(scheduler.sigmas[0]) == pytest.approx(2.0)
    assert float(scheduler.sigmas[-2]) == pytest.approx(0.3)
    assert float(scheduler.sigmas[-1]) == 0
    assert scheduler.init_noise_sigma == 2.0


def test_partial_schedule_runs_to_the_end() -> None:
    scheduler = _scheduler(2.0)
    scheduler.set_timesteps(20)
    sample = torch.randn(1, 4, 16, generator=torch.Generator().manual_seed(0))
    for t in scheduler.timesteps:
        scheduler.scale_model_input(sample, t)
        sample = scheduler.step(torch.zeros_like(sample), t, sample).prev_sample
    assert scheduler.step_index == 20
    assert torch.isfinite(sample).all()


@pytest.mark.parametrize("start", [None, 2.0])
def test_noise_follows_the_seed_on_every_generation(start: float | None) -> None:
    """diffusers resets the Brownian-tree noise in set_timesteps; the partial
    schedule must keep that (a regression test on upstream behaviour)."""

    def run(seed: int, scheduler: PartialCosineScheduler) -> torch.Tensor:
        scheduler.set_timesteps(10)
        sample = torch.ones(1, 4, 16)
        generator = torch.Generator().manual_seed(seed)
        for t in scheduler.timesteps:
            scheduler.scale_model_input(sample, t)
            sample = scheduler.step(
                torch.zeros_like(sample), t, sample, generator=generator
            ).prev_sample
        return sample

    scheduler = _scheduler(start)
    first, second = run(1, scheduler), run(2, scheduler)
    assert not torch.equal(first, second)
    assert torch.equal(run(2, _scheduler(start)), second)


# The source sound


def _wav(samples: Any, rate: int = SAMPLE_RATE, fmt: str = "WAV") -> bytes:
    buffer = io.BytesIO()
    soundfile.write(buffer, samples, rate, format=fmt)
    return buffer.getvalue()


def _tone(seconds: float, rate: int = SAMPLE_RATE, channels: int = 1) -> Any:
    t = np.arange(int(seconds * rate)) / rate
    tone = (0.3 * np.sin(2 * np.pi * 440 * t)).astype(np.float32)
    return np.stack([tone] * channels, axis=1)


def test_mono_becomes_stereo() -> None:
    samples = read_source(_wav(_tone(1.0)))
    assert samples.shape == (SAMPLE_RATE, 2)


def test_stereo_48k_is_resampled() -> None:
    samples = read_source(_wav(_tone(1.0, rate=48_000, channels=2), rate=48_000))
    assert samples.shape == (SAMPLE_RATE, 2)


def test_ogg_decodes() -> None:
    assert read_source(_wav(_tone(1.0), fmt="OGG")).shape == (SAMPLE_RATE, 2)


def test_other_rates_are_resampled() -> None:
    samples = read_source(_wav(_tone(2.0, rate=22_050), rate=22_050))
    assert samples.shape == (2 * SAMPLE_RATE, 2)


def test_flac_is_accepted() -> None:
    assert read_source(_wav(_tone(1.0, channels=2), fmt="FLAC")).shape[1] == 2


def test_only_the_models_window_is_used() -> None:
    samples = read_source(_wav(_tone(60.0, rate=8000), rate=8000))
    assert len(samples) == 47 * SAMPLE_RATE


@pytest.mark.parametrize(
    ("data", "message"),
    [
        (b"not audio", "cannot read"),
        (_wav(_tone(0.2)), "shorter"),
        (_wav(np.zeros((SAMPLE_RATE, 1), dtype=np.float32)), "silent"),
        (_wav(_tone(1.0, channels=4)), "channels"),
    ],
)
def test_bad_sources_are_rejected(data: bytes, message: str) -> None:
    with pytest.raises(InvalidInput, match=message):
        read_source(data)


# Generation with a stand-in pipeline


class _Pipeline:
    def __init__(self, fail: Exception | None = None) -> None:
        self.fail = fail
        self.calls: list[dict[str, Any]] = []

    def __call__(self, **kwargs: Any) -> Any:
        if self.fail is not None:
            raise self.fail
        self.calls.append(kwargs)
        length = round(kwargs["audio_end_in_s"] * SAMPLE_RATE)
        return types.SimpleNamespace(audios=torch.zeros(1, 2, length))


def _server(pipeline: _Pipeline) -> tuple[StableAudioServer, Any]:
    instance = StableAudioServer()
    instance._pipeline = pipeline
    instance._scheduler = types.SimpleNamespace(
        start_sigma="unset", config=types.SimpleNamespace(sigma_min=0.3, sigma_max=500)
    )
    return instance, instance._scheduler


def _job(task: str, params: Any, seeds: Sequence[int] = (1, 2), **inputs: InputData) -> Job:
    return Job(task=task, prompt="a door creak", inputs=inputs, params=params, seeds=tuple(seeds))


def test_text_to_audio_runs_the_full_schedule() -> None:
    pipeline = _Pipeline()
    instance, scheduler = _server(pipeline)
    outputs = instance.generate(_job("text-to-audio", TextToAudioParams(seconds=2.0)))
    assert len(outputs) == 2
    assert scheduler.start_sigma is None
    assert outputs[0].meta["seconds"] == 2.0
    assert pipeline.calls[0]["initial_audio_waveforms"] is None
    assert soundfile.info(io.BytesIO(outputs[0].data)).samplerate == SAMPLE_RATE


def test_audio_to_audio_skips_the_start(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(torch.Tensor, "to", lambda self, *args, **kwargs: self)
    pipeline = _Pipeline()
    instance, scheduler = _server(pipeline)
    source = InputData("audio/wav", _wav(_tone(3.0)))
    [output] = instance.generate(
        _job("audio-to-audio", AudioToAudioParams(strength=0.3), seeds=(5,), audio=source)
    )
    assert scheduler.start_sigma == pytest.approx(start_sigma(0.3, 0.3))
    assert output.meta["seconds"] == 3.0
    assert pipeline.calls[0]["audio_end_in_s"] == 3.0
    assert pipeline.calls[0]["initial_audio_waveforms"].shape == (1, 2, 3 * SAMPLE_RATE)


def test_bad_source_is_rejected_before_the_model() -> None:
    pipeline = _Pipeline()
    instance, _ = _server(pipeline)
    with pytest.raises(InvalidInput):
        instance.generate(
            _job("audio-to-audio", AudioToAudioParams(), audio=InputData("audio/wav", b"RIFF"))
        )
    assert pipeline.calls == []


def test_out_of_memory_is_retryable() -> None:
    instance, _ = _server(_Pipeline(fail=RuntimeError("CUDA out of memory. Tried to allocate")))
    with pytest.raises(GenerationError, match="out of GPU memory") as raised:
        instance.generate(_job("text-to-audio", TextToAudioParams()))
    assert raised.value.retryable
    assert raised.value.__context__ is None


def test_other_errors_propagate() -> None:
    instance, _ = _server(_Pipeline(fail=RuntimeError("shape mismatch")))
    with pytest.raises(RuntimeError, match="shape mismatch"):
        instance.generate(_job("text-to-audio", TextToAudioParams()))


def test_loudness_is_normalized_to_minus_one_db() -> None:
    class Loud(_Pipeline):
        def __call__(self, **kwargs: Any) -> Any:
            return types.SimpleNamespace(audios=torch.full((1, 2, SAMPLE_RATE), 0.05))

    instance, _ = _server(Loud())
    [output] = instance.generate(_job("text-to-audio", TextToAudioParams(seconds=1.0), seeds=(1,)))
    samples, _ = soundfile.read(io.BytesIO(output.data))
    assert np.abs(samples).max() == pytest.approx(10 ** (-1 / 20), abs=1e-3)
    assert output.meta["peak_before_normalizing"] == 0.05
    instance, _ = _server(Loud())
    params = TextToAudioParams(seconds=1.0, normalize=False)
    [output] = instance.generate(_job("text-to-audio", params, seeds=(1,)))
    samples, _ = soundfile.read(io.BytesIO(output.data))
    assert np.abs(samples).max() == pytest.approx(0.05, abs=1e-3)


def test_gain_is_capped_for_near_silence() -> None:
    class Quiet(_Pipeline):
        def __call__(self, **kwargs: Any) -> Any:
            return types.SimpleNamespace(audios=torch.full((1, 2, SAMPLE_RATE), 1e-4))

    instance, _ = _server(Quiet())
    [output] = instance.generate(_job("text-to-audio", TextToAudioParams(seconds=1.0), seeds=(1,)))
    samples, _ = soundfile.read(io.BytesIO(output.data))
    assert np.abs(samples).max() == pytest.approx(1e-2, rel=0.05)  # +40 dB, not +79


def test_audio_to_audio_keeps_the_source_level(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(torch.Tensor, "to", lambda self, *args, **kwargs: self)

    class Loud(_Pipeline):
        def __call__(self, **kwargs: Any) -> Any:
            return types.SimpleNamespace(audios=torch.full((1, 2, 3 * SAMPLE_RATE), 0.9))

    instance, _ = _server(Loud())
    source = InputData("audio/wav", _wav(_tone(3.0)))  # peak 0.3
    [output] = instance.generate(
        _job("audio-to-audio", AudioToAudioParams(), seeds=(1,), audio=source)
    )
    samples, _ = soundfile.read(io.BytesIO(output.data))
    assert np.abs(samples).max() == pytest.approx(0.3, abs=2e-3)


def test_output_is_clipped() -> None:
    data = server.encode_wav(np.full((10, 2), 3.0, dtype=np.float32))
    samples, _ = soundfile.read(io.BytesIO(data))
    assert samples.max() <= 1.0
