"""The fake server: determinism, all tasks, simulated failures, contract check."""

import base64
import json
import signal
import socket
import struct
import subprocess
import sys
import time
from collections.abc import Generator
from typing import Any

import httpx2 as httpx
import pytest
from fake_model_server import FakeModelServer
from fastapi.testclient import TestClient
from model_server_sdk.contract import GenerateResponse
from support import live_server, wait_until_loaded

from model_server_sdk import KnownTask, create_app, media

PNG = base64.b64encode(media.encode_png(4, 4, lambda x, y: (x, y, 0))).decode()
WAV = base64.b64encode(media.encode_wav(media.tone(440, 0.1))).decode()
GLB = base64.b64encode(media.encode_glb(*media.tetrahedron())).decode()


@pytest.fixture
def client() -> Generator[TestClient]:
    with TestClient(create_app(FakeModelServer())) as client:
        assert wait_until_loaded(client) == "ready"
        yield client


def generate(client: TestClient, **body: Any) -> GenerateResponse:
    response = client.post("/v1/generate", json=body)
    assert response.status_code == 200, response.text
    return GenerateResponse.model_validate(response.json())


def payloads(response: GenerateResponse) -> list[bytes]:
    return [base64.b64decode(output.data_b64) for output in response.outputs]


def test_declares_every_known_task(client: TestClient) -> None:
    tasks = {task["task"] for task in client.get("/v1/info").json()["tasks"]}
    assert tasks == {task.value for task in KnownTask}


def test_outputs_are_deterministic(client: TestClient) -> None:
    first = generate(client, task="text-to-image", prompt="мох", seed=1, count=2)
    again = generate(client, task="text-to-image", prompt="мох", seed=1, count=2)
    assert payloads(first) == payloads(again)
    assert payloads(first)[0] != payloads(first)[1]
    single = generate(client, task="text-to-image", prompt="мох", seed=2)
    assert payloads(single)[0] == payloads(first)[1]  # a candidate reproduces on its own
    other = generate(client, task="text-to-image", prompt="камень", seed=1)
    assert payloads(other)[0] != payloads(first)[0]


def test_output_depends_on_input(client: TestClient) -> None:
    other_png = base64.b64encode(media.encode_png(4, 4, lambda x, y: (9, 9, 9))).decode()
    outputs = [
        payloads(
            generate(
                client,
                task="image-to-3d",
                seed=1,
                inputs=[{"role": "image", "mime": "image/png", "data_b64": data}],
            )
        )[0]
        for data in (PNG, other_png)
    ]
    assert outputs[0] != outputs[1]


@pytest.mark.parametrize(
    ("task", "extra", "mime"),
    [
        ("text-to-image", {"prompt": "x", "params": {"width": 32, "height": 16}}, "image/png"),
        (
            "image-to-image",
            {"inputs": [{"role": "image", "mime": "image/png", "data_b64": PNG}]},
            "image/png",
        ),
        (
            "image-to-3d",
            {"inputs": [{"role": "image", "mime": "image/png", "data_b64": PNG}]},
            "model/gltf-binary",
        ),
        ("text-to-speech", {"prompt": "Привет", "params": {"voice": "beta"}}, "audio/wav"),
        ("text-to-audio", {"prompt": "шаги по гравию", "params": {"duration_s": 0.2}}, "audio/wav"),
        (
            "audio-to-audio",
            {"inputs": [{"role": "audio", "mime": "audio/wav", "data_b64": WAV}]},
            "audio/wav",
        ),
        (
            "text-to-song",
            {"prompt": "[Verse]\nМох и камень", "params": {"style": "folk"}},
            "audio/wav",
        ),
        (
            "3d-paint",
            {
                "inputs": [
                    {"role": "mesh", "mime": "model/gltf-binary", "data_b64": GLB},
                    {"role": "image", "mime": "image/png", "data_b64": PNG},
                ]
            },
            "model/gltf-binary",
        ),
        (
            "3d-to-rig",
            {"inputs": [{"role": "mesh", "mime": "model/gltf-binary", "data_b64": GLB}]},
            "model/x-fbx",
        ),
    ],
)
def test_every_task_returns_valid_files(
    client: TestClient, task: str, extra: dict[str, Any], mime: str
) -> None:
    response = generate(client, task=task, count=2, **extra)
    for output, data in zip(response.outputs, payloads(response), strict=True):
        assert output.mime == mime
        assert media.looks_like(mime, data) is True


def test_png_dimensions_follow_params(client: TestClient) -> None:
    data = payloads(
        generate(client, task="text-to-image", prompt="x", params={"width": 32, "height": 16})
    )[0]
    assert struct.unpack(">II", data[16:24]) == (32, 16)


def test_glb_is_well_formed(client: TestClient) -> None:
    data = payloads(
        generate(
            client,
            task="image-to-3d",
            inputs=[{"role": "image", "mime": "image/png", "data_b64": PNG}],
        )
    )[0]
    json_length, kind = struct.unpack_from("<I4s", data, 12)
    assert kind == b"JSON"
    document = json.loads(data[20 : 20 + json_length])
    binary_length, binary_kind = struct.unpack_from("<I4s", data, 20 + json_length)
    assert binary_kind == b"BIN\0"
    assert binary_length == document["buffers"][0]["byteLength"]
    assert (20 + json_length) % 4 == 0
    assert binary_length % 4 == 0


@pytest.mark.parametrize(
    ("fail", "status", "code"),
    [
        ("generation", 500, "generation_failed"),
        ("retryable", 500, "generation_failed"),
        ("invalid_input", 422, "invalid_request"),
        ("internal", 500, "internal"),
    ],
)
def test_simulated_failures(client: TestClient, fail: str, status: int, code: str) -> None:
    response = client.post(
        "/v1/generate", json={"task": "text-to-audio", "prompt": "x", "params": {"fail": fail}}
    )
    assert response.status_code == status
    assert response.json()["error"]["code"] == code
    assert response.json()["error"]["retryable"] is (fail == "retryable")


def test_task_subset_and_model_id() -> None:
    server = FakeModelServer(model_id="fake-b", tasks=["text-to-speech"])
    with TestClient(create_app(server)) as client:
        info = client.get("/v1/info").json()
    assert info["model"]["id"] == "fake-b"
    assert [task["task"] for task in info["tasks"]] == ["text-to-speech"]
    with pytest.raises(ValueError, match="unknown tasks: paint"):
        FakeModelServer(tasks=["paint"])


def test_load_error() -> None:
    with TestClient(create_app(FakeModelServer(load_error="no weights"))) as client:
        assert wait_until_loaded(client) == "error"


def test_command_line_entry_points() -> None:
    """The fake server and the checker run as documented."""
    with live_server(create_app(FakeModelServer())) as url:
        completed = subprocess.run(
            [
                sys.executable,
                "-m",
                "model_server_sdk.check",
                url,
                "--task",
                "text-to-speech",
                "--wait",
                "5",
            ],
            capture_output=True,
            text=True,
            check=False,
            timeout=60,
        )
    assert completed.returncode == 0, completed.stdout + completed.stderr
    assert "PASS generate text-to-speech" in completed.stdout
    help_text = subprocess.run(
        [sys.executable, "-m", "fake_model_server", "--help"],
        capture_output=True,
        text=True,
        check=True,
        timeout=60,
    ).stdout
    assert "--load-error" in help_text


def test_ctrl_c_stops_server_during_long_load() -> None:
    """The model thread must not keep the process alive while weights load."""
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    command = [sys.executable, "-m", "fake_model_server", "--port", str(port), "--load-delay", "60"]
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    try:
        deadline = time.monotonic() + 30
        while True:
            try:
                status = httpx.get(f"http://127.0.0.1:{port}/v1/info").json()["status"]
            except httpx.TransportError:
                status = None
            if status == "loading":
                break
            assert time.monotonic() < deadline, "server did not start"
            time.sleep(0.05)
        started = time.monotonic()
        process.send_signal(signal.SIGINT)
        process.wait(timeout=10)
        assert time.monotonic() - started < 10
    finally:
        process.kill()
        process.wait()
