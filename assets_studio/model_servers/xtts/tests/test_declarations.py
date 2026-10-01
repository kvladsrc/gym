"""Declarations, text front-ends and voice samples; no GPU or weights required."""

import io
import typing

import numpy as np
import pytest
import soundfile
from xtts_model_server.server import (
    MAX_SAMPLE_SECONDS,
    REPO,
    REVISION,
    SAMPLE_RATE,
    Language,
    XttsServer,
    _check_sample,
    encode_wav,
)

from model_server_sdk import InvalidInput, create_app


def test_declarations_pass_the_sdk_validation() -> None:
    create_app(XttsServer())


def _vocabulary() -> str:
    """The model's tokenizer vocabulary: a small file (~0.4 MB) of the pinned
    revision, downloaded once if the model itself is not (coqui-tts ships none)."""
    from huggingface_hub import hf_hub_download

    return hf_hub_download(REPO, "vocab.json", revision=REVISION)


@pytest.mark.parametrize("language", typing.get_args(Language))
def test_every_declared_language_can_be_tokenised(language) -> None:
    """Each language's text front-end and its dependencies are installed."""
    from TTS.tts.layers.xtts.tokenizer import VoiceBpeTokenizer

    assert VoiceBpeTokenizer(_vocabulary()).encode("тест test 1", language)


@pytest.mark.parametrize(
    ("language", "sentence"),
    [
        ("ru", "Мост через реку давно разрушен, придётся искать брод. "),
        ("es", "El puente sobre el río está roto, hay que buscar un vado. "),
        ("ar", "الجسر فوق النهر مكسور منذ زمن، علينا أن نبحث عن مخاضة. "),
    ],
)
def test_long_text_is_split_into_sentences(language, sentence) -> None:
    """Splitting needs spaCy, which coqui-tts does not install by itself."""
    from TTS.tts.layers.xtts.tokenizer import VoiceBpeTokenizer, split_sentence

    limit = VoiceBpeTokenizer(_vocabulary()).char_limits[language]
    parts = split_sentence(sentence * 12, language, limit)
    assert len(parts) > 1
    assert all(len(part) <= limit for part in parts)


def test_speech_is_16_bit_wav() -> None:
    data = encode_wav(np.zeros(SAMPLE_RATE, dtype=np.float32))
    info = soundfile.info(io.BytesIO(data))
    assert (info.samplerate, info.channels, info.subtype, info.format) == (
        SAMPLE_RATE,
        1,
        "PCM_16",
        "WAV",
    )
    assert info.duration == pytest.approx(1.0)


def tone(seconds: float, rate: int = SAMPLE_RATE, channels: int = 1) -> bytes:
    t = np.arange(int(seconds * rate)) / rate
    wave = (0.3 * np.sin(2 * np.pi * 220 * t)).astype(np.float32)
    buffer = io.BytesIO()
    soundfile.write(buffer, np.stack([wave] * channels, axis=1), rate, format="WAV")
    return buffer.getvalue()


@pytest.mark.parametrize(("rate", "channels"), [(SAMPLE_RATE, 1), (44_100, 2), (16_000, 1)])
def test_voice_samples_in_any_rate_and_channels_are_accepted(rate, channels) -> None:
    sample = tone(3, rate, channels)
    assert _check_sample(sample) == sample


@pytest.mark.parametrize(
    ("sample", "message"),
    [
        (tone(0.25), "at least a few seconds"),
        (encode_wav(np.zeros(3 * SAMPLE_RATE, dtype=np.float32)), "silent"),
        (b"not audio", "cannot read"),
        (tone(3)[:2000], None),  # header promises 3 s, data is cut short
    ],
)
def test_bad_voice_samples_are_input_errors(sample, message) -> None:
    with pytest.raises(InvalidInput, match=message):
        _check_sample(sample)


def test_overlong_voice_sample_is_rejected_before_decoding() -> None:
    with pytest.raises(InvalidInput, match="at most"):
        _check_sample(tone(MAX_SAMPLE_SECONDS + 1, rate=8000))
