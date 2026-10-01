"""Contract check for a running model server.

Usage: ``python -m model_server_sdk.check http://127.0.0.1:9101``

Waits until the model has loaded, validates the declarations, runs every
declared task with sample inputs and default parameters, checks the seed
rules and probes the error paths. Exits with status 1 if any check fails;
warnings do not fail the run but must be explained when a server is
accepted. Uses only the standard library for HTTP so that it runs in any
model server environment.

Seed checks (ADR-001):

- the same request twice gives the same output, i.e. the seed controls the
  randomness (``--allow-nondeterministic`` turns a failure into a warning);
- outputs for different seeds differ, unless the model is genuinely
  deterministic (``--allow-seed-independent``);
- output ``i`` of a batch equals a single request with its seed. GPU kernels
  are not always bit-exact across batch sizes, so a mismatch is a warning
  unless ``--strict-determinism`` is given.
"""

import argparse
import base64
import http.client
import json
import sys
import time
import urllib.error
import urllib.request
from collections.abc import Callable, Mapping
from dataclasses import dataclass
from typing import Any, Literal

from pydantic import ValidationError

from model_server_sdk import media
from model_server_sdk.contract import (
    CONTRACT_VERSION,
    ErrorCode,
    ErrorDetail,
    ErrorResponse,
    GenerateResponse,
    Info,
    TaskInfo,
    input_spec_problems,
    mime_matches,
    output_seed,
    params_schema_problems,
)

SAMPLE_PROMPT = "маленький красный кубик на белом фоне"
SAMPLE_SEED = 4242

_SAMPLES: dict[str, Callable[[], bytes]] = {
    "image/png": lambda: media.encode_png(256, 256, lambda x, y: (x % 256, y % 256, 128)),
    "audio/wav": lambda: media.encode_wav(media.tone(440, 1.0)),
    "model/gltf-binary": lambda: media.encode_glb(*media.tetrahedron()),
    "text/plain": lambda: SAMPLE_PROMPT.encode(),
    "video/mp4": lambda: media.SAMPLE_MP4,
}

Level = Literal["pass", "warn", "fail"]
# (label, request body, expected HTTP status, expected error code)
_Probe = tuple[str, dict[str, Any], int, ErrorCode]


@dataclass(frozen=True)
class Options:
    wait_s: float = 600
    timeout_s: float = 900
    only_tasks: frozenset[str] | None = None
    strict_determinism: bool = False
    allow_seed_independent: bool = False
    allow_nondeterministic: bool = False
    poll_s: float = 1.0
    # Parameters sent with every generation instead of the defaults, for
    # models whose defaults take too long to check (e.g. minutes per video).
    params: tuple[tuple[str, Any], ...] = ()


@dataclass(frozen=True)
class Result:
    name: str
    level: Level
    detail: str = ""

    @property
    def passed(self) -> bool:
        return self.level != "fail"


class _Http:
    def __init__(self, base_url: str, timeout_s: float) -> None:
        if not base_url.startswith(("http://", "https://")):
            raise ValueError(f"expected an http(s) URL, got {base_url!r}")
        self.base_url = base_url.rstrip("/")
        self.timeout_s = timeout_s

    def call(self, method: str, path: str, body: object = None) -> tuple[int, Any]:
        """Return (HTTP status, parsed JSON or raw text); raise OSError on transport failure."""
        data = None if body is None else json.dumps(body).encode()
        request = urllib.request.Request(  # noqa: S310 (scheme checked in __init__)
            self.base_url + path,
            data=data,
            method=method,
            headers={"Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(request, timeout=self.timeout_s) as response:  # noqa: S310
                return response.status, _json_or_text(response.read())
        except urllib.error.HTTPError as error:
            return error.code, _json_or_text(error.read())
        except http.client.HTTPException as error:
            raise OSError(f"{type(error).__name__}: {error}") from error


def _json_or_text(payload: bytes) -> Any:
    try:
        return json.loads(payload)
    except (json.JSONDecodeError, UnicodeDecodeError):
        return payload.decode(errors="replace")[:500]


def check(base_url: str, options: Options | None = None) -> list[Result]:
    options = options or Options()
    http = _Http(base_url, options.timeout_s)
    info, result = _wait_for_model(http, options)
    results = [result]
    if info is None:
        return results
    tasks = info.tasks
    if options.only_tasks is not None:
        tasks = [task for task in tasks if task.task in options.only_tasks]
        missing = sorted(options.only_tasks - {task.task for task in tasks})
        if missing:
            results.append(Result("tasks", "fail", f"not declared: {', '.join(missing)}"))
    for task in tasks:
        results.extend(_check_task(http, info, task, options))
    results.extend(_check_errors(http, tasks))
    return results


def _wait_for_model(http: _Http, options: Options) -> tuple[Info | None, Result]:
    deadline = time.monotonic() + options.wait_s
    while True:
        try:
            status, body = http.call("GET", "/v1/info")
        except OSError as error:
            if time.monotonic() > deadline:
                return None, Result("info", "fail", f"server unreachable: {error}")
            time.sleep(options.poll_s)
            continue
        if status != 200:
            return None, Result("info", "fail", f"HTTP {status}: {body}")
        try:
            info = Info.model_validate(body)
        except ValidationError as error:
            return None, Result("info", "fail", f"invalid body: {error}")
        if info.contract != CONTRACT_VERSION:
            detail = f"contract {info.contract}, expected {CONTRACT_VERSION}"
            return None, Result("info", "fail", detail)
        if info.status == "error":
            return None, Result("info", "fail", f"model failed to load: {info.status_message}")
        if info.status in ("ready", "busy"):
            break
        if time.monotonic() > deadline:
            detail = f"model still loading after {options.wait_s:.0f} s"
            return None, Result("info", "fail", detail)
        time.sleep(options.poll_s)
    problems: list[str] = [] if info.tasks else ["server declares no tasks"]
    for task in info.tasks:
        problems += [f"{task.task}: {p}" for p in params_schema_problems(task.params_schema)]
        for spec in task.inputs:
            problems += [f"{task.task}: {p}" for p in input_spec_problems(spec)]
    if problems:
        return None, Result("info", "fail", "; ".join(problems))
    names = ", ".join(task.task for task in info.tasks)
    return info, Result("info", "pass", f"{info.model.id} ({info.model.name}); tasks: {names}")


def _sample_request(
    task: TaskInfo, *, count: int, seed: int, params: Mapping[str, Any] | None = None
) -> dict[str, Any]:
    inputs: list[dict[str, str]] = []
    for spec in task.inputs:
        if not spec.required:
            continue
        mime = next(
            (known for known in _SAMPLES for pattern in spec.mime if mime_matches(known, pattern)),
            None,
        )
        if mime is None:
            raise LookupError(f"no sample input for role {spec.role} ({spec.mime})")
        data = base64.b64encode(_SAMPLES[mime]()).decode("ascii")
        inputs.append({"role": spec.role, "mime": mime, "data_b64": data})
    request: dict[str, Any] = {"task": task.task, "inputs": inputs, "count": count, "seed": seed}
    if params:
        request["params"] = dict(params)
    if task.prompt != "none":
        request["prompt"] = SAMPLE_PROMPT
    return request


@dataclass(frozen=True)
class _Generation:
    payloads: list[bytes]
    problems: list[str]
    notes: list[str]


def _generate(
    http: _Http,
    info: Info,
    task: TaskInfo,
    *,
    count: int,
    seed: int,
    params: Mapping[str, Any] | None = None,
) -> _Generation:
    request = _sample_request(task, count=count, seed=seed, params=params)
    started = time.monotonic()
    try:
        status, body = http.call("POST", "/v1/generate", request)
    except OSError as error:
        return _Generation([], [f"connection failed (server crashed?): {error}"], [])
    elapsed = time.monotonic() - started
    if status != 200:
        return _Generation([], [f"HTTP {status}: {body}"], [])
    try:
        response = GenerateResponse.model_validate(body)
    except ValidationError as error:
        return _Generation([], [f"invalid body: {error}"], [])
    problems: list[str] = []
    notes: set[str] = set()
    if response.model != info.model:
        problems.append(f"model {response.model.id} differs from /v1/info {info.model.id}")
    if response.task != task.task:
        problems.append(f"task {response.task}")
    if response.seed != seed:
        problems.append(f"seed {response.seed}, requested {seed}")
    properties: dict[str, dict[str, Any]] = task.params_schema.get("properties", {})
    defaults = {name: prop.get("default") for name, prop in properties.items()}
    expected = {**defaults, **(params or {})}
    if response.params != expected:
        what = "the given parameters" if params else "the defaults"
        problems.append(f"params {response.params}, expected {what} {expected}")
    if len(response.outputs) != count:
        problems.append(f"{len(response.outputs)} outputs, requested {count}")
    payloads: list[bytes] = []
    for index, output in enumerate(response.outputs):
        if output.meta.get("seed") != output_seed(seed, index):
            problems.append(f"output {index}: meta.seed {output.meta.get('seed')}")
        if not any(mime_matches(output.mime, pattern) for pattern in task.output_mime):
            problems.append(f"output {index}: {output.mime} not in {task.output_mime}")
        try:
            data = base64.b64decode(output.data_b64, validate=True)
        except ValueError:
            problems.append(f"output {index}: invalid base64")
            continue
        verdict = media.looks_like(output.mime, data)
        if verdict is False:
            problems.append(f"output {index}: content is not {output.mime}")
        elif verdict is None:
            notes.add(f"content of {output.mime} not verified")
        problems.extend(
            f"output {index}: {issue}" for issue in _canonical_problems(output.mime, data)
        )
        payloads.append(data)
    return _Generation(
        payloads, problems, [f"{len(payloads)} outputs in {elapsed:.1f} s", *sorted(notes)]
    )


def _canonical_problems(mime: str, data: bytes) -> list[str]:
    """Departures from the canonical formats of ADR-003 that need no decoder."""
    if mime == "text/plain":
        issues: list[str] = []
        if data.startswith(b"\xef\xbb\xbf"):
            issues.append("text starts with a BOM")
        if b"\r" in data:
            issues.append(r"text has \r line breaks (use \n)")
        return issues
    if mime == "video/mp4":
        return media.mp4_problems(data)
    return []


def _check_task(http: _Http, info: Info, task: TaskInfo, options: Options) -> list[Result]:
    name = f"generate {task.task}"
    try:
        _sample_request(task, count=1, seed=SAMPLE_SEED)
    except LookupError as error:
        return [Result(name, "fail", str(error))]
    count = min(2, task.max_count)
    params = dict(options.params)
    batch = _generate(http, info, task, count=count, seed=SAMPLE_SEED, params=params)
    if batch.problems:
        return [Result(name, "fail", "; ".join(batch.problems))]
    level: Level = "warn" if len(batch.notes) > 1 else "pass"
    results = [Result(name, level, "; ".join(batch.notes))]

    # The output for seed S+1 on its own must repeat exactly, must differ from
    # the output for seed S and should equal output 1 of the batch.
    second_seed = output_seed(SAMPLE_SEED, 1)
    single = _generate(http, info, task, count=1, seed=second_seed, params=params)
    repeat = _generate(http, info, task, count=1, seed=second_seed, params=params)
    problems = single.problems + repeat.problems
    if problems:
        return [*results, Result(f"seeds {task.task}", "fail", "; ".join(problems))]
    deterministic = single.payloads[0] == repeat.payloads[0]
    if deterministic:
        results.append(Result(f"determinism {task.task}", "pass"))
    else:
        level = "warn" if options.allow_nondeterministic else "fail"
        detail = (
            f"two identical requests with seed {second_seed} gave different outputs: "
            "the seed does not control the randomness (unseeded generator?)"
        )
        results.append(Result(f"determinism {task.task}", level, detail))
    if not deterministic:
        detail = "not verifiable: outputs differ even for the same seed"
        results.append(Result(f"seed dependence {task.task}", "warn", detail))
    elif single.payloads[0] == batch.payloads[0]:
        level = "warn" if options.allow_seed_independent else "fail"
        detail = "outputs for different seeds are identical: variants would be duplicates"
        results.append(Result(f"seed dependence {task.task}", level, detail))
    else:
        results.append(Result(f"seed dependence {task.task}", "pass"))
    if count < 2:
        return results
    if single.payloads[0] == batch.payloads[1]:
        results.append(Result(f"reproducibility {task.task}", "pass"))
    else:
        level = "fail" if options.strict_determinism else "warn"
        if deterministic:
            detail = (
                f"batch output 1 differs from a single request with seed {second_seed}, "
                "although single requests repeat exactly: either GPU numerics differ "
                "between batch sizes (acceptable) or the batch uses one generator "
                "instead of one per seed (a bug)"
            )
        else:
            detail = f"batch output 1 differs from a single request with seed {second_seed}"
        results.append(Result(f"reproducibility {task.task}", level, detail))
    return results


def _task_probes(task: TaskInfo) -> list[_Probe]:
    try:
        request = _sample_request(task, count=1, seed=SAMPLE_SEED)
    except LookupError:
        return []
    invalid: ErrorCode = "invalid_request"
    unknown_param = {**request, "params": {"contract_check_unknown": 1}}
    probes: list[_Probe] = [
        (f"{task.task}: unknown parameter", unknown_param, 422, invalid),
        (f"{task.task}: count above max", {**request, "count": task.max_count + 1}, 422, invalid),
    ]
    if request["inputs"]:
        probes.append((f"{task.task}: missing inputs", {**request, "inputs": []}, 422, invalid))
    if task.prompt == "none":
        probes.append((f"{task.task}: unexpected prompt", {**request, "prompt": "x"}, 422, invalid))
    if task.prompt == "required":
        without = {key: value for key, value in request.items() if key != "prompt"}
        probes.append((f"{task.task}: missing prompt", without, 422, invalid))
    return probes


def _check_errors(http: _Http, tasks: list[TaskInfo]) -> list[Result]:
    unknown_task = {"task": "contract-check-unknown"}
    probes: list[_Probe] = [("unsupported task", unknown_task, 400, "unsupported_task")]
    for task in tasks:
        probes += _task_probes(task)
    results = [
        _probe(http, f"error: {label}", "POST", "/v1/generate", body, status, code)
        for label, body, status, code in probes
    ]
    unknown_path = "/v1/contract-check-unknown"
    results.append(
        _probe(http, "error: unknown path", "GET", unknown_path, None, 404, "invalid_request")
    )
    return results


def _probe(
    http: _Http, name: str, method: str, path: str, body: object, status: int, code: ErrorCode
) -> Result:
    try:
        actual_status, payload = http.call(method, path, body)
    except OSError as error:
        return Result(name, "fail", f"connection failed: {error}")
    error = _parse_error(payload)
    if actual_status == status and error is not None and error.code == code:
        return Result(name, "pass")
    return Result(name, "fail", f"expected {status} {code}, got {actual_status}: {payload}")


def _parse_error(payload: Any) -> ErrorDetail | None:
    try:
        return ErrorResponse.model_validate(payload).error
    except ValidationError:
        return None


def _parse_param(item: str) -> tuple[str, Any]:
    name, separator, raw = item.partition("=")
    if not separator or not name:
        raise SystemExit(f"--param {item!r}: expected NAME=VALUE")
    try:
        return name, json.loads(raw)
    except json.JSONDecodeError:
        return name, raw


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Check a model server against contract v1.")
    parser.add_argument("url", help="server base URL, e.g. http://127.0.0.1:9101")
    parser.add_argument("--task", action="append", dest="tasks", help="check only this task")
    parser.add_argument("--wait", type=float, default=600, help="seconds to wait for loading")
    parser.add_argument("--timeout", type=float, default=900, help="seconds per request")
    parser.add_argument(
        "--strict-determinism",
        action="store_true",
        help="fail when a batch output differs from a single request with the same seed",
    )
    parser.add_argument(
        "--allow-seed-independent",
        action="store_true",
        help="only warn when outputs do not depend on the seed (deterministic models)",
    )
    parser.add_argument(
        "--allow-nondeterministic",
        action="store_true",
        help="only warn when identical requests give different outputs",
    )
    parser.add_argument(
        "--param",
        action="append",
        default=[],
        metavar="NAME=VALUE",
        help="send this parameter instead of its default (VALUE as JSON, else a string)",
    )
    arguments = parser.parse_args(argv)
    options = Options(
        wait_s=arguments.wait,
        timeout_s=arguments.timeout,
        only_tasks=frozenset(arguments.tasks) if arguments.tasks else None,
        strict_determinism=arguments.strict_determinism,
        allow_seed_independent=arguments.allow_seed_independent,
        allow_nondeterministic=arguments.allow_nondeterministic,
        params=tuple(_parse_param(item) for item in arguments.param),
    )
    results = check(arguments.url, options)
    for result in results:
        detail = f" — {result.detail}" if result.detail else ""
        print(f"{result.level.upper()} {result.name}{detail}")
    failed = sum(not result.passed for result in results)
    warned = sum(result.level == "warn" for result in results)
    print(f"{len(results)} checks: {failed} failed, {warned} warnings")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
