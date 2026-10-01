"""The contract checker: passes compliant servers, catches broken ones."""

import base64
import dataclasses
import random
from collections.abc import Callable, Generator
from contextlib import contextmanager
from typing import Any

import pytest
from fake_model_server import ALL_TASKS, FakeModelServer
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse, PlainTextResponse, Response
from fastapi.testclient import TestClient
from model_server_sdk.check import Options, Result, check
from model_server_sdk.contract import SEED_MODULUS
from support import live_server

from model_server_sdk import Job, Output, create_app

FAST = Options(wait_s=5, timeout_s=30, poll_s=0.05)


def run_check(app: FastAPI, options: Options = FAST) -> list[Result]:
    with live_server(app) as url:
        return check(url, options)


def levels(results: list[Result], prefix: str) -> set[str]:
    return {result.level for result in results if result.name.startswith(prefix)}


def failures(results: list[Result]) -> list[Result]:
    return [result for result in results if not result.passed]


def test_fake_server_passes_without_warnings() -> None:
    results = run_check(create_app(FakeModelServer(load_delay_s=0.2)))
    assert [result for result in results if result.level != "pass"] == []
    names = {result.name for result in results}
    for task in ALL_TASKS:
        assert {f"generate {task.task}", f"seed dependence {task.task}"} <= names
        assert f"determinism {task.task}" in names
        assert f"reproducibility {task.task}" in names
    assert "error: image-to-3d: unexpected prompt" in names
    assert "error: text-to-image: missing prompt" in names
    assert "error: unknown path" in names


def test_reports_load_failure() -> None:
    results = run_check(create_app(FakeModelServer(load_error="no weights")))
    assert [(result.name, result.level) for result in results] == [("info", "fail")]
    assert "no weights" in results[0].detail


def test_reports_missing_task() -> None:
    options = dataclasses.replace(FAST, only_tasks=frozenset({"image-to-3d"}))
    results = run_check(create_app(FakeModelServer(tasks=["text-to-image"])), options)
    assert levels(results, "tasks") == {"fail"}


def test_unreachable_server_fails_after_waiting() -> None:
    options = dataclasses.replace(FAST, wait_s=0.2)
    results = check("http://127.0.0.1:9", options)
    assert [(result.name, result.level) for result in results] == [("info", "fail")]
    assert "unreachable" in results[0].detail


def test_rejects_non_http_url() -> None:
    with pytest.raises(ValueError, match="http"):
        check("file:///etc/passwd", FAST)


class SeedBlindServer(FakeModelServer):
    """Ignores the seed: every candidate is the same."""

    def generate(self, job: Job) -> list[Output]:
        outputs = super().generate(dataclasses.replace(job, seeds=(0,) * len(job.seeds)))
        return outputs


class BatchGeneratorServer(FakeModelServer):
    """One generator for the whole batch: output i depends on seeds[0] and i."""

    def generate(self, job: Job) -> list[Output]:
        seeds = tuple((job.seeds[0] * 7 + i) % SEED_MODULUS for i in range(len(job.seeds)))
        return super().generate(dataclasses.replace(job, seeds=seeds))


class UnseededServer(FakeModelServer):
    """Ignores the seed and draws fresh randomness: nothing is reproducible."""

    def generate(self, job: Job) -> list[Output]:
        seeds = tuple(random.randrange(SEED_MODULUS) for _ in job.seeds)
        return super().generate(dataclasses.replace(job, seeds=seeds))


def test_unseeded_server_fails() -> None:
    options = dataclasses.replace(FAST, only_tasks=frozenset({"text-to-image"}))
    results = run_check(create_app(UnseededServer()), options)
    assert levels(results, "determinism") == {"fail"}
    lenient = dataclasses.replace(options, allow_nondeterministic=True)
    results = run_check(create_app(UnseededServer()), lenient)
    assert levels(results, "determinism") == {"warn"}
    assert levels(results, "seed dependence") == {"warn"}
    assert not failures(results)


def test_seed_blind_server_fails() -> None:
    options = dataclasses.replace(FAST, only_tasks=frozenset({"text-to-image"}))
    results = run_check(create_app(SeedBlindServer()), options)
    assert levels(results, "seed dependence") == {"fail"}
    lenient = dataclasses.replace(options, allow_seed_independent=True)
    results = run_check(create_app(SeedBlindServer()), lenient)
    assert levels(results, "seed dependence") == {"warn"}
    assert not failures(results)


def test_batch_generator_server_is_flagged() -> None:
    options = dataclasses.replace(FAST, only_tasks=frozenset({"text-to-audio"}))
    results = run_check(create_app(BatchGeneratorServer()), options)
    assert levels(results, "reproducibility") == {"warn"}
    assert levels(results, "seed dependence") == {"pass"}
    strict = dataclasses.replace(options, strict_determinism=True)
    assert levels(run_check(create_app(BatchGeneratorServer()), strict), "reproducibility") == {
        "fail"
    }


Mutation = Callable[[int, Any], tuple[int, Any] | Response]


@contextmanager
def proxy(mutate: Mutation) -> Generator[FastAPI]:
    """A server that forwards to the fake server and corrupts its answers."""
    with TestClient(create_app(FakeModelServer(tasks=["text-to-image"]))) as inner:
        app = FastAPI()

        @app.get("/v1/info")
        def info() -> Any:  # pyright: ignore[reportUnusedFunction]
            return inner.get("/v1/info").json()

        @app.post("/v1/generate")
        async def generate(request: Request) -> Response:  # pyright: ignore[reportUnusedFunction]
            response = inner.post("/v1/generate", json=await request.json())
            mutated = mutate(response.status_code, response.json())
            if isinstance(mutated, Response):
                return mutated
            status, body = mutated
            return JSONResponse(body, status_code=status)

        yield app


def on_success(change: Callable[[dict[str, Any]], None]) -> Mutation:
    def mutate(status: int, body: Any) -> tuple[int, Any]:
        if status == 200:
            change(body)
        return status, body

    return mutate


def drop_output(body: dict[str, Any]) -> None:
    body["outputs"].pop()


def set_mime(body: dict[str, Any]) -> None:
    body["outputs"][0]["mime"] = "image/jpeg"


def corrupt_data(body: dict[str, Any]) -> None:
    body["outputs"][0]["data_b64"] = base64.b64encode(b"not a png").decode()


def shift_meta_seed(body: dict[str, Any]) -> None:
    body["outputs"][-1]["meta"]["seed"] += 1


def change_params(body: dict[str, Any]) -> None:
    body["params"]["width"] = 999


def rename_model(body: dict[str, Any]) -> None:
    body["model"]["id"] = "other"


@pytest.mark.parametrize(
    ("change", "fragment"),
    [
        (drop_output, "outputs, requested"),
        (set_mime, "not in"),
        (corrupt_data, "content is not image/png"),
        (shift_meta_seed, "meta.seed"),
        (change_params, "expected the defaults"),
        (rename_model, "differs from /v1/info"),
    ],
)
def test_broken_responses_fail(change: Callable[[dict[str, Any]], None], fragment: str) -> None:
    with proxy(on_success(change)) as app:
        results = run_check(app)
    generate = [result for result in results if result.name == "generate text-to-image"]
    assert generate[0].level == "fail"
    assert fragment in generate[0].detail


def test_non_contract_error_bodies_fail() -> None:
    def plain_errors(status: int, body: Any) -> tuple[int, Any] | Response:
        if status == 200:
            return status, body
        return PlainTextResponse("Internal Server Error", status_code=500)

    with proxy(plain_errors) as app:
        results = run_check(app)
    error_probes = [result for result in results if result.name.startswith("error: ")]
    assert error_probes
    assert all(
        result.level == "fail" for result in error_probes if "unknown path" not in result.name
    )
    assert "got 500: Internal Server Error" in error_probes[0].detail


def test_invalid_declaration_fails_info() -> None:
    def info_with_bad_schema() -> FastAPI:
        app = FastAPI()

        @app.get("/v1/info")
        def info() -> Any:  # pyright: ignore[reportUnusedFunction]
            with TestClient(create_app(FakeModelServer(tasks=["text-to-image"]))) as inner:
                body = inner.get("/v1/info").json()
            body["status"] = "ready"
            del body["tasks"][0]["params_schema"]["properties"]["width"]["default"]
            return body

        return app

    results = run_check(info_with_bad_schema())
    assert [(result.name, result.level) for result in results] == [("info", "fail")]
    assert "width: no default" in results[0].detail


def test_parameters_can_replace_the_defaults() -> None:
    """For slow models: the check generates with the given parameters, and the
    server's echo of the parameters is compared with them."""
    with live_server(create_app(FakeModelServer(tasks=["text-to-image"]))) as url:
        options = dataclasses.replace(FAST, params=(("width", 32), ("height", 16)))
        results = check(url, options)
    assert all(result.passed for result in results), results


def test_parameter_arguments_are_json_or_text() -> None:
    from model_server_sdk.check import _parse_param  # pyright: ignore[reportPrivateUsage]

    assert _parse_param("seconds=1.0") == ("seconds", 1.0)
    assert _parse_param("size=480p") == ("size", "480p")
    assert _parse_param("thinking=true") == ("thinking", True)
    with pytest.raises(SystemExit):
        _parse_param("no-equals")
