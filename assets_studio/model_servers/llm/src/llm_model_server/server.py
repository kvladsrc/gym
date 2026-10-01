"""Text generation (contract v1, ADR-003 ``text-to-text``) with a GGUF model.

The model runs in llama.cpp's server, a pinned build started as a child
process on a private port (``runtime.py``); this server translates the
contract to its OpenAI-style chat API. llama.cpp places as many layers on the
GPU as fit and keeps the rest in RAM, so models larger than the card run,
only slower. Each variant is a separate request with its own seed and no
prompt cache, so a single request with that seed repeats it exactly.
"""

import ctypes
import json
import logging
import os
import re
import signal
import socket
import subprocess
import time
import urllib.error
import urllib.request
from collections.abc import Callable
from typing import Any

from pydantic import BaseModel, ConfigDict, Field

from llm_model_server import runtime
from llm_model_server.models import ModelSpec
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
)

logger = logging.getLogger("llm_model_server")

CONTEXT_TOKENS = 16_384
LOAD_TIMEOUT_S = 900  # reading ~17 GB from disk the first time
REQUEST_TIMEOUT_S = 3600  # minutes at a few tokens per second are fine
# Room for the reasoning when thinking is on: it counts against max_tokens.
THINKING_TOKENS = 4096
# llama.cpp reads this seed as "pick a random one".
_LLAMA_RANDOM_SEED = 0xFFFFFFFF
DEFAULT_SYSTEM = (
    "Ты помогаешь делать компьютерную игру: пишешь описания, диалоги, имена, "
    "задания и тексты интерфейса. Отвечай по-русски, если не попросили иначе, "
    "без вступлений и пояснений — только сам текст."
)
# Control characters other than tab and line feed (ADR-003 canonical text).
_CONTROL = re.compile("[\x00-\x08\x0b-\x1f\x7f-\x9f]")


class Params(BaseModel):
    """The parameters every model has; sampling defaults come from the model."""

    model_config = ConfigDict(extra="forbid")

    max_tokens: int = Field(
        default=1024, ge=16, le=8192, description="Длина ответа, токенов", json_schema_extra=PRIMARY
    )
    temperature: float = Field(default=1.0, ge=0, le=2, description="Вариативность")
    top_k: int = Field(default=40, ge=0, le=200, description="Top-k (0 — без ограничения)")
    top_p: float = Field(default=0.95, gt=0, le=1, description="Top-p")
    thinking: bool = Field(default=False, description="Размышлять перед ответом (дольше)")
    system: str = Field(default=DEFAULT_SYSTEM, description="Системная инструкция")


def params_for(spec: ModelSpec) -> type[Params]:
    """Parameters with the sampling the model card recommends as defaults."""

    class ModelParams(Params):
        temperature: float = Field(
            default=spec.temperature,
            ge=0,
            le=2,
            description="Вариативность",
            json_schema_extra=PRIMARY,
        )
        top_k: int = Field(
            default=spec.top_k, ge=0, le=200, description="Top-k (0 — без ограничения)"
        )
        top_p: float = Field(default=spec.top_p, gt=0, le=1, description="Top-p")

    return ModelParams


def messages(prompt: str, source: str | None, system: str) -> list[dict[str, str]]:
    """The chat: the instruction, and the text to work on when there is one,
    fenced so that instructions inside it are not taken for the task."""
    request = (
        prompt
        if source is None
        else (
            f"{prompt}\n\nВерни только изменённый текст, без пояснений.\n\n"
            f"<текст>\n{source}\n</текст>"
        )
    )
    chat = [{"role": "user", "content": request}]
    return [{"role": "system", "content": system}, *chat] if system.strip() else chat


def canonical_text(text: str) -> str:
    """ADR-003 text: no BOM, ``\\n`` line breaks, no control characters."""
    text = text.lstrip("\ufeff").replace("\r\n", "\n").replace("\r", "\n")
    return _CONTROL.sub("", text).strip() + "\n"


def llama_seed(seed: int) -> int:
    """The contract's seed as llama.cpp takes it (one value means "random")."""
    return seed % _LLAMA_RANDOM_SEED


class LlmServer(ModelServer):
    def __init__(self, spec: ModelSpec) -> None:
        self.spec = spec
        self.model = ModelInfo(
            id=spec.key.replace(".", "_"),  # contract ids have no dots
            name=spec.name,
            revision=f"{spec.revision[:8]}+llama.cpp.{runtime.BUILD}",
            license=spec.license,
            source=spec.source,
        )
        self.tasks = (
            TaskSpec(
                KnownTask.TEXT_TO_TEXT,
                params_for(spec),
                ("text/plain",),
                inputs=(
                    InputSpec(
                        role="text",
                        mime=["text/plain"],
                        required=False,
                        description="Текст для правки или продолжения",
                    ),
                ),
            ),
        )
        self._process: subprocess.Popen[bytes] | None = None
        self._url = ""
        self._weights = ""

    def load(self) -> None:
        from huggingface_hub import hf_hub_download

        runtime.install()
        self._weights = hf_hub_download(self.spec.repo, self.spec.file, revision=self.spec.revision)
        self._start()

    def _start(self) -> None:
        port = _free_port()
        self._url = f"http://127.0.0.1:{port}"
        command = [
            str(runtime.server_binary()),
            *("--model", self._weights, "--host", "127.0.0.1", "--port", str(port)),
            *("--ctx-size", str(CONTEXT_TOKENS), "--parallel", "1"),
            # Thinking goes to a separate field, never into the text.
            *("--reasoning-format", "deepseek", "--no-webui"),
        ]
        logger.info("starting llama.cpp %s with %s", runtime.BUILD, self.spec.file)
        # Its output goes to this server's terminal, where its errors are seen.
        self._process = subprocess.Popen(  # noqa: S603 -- our pinned binary, fixed arguments
            command,
            env={**os.environ, "LD_LIBRARY_PATH": runtime.library_path()},
            preexec_fn=_die_with_parent(),
        )
        try:
            deadline = time.monotonic() + LOAD_TIMEOUT_S
            while not self._healthy():
                if self._process.poll() is not None:
                    raise RuntimeError(f"llama-server exited with code {self._process.returncode}")
                if time.monotonic() > deadline:
                    raise RuntimeError(f"llama-server did not load in {LOAD_TIMEOUT_S} s")
                time.sleep(1)
        except BaseException:
            self.close()  # never leave a half-started model holding the GPU
            raise

    def close(self) -> None:
        process = self._process
        if process is None or process.poll() is not None:
            return
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        source = None
        if "text" in job.inputs:
            try:
                source = job.inputs["text"].data.decode("utf-8-sig")
            except UnicodeDecodeError as error:
                raise InvalidInput(f"the text is not UTF-8: {error}") from error
        if self._process is not None and self._process.poll() is not None:
            # It crashed (e.g. the GPU memory it had was taken): start it again once.
            logger.warning("llama-server had exited (%s); restarting", self._process.returncode)
            try:
                self._start()
            except Exception as error:  # e.g. OSError: the binary is gone
                raise GenerationError(f"llama-server cannot start again: {error}") from error
        chat = messages(job.prompt or "", source, job.params.system)
        return [self._complete(chat, job.params, seed) for seed in job.seeds]

    def _complete(self, chat: list[dict[str, str]], params: Params, seed: int) -> Output:
        thinking = params.thinking and self.spec.thinking
        template: dict[str, Any] = {"enable_thinking": thinking}
        if thinking:
            template.update(self.spec.thinking_kwargs)
        body: dict[str, Any] = {
            "messages": chat,
            "max_tokens": params.max_tokens + (THINKING_TOKENS if thinking else 0),
            "temperature": params.temperature,
            "top_k": params.top_k,
            "top_p": params.top_p,
            "min_p": 0.0,  # the model cards' setting, not llama.cpp's 0.05
            "seed": llama_seed(seed),
            # A cached prefix is evaluated in other batch shapes than a fresh
            # one: the same seed must give the same text.
            "cache_prompt": False,
            "chat_template_kwargs": template,
        }
        reply = self._post("/v1/chat/completions", body)
        choice = reply["choices"][0]
        message = choice["message"]
        text = canonical_text(message.get("content") or "")
        if text == "\n":
            raise GenerationError(
                "the model returned no text (raise the length if it spent it on thinking)"
            )
        timings = reply.get("timings", {})
        meta: dict[str, Any] = {
            "tokens": int(timings.get("predicted_n", 0)),
            "tokens_per_s": round(float(timings.get("predicted_per_second", 0.0)), 1),
            "prompt_tokens": int(timings.get("prompt_n", 0)),
            # "length": cut off at the length limit.
            "finish_reason": choice.get("finish_reason"),
            "thought": bool(message.get("reasoning_content")),
        }
        return Output("text/plain", text.encode(), meta)

    def _healthy(self) -> bool:
        try:
            with urllib.request.urlopen(f"{self._url}/health", timeout=5) as response:  # noqa: S310
                return response.status == 200
        except (urllib.error.URLError, OSError):
            return False

    def _post(self, path: str, body: dict[str, Any]) -> dict[str, Any]:
        request = urllib.request.Request(  # noqa: S310 -- the child on loopback
            f"{self._url}{path}",
            data=json.dumps(body).encode(),
            headers={"Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT_S) as response:  # noqa: S310
                return json.loads(response.read())
        except urllib.error.HTTPError as error:
            detail = error.read().decode(errors="replace")[:500]
            if error.code == 400 and "exceed_context_size" in detail:
                # The user's text and task do not fit the context: their request.
                raise InvalidInput(f"the text is too long for the model: {detail}") from error
            raise GenerationError(f"llama-server answered {error.code}: {detail}") from error
        except (urllib.error.URLError, OSError) as error:
            exited = self._process is not None and self._process.poll() is not None
            raise GenerationError(
                "llama-server has exited; the next request starts it again"
                if exited
                else f"llama-server did not answer: {error}",
                retryable=True,
            ) from error


def _free_port() -> int:
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        return probe.getsockname()[1]


def _die_with_parent() -> Callable[[], None]:
    """A pre-exec hook: the child gets SIGTERM when this server process dies,
    so even a killed server never leaves the model holding the GPU.

    PR_SET_PDEATHSIG fires when the *thread* that forked exits. The child is
    started from ``load()`` or ``generate()``, which the SDK runs on its model
    thread for the whole life of the process; it must stay that way.
    The C function is looked up here, in the parent: loading a library in the
    forked child of a threaded process can deadlock.
    """
    prctl = ctypes.CDLL(None, use_errno=True).prctl
    parent = os.getpid()
    pr_set_pdeathsig = 1

    def hook() -> None:
        if prctl(pr_set_pdeathsig, signal.SIGTERM) != 0:
            os._exit(1)
        if os.getppid() != parent:  # the parent died before prctl took effect
            os._exit(1)

    return hook
