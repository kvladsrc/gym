"""Declarations, the split of the model into turn-taking halves, the output
and failure handling; no GPU or weights required."""

import contextlib
import io
import types
from collections.abc import Sequence
from typing import Any

import numpy as np
import pytest
import soundfile
import torch
from yue2.modeling_yue2 import YuE2Config, YuE2ForCausalLM
from yue2_model_server.server import (
    SAMPLE_RATE,
    TASKS,
    SongParams,
    YuE2Server,
    ar_modules,
    encode_wav,
    move,
    nar_modules,
    nar_turn,
    place,
    shared_modules,
)

from model_server_sdk import GenerationError, Job, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(YuE2Server())
    assert [task.task for task in TASKS] == ["text-to-song"]


@pytest.fixture
def model() -> Any:
    config = YuE2Config(
        hidden_size=64,
        num_hidden_layers=2,
        num_attention_heads=4,
        num_key_value_heads=2,
        head_dim=16,
        intermediate_size=128,
        vocab_size=97,
        max_position_embeddings=64,
        max_latent_frames=64,
        latent_dim=8,
    )
    return YuE2ForCausalLM(config).eval()


def _tensors(modules: Sequence[Any]) -> set[int]:
    found: set[int] = set()
    for module in modules:
        found |= {id(p) for p in module.parameters()} | {id(b) for b in module.buffers()}
    return found


def test_the_halves_and_shared_parts_cover_the_model_exactly_once(model: Any) -> None:
    groups = [
        _tensors(ar_modules(model)),
        _tensors(nar_modules(model)),
        _tensors(shared_modules(model)),
    ]
    every = {id(p) for p in model.parameters()} | {id(b) for b in model.buffers()}
    assert set.union(*groups) == every
    assert sum(len(group) for group in groups) == len(every)


@pytest.mark.parametrize("half", ["ar", "nar"])
def test_place_moves_one_half_and_leaves_the_other(model: Any, half: str) -> None:
    place(model, half, torch.device("meta"))  # the "GPU"
    on, off = (ar_modules(model), nar_modules(model))
    if half == "nar":
        on, off = off, on
    assert {p.device.type for m in (*on, *shared_modules(model)) for p in m.parameters()} == {
        "meta"
    }
    assert {p.device.type for m in off for p in m.parameters()} == {"cpu"}


@pytest.mark.parametrize("fails", [False, True])
def test_after_the_nar_turn_the_ar_half_is_back_for_the_next_chunk(
    model: Any, fails: bool, monkeypatch: pytest.MonkeyPatch
) -> None:
    # Tensors cannot leave the meta device, so the turns are recorded instead.
    import yue2_model_server.server as server

    turns: list[str] = []
    monkeypatch.setattr(server, "place", lambda _model, half, _device: turns.append(half))
    with contextlib.suppress(RuntimeError), nar_turn(model, True):
        assert turns == ["nar"]
        if fails:
            raise RuntimeError("chunk failed")
    assert turns == ["nar", "ar"]


def test_turns_reuse_the_cpu_weights_instead_of_copying(model: Any) -> None:
    before = {name: p.data_ptr() for name, p in model.named_parameters()}
    for half in ("ar", "nar", "ar"):
        place(model, half, torch.device("meta"))
    move(model, "cpu")
    # Back on the CPU, every parameter points at the very tensor it was loaded
    # into: turns allocate no CPU memory.
    assert {name: p.data_ptr() for name, p in model.named_parameters()} == before
    assert {p.device.type for p in model.parameters()} == {"cpu"}


def test_wav_is_48k_stereo_pcm16() -> None:
    samples = np.zeros((4800, 2), dtype=np.float32)
    samples[0] = [2.0, -2.0]  # clipped, not wrapped
    info = soundfile.info(io.BytesIO(encode_wav(samples)))
    assert (info.samplerate, info.channels, info.subtype) == (SAMPLE_RATE, 2, "PCM_16")
    decoded, _ = soundfile.read(io.BytesIO(encode_wav(samples)))
    assert decoded[0][0] > 0.99
    assert decoded[0][1] < -0.99


# Generation with a stand-in pipeline


class _Tokenizer:
    """A token per character: enough to measure the prefix."""

    def encode(self, text: str) -> list[int]:
        return [ord(char) % 1000 for char in text]


class _Pipeline:
    def __init__(self, fail: Exception | None = None, truncated: bool = False) -> None:
        from yue2.protocol import GenerationConfig

        self.fail = fail
        self.truncated = truncated
        self.calls: list[dict[str, Any]] = []
        self._model = None
        self.tokenizer = _Tokenizer()
        self.generation_config = GenerationConfig()

    def __call__(self, **kwargs: Any) -> Any:
        if self.fail is not None:
            raise self.fail
        self.calls.append(kwargs)
        return types.SimpleNamespace(
            audio=np.zeros((SAMPLE_RATE // 2, 2), dtype=np.float32),
            truncated={"abc": False, "semantic": self.truncated},
            abc="X:1\nK:C\nCDEF|",
        )


def _server(pipeline: _Pipeline) -> YuE2Server:
    server = YuE2Server()
    server._pipeline = pipeline
    return server


def _job(params: SongParams, seeds: Sequence[int] = (1, 2), prompt: str = "[Verse]\nLa la") -> Job:
    return Job(task="text-to-song", prompt=prompt, inputs={}, params=params, seeds=tuple(seeds))


def test_one_song_per_seed_with_lyrics_style_and_plan() -> None:
    pipeline = _Pipeline()
    outputs = _server(pipeline).generate(
        _job(SongParams(style=" jazz ", plan="melody"), seeds=(3, 2**32 - 1))
    )
    assert [call["seed"] for call in pipeline.calls] == [3, 2**32 - 1]
    assert {call["style"] for call in pipeline.calls} == {"jazz"}
    assert {call["cot"] for call in pipeline.calls} == {"melody"}
    assert {call["lyrics"] for call in pipeline.calls} == {"[Verse]\nLa la"}
    assert outputs[0].mime == "audio/wav"
    assert outputs[0].meta["duration_s"] == 0.5
    assert outputs[0].meta["channels"] == 2
    assert outputs[0].meta["score"].startswith("X:1")


def test_truncation_is_reported() -> None:
    (output,) = _server(_Pipeline(truncated=True)).generate(_job(SongParams(), seeds=(1,)))
    assert output.meta["truncated"] is True


def test_blank_lyrics_are_an_input_error() -> None:
    from model_server_sdk import InvalidInput

    pipeline = _Pipeline()
    with pytest.raises(InvalidInput):
        _server(pipeline).generate(_job(SongParams(), prompt="  \n "))
    assert pipeline.calls == []


def test_lyrics_too_long_for_the_context_are_an_input_error() -> None:
    from model_server_sdk import InvalidInput

    message = "Prefix + requested generation budget exceeds 24576; no implicit truncation"
    with pytest.raises(InvalidInput, match="too long"):
        _server(_Pipeline(fail=ValueError(message))).generate(_job(SongParams()))


@pytest.mark.parametrize(("plan", "fits"), [("full", 11000), ("off", 15000)])
def test_lyrics_are_checked_against_the_worst_case_before_anything_runs(
    plan: str, fits: int
) -> None:
    from model_server_sdk import InvalidInput

    # 24576 tokens of context, minus 9000 for the song and, with a plan,
    # 4096 + 2 for the score: what remains is for the instructions and lyrics.
    pipeline = _Pipeline()
    _server(pipeline).generate(_job(SongParams(plan=plan), seeds=(1,), prompt="a" * fits))
    with pytest.raises(InvalidInput, match="too long"):
        _server(pipeline).generate(
            _job(
                SongParams(plan=plan), seeds=(1,), prompt="a" * 12000 * (2 if plan == "off" else 1)
            )
        )
    assert len(pipeline.calls) == 1


@pytest.mark.parametrize("plan", ["full", "off"])
@pytest.mark.parametrize("over", [0, 1])
def test_the_length_limit_is_exact(plan: str, over: int) -> None:
    from yue2.protocol import CONTEXT, SongRequest, token_prefixes

    from model_server_sdk import InvalidInput

    pipeline = _Pipeline()
    config = pipeline.generation_config
    budgets = config.semantic.max_tokens + (config.abc.max_tokens + 2 if plan != "off" else 0)
    empty = SongRequest(style=SongParams().style, lyrics="", cot=plan)
    room = CONTEXT - budgets - len(token_prefixes(empty, pipeline.tokenizer))
    job = _job(SongParams(plan=plan), seeds=(1,), prompt="a" * (room + over))
    if over:
        with pytest.raises(InvalidInput):
            _server(pipeline).generate(job)
    else:
        _server(pipeline).generate(job)
    assert len(pipeline.calls) == 1 - over


def test_other_value_errors_are_not_input_errors() -> None:
    with pytest.raises(ValueError, match="non-finite"):
        _server(_Pipeline(fail=ValueError("VAE produced non-finite audio"))).generate(
            _job(SongParams())
        )


def test_out_of_memory_is_a_retryable_failure() -> None:
    pipeline = _Pipeline(fail=RuntimeError("CUDA out of memory. Tried to allocate 2 GiB"))
    with pytest.raises(GenerationError) as caught:
        _server(pipeline).generate(_job(SongParams()))
    assert caught.value.retryable


def test_other_errors_are_not_swallowed() -> None:
    pipeline = _Pipeline(fail=RuntimeError("shape mismatch"))
    with pytest.raises(RuntimeError, match="shape mismatch"):
        _server(pipeline).generate(_job(SongParams()))
