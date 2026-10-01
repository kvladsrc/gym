"""Browser walkthrough of the web UI (M3) against fake model servers.

Needs the built UI (`just web-build`), its node_modules and Google Chrome;
skipped otherwise. Screenshots go to build/screens for visual review.
"""

import os
import shutil
import socket
import subprocess
import threading
import time
from collections.abc import Callable
from pathlib import Path

import pytest
import uvicorn
from fake_model_server import FakeModelServer
from fastapi import FastAPI
from studio.api.app import create_app as create_studio_app
from studio.config import Server, StudioConfig
from studio.core import Studio
from support import live_server

from model_server_sdk import create_app

ROOT = Path(__file__).resolve().parents[1]
WEB = ROOT / "web"
CHROME = os.environ.get("CHROME") or shutil.which("google-chrome")

pytestmark = pytest.mark.skipif(
    not (WEB / "dist" / "index.html").is_file()
    or not (WEB / "node_modules").is_dir()
    or CHROME is None,
    reason="needs `just web-install web-build` and google-chrome",
)


def model_server(server_id: str, title: str, url: str) -> Server:
    return Server.model_validate({"id": server_id, "title": title, "url": url})


class Switchable:
    """A model server on a fixed port that the walkthrough can stop and start."""

    def __init__(self, make_app: Callable[[], FastAPI]) -> None:
        self.make_app = make_app
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 0))
            self.port: int = probe.getsockname()[1]
        self.server: uvicorn.Server | None = None
        self.thread: threading.Thread | None = None

    @property
    def url(self) -> str:
        return f"http://127.0.0.1:{self.port}"

    def start(self) -> None:
        config = uvicorn.Config(
            self.make_app(), host="127.0.0.1", port=self.port, log_level="warning"
        )
        self.server = uvicorn.Server(config)
        self.thread = threading.Thread(target=self.server.run, daemon=True)
        self.thread.start()
        deadline = time.monotonic() + 10
        while not self.server.started:
            assert time.monotonic() < deadline, "model server did not start"
            time.sleep(0.01)

    def stop(self) -> None:
        if self.server is not None and self.thread is not None:
            self.server.should_exit = True
            self.thread.join(timeout=10)
            self.server = None


def control_app(image: Switchable) -> FastAPI:
    app = FastAPI()

    @app.post("/stop")
    def stop() -> None:  # pyright: ignore[reportUnusedFunction]
        image.stop()

    @app.post("/start")
    def start() -> None:  # pyright: ignore[reportUnusedFunction]
        image.start()

    return app


def test_web_ui_walkthrough(tmp_path: Path) -> None:
    image = Switchable(
        lambda: create_app(FakeModelServer(tasks=["text-to-image", "image-to-image"]))
    )
    second = create_app(FakeModelServer(model_id="second", tasks=["text-to-image"]))
    meshes = create_app(FakeModelServer(model_id="mesh", tasks=["image-to-3d"]))
    sounds = create_app(
        FakeModelServer(
            model_id="sound", tasks=["text-to-speech", "text-to-audio", "audio-to-audio"]
        )
    )
    media = create_app(
        FakeModelServer(model_id="media", tasks=["text-to-text", "image-to-video", "text-to-3d"])
    )
    screens = ROOT / "build" / "screens"
    shutil.rmtree(screens, ignore_errors=True)
    image.start()
    try:
        with (
            live_server(second) as second_url,
            live_server(meshes) as mesh_url,
            live_server(sounds) as sound_url,
            live_server(media) as media_url,
            live_server(control_app(image)) as control_url,
        ):
            servers = (
                model_server("image", "Основная", image.url),
                model_server("second", "Вторая", second_url),
                model_server("mesh", "Меш", mesh_url),
                model_server("audio", "Звуки", sound_url),
                model_server("media", "Медиа", media_url),
                model_server("offline", "Офлайн", "http://127.0.0.1:9"),
            )
            studio = Studio(StudioConfig(data_dir=tmp_path, servers=servers, poll_interval_s=0.2))
            app = create_studio_app(studio, web_dir=WEB / "dist")
            with live_server(app) as url:
                completed = subprocess.run(
                    ["node", "e2e/smoke.mjs", url, str(screens)],
                    cwd=WEB,
                    env={**os.environ, "CHROME": str(CHROME), "CONTROL": control_url},
                    capture_output=True,
                    text=True,
                    timeout=240,
                    check=False,
                )
    finally:
        image.stop()
    assert completed.returncode == 0, completed.stdout + completed.stderr
