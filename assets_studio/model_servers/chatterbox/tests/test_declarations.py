"""Declarations, voice samples, text limits and the stress dictionary; no GPU or weights."""

import hashlib
import io
import typing
import zipfile

import numpy as np
import pytest
import soundfile
from chatterbox_model_server import server
from chatterbox_model_server.server import (
    DICTIONARY_FILES,
    FILES,
    MAX_SAMPLE_SECONDS,
    MAX_TEXT_LENGTH,
    REPO,
    REVISION,
    ChatterboxServer,
    Language,
    Params,
    cached,
    check_sample,
    dictionary_dir,
    dictionary_ready,
    download_dictionary,
    encode_wav,
    make_stresser,
    stresser_class,
)

from model_server_sdk import InvalidInput, Job, create_app


def test_declarations_pass_the_sdk_validation() -> None:
    create_app(ChatterboxServer())


def test_a_request_without_parameters_speaks_russian() -> None:
    assert Params().language == "ru"


def test_the_declared_languages_are_upstreams() -> None:
    from chatterbox.mtl_tts import SUPPORTED_LANGUAGES

    assert set(typing.get_args(Language)) == set(SUPPORTED_LANGUAGES)


def test_the_files_are_those_upstream_takes_for_v3() -> None:
    from chatterbox.mtl_tts import MULTILINGUAL_T3_MODELS

    assert MULTILINGUAL_T3_MODELS["v3"] in FILES


def test_speech_is_16_bit_wav() -> None:
    data = encode_wav(np.zeros(24000, dtype=np.float32))
    info = soundfile.info(io.BytesIO(data))
    assert (info.samplerate, info.channels, info.subtype, info.format) == (
        24000,
        1,
        "PCM_16",
        "WAV",
    )
    assert info.duration == pytest.approx(1.0)


def tone(seconds: float, rate: int = 16000, level: float = 0.3) -> bytes:
    t = np.arange(int(seconds * rate)) / rate
    wave = (level * np.sin(2 * np.pi * 220 * t)).astype(np.float32)
    buffer = io.BytesIO()
    soundfile.write(buffer, wave, rate, format="WAV")
    return buffer.getvalue()


def test_a_usable_sample_passes_unchanged() -> None:
    data = tone(3.0)
    assert check_sample(data) == data


@pytest.mark.parametrize(
    ("data", "message"),
    [
        (tone(0.5), "at least a few seconds"),
        (tone(MAX_SAMPLE_SECONDS + 1, rate=8000), "at most"),
        (tone(2.0, level=0.0), "silent"),
        (b"RIFF0000WAVEnot audio", "cannot read"),
    ],
)
def test_unusable_samples_are_refused(data, message) -> None:
    with pytest.raises(InvalidInput, match=message):
        check_sample(data)


@pytest.mark.parametrize(
    ("text", "message"), [("  ", "text"), ("а" * (MAX_TEXT_LENGTH + 1), "limit")]
)
def test_unusable_texts_are_refused_before_the_model_is_needed(text, message) -> None:
    job = Job(task="text-to-speech", prompt=text, inputs={}, params=Params(), seeds=(1,))
    with pytest.raises(InvalidInput, match=message):
        ChatterboxServer().generate(job)


def test_a_partial_snapshot_is_not_taken_for_the_checkpoint(tmp_path, monkeypatch) -> None:
    snapshot = tmp_path / f"models--{REPO.replace('/', '--')}" / "snapshots" / REVISION
    snapshot.mkdir(parents=True)
    monkeypatch.setattr("huggingface_hub.constants.HF_HUB_CACHE", str(tmp_path))
    (snapshot / "ve.pt").write_text("x")
    assert cached() is None
    for name in FILES:
        (snapshot / name).write_text("x")
    assert cached() == str(snapshot)


def test_the_dictionary_lives_where_the_justfile_puts_it(monkeypatch, tmp_path) -> None:
    monkeypatch.setenv("CHATTERBOX_RUSSIAN_DICTIONARY", str(tmp_path))
    assert dictionary_dir() == tmp_path
    monkeypatch.delenv("CHATTERBOX_RUSSIAN_DICTIONARY")
    monkeypatch.setenv("XDG_CACHE_HOME", str(tmp_path))
    assert dictionary_dir() == tmp_path / "assets-studio" / "chatterbox-russian-dictionary"


def fake_archive(path) -> str:
    with zipfile.ZipFile(path, "w") as zipped:
        for name in DICTIONARY_FILES:
            zipped.writestr(name, f"contents of {name}")
    return hashlib.sha256(path.read_bytes()).hexdigest()


def test_the_dictionary_is_checked_and_unpacked(tmp_path, monkeypatch) -> None:
    source = tmp_path / "source.zip"
    monkeypatch.setattr(server, "DICTIONARY_URL", source.as_uri())
    monkeypatch.setattr(server, "DICTIONARY_SHA256", fake_archive(source))
    target = tmp_path / "dictionary"
    assert download_dictionary(target) == target
    assert dictionary_ready(target)
    assert (target / "russian_dict.db").read_text() == "contents of russian_dict.db"
    assert sorted(p.name for p in target.iterdir()) == sorted(DICTIONARY_FILES)


def test_a_dictionary_with_another_checksum_is_refused(tmp_path, monkeypatch) -> None:
    source = tmp_path / "source.zip"
    fake_archive(source)
    monkeypatch.setattr(server, "DICTIONARY_URL", source.as_uri())
    monkeypatch.setattr(server, "DICTIONARY_SHA256", "0" * 64)
    target = tmp_path / "dictionary"
    with pytest.raises(RuntimeError, match="checksum"):
        download_dictionary(target)
    assert not dictionary_ready(target)
    assert not (target / "russian_dict.zip.part").exists()


def test_a_whole_archive_left_behind_is_not_downloaded_again(tmp_path, monkeypatch) -> None:
    target = tmp_path / "dictionary"
    target.mkdir()
    checksum = fake_archive(target / "russian_dict.zip.part")
    monkeypatch.setattr(server, "DICTIONARY_URL", (tmp_path / "missing.zip").as_uri())
    monkeypatch.setattr(server, "DICTIONARY_SHA256", checksum)
    download_dictionary(target)
    assert dictionary_ready(target)


def test_the_stresser_leaves_no_debug_database(tmp_path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    assert stresser_class().__name__ == "RussianTextStresser"
    assert list(tmp_path.iterdir()) == []


def test_a_missing_dictionary_stops_the_server_from_starting(tmp_path) -> None:
    with pytest.raises(RuntimeError, match="missing"):
        make_stresser(tmp_path)


@pytest.mark.skipif(not dictionary_ready(dictionary_dir()), reason="no stress dictionary")
def test_russian_gets_stress_marks() -> None:
    stressed = make_stresser(dictionary_dir()).stress_text("Старого дома.")
    assert "́" in stressed
