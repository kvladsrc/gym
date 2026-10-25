"""Browser test against a separate flow process, real studio queue and fake models."""

import os
import shutil
import subprocess
from pathlib import Path

import pytest
from fake_model_server import FakeModelServer
from studio.api.app import create_app as studio_app
from studio.config import Server, StudioConfig
from studio.core import Studio
from studio_flow.app import create_app
from support import live_server

from model_server_sdk import create_app as model_app

ROOT = Path(__file__).resolve().parents[2]
CHROME = os.environ.get("CHROME") or shutil.which("google-chrome")


@pytest.mark.skipif(
    not CHROME or not (ROOT / "web/dist/flow.html").is_file(),
    reason="needs Chrome and just web-build",
)
def test_saved_graph_editor_and_two_runs(tmp_path):
    with live_server(
        model_app(FakeModelServer(tasks=["text-to-image", "image-to-image"]))
    ) as model_url:
        config = StudioConfig(
            data_dir=tmp_path / "studio",
            poll_interval_s=0.05,
            servers=(Server(id="image", title="Fake image", url=model_url),),
        )
        studio = Studio(config)
        with live_server(studio_app(studio, web_dir=ROOT / "web/dist")) as studio_url:
            flow = create_app(database=tmp_path / "flow.sqlite3", studio_url=studio_url)
            with live_server(flow) as url:
                done = subprocess.run(
                    [
                        os.environ.get("NODE", "node"),
                        "e2e/flow.mjs",
                        url,
                        str(ROOT / "build/flow-screens"),
                    ],
                    cwd=ROOT / "web",
                    # Node and Chrome supply their own native libraries; CUDA shell
                    # paths must not override Node's ICU libraries.
                    env={
                        **{k: v for k, v in os.environ.items() if k != "LD_LIBRARY_PATH"},
                        "CHROME": str(CHROME),
                    },
                    capture_output=True,
                    text=True,
                    timeout=120,
                    check=False,
                )
    assert done.returncode == 0, done.stdout + done.stderr
