"""Declarations, the checkpoints' vocabularies and voice samples; no GPU or weights.

The checkpoints' config.json files (a few kB each) of the pinned revisions are
downloaded once if the models themselves are not.
"""

import io
import json
import typing

import numpy as np
import pytest
import soundfile
from qwen3_tts_model_server.server import (
    FILES,
    LANGUAGES,
    MAX_SAMPLE_SECONDS,
    VARIANTS,
    CloneParams,
    DesignParams,
    Language,
    Qwen3TtsServer,
    Speaker,
    VoiceParams,
    cached,
    encode_wav,
    read_sample,
)

from model_server_sdk import InvalidInput, Job, create_app


def config(variant: str) -> dict:
    from huggingface_hub import hf_hub_download

    model = VARIANTS[variant]
    path = hf_hub_download(model.repository, "config.json", revision=model.revision)
    with open(path, encoding="utf-8") as file:
        return json.load(file)


@pytest.mark.parametrize("variant", sorted(VARIANTS))
def test_declarations_pass_the_sdk_validation(variant) -> None:
    server = Qwen3TtsServer(variant)
    create_app(server)
    assert server.model.id == f"qwen3-tts-{variant}"


@pytest.mark.parametrize("variant", sorted(VARIANTS))
def test_each_server_runs_the_checkpoint_it_names(variant) -> None:
    settings = config(variant)
    assert settings["tts_model_type"] == VARIANTS[variant].kind
    assert settings["tts_model_size"] == "1b7"


@pytest.mark.parametrize("variant", sorted(VARIANTS))
def test_every_declared_language_is_the_checkpoints(variant) -> None:
    known = set(config(variant)["talker_config"]["codec_language_id"])
    assert set(typing.get_args(Language)) == set(LANGUAGES)
    assert set(LANGUAGES.values()) <= known


def test_the_built_in_voices_are_the_checkpoints() -> None:
    assert set(typing.get_args(Speaker)) == set(config("voice")["talker_config"]["spk_id"])


@pytest.mark.parametrize("params", [DesignParams, VoiceParams, CloneParams])
def test_a_request_without_parameters_is_valid(params) -> None:
    assert params().language == "ru"


def test_speech_is_16_bit_wav() -> None:
    data = encode_wav(np.zeros(24000, dtype=np.float32), 24000)
    info = soundfile.info(io.BytesIO(data))
    assert (info.samplerate, info.channels, info.subtype, info.format) == (
        24000,
        1,
        "PCM_16",
        "WAV",
    )
    assert info.duration == pytest.approx(1.0)


def tone(seconds: float, rate: int = 16000, channels: int = 1, level: float = 0.3) -> bytes:
    t = np.arange(int(seconds * rate)) / rate
    wave = (level * np.sin(2 * np.pi * 220 * t)).astype(np.float32)
    buffer = io.BytesIO()
    soundfile.write(buffer, np.stack([wave] * channels, axis=1), rate, format="WAV")
    return buffer.getvalue()


def test_a_stereo_sample_becomes_mono_at_its_own_rate() -> None:
    samples, rate = read_sample(tone(2.0, rate=44100, channels=2))
    assert rate == 44100
    assert samples.shape == (88200,)
    assert samples.dtype == np.float32


@pytest.mark.parametrize(
    ("data", "message"),
    [
        (tone(0.5), "at least a few seconds"),
        (tone(MAX_SAMPLE_SECONDS + 1), "at most"),
        (tone(2.0, level=0.0), "silent"),
        (b"RIFF0000WAVEnot audio", "cannot read"),
    ],
)
def test_unusable_samples_are_refused(data, message) -> None:
    with pytest.raises(InvalidInput, match=message):
        read_sample(data)


def test_empty_text_is_refused_before_the_model_is_needed() -> None:
    job = Job(task="text-to-speech", prompt="  ", inputs={}, params=DesignParams(), seeds=(1,))
    with pytest.raises(InvalidInput, match="text"):
        Qwen3TtsServer("design").generate(job)


def test_a_partial_snapshot_is_not_taken_for_the_checkpoint(tmp_path, monkeypatch) -> None:
    """One file in the cache creates the snapshot directory; the weights are
    still missing, so the server must download them."""
    variant = VARIANTS["clone"]
    folder = variant.repository.replace("/", "--")
    snapshot = tmp_path / f"models--{folder}" / "snapshots" / variant.revision
    snapshot.mkdir(parents=True)
    monkeypatch.setattr("huggingface_hub.constants.HF_HUB_CACHE", str(tmp_path))
    (snapshot / "config.json").write_text("{}")
    assert cached(variant) is None
    for name in FILES:
        (snapshot / name).parent.mkdir(parents=True, exist_ok=True)
        (snapshot / name).write_text("{}")
    assert cached(variant) == str(snapshot)
