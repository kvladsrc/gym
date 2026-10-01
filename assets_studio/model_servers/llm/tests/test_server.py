"""Declarations, prompting and the bridge to llama-server, against a stand-in
for llama-server (no GPU, no weights)."""

import json
import threading
from collections.abc import Generator
from http.server import BaseHTTPRequestHandler, HTTPServer
from typing import Any, ClassVar

import pytest
from llm_model_server import runtime
from llm_model_server.models import MODELS
from llm_model_server.server import (
    DEFAULT_SYSTEM,
    LlmServer,
    Params,
    canonical_text,
    llama_seed,
    messages,
    params_for,
)

from model_server_sdk import GenerationError, InputData, InvalidInput, Job, create_app


def test_declarations_pass_the_sdk_validation() -> None:
    for spec in MODELS.values():
        create_app(LlmServer(spec))


def test_runtime_is_pinned_with_checksums() -> None:
    assert all(len(archive.sha256) == 64 for archive in runtime.ARCHIVES)
    assert runtime.BUILD in runtime.server_binary().parts[-2]


def test_messages() -> None:
    assert messages("Имя для таверны", None, "sys") == [
        {"role": "system", "content": "sys"},
        {"role": "user", "content": "Имя для таверны"},
    ]
    [_, user] = messages("Сократи", "Длинный текст.", DEFAULT_SYSTEM)
    assert user["content"].startswith("Сократи\n\nВерни только изменённый текст")
    assert user["content"].endswith("<текст>\nДлинный текст.\n</текст>")
    assert messages("x", None, "  ") == [{"role": "user", "content": "x"}]


class _Llama(BaseHTTPRequestHandler):
    requests: ClassVar[list[dict[str, Any]]] = []
    reply: ClassVar[dict[str, Any]] = {}
    status: ClassVar[int] = 200

    def do_POST(self) -> None:
        body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        type(self).requests.append(body)
        payload = json.dumps(type(self).reply).encode()
        self.send_response(type(self).status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def log_message(self, format: str, *args: Any) -> None:
        pass


class _Stub:
    """A stand-in for llama-server: records requests, answers ``reply``."""

    def __init__(self, url: str) -> None:
        self.url = url

    @property
    def requests(self) -> list[dict[str, Any]]:
        return _Llama.requests

    @property
    def reply(self) -> dict[str, Any]:
        return _Llama.reply

    @reply.setter
    def reply(self, value: dict[str, Any]) -> None:
        _Llama.reply = value

    @property
    def status(self) -> int:
        return _Llama.status

    @status.setter
    def status(self, value: int) -> None:
        _Llama.status = value


@pytest.fixture
def llama() -> Generator[_Stub]:
    _Llama.requests = []
    _Llama.status = 200
    _Llama.reply = {
        "choices": [{"message": {"content": "Таверна «Сонный гусь»\r\n"}, "finish_reason": "stop"}],
        "usage": {"completion_tokens": 7},
    }
    httpd = HTTPServer(("127.0.0.1", 0), _Llama)
    thread = threading.Thread(target=httpd.serve_forever, daemon=True)
    thread.start()
    yield _Stub(f"http://127.0.0.1:{httpd.server_address[1]}")
    httpd.shutdown()
    httpd.server_close()


def _server(stub: _Stub, model: str = "qwen3.8-27b") -> LlmServer:
    server = LlmServer(MODELS[model])
    server._url = stub.url
    return server


def _job(seeds: tuple[int, ...] = (1, 2), model: str = "qwen3.8-27b", **params: Any) -> Job:
    return Job(
        task="text-to-text",
        prompt="Название таверны",
        inputs={},
        params=params_for(MODELS[model])(**params),
        seeds=seeds,
    )


def test_each_variant_is_a_request_with_its_seed(llama: _Stub) -> None:
    outputs = _server(llama).generate(_job())
    assert [request["seed"] for request in llama.requests] == [1, 2]
    first = llama.requests[0]
    assert first["chat_template_kwargs"] == {"enable_thinking": False}
    assert (first["temperature"], first["top_k"], first["min_p"]) == (1.0, 20, 0.0)
    assert first["cache_prompt"] is False
    assert first["max_tokens"] == 1024
    assert first["messages"][-1] == {"role": "user", "content": "Название таверны"}
    assert outputs[0].data == "Таверна «Сонный гусь»\n".encode()
    assert outputs[0].meta["finish_reason"] == "stop"


def test_thinking_and_source_text(llama: _Stub) -> None:
    job = Job(
        task="text-to-text",
        prompt="Сократи",
        inputs={"text": InputData("text/plain", "Очень длинное описание.\n".encode())},
        params=params_for(MODELS["qwen3.8-27b"])(thinking=True),
        seeds=(5,),
    )
    _server(llama).generate(job)
    request = llama.requests[0]
    assert request["chat_template_kwargs"] == {"enable_thinking": True, "reasoning_effort": "low"}
    assert request["max_tokens"] == 1024 + 4096
    assert "Очень длинное описание." in request["messages"][-1]["content"]


def test_empty_answer_is_an_error(llama: _Stub) -> None:
    llama.reply["choices"][0]["message"]["content"] = "  "
    with pytest.raises(GenerationError, match="no text"):
        _server(llama).generate(_job(seeds=(1,)))


def test_llama_errors_become_generation_errors(llama: _Stub) -> None:
    llama.status = 500
    llama.reply = {"error": "boom"}
    with pytest.raises(GenerationError, match="answered 500"):
        _server(llama).generate(_job(seeds=(1,)))


def test_rejected_request_is_invalid_input(llama: _Stub) -> None:
    llama.status = 400
    llama.reply = {"error": {"type": "exceed_context_size_error"}}
    with pytest.raises(InvalidInput, match="exceed_context_size_error"):
        _server(llama).generate(_job(seeds=(1,)))


def test_other_rejections_are_not_blamed_on_the_user(llama: _Stub) -> None:
    llama.status = 400
    llama.reply = {"error": {"type": "invalid_request_error", "message": "unknown kwarg"}}
    with pytest.raises(GenerationError, match="answered 400"):
        _server(llama).generate(_job(seeds=(1,)))


def test_gemma_thinking_has_no_effort_setting(llama: _Stub) -> None:
    _server(llama, "gemma-4-12b").generate(_job(seeds=(1,), model="gemma-4-12b", thinking=True))
    assert llama.requests[0]["chat_template_kwargs"] == {"enable_thinking": True}
    assert llama.requests[0]["top_k"] == 64


def test_text_that_is_not_utf8_is_invalid_input(llama: _Stub) -> None:
    job = Job(
        task="text-to-text",
        prompt="x",
        inputs={"text": InputData("text/plain", b"\xff\xfe broken")},
        params=Params(),
        seeds=(1,),
    )
    with pytest.raises(InvalidInput, match="not UTF-8"):
        _server(llama).generate(job)


def test_meta_comes_from_llama_timings(llama: _Stub) -> None:
    llama.reply["timings"] = {"predicted_n": 7, "predicted_per_second": 3.14, "prompt_n": 40}
    [output] = _server(llama).generate(_job(seeds=(1,)))
    assert output.meta["tokens"] == 7
    assert output.meta["tokens_per_s"] == 3.1
    assert output.meta["prompt_tokens"] == 40
    assert output.meta["thought"] is False


def test_sampling_defaults_follow_the_model_card() -> None:
    gemma = params_for(MODELS["gemma-4-12b"]).model_json_schema()["properties"]
    qwen = params_for(MODELS["qwen3.8-27b"]).model_json_schema()["properties"]
    assert (gemma["top_k"]["default"], qwen["top_k"]["default"]) == (64, 20)
    assert gemma["temperature"]["x-primary"] is True


def test_canonical_text() -> None:
    assert canonical_text("\ufeffa\r\nb\rc\x07\x85 \n") == "a\nb\nc\n"
    assert canonical_text("  ") == "\n"


def test_llama_random_seed_is_avoided() -> None:
    assert llama_seed(0xFFFFFFFF) == 0
    assert llama_seed(42) == 42


def test_unreachable_llama_is_retryable() -> None:
    server = LlmServer(MODELS["gemma-4-12b"])
    server._url = "http://127.0.0.1:9"
    with pytest.raises(GenerationError) as raised:
        server.generate(_job(seeds=(1,), model="gemma-4-12b"))
    assert raised.value.retryable
