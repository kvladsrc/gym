"""Contract behaviour of model_server_sdk, independent of the fake server."""

import base64
import threading
import time
from collections.abc import Callable, Generator, Sequence
from contextlib import contextmanager
from enum import StrEnum
from typing import Any, Literal

import httpx2 as httpx
import pytest
from fastapi.testclient import TestClient
from model_server_sdk.contract import ErrorResponse, GenerateResponse, Info
from pydantic import BaseModel, Field
from support import live_server, wait_until_loaded

from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    create_app,
)


class EchoParams(BaseModel):
    repeat: int = Field(default=1, ge=1, le=3)


class NoDefaultParams(BaseModel):
    size: int


class FactoryDefaultParams(BaseModel):
    tags: str = Field(default_factory=str)


class AliasParams(BaseModel):
    guidance: float = Field(default=1.0, alias="guidance-scale")


class NestedParams(BaseModel):
    sizes: list[int] = Field(default_factory=lambda: [1])


class Mode(StrEnum):
    FAST = "fast"


class EnumParams(BaseModel):
    mode: Mode = Mode.FAST


class OptionalParams(BaseModel):
    negative: str | None = None


class ChoiceParams(BaseModel):
    """Everything the rules allow."""

    steps: int = 4
    scale: float = 1.5
    tiled: bool = False
    name: str = ""
    mode: Literal["fast", "slow"] = "fast"


ECHO = TaskSpec("echo", EchoParams, ("text/plain",), prompt="optional", max_count=3)
UPPER = TaskSpec(
    "upper",
    EchoParams,
    ("text/plain",),
    prompt="none",
    inputs=(InputSpec(role="text", mime=["text/*"]),),
)


class EchoServer(ModelServer):
    """Echoes the prompt (or uppercases an input) once per seed."""

    def __init__(
        self,
        *,
        load: Callable[[], None] = lambda: None,
        behaviour: Callable[[Job], Sequence[Output] | None] = lambda _: None,
    ) -> None:
        self._load = load
        self._behaviour = behaviour

    model = ModelInfo(id="echo", name="Echo")
    tasks: Sequence[TaskSpec] = (ECHO, UPPER)

    def load(self) -> None:
        self._load()

    def generate(self, job: Job) -> Sequence[Output]:
        override = self._behaviour(job)
        if override is not None:
            return override
        assert isinstance(job.params, EchoParams)
        upper = job.task == "upper"
        text = job.inputs["text"].data.decode().upper() if upper else job.prompt or ""
        return [
            Output("text/plain", f"{text * job.params.repeat}#{seed}".encode(), {"n": index})
            for index, seed in enumerate(job.seeds)
        ]


@contextmanager
def client_for(server: ModelServer) -> Generator[TestClient]:
    with TestClient(create_app(server)) as client:
        yield client


def b64(data: bytes) -> str:
    return base64.b64encode(data).decode()


def assert_error(response: httpx.Response, status: int, code: str, *, retryable: bool) -> str:
    assert response.status_code == status, response.text
    error = ErrorResponse.model_validate(response.json()).error
    assert (error.code, error.retryable) == (code, retryable)
    return error.message


@pytest.fixture
def client() -> Generator[TestClient]:
    with client_for(EchoServer()) as client:
        assert wait_until_loaded(client) == "ready"
        yield client


def test_info_describes_tasks(client: TestClient) -> None:
    info = Info.model_validate(client.get("/v1/info").json())
    assert info.contract == 1
    assert info.status == "ready"
    assert info.model.id == "echo"
    echo, upper = info.tasks
    assert (echo.task, echo.prompt, echo.max_count, echo.inputs) == ("echo", "optional", 3, [])
    assert echo.params_schema["properties"]["repeat"]["default"] == 1
    assert upper.inputs[0].role == "text"


def test_generate_returns_one_output_per_seed(client: TestClient) -> None:
    response = client.post(
        "/v1/generate",
        json={
            "task": "echo",
            "prompt": "мох",
            "count": 3,
            "seed": 2**32 - 2,
            "params": {"repeat": 2},
        },
    )
    assert response.status_code == 200, response.text
    body = GenerateResponse.model_validate(response.json())
    assert body.seed == 2**32 - 2
    assert body.model.id == "echo"
    seeds = [output.meta["seed"] for output in body.outputs]
    assert seeds == [2**32 - 2, 2**32 - 1, 0]  # wraps around modulo 2**32
    assert [output.meta["n"] for output in body.outputs] == [0, 1, 2]
    assert base64.b64decode(body.outputs[0].data_b64).decode() == f"мохмох#{2**32 - 2}"


def test_seed_is_chosen_when_omitted(client: TestClient) -> None:
    body = GenerateResponse.model_validate(
        client.post("/v1/generate", json={"task": "echo", "count": 2}).json()
    )
    assert [output.meta["seed"] for output in body.outputs] == [body.seed, (body.seed + 1) % 2**32]


def test_inputs_are_decoded_by_role(client: TestClient) -> None:
    response = client.post(
        "/v1/generate",
        json={
            "task": "upper",
            "inputs": [{"role": "text", "mime": "text/plain", "data_b64": b64(b"abc")}],
            "seed": 7,
        },
    )
    body = GenerateResponse.model_validate(response.json())
    assert base64.b64decode(body.outputs[0].data_b64) == b"ABC#7"


TEXT_INPUT = {"role": "text", "mime": "text/plain", "data_b64": b64(b"abc")}


@pytest.mark.parametrize(
    ("body", "fragment"),
    [
        ({"task": "echo", "count": 0}, "count"),
        ({"task": "echo", "count": 4}, "max_count"),
        ({"task": "echo", "seed": -1}, "seed"),
        ({"task": "echo", "surprise": 1}, "surprise"),
        ({"task": "echo", "params": {"repeat": 9}}, "params.repeat"),
        ({"task": "echo", "params": {"colour": 1}}, "unknown parameters: colour"),
        ({"task": "upper"}, "missing inputs: text"),
        ({"task": "upper", "prompt": "x", "inputs": [TEXT_INPUT]}, "does not accept a prompt"),
        ({"task": "upper", "inputs": [TEXT_INPUT, TEXT_INPUT]}, "duplicate input role"),
        ({"task": "upper", "inputs": [{**TEXT_INPUT, "role": "image"}]}, "unknown input role"),
        ({"task": "upper", "inputs": [{**TEXT_INPUT, "mime": "image/png"}]}, "not one of"),
        ({"task": "upper", "inputs": [{**TEXT_INPUT, "data_b64": "@@@"}]}, "invalid base64"),
        ({"task": "upper", "inputs": [{**TEXT_INPUT, "data_b64": ""}]}, "empty file"),
    ],
)
def test_invalid_requests_are_rejected(
    client: TestClient, body: dict[str, Any], fragment: str
) -> None:
    message = assert_error(
        client.post("/v1/generate", json=body), 422, "invalid_request", retryable=False
    )
    assert fragment in message


def test_required_prompt_must_not_be_blank() -> None:
    required = TaskSpec("say", EchoParams, ("text/plain",))

    class SayServer(EchoServer):
        tasks = (required,)

    with client_for(SayServer()) as client:
        wait_until_loaded(client)
        response = client.post("/v1/generate", json={"task": "say", "prompt": "   "})
        assert "requires a prompt" in assert_error(
            response, 422, "invalid_request", retryable=False
        )


def test_unknown_task(client: TestClient) -> None:
    assert_error(
        client.post("/v1/generate", json={"task": "paint"}),
        400,
        "unsupported_task",
        retryable=False,
    )


def test_not_ready_while_loading() -> None:
    release = threading.Event()

    def load() -> None:
        release.wait(5)

    with client_for(EchoServer(load=load)) as client:
        assert client.get("/v1/info").json()["status"] == "loading"
        response = client.post("/v1/generate", json={"task": "echo"})
        assert_error(response, 503, "not_ready", retryable=True)
        release.set()
        assert wait_until_loaded(client) == "ready"


def test_load_failure_is_reported() -> None:
    def fail() -> None:
        raise OSError("weights not found")

    with client_for(EchoServer(load=fail)) as client:
        assert wait_until_loaded(client) == "error"
        info = client.get("/v1/info").json()
        assert info["status_message"] == "OSError: weights not found"
        message = assert_error(
            client.post("/v1/generate", json={"task": "echo"}), 503, "not_ready", retryable=False
        )
        assert "weights not found" in message


Behaviour = Callable[[Job], Sequence[Output] | None]


def raising(error: BaseException) -> Behaviour:
    def behaviour(_: Job) -> Sequence[Output] | None:
        raise error

    return behaviour


def returning_raw(value: object) -> Behaviour:
    def behaviour(_: Job) -> Sequence[Output] | None:
        return value  # pyright: ignore[reportReturnType]

    return behaviour


def returning(mime: str, data: bytes, *, per_seed: bool = True) -> Behaviour:
    def behaviour(job: Job) -> Sequence[Output] | None:
        return [Output(mime, data)] * (len(job.seeds) if per_seed else 0)

    return behaviour


@pytest.mark.parametrize(
    ("behaviour", "status", "code", "retryable"),
    [
        (raising(GenerationError("out of memory", retryable=True)), 500, "generation_failed", True),
        (raising(GenerationError("bad mesh")), 500, "generation_failed", False),
        (raising(InvalidInput("image too small")), 422, "invalid_request", False),
        (raising(ZeroDivisionError("oops")), 500, "internal", False),
        (raising(SystemExit(2)), 500, "internal", False),
        (returning("text/plain", b"x", per_seed=False), 500, "internal", False),
        (returning("image/png", b"x"), 500, "internal", False),
        (returning("text/plain", b""), 500, "internal", False),
        (returning_raw(42), 500, "internal", False),
        (returning_raw(["text"]), 500, "internal", False),
        (returning_raw([Output("text/plain", "text")]), 500, "internal", False),  # pyright: ignore[reportArgumentType]
        (returning_raw([Output("text/plain", b"x", {"bad": object()})]), 500, "internal", False),
    ],
)
def test_implementation_errors(
    behaviour: Callable[[Job], Sequence[Output] | None], status: int, code: str, retryable: bool
) -> None:
    with client_for(EchoServer(behaviour=behaviour)) as client:
        wait_until_loaded(client)
        response = client.post("/v1/generate", json={"task": "echo"})
        assert_error(response, status, code, retryable=retryable)
        # A failed generation must not leave the server stuck in "busy".
        assert client.get("/v1/info").json()["status"] == "ready"


def test_concurrent_request_gets_busy() -> None:
    started, release = threading.Event(), threading.Event()

    def block(_: Job) -> None:
        started.set()
        release.wait(5)

    with live_server(create_app(EchoServer(behaviour=block))) as url:
        wait_for = httpx.get(f"{url}/v1/info").json()
        assert wait_for["status"] in ("loading", "ready")
        with httpx.Client(base_url=url, timeout=10) as http:
            wait_until_ready(http)
            first: dict[str, httpx.Response] = {}
            thread = threading.Thread(
                target=lambda: first.update(
                    response=http.post("/v1/generate", json={"task": "echo"})
                )
            )
            thread.start()
            assert started.wait(5)
            assert http.get("/v1/info").json()["status"] == "busy"
            assert_error(
                http.post("/v1/generate", json={"task": "echo"}), 409, "busy", retryable=True
            )
            release.set()
            thread.join(5)
            assert first["response"].status_code == 200
            assert http.get("/v1/info").json()["status"] == "ready"


def test_server_must_declare_model_and_tasks() -> None:
    class Incomplete(ModelServer):
        def load(self) -> None: ...

        def generate(self, job: Job) -> Sequence[Output]:
            return []

    with pytest.raises(TypeError, match="must define 'model'"):
        create_app(Incomplete())


@pytest.mark.parametrize(
    ("params", "inputs", "max_count", "fragment"),
    [
        (NoDefaultParams, (), 1, "size: no default"),
        (FactoryDefaultParams, (), 1, "tags: no default"),
        (AliasParams, (), 1, "aliases are not supported"),
        (NestedParams, (), 1, "sizes: not a flat scalar"),
        (EnumParams, (), 1, "use Literal"),
        (OptionalParams, (), 1, "negative: not a flat scalar"),
        (EchoParams, (InputSpec(role="a", mime=["text/*"]),) * 2, 1, "duplicate input roles"),
        (EchoParams, (InputSpec(role="a", mime=["image/jpeg"]),), 1, "a: must accept image/png"),
        (EchoParams, (InputSpec(role="a", mime=["audio/mpeg"]),), 1, "a: must accept audio/wav"),
        (EchoParams, (), 0, "max_count"),
    ],
)
def test_invalid_declarations_fail_at_startup(
    params: type[BaseModel], inputs: tuple[InputSpec, ...], max_count: int, fragment: str
) -> None:
    with pytest.raises(TypeError, match=fragment):
        TaskSpec("bad", params, ("text/plain",), inputs=inputs, max_count=max_count)


def test_valid_declarations() -> None:
    TaskSpec("ok", ChoiceParams, ("text/plain",))
    TaskSpec("ok", EchoParams, ("text/plain",), inputs=(InputSpec(role="a", mime=["image/*"]),))
    image = InputSpec(role="a", mime=["image/png", "image/jpeg"])
    TaskSpec("ok", EchoParams, ("text/plain",), inputs=(image,))


def test_response_reports_effective_params(client: TestClient) -> None:
    body = client.post("/v1/generate", json={"task": "echo"}).json()
    assert body["params"] == {"repeat": 1}
    body = client.post("/v1/generate", json={"task": "echo", "params": {"repeat": 3}}).json()
    assert body["params"] == {"repeat": 3}


def test_unknown_path_uses_error_shape(client: TestClient) -> None:
    assert_error(client.get("/v1/nothing"), 404, "invalid_request", retryable=False)
    assert_error(client.get("/v1/generate"), 405, "invalid_request", retryable=False)


def test_load_and_generate_share_one_thread() -> None:
    threads: list[int] = []

    def record(_: Job) -> None:
        threads.append(threading.get_ident())

    def load() -> None:
        threads.append(threading.get_ident())

    with client_for(EchoServer(load=load, behaviour=record)) as client:
        wait_until_loaded(client)
        client.post("/v1/generate", json={"task": "echo"})
        client.post("/v1/generate", json={"task": "echo"})
    assert len(threads) == 3
    assert len(set(threads)) == 1
    assert threads[0] != threading.get_ident()


def test_system_exit_during_load_is_reported() -> None:
    def load() -> None:
        raise SystemExit(3)

    with client_for(EchoServer(load=load)) as client:
        assert wait_until_loaded(client) == "error"
        assert client.get("/v1/info").json()["status_message"] == "SystemExit: 3"


def test_client_disconnect_keeps_generation_exclusive() -> None:
    """The lock is held until the model finishes, even if the client gave up."""
    started, release, finished = threading.Event(), threading.Event(), threading.Event()

    def block(_: Job) -> None:
        started.set()
        release.wait(5)
        finished.set()

    with (
        live_server(create_app(EchoServer(behaviour=block))) as url,
        httpx.Client(base_url=url, timeout=10) as http,
    ):
        wait_until_ready(http)
        with pytest.raises(httpx.ReadTimeout):
            http.post("/v1/generate", json={"task": "echo"}, timeout=0.3)
        assert started.is_set()
        assert http.get("/v1/info").json()["status"] == "busy"
        assert_error(http.post("/v1/generate", json={"task": "echo"}), 409, "busy", retryable=True)
        release.set()
        assert finished.wait(5)
        wait_until_ready(http)
        assert http.post("/v1/generate", json={"task": "echo"}).status_code == 200


def wait_until_ready(http: httpx.Client, timeout_s: float = 5) -> None:
    deadline = time.monotonic() + timeout_s
    while http.get("/v1/info").json()["status"] != "ready":
        assert time.monotonic() < deadline, "server did not become ready"
        time.sleep(0.01)


def test_openapi_documents_contract(client: TestClient) -> None:
    schema = client.get("/openapi.json").json()
    responses = schema["paths"]["/v1/generate"]["post"]["responses"]
    assert {"200", "400", "409", "422", "500", "503"} <= set(responses)


def test_out_of_memory_detection() -> None:
    from model_server_sdk import is_out_of_memory

    class OutOfMemoryError(RuntimeError):  # the name torch.cuda uses
        pass

    assert is_out_of_memory(OutOfMemoryError("CUDA out of memory. Tried to allocate 2 GiB"))
    assert is_out_of_memory(RuntimeError("CUBLAS_STATUS_ALLOC_FAILED when calling cublasCreate"))
    assert not is_out_of_memory(RuntimeError("shape mismatch"))
