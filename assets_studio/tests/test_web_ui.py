"""Browser walkthrough of the web UI (M3) against fake model servers.

Needs the built UI (`just web-build`), its node_modules and Google Chrome;
skipped otherwise. Screenshots go to build/screens for visual review.
"""

import io
import json
import os
import shutil
import socket
import struct
import subprocess
import threading
import time
from collections.abc import Callable
from pathlib import Path

import pytest
import uvicorn
from fake_model_server import FakeModelServer
from fastapi import FastAPI
from PIL import Image, ImageStat
from studio.api.app import create_app as create_studio_app
from studio.config import Server, StudioConfig
from studio.core import Studio
from support import live_server
from test_studio import png

from model_server_sdk import create_app, media

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


def test_3d_and_animation_sections(tmp_path: Path) -> None:
    """The browser fixture declares FBX and native JSON tasks on one server."""
    studio = Studio(StudioConfig(data_dir=tmp_path))
    app = create_studio_app(studio, web_dir=WEB / "dist")
    with live_server(app) as url:
        completed = subprocess.run(
            ["node", "e2e/sections.mjs", url],
            cwd=WEB,
            env={**os.environ, "CHROME": str(CHROME)},
            capture_output=True,
            text=True,
            timeout=60,
            check=False,
        )
    assert completed.returncode == 0, completed.stdout + completed.stderr


def test_library_bulk_deletion(tmp_path: Path) -> None:
    """Selection and partial deletion use an isolated library and fake model server."""
    with live_server(create_app(FakeModelServer())) as model_url:
        studio = Studio(
            StudioConfig(
                data_dir=tmp_path,
                servers=(model_server("image", "Image", model_url),),
                poll_interval_s=0.2,
            )
        )
        app = create_studio_app(studio, web_dir=WEB / "dist")
        with live_server(app) as url:
            for index in range(7):
                studio.library.import_bytes(png(), title=f"Bulk {index}")
            completed = subprocess.run(
                ["node", "e2e/bulk-deletion.mjs", url],
                cwd=WEB,
                env={**os.environ, "CHROME": str(CHROME)},
                capture_output=True,
                text=True,
                timeout=90,
                check=False,
            )
    assert completed.returncode == 0, completed.stdout + completed.stderr


def test_mesh_inspection(tmp_path: Path) -> None:
    """Actual GLB renderer: wire overlay, clay, original materials and camera."""
    studio = Studio(StudioConfig(data_dir=tmp_path))
    app = create_studio_app(studio, web_dir=WEB / "dist")
    with live_server(app) as url:
        completed = subprocess.run(
            ["node", "e2e/mesh-inspection.mjs", url],
            cwd=WEB,
            env={**os.environ, "CHROME": str(CHROME)},
            capture_output=True,
            text=True,
            timeout=90,
            check=False,
        )
    assert completed.returncode == 0, completed.stdout + completed.stderr


def textured_quad() -> bytes:
    """A square facing the front (+Z), coloured by an embedded PNG as generated
    models are: red over blue."""
    texture = Image.new("RGB", (8, 8), (20, 20, 220))
    texture.paste((220, 20, 20), (0, 0, 8, 4))
    encoded = io.BytesIO()
    texture.save(encoded, format="PNG")
    chunks = (
        struct.pack("<12f", -1, -1, 0, 1, -1, 0, 1, 1, 0, -1, 1, 0),
        struct.pack("<8f", 0, 1, 1, 1, 1, 0, 0, 0),  # glTF: v=0 is the top
        struct.pack("<6H", 0, 1, 2, 0, 2, 3),
        encoded.getvalue(),
    )
    views: list[dict[str, int]] = []
    binary = b""
    for chunk in chunks:
        views.append({"buffer": 0, "byteOffset": len(binary), "byteLength": len(chunk)})
        binary += chunk.ljust(-(-len(chunk) // 4) * 4, b"\0")
    document = {
        "asset": {"version": "2.0"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"mesh": 0}],
        "meshes": [
            {
                "primitives": [
                    {"attributes": {"POSITION": 0, "TEXCOORD_0": 1}, "indices": 2, "material": 0}
                ]
            }
        ],
        "materials": [
            {"pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicFactor": 0}}
        ],
        "textures": [{"source": 0}],
        "images": [{"bufferView": 3, "mimeType": "image/png"}],
        "buffers": [{"byteLength": len(binary)}],
        "bufferViews": views,
        "accessors": [
            {
                "bufferView": 0,
                "componentType": 5126,
                "count": 4,
                "type": "VEC3",
                "min": [-1, -1, 0],
                "max": [1, 1, 0],
            },
            {"bufferView": 1, "componentType": 5126, "count": 4, "type": "VEC2"},
            {"bufferView": 2, "componentType": 5123, "count": 6, "type": "SCALAR"},
        ],
    }
    text = json.dumps(document).encode()
    text = text.ljust(-(-len(text) // 4) * 4, b" ")
    return (
        struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(text) + 8 + len(binary))
        + struct.pack("<I4s", len(text), b"JSON")
        + text
        + struct.pack("<I4s", len(binary), b"BIN\0")
        + binary
    )


def mean_colour(picture: Image.Image) -> tuple[float, ...]:
    """The mean colour of the picture's opaque pixels."""
    return tuple(ImageStat.Stat(picture.convert("RGB"), picture.getchannel("A")).mean)


def test_mesh_previews(tmp_path: Path) -> None:
    """Library thumbnails of meshes: drawn by the browser, kept as pictures (ADR-011)."""
    studio = Studio(StudioConfig(data_dir=tmp_path))
    app = create_studio_app(studio, web_dir=WEB / "dist")
    library = studio.library
    with live_server(app) as url:
        green = library.import_bytes(
            media.encode_glb(*media.tetrahedron(), color=(0.1, 0.6, 0.1, 1.0)),
            title="tetrahedron",
        )
        quad = library.import_bytes(textured_quad(), title="textured quad")
        # A valid header around a broken scene: it cannot be drawn.
        scene = b'{"asset": '
        library.import_bytes(
            struct.pack("<4sII", b"glTF", 2, 20 + len(scene))
            + struct.pack("<I4s", len(scene), b"JSON")
            + scene,
            title="broken",
        )
        assert library.set_preview(green, png(), 0) is not None  # by an older renderer
        completed = subprocess.run(
            ["node", "e2e/mesh-previews.mjs", url],
            cwd=WEB,
            env={**os.environ, "CHROME": str(CHROME)},
            capture_output=True,
            text=True,
            timeout=120,
            check=False,
        )
    assert completed.returncode == 0, completed.stdout + completed.stderr

    pictures: dict[str, Image.Image] = {}
    for asset in (green, quad):
        kept = library.asset(asset.id)
        assert kept is not None
        assert kept.preview_version == 1
        path = library.preview_file(kept)
        assert path is not None
        pictures[asset.id] = Image.open(path).convert("RGBA")
    for picture in pictures.values():
        # Cut to the model's outline: transparent around it, filling the picture.
        assert max(picture.size) == 512
        alpha = picture.getchannel("A")
        assert alpha.getpixel((0, 0)) == 0
        assert alpha.getpixel((picture.width - 1, picture.height - 1)) == 0
        left, top, right, bottom = alpha.getbbox() or (0, 0, 0, 0)
        assert max(right - left, bottom - top) > 0.85 * max(picture.size)
    # Lit in its own colour: the flat green material, the textured red over blue.
    red, green_channel, blue = mean_colour(pictures[green.id])
    assert green_channel > 1.5 * max(red, blue)
    square = pictures[quad.id]
    upper = mean_colour(square.crop((0, 0, square.width, square.height // 3)))
    lower = mean_colour(square.crop((0, 2 * square.height // 3, square.width, square.height)))
    assert upper[0] > 2 * upper[2], upper
    assert lower[2] > 2 * lower[0], lower
