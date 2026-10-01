"""Turns a ``ModelServer`` implementation into a contract-compliant HTTP app.

An implementation declares its tasks and provides ``load`` and ``generate``.
Everything the contract promises — background loading, validation, error
codes, seeds, one generation at a time — is handled here.
"""

import asyncio
import base64
import binascii
import logging
import queue
import random
import threading
import time
from abc import ABC, abstractmethod
from collections.abc import AsyncGenerator, Callable, Mapping, Sequence
from concurrent.futures import Future
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from typing import Any, TypeVar

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel, ValidationError
from starlette.exceptions import HTTPException as StarletteHTTPException

from model_server_sdk.contract import (
    CONTRACT_VERSION,
    SEED_MODULUS,
    ErrorCode,
    ErrorDetail,
    ErrorResponse,
    GenerateRequest,
    GenerateResponse,
    Info,
    InputSpec,
    ModelInfo,
    OutputFile,
    PromptUsage,
    ServerStatus,
    TaskInfo,
    Timing,
    input_spec_problems,
    mime_matches,
    output_seed,
    params_schema_problems,
)

logger = logging.getLogger("model_server_sdk")

_R = TypeVar("_R")


@dataclass(frozen=True)
class TaskSpec:
    """A task the server can perform.

    ``params`` is a pydantic model of flat scalar fields (``str``, ``int``,
    ``float``, ``bool`` or ``Literal``), each with a default, so that the
    studio can render a form and a request without parameters is valid.
    """

    task: str
    params: type[BaseModel]
    output_mime: tuple[str, ...]
    prompt: PromptUsage = "required"
    inputs: tuple[InputSpec, ...] = ()
    max_count: int = 4

    def __post_init__(self) -> None:
        problems = params_schema_problems(self.params.model_json_schema())
        problems += [
            f"{name}: aliases are not supported"
            for name, info in self.params.model_fields.items()
            if info.alias or info.validation_alias or info.serialization_alias
        ]
        for spec in self.inputs:
            problems += input_spec_problems(spec)
        roles = [spec.role for spec in self.inputs]
        if len(roles) != len(set(roles)):
            problems.append("duplicate input roles")
        if self.max_count < 1:
            problems.append("max_count must be at least 1")
        if problems:
            raise TypeError(f"{self.task}: {'; '.join(problems)}")
        # Validate the declaration against the wire schema once, at startup.
        self.info()

    def info(self) -> TaskInfo:
        return TaskInfo(
            task=self.task,
            prompt=self.prompt,
            inputs=list(self.inputs),
            params_schema=self.params.model_json_schema(),
            max_count=self.max_count,
            output_mime=list(self.output_mime),
        )


@dataclass(frozen=True)
class InputData:
    mime: str
    data: bytes


@dataclass(frozen=True)
class Job:
    """A validated request, as seen by ``ModelServer.generate``.

    ``seeds[i]`` is the seed for the ``i``-th output; ``len(seeds)`` is the
    number of outputs to produce. Each output must depend on its own seed
    only: a request for ``seeds[i]`` alone must reproduce output ``i``.
    """

    task: str
    prompt: str | None
    inputs: Mapping[str, InputData]
    params: BaseModel
    seeds: tuple[int, ...]


@dataclass(frozen=True)
class Output:
    mime: str
    data: bytes
    meta: Mapping[str, Any] = field(default_factory=dict[str, Any])


class GenerationError(Exception):
    """The model could not fulfil a valid request."""

    def __init__(self, message: str, *, retryable: bool = False) -> None:
        super().__init__(message)
        self.retryable = retryable


class InvalidInput(Exception):
    """A request passed contract validation but the model cannot accept it."""


class ModelServer(ABC):
    """Base class for a model server.

    Subclasses set ``model`` and ``tasks`` as class attributes or in
    ``__init__``. ``load`` and every ``generate`` call run one at a time on
    the same dedicated thread, so thread-local setup made in ``load`` (for
    example ``torch.set_grad_enabled(False)``) stays in effect.
    """

    model: ModelInfo
    tasks: Sequence[TaskSpec]

    @abstractmethod
    def load(self) -> None:
        """Load weights. Called once, in the background, after startup."""

    @abstractmethod
    def generate(self, job: Job) -> Sequence[Output]:
        """Produce exactly ``len(job.seeds)`` outputs, in seed order."""


class _ContractError(Exception):
    def __init__(self, status: int, code: ErrorCode, message: str, *, retryable: bool) -> None:
        super().__init__(message)
        self.status = status
        self.detail = ErrorDetail(code=code, message=message, retryable=retryable)


def _invalid(message: str, status: int = 422) -> _ContractError:
    return _ContractError(status, "invalid_request", message, retryable=False)


def _internal(message: str) -> _ContractError:
    return _ContractError(500, "internal", message, retryable=False)


def _error_response(error: _ContractError) -> JSONResponse:
    return JSONResponse(
        ErrorResponse(error=error.detail).model_dump(mode="json"), status_code=error.status
    )


class _ModelThread:
    """A single daemon thread that runs model calls in submission order.

    Unlike ``ThreadPoolExecutor``, the thread does not keep the interpreter
    alive: Ctrl+C stops the server even during a long ``load``.
    """

    def __init__(self) -> None:
        self._calls: queue.SimpleQueue[tuple[Callable[[], Any], Future[Any]]] = queue.SimpleQueue()
        threading.Thread(target=self._work, name="model", daemon=True).start()

    def submit(self, call: Callable[[], _R]) -> "Future[_R]":
        future: Future[_R] = Future()
        self._calls.put((call, future))
        return future

    def _work(self) -> None:
        while True:
            call, future = self._calls.get()
            if not future.set_running_or_notify_cancel():
                continue
            try:
                future.set_result(call())
            except BaseException as error:
                future.set_exception(error)


class _State:
    def __init__(self) -> None:
        self.loaded = False
        self.load_error: str | None = None
        self.generation = threading.Lock()

    @property
    def status(self) -> ServerStatus:
        if self.load_error is not None:
            return "error"
        if not self.loaded:
            return "loading"
        return "busy" if self.generation.locked() else "ready"


def create_app(server: ModelServer) -> FastAPI:
    """Build the HTTP application for ``server``."""
    for attribute in ("model", "tasks"):
        if not hasattr(server, attribute):
            raise TypeError(f"{type(server).__name__} must define {attribute!r}")
    specs = {spec.task: spec for spec in server.tasks}
    if len(specs) != len(server.tasks):
        raise TypeError("duplicate task declarations")
    model = ModelInfo.model_validate(server.model.model_dump())
    state = _State()
    # One thread for the model: load() and generate() share thread-local state.
    model_thread = _ModelThread()

    def load() -> None:
        started = time.monotonic()
        try:
            server.load()
        except BaseException as error:  # a library calling sys.exit must not hang "loading"
            logger.exception("model failed to load")
            state.load_error = f"{type(error).__name__}: {error}"
        else:
            state.loaded = True
            logger.info("model %s loaded in %.1f s", model.id, time.monotonic() - started)

    @asynccontextmanager
    async def lifespan(_: FastAPI) -> AsyncGenerator[None]:
        model_thread.submit(load)
        yield

    app = FastAPI(title=f"{model.name} — model server", lifespan=lifespan)

    @app.exception_handler(_ContractError)
    async def contract_error(_: Request, error: _ContractError) -> JSONResponse:  # pyright: ignore[reportUnusedFunction]
        return _error_response(error)

    @app.exception_handler(RequestValidationError)
    async def validation_error(_: Request, error: RequestValidationError) -> JSONResponse:  # pyright: ignore[reportUnusedFunction]
        return _error_response(_invalid(_describe(error.errors())))

    @app.exception_handler(StarletteHTTPException)
    async def http_error(_: Request, error: StarletteHTTPException) -> JSONResponse:  # pyright: ignore[reportUnusedFunction]
        return _error_response(_invalid(str(error.detail), status=error.status_code))

    @app.exception_handler(Exception)
    async def unexpected_error(_: Request, error: Exception) -> JSONResponse:  # pyright: ignore[reportUnusedFunction]
        logger.exception("unexpected error")
        return _error_response(_internal(f"{type(error).__name__}: {error}"))

    @app.get("/v1/info")
    def info() -> Info:  # pyright: ignore[reportUnusedFunction]
        return Info(
            contract=CONTRACT_VERSION,
            status=state.status,
            status_message=state.load_error,
            model=model,
            tasks=[spec.info() for spec in specs.values()],
        )

    error_responses: dict[int | str, dict[str, Any]] = {
        status: {"model": ErrorResponse} for status in (400, 409, 422, 500, 503)
    }

    @app.post("/v1/generate", responses=error_responses)
    async def generate(request: GenerateRequest) -> GenerateResponse:  # pyright: ignore[reportUnusedFunction]
        if state.load_error is not None:
            raise _ContractError(
                503, "not_ready", f"model failed to load: {state.load_error}", retryable=False
            )
        if not state.loaded:
            raise _ContractError(503, "not_ready", "model is loading", retryable=True)
        spec = specs.get(request.task)
        if spec is None:
            raise _ContractError(
                400, "unsupported_task", f"unsupported task: {request.task}", retryable=False
            )
        job = _build_job(spec, request)
        if not state.generation.acquire(blocking=False):
            raise _ContractError(409, "busy", "another generation is running", retryable=True)
        # The model thread releases the lock when generation really ends. If
        # the client disconnects, shield() lets this request be cancelled
        # while the generation (and the lock) run to completion.
        try:
            future = model_thread.submit(lambda: _run(server, spec, job))
        except BaseException:
            state.generation.release()
            raise
        future.add_done_callback(lambda _: state.generation.release())
        outcome = asyncio.wrap_future(future)
        # If the client has gone, nobody awaits the outcome: consume its
        # exception so asyncio does not log "exception was never retrieved".
        outcome.add_done_callback(lambda done: done.cancelled() or done.exception())
        return await asyncio.shield(outcome)

    def _run(server: ModelServer, spec: TaskSpec, job: Job) -> GenerateResponse:
        started = time.monotonic()
        try:
            outputs = server.generate(job)
        except GenerationError as error:
            raise _ContractError(
                500, "generation_failed", str(error), retryable=error.retryable
            ) from error
        except InvalidInput as error:
            raise _invalid(str(error)) from error
        except BaseException as error:  # SystemExit from a library must not escape the contract
            logger.exception("generation crashed")
            raise _internal(f"{type(error).__name__}: {error}") from error
        elapsed = time.monotonic() - started
        return _response(model, spec, job, outputs, elapsed)

    return app


def _build_job(spec: TaskSpec, request: GenerateRequest) -> Job:
    if spec.prompt == "required" and not (request.prompt and request.prompt.strip()):
        raise _invalid(f"{spec.task} requires a prompt")
    if spec.prompt == "none" and request.prompt is not None:
        raise _invalid(f"{spec.task} does not accept a prompt")
    if request.count > spec.max_count:
        raise _invalid(f"count {request.count} exceeds max_count {spec.max_count}")
    inputs = _decode_inputs(spec, request)
    unknown = sorted(set(request.params) - set(spec.params.model_fields))
    if unknown:
        raise _invalid(f"unknown parameters: {', '.join(unknown)}")
    try:
        params = spec.params.model_validate(request.params)
    except ValidationError as error:
        raise _invalid(_describe(error.errors(), prefix="params")) from error
    seed = request.seed if request.seed is not None else random.randrange(SEED_MODULUS)  # noqa: S311 (not security-related)
    return Job(
        task=spec.task,
        prompt=request.prompt,
        inputs=inputs,
        params=params,
        seeds=tuple(output_seed(seed, index) for index in range(request.count)),
    )


def _decode_inputs(spec: TaskSpec, request: GenerateRequest) -> dict[str, InputData]:
    declared = {input_spec.role: input_spec for input_spec in spec.inputs}
    inputs: dict[str, InputData] = {}
    for item in request.inputs:
        input_spec = declared.get(item.role)
        if input_spec is None:
            raise _invalid(f"unknown input role: {item.role}")
        if item.role in inputs:
            raise _invalid(f"duplicate input role: {item.role}")
        if not any(mime_matches(item.mime, pattern) for pattern in input_spec.mime):
            raise _invalid(f"input {item.role}: {item.mime} is not one of {input_spec.mime}")
        try:
            data = base64.b64decode(item.data_b64, validate=True)
        except binascii.Error as error:
            raise _invalid(f"input {item.role}: invalid base64") from error
        if not data:
            raise _invalid(f"input {item.role}: empty file")
        inputs[item.role] = InputData(mime=item.mime, data=data)
    missing = [role for role, input_spec in declared.items() if input_spec.required]
    missing = [role for role in missing if role not in inputs]
    if missing:
        raise _invalid(f"missing inputs: {', '.join(missing)}")
    return inputs


def _response(
    model: ModelInfo, spec: TaskSpec, job: Job, outputs: object, elapsed: float
) -> GenerateResponse:
    """Serialise the implementation's outputs; any contract violation is a server bug."""
    if not isinstance(outputs, Sequence):
        raise _internal(f"generate() returned {type(outputs).__name__}, expected a sequence")
    items: Sequence[object] = outputs  # pyright: ignore[reportUnknownVariableType]
    if len(items) != len(job.seeds):
        raise _internal(f"generate() returned {len(items)} outputs, expected {len(job.seeds)}")
    files: list[OutputFile] = []
    for index, (output, seed) in enumerate(zip(items, job.seeds, strict=True)):
        if not isinstance(output, Output):
            raise _internal(f"output {index} is {type(output).__name__}, expected Output")
        if not isinstance(output.data, bytes | bytearray) or not output.data:  # pyright: ignore[reportUnnecessaryIsInstance]
            raise _internal(f"output {index}: data must be non-empty bytes")
        if not any(mime_matches(output.mime, pattern) for pattern in spec.output_mime):
            raise _internal(f"output {index}: {output.mime} is not in {list(spec.output_mime)}")
        try:
            file = OutputFile(
                mime=output.mime,
                data_b64=base64.b64encode(output.data).decode("ascii"),
                meta={**output.meta, "seed": seed},
            )
            file.model_dump_json()  # meta must be JSON-serialisable (e.g. no numpy scalars)
        except (ValidationError, TypeError, ValueError) as error:
            raise _internal(f"output {index}: {error}") from error
        files.append(file)
    return GenerateResponse(
        model=model,
        task=spec.task,
        seed=job.seeds[0],
        params=job.params.model_dump(mode="json"),
        outputs=files,
        timing=Timing(generate_s=elapsed),
    )


def _describe(errors: Sequence[Any], prefix: str | None = None) -> str:
    parts: list[str] = []
    for error in errors:
        location = [str(part) for part in error.get("loc", ()) if part != "body"]
        if prefix:
            location.insert(0, prefix)
        parts.append(f"{'.'.join(location) or 'body'}: {error.get('msg', 'invalid')}")
    return "; ".join(parts)
