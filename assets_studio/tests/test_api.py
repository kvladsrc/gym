"""HTTP API, event stream and MCP endpoint (M2), end to end on live servers."""

import asyncio
import contextlib
import json
import signal
import socket
import subprocess
import sys
import threading
import time
from collections.abc import Generator
from contextlib import contextmanager
from pathlib import Path
from typing import Any

import httpx2 as httpx
import pytest
from fake_model_server import FakeModelServer
from mcp import Client
from studio.api.app import create_app as create_studio_app
from studio.config import Server, StudioConfig
from studio.core import Studio
from support import live_server

from model_server_sdk import create_app, media


def model_server(server_id: str, url: str) -> Server:
    return Server.model_validate({"id": server_id, "title": server_id.title(), "url": url})


@contextmanager
def studio_server(data_dir: Path, *servers: Server) -> Generator[str]:
    studio = Studio(StudioConfig(data_dir=data_dir, servers=servers, poll_interval_s=0.05))
    with live_server(create_studio_app(studio)) as url:
        yield url


@contextmanager
def fake_and_studio(tmp_path: Path) -> Generator[tuple[str, httpx.Client]]:
    with (
        live_server(create_app(FakeModelServer())) as model_url,
        studio_server(tmp_path, model_server("image", model_url)) as url,
        httpx.Client(base_url=url, timeout=30) as http,
    ):
        yield url, http


def wait_ready(http: httpx.Client) -> None:
    for _ in range(500):
        if http.get("/api/servers").json()[0]["state"] == "ready":
            return
        threading.Event().wait(0.01)
    raise AssertionError("model server never became ready")


def test_rest_generation_end_to_end(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (_, http):
        wait_ready(http)
        (image_tab,) = http.get("/api/servers").json()
        assert image_tab["model"]["id"] == "fake"
        assert "text-to-image" in {task["task"] for task in image_tab["tasks"]}

        created = http.post(
            "/api/jobs",
            json={"server": "image", "task": "text-to-image", "prompt": "мох", "count": 2},
        )
        assert created.status_code == 201
        job = http.post(f"/api/jobs/{created.json()['id']}/wait", params={"timeout_s": 10}).json()
        assert job["status"] == "succeeded"
        assert len(job["outputs"]) == 2

        asset = http.get(f"/api/assets/{job['outputs'][0]}").json()
        assert asset["kind"] == "image"
        file = http.get(asset["file_url"])
        assert file.headers["content-type"] == "image/png"
        assert file.content.startswith(b"\x89PNG")
        assert [item["id"] for item in http.get("/api/jobs").json()] == [job["id"]]
        listed = http.get("/api/jobs", params={"status": ["failed", "cancelled"]}).json()
        assert listed == []


def test_upload_chain_and_lineage(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (_, http):
        picture = media.encode_png(8, 8, lambda x, y: (x * 30, y * 30, 0))
        uploaded = http.post(
            "/api/assets",
            content=picture,
            params={"title": "эскиз"},
            headers={"Content-Type": "application/octet-stream"},
        )
        assert uploaded.status_code == 201, uploaded.text
        asset = uploaded.json()
        assert (asset["kind"], asset["origin"], asset["title"]) == ("image", "upload", "эскиз")

        variation = http.post(
            "/api/jobs",
            json={"server": "image", "task": "image-to-image", "inputs": {"image": asset["id"]}},
        ).json()
        mesh = http.post(
            "/api/jobs",
            json={
                "server": "image",
                "task": "image-to-3d",
                "dependencies": {"image": {"job_id": variation["id"]}},
            },
        ).json()
        done = http.post(f"/api/jobs/{mesh['id']}/wait", params={"timeout_s": 10}).json()
        assert done["status"] == "succeeded"
        varied = http.get(f"/api/jobs/{variation['id']}").json()["outputs"][0]
        assert http.get(f"/api/assets/{done['outputs'][0]}/lineage").json() == [
            {"asset_id": varied, "relation": "input"}
        ]
        assert http.get(f"/api/assets/{varied}/lineage").json() == [
            {"asset_id": asset["id"], "relation": "input"}
        ]
        meshes = http.get("/api/assets", params={"kind": "mesh"}).json()
        assert [item["id"] for item in meshes] == done["outputs"]


def test_errors(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (_, http):
        assert http.get("/api/jobs/nope").status_code == 404
        unknown = http.get("/api/nope")
        assert unknown.status_code == 404
        assert "no such API endpoint" in unknown.json()["detail"]
        assert http.get("/api/assets/nope").status_code == 404
        assert http.get("/api/assets/nope/file").status_code == 404
        unknown_tab = http.post("/api/jobs", json={"server": "nope", "task": "text-to-image"})
        assert unknown_tab.status_code == 422
        assert "unknown server" in unknown_tab.json()["detail"]
        assert http.post("/api/jobs", json={"server": "image"}).status_code == 422
        bad_file = http.post(
            "/api/assets",
            content=b"\x00binary",
            headers={"Content-Type": "application/octet-stream"},
        )
        assert bad_file.status_code == 422
        done = http.post(
            "/api/jobs", json={"server": "image", "task": "text-to-image", "prompt": "x"}
        ).json()
        http.post(f"/api/jobs/{done['id']}/wait", params={"timeout_s": 10})
        assert http.post(f"/api/jobs/{done['id']}/cancel").status_code == 409
        assert http.post(f"/api/jobs/{done['id']}/retry").status_code == 409


def test_idempotent_job_creation(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (_, http):
        body = {"server": "image", "task": "text-to-image", "prompt": "x", "idempotency_key": "a1"}
        first = http.post("/api/jobs", json=body).json()
        second = http.post("/api/jobs", json=body).json()
        assert first["id"] == second["id"]


def test_event_stream(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (url, http):
        events: list[tuple[str, Any]] = []
        finished = threading.Event()
        connected = threading.Event()

        def listen() -> None:
            with (
                httpx.Client(base_url=url, timeout=30) as client,
                client.stream("GET", "/api/events") as response,
            ):
                assert response.headers["content-type"].startswith("text/event-stream")
                name = None
                for line in response.iter_lines():
                    if line.startswith("event: "):
                        name = line.removeprefix("event: ")
                    elif line.startswith("data: ") and name:
                        data = json.loads(line.removeprefix("data: "))
                        events.append((name, data))
                        connected.set()
                        if name == "job" and data["status"] == "succeeded":
                            finished.set()
                            return

        listener = threading.Thread(target=listen, daemon=True)
        listener.start()
        assert connected.wait(10)
        job = http.post(
            "/api/jobs", json={"server": "image", "task": "text-to-image", "prompt": "x"}
        ).json()
        assert finished.wait(10)
        names = [name for name, _ in events]
        assert names[0] == "servers"
        job_states = [data["status"] for name, data in events if name == "job"]
        assert job_states[0] in ("queued", "running")
        assert job_states[-1] == "succeeded"
        assert all(data["id"] == job["id"] for name, data in events if name == "job")


def call(url: str, tool: str, arguments: dict[str, Any] | None = None) -> Any:
    async def run() -> Any:
        async with Client(f"{url}/mcp") as client:
            result = await client.call_tool(tool, arguments or {})
            if result.is_error:
                text: str = getattr(result.content[0], "text", "")
                return {"error": text}
            return result.structured_content

    return asyncio.run(run())


def test_mcp_agent_scenario(tmp_path: Path) -> None:
    """The M2 acceptance scenario: list servers, generate, wait, get the file."""
    with fake_and_studio(tmp_path) as (url, http):
        wait_ready(http)
        servers = call(url, "list_servers")["result"]
        assert servers[0]["id"] == "image"
        assert servers[0]["state"] == "ready"

        job = call(url, "generate", {"server": "image", "task": "text-to-audio", "prompt": "шаги"})
        done = call(url, "wait_for_job", {"job_id": job["id"], "timeout_s": 10})
        assert done["status"] == "succeeded"
        assert Path(done["output_paths"][0]).read_bytes()[:4] == b"RIFF"
        asset = call(url, "get_asset", {"asset_id": done["outputs"][0]})
        assert asset["kind"] == "audio"
        assert Path(asset["path"]).read_bytes()[:4] == b"RIFF"

        mesh = call(
            url,
            "generate",
            {
                "server": "image",
                "task": "image-to-3d",
                "input_jobs": {"image": {"job_id": job["id"]}},
            },
        )
        assert "error" not in mesh  # audio into an image role fails on the server, not here
        failed = call(url, "wait_for_job", {"job_id": mesh["id"], "timeout_s": 10})
        assert failed["status"] == "failed"
        assert failed["error_code"] == "invalid_request"


def test_mcp_import_and_errors(tmp_path: Path) -> None:
    source = tmp_path / "input.png"
    source.write_bytes(media.encode_png(4, 4, lambda x, y: (1, 2, 3)))
    with fake_and_studio(tmp_path / "data") as (url, _):
        imported = call(url, "import_asset", {"path": str(source), "title": "вход"})
        assert (imported["kind"], imported["title"]) == ("image", "вход")
        assert Path(imported["path"]).read_bytes() == source.read_bytes()
        listed = call(url, "list_assets", {"kind": "image"})["result"]
        assert [asset["id"] for asset in listed] == [imported["id"]]
        assert "not found" in call(url, "get_job", {"job_id": "nope"})["error"]
        assert "exactly one" in call(url, "import_asset", {})["error"]
        assert (
            "unknown server"
            in call(url, "generate", {"server": "x", "task": "text-to-image"})["error"]
        )


def test_servers_report_their_state(tmp_path: Path) -> None:
    with studio_server(tmp_path, model_server("voice", "http://127.0.0.1:9")) as url:
        with httpx.Client(base_url=url) as http:
            (voice,) = http.get("/api/servers").json()
            for _ in range(200):
                if voice["message"] != "not checked yet":
                    break
                threading.Event().wait(0.01)
                (voice,) = http.get("/api/servers").json()
        assert voice["state"] == "unavailable"
        assert voice["model"] is None
        assert voice["message"]


def test_tasks_are_remembered_after_the_server_stops(tmp_path: Path) -> None:
    port = _free_port()
    model = create_app(FakeModelServer(tasks=["text-to-image"]))
    with studio_server(tmp_path, model_server("image", f"http://127.0.0.1:{port}")) as url:
        http = httpx.Client(base_url=url, timeout=30)
        stop = _serve_model(model, port)
        try:
            wait_ready(http)
        finally:
            stop()
        image: dict[str, Any] = http.get("/api/servers").json()[0]
        for _ in range(500):
            if image["state"] == "unavailable":
                break
            time.sleep(0.01)
            image = http.get("/api/servers").json()[0]
        assert image["state"] == "unavailable"
        # The last declaration stays: the model, its name as the title, its tasks.
        assert image["model"]["id"] == "fake"
        assert image["seen"] is True
        tasks: list[dict[str, Any]] = image["tasks"]
        assert [task["task"] for task in tasks] == ["text-to-image"]
    # ... and survives a restart of the studio while the server is down.
    with studio_server(tmp_path, model_server("image", f"http://127.0.0.1:{port}")) as url:
        image = httpx.get(f"{url}/api/servers", timeout=30).json()[0]
        assert image["state"] == "unavailable"
        assert image["seen"] is True
        assert [task["task"] for task in image["tasks"]] == ["text-to-image"]


def test_a_server_never_seen_has_no_tasks(tmp_path: Path) -> None:
    with studio_server(tmp_path, model_server("ghost", f"http://127.0.0.1:{_free_port()}")) as url:
        [ghost] = httpx.get(f"{url}/api/servers", timeout=30).json()
        assert (ghost["seen"], ghost["tasks"], ghost["title"]) == (False, [], "Ghost")


def test_generated_assets_are_named_after_the_prompt(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (_, http):
        long_prompt = "мох " * 40
        job = http.post(
            "/api/jobs", json={"server": "image", "task": "text-to-image", "prompt": long_prompt}
        ).json()
        done = http.post(f"/api/jobs/{job['id']}/wait", params={"timeout_s": 10}).json()
        title = http.get(f"/api/assets/{done['outputs'][0]}").json()["title"]
        assert title.startswith("мох мох")
        assert len(title) == 80
        assert title.endswith("…")
        variants = http.post(
            "/api/jobs",
            json={"server": "image", "task": "text-to-image", "prompt": "гриб", "count": 2},
        ).json()
        done = http.post(f"/api/jobs/{variants['id']}/wait", params={"timeout_s": 10}).json()
        titles = [http.get(f"/api/assets/{asset}").json()["title"] for asset in done["outputs"]]
        assert titles == ["гриб · 1", "гриб · 2"]


def _serve_model(app: Any, port: int) -> Any:
    import uvicorn

    server = uvicorn.Server(uvicorn.Config(app, host="127.0.0.1", port=port, log_level="warning"))
    thread = threading.Thread(target=server.run, daemon=True)
    thread.start()

    def stop() -> None:
        server.should_exit = True
        thread.join(timeout=10)

    return stop


def test_openapi_is_published(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (_, http):
        paths = http.get("/openapi.json").json()["paths"]
    assert {"/api/servers", "/api/jobs", "/api/jobs/{job_id}/wait", "/api/assets"} <= set(paths)


def test_foreign_host_is_rejected(tmp_path: Path) -> None:
    """DNS rebinding: a page on another hostname resolving to 127.0.0.1."""
    with fake_and_studio(tmp_path) as (_, http):
        assert http.get("/api/servers", headers={"Host": "evil.example"}).status_code == 400
        assert http.get("/api/servers", headers={"Host": "localhost"}).status_code == 200


def test_error_shape_is_always_a_message(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (_, http):
        invalid = http.post("/api/jobs", json={"server": "image", "count": 0})
        assert invalid.status_code == 422
        detail = invalid.json()["detail"]
        assert isinstance(detail, str)
        assert "task" in detail
        assert "count" in detail


def test_file_download_name_and_missing_file(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (_, http):
        picture = media.encode_png(4, 4, lambda x, y: (0, 0, 0))
        asset = http.post(
            "/api/assets",
            content=picture,
            params={"title": "мох/камень"},
            headers={"Content-Type": "application/octet-stream"},
        ).json()
        response = http.get(asset["file_url"])
        assert "inline" in response.headers["content-disposition"]
        assert ".png" in response.headers["content-disposition"]
        for blob in (tmp_path / "blobs").rglob("*.png"):
            blob.unlink()
        assert http.get(asset["file_url"]).status_code == 410


def test_ctrl_c_with_open_event_stream_and_long_poll(tmp_path: Path) -> None:
    """The web UI always holds an event stream; Ctrl+C must still stop the studio."""
    port = _free_port()
    config = tmp_path / "studio.toml"
    config.write_text(
        f'data_dir = "{tmp_path / "data"}"\n[[servers]]\nid = "image"\nurl = "http://127.0.0.1:9"\n'
    )
    command = [sys.executable, "-m", "studio", "--config", str(config), "--port", str(port)]
    process = subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    base = f"http://127.0.0.1:{port}"
    try:
        for _ in range(300):
            try:
                httpx.get(f"{base}/api/servers")
                break
            except httpx.TransportError:
                time.sleep(0.05)
        job = httpx.post(
            f"{base}/api/jobs", json={"server": "image", "task": "text-to-image", "prompt": "x"}
        ).json()
        opened = threading.Event()

        def hold_stream() -> None:
            with (
                contextlib.suppress(httpx.HTTPError),
                httpx.stream("GET", f"{base}/api/events", timeout=None) as response,
            ):
                opened.set()
                for _ in response.iter_lines():
                    pass

        def hold_long_poll() -> None:
            with contextlib.suppress(httpx.HTTPError):
                httpx.post(f"{base}/api/jobs/{job['id']}/wait?timeout_s=600", timeout=None)

        for target in (hold_stream, hold_long_poll):
            threading.Thread(target=target, daemon=True).start()
        assert opened.wait(10)
        time.sleep(0.3)
        started = time.monotonic()
        process.send_signal(signal.SIGINT)
        process.wait(timeout=15)
        assert time.monotonic() - started < 10
    finally:
        process.kill()
        process.wait()


def _free_port() -> int:
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        return probe.getsockname()[1]


@pytest.fixture(autouse=True)
def _quiet_mcp_logs(caplog: pytest.LogCaptureFixture) -> None:
    caplog.set_level("WARNING")
