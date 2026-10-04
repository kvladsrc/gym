"""YuE2 server: songs with vocals from lyrics and a style (contract v1).

YuE2-3B plans a melody and chords, writes semantic music tokens with its
autoregressive (AR) half, turns them into acoustic latents with its flow
matching (NAR) half and decodes those with a VAE into 48 kHz stereo. The
authors' setup is a 24 GB card; the whole model is ~7.1 GB in bf16.

On 8 GB the halves take turns on the GPU: every layer holds an AR and a NAR
copy of its attention and MLP, so while tokens are written only the AR copies
(with the embedding table and the output head) are on the GPU, and while
latents are solved only the NAR copies are. Same weights, same numbers; only
where they sit changes.

Weights: CC BY-NC 4.0 with an additional permission for personal users and
creators, who may also monetize what they make.
"""

import contextlib
import gc
import io
import logging
from collections.abc import Iterator
from typing import Any, Literal

import numpy as np
import soundfile
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
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

logger = logging.getLogger("yue2_model_server")

MODEL_REPO = "m-a-p/YuE2-3B"
VAE_REPO = "m-a-p/YuE2-Vae"
# Git revisions, not secrets.
MODEL_REVISION = "c044757a011169583f363168348ae380946efff8"  # pragma: allowlist secret
VAE_REVISION = "152733a19ad43aa67e367f9b5503ef8075bb5126"  # pragma: allowlist secret
SAMPLE_RATE = 48000
# The pipeline caps its share of the GPU at (budget - 2 GiB); the turn-taking
# halves need the whole card, and VAE chunks this small (512 frames) at <= 12.
MEMORY_BUDGET_GIB = 8
# yue2's message when the lyrics and the plan leave no room for the song.
TOO_LONG = "exceeds"

DEFAULT_STYLE = (
    "English, warm piano pop, expressive female voice, acoustic piano, "
    "rounded bass and light drums, lyrical memorable melody, 88 BPM"
)


class SongParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    style: str = Field(
        default=DEFAULT_STYLE,
        min_length=1,
        max_length=1000,
        description="Style: language, genre, instruments, voice, tempo (in English)",
        json_schema_extra=ui(
            ru="Стиль: язык, жанр, инструменты, голос, темп (по-английски)", primary=True
        ),
    )
    plan: Literal["full", "melody", "off"] = Field(
        default="full",
        description="Plan: full: melody and chords, melody: melody only, off: no plan",
        json_schema_extra=ui(
            ru="План: full — мелодия и аккорды, melody — только мелодия, off — без плана"
        ),
    )


TASKS = (TaskSpec(KnownTask.TEXT_TO_SONG, SongParams, ("audio/wav",), prompt="required"),)


def ar_modules(model: Any) -> list[Any]:
    """What only the AR half runs: the same list as yue2.nar._offload_ar."""
    modules = [model.model.embed_tokens, model.lm_head]
    for layer in model.model.layers:
        modules.extend(
            (layer.input_layernorm, layer.self_attn, layer.post_attention_layernorm, layer.mlp)
        )
    return modules


def nar_modules(model: Any) -> list[Any]:
    """The NAR copies of every layer's attention and MLP."""
    modules: list[Any] = []
    for layer in model.model.layers:
        modules.extend(
            (
                layer.nar_input_layernorm,
                layer.nar_self_attn,
                layer.nar_pre_mlp_layernorm,
                layer.nar_mlp,
            )
        )
    return modules


def shared_modules(model: Any) -> list[Any]:
    """Small parts both halves use; they stay on the GPU."""
    return [
        model.model.norm,
        model.model.rotary_emb,
        model.llm2vae,
        model.vae2llm,
        model.time_embedder,
        model.latent_pos_embed,
    ]


def move(module: Any, device: Any) -> None:
    """Move ``module``'s weights without new CPU allocations.

    Every module keeps the CPU tensors its parameters were loaded into: going
    to the GPU makes copies from them, coming back only puts them back.
    module.to("cpu") would copy each GPU half into fresh CPU memory on every
    turn, and the freed memory is not returned to the system: the server grew
    to 17.5 GB of RAM for a 7.6 GB model after two songs. Inference never
    changes weights, so the CPU copies stay valid; buffers (rotary tables) are
    kept the same way."""
    import torch

    device = torch.device(device)
    for child in module.modules():
        masters: dict[str, Any] | None = getattr(child, "_cpu_masters", None)
        if masters is None:
            masters = {}
            child._cpu_masters = masters
        for name, parameter in list(child.named_parameters(recurse=False)):
            master = masters.get(name)
            if master is None:
                master = (
                    parameter.detach()
                    if parameter.device.type == "cpu"
                    else parameter.detach().cpu()
                )
                masters[name] = master
            tensor = master if device.type == "cpu" else master.to(device)
            child._parameters[name] = torch.nn.Parameter(tensor, requires_grad=False)
        for name, buffer in list(child.named_buffers(recurse=False)):
            key = f"buffer:{name}"
            if key not in masters:
                masters[key] = buffer if buffer.device.type == "cpu" else buffer.cpu()
            child._buffers[name] = masters[key] if device.type == "cpu" else masters[key].to(device)


def place(model: Any, half: Literal["ar", "nar"], device: Any) -> None:
    """Put one half (and the shared parts) on ``device``, the other on the CPU.
    The other half leaves first, so the two never share the GPU."""
    import torch

    on, off = (
        (ar_modules(model), nar_modules(model))
        if half == "ar"
        else (nar_modules(model), ar_modules(model))
    )
    for module in off:
        move(module, "cpu")
    if torch.cuda.is_available():
        torch.cuda.empty_cache()
    for module in (*shared_modules(model), *on):
        move(module, device)


def _pipeline_class() -> Any:
    """YuE2Pipeline with the turn-taking placement (imported lazily: torch)."""
    import torch
    from yue2 import YuE2Pipeline

    class TurnTakingPipeline(YuE2Pipeline):  # type: ignore[misc]
        def _load_model(self, for_nar: bool = False) -> Any:
            if self._model is None:
                from yue2.modeling_yue2 import YuE2ForCausalLM

                self._model = YuE2ForCausalLM.from_pretrained(
                    self.model_dir,
                    local_files_only=True,
                    torch_dtype=torch.bfloat16,
                    low_cpu_mem_usage=True,
                ).eval()
            # Synthesis first runs the AR layers over the prefix, then swaps
            # halves in yue2.nar._offload_ar (patched below).
            place(self._model, "ar", self.device)
            return self._model

        def decode(self, *args: Any, **kwargs: Any) -> Any:
            # The official decode moves the model off the GPU with .to("cpu"),
            # which would copy it; release the GPU copies first, so that is a no-op.
            if self._model is not None:
                move(self._model, "cpu")
                torch.cuda.empty_cache()
            return super().decode(*args, **kwargs)

    return TurnTakingPipeline


@contextlib.contextmanager
def nar_turn(model: Any, enabled: bool) -> Iterator[None]:
    """Replaces yue2.nar._offload_ar: the NAR half takes the GPU while a chunk
    of latents is solved, then gives it back to the AR half, which prefills
    the next chunk (long songs have several)."""
    device = next(iter(shared_modules(model)[0].parameters())).device
    place(model, "nar", device)
    try:
        yield
    finally:
        place(model, "ar", device)


class YuE2Server(ModelServer):
    model = ModelInfo(
        id="yue2-3b",
        name="YuE2 3B",
        revision=f"{MODEL_REVISION[:8]}+vae.{VAE_REVISION[:8]}",
        license="CC BY-NC 4.0 + creator permission",
        source=f"https://huggingface.co/{MODEL_REPO}",
    )
    tasks = TASKS

    def __init__(self) -> None:
        self._pipeline: Any = None

    def load(self) -> None:
        import torch
        import yue2.nar

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("YuE2 needs a CUDA GPU with bf16")
        yue2.nar._offload_ar = nar_turn
        pipeline = _pipeline_class().from_pretrained(
            MODEL_REPO,
            vae=VAE_REPO,
            revision=MODEL_REVISION,
            vae_revision=VAE_REVISION,
            device="cuda",
            memory_budget_gib=MEMORY_BUDGET_GIB,
            offload_ar=True,  # makes synthesis enter _nar_turn
            progress=False,
        )
        # The pipeline caps the process at (budget - 2 GiB) of the card; the
        # halves take turns precisely so that the whole card can be used.
        torch.cuda.set_per_process_memory_fraction(1.0, pipeline.device)
        self._pipeline = pipeline

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, SongParams)
        lyrics = (job.prompt or "").strip()
        if not lyrics:
            raise InvalidInput("lyrics are required: the words to sing, with [Verse] / [Chorus]")
        self._check_length(lyrics, job.params)
        out_of_memory: str | None = None
        try:
            return [self._song(lyrics, job.params, seed) for seed in job.seeds]
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            # Keep only the text: the traceback would pin the failed call's tensors.
            out_of_memory = str(error)
            logger.warning("out of GPU memory: %s", out_of_memory)
        if self._pipeline._model is not None:
            move(self._pipeline._model, "cpu")
        gc.collect()
        torch.cuda.empty_cache()
        raise GenerationError(
            "out of GPU memory; free GPU memory (other model servers) and retry", retryable=True
        )

    def _check_length(self, lyrics: str, params: SongParams) -> None:
        """Lyrics and style must leave the full score and song budgets in the
        model's context. yue2 itself checks only after planning, against the
        plan it got, so the same lyrics could pass for one seed and fail for
        another; this checks the worst case, before anything runs."""
        from yue2.protocol import CONTEXT, SongRequest, token_prefixes

        request = SongRequest(style=params.style.strip(), lyrics=lyrics, cot=params.plan)
        config = self._pipeline.generation_config
        needed = len(token_prefixes(request, self._pipeline.tokenizer)) + config.semantic.max_tokens
        if params.plan != "off":
            needed += config.abc.max_tokens + 2  # the score, then its end and the music start
        if needed > CONTEXT:
            raise InvalidInput(
                f"the lyrics and style are too long for one song ({needed} of {CONTEXT} "
                "tokens with the score and song budgets); shorten them"
            )

    def _song(self, lyrics: str, params: SongParams, seed: int) -> Output:
        try:
            song = self._pipeline(
                style=params.style.strip(), lyrics=lyrics, cot=params.plan, seed=seed
            )
        except ValueError as error:
            if TOO_LONG not in str(error):
                raise
            raise InvalidInput(
                "the lyrics and style are too long for one song; shorten them"
            ) from error
        truncated = song.truncated
        return Output(
            "audio/wav",
            encode_wav(song.audio),
            {
                "duration_s": round(len(song.audio) / SAMPLE_RATE, 2),
                "sample_rate": SAMPLE_RATE,
                "channels": int(song.audio.shape[1]) if song.audio.ndim == 2 else 1,
                # The model hit a token limit: the song may end early.
                "truncated": bool(truncated["abc"] or truncated["semantic"]),
                # The melody-and-chord plan, as ABC notation (none with plan=off).
                "score": song.abc,
            },
        )


def encode_wav(samples: Any) -> bytes:
    """16-bit PCM WAV at 48 kHz; ``samples`` is [frames] or [frames, channels]."""
    buffer = io.BytesIO()
    soundfile.write(buffer, np.clip(samples, -1, 1), SAMPLE_RATE, format="WAV", subtype="PCM_16")
    return buffer.getvalue()
