"""Run the studio: ``python -m studio [--config PATH] [--host HOST] [--port PORT]``."""

import argparse
import contextlib
import logging
from pathlib import Path
from types import FrameType

import uvicorn

from studio.api.app import create_app
from studio.config import DEFAULT_CONFIG_PATH, load_config
from studio.core import Studio

DEFAULT_PORT = 9000
# assets_studio/web/dist, built by `just web-build`.
WEB_DIR = Path(__file__).resolve().parents[3] / "web" / "dist"
_GRACEFUL_SHUTDOWN_S = 3


class _Server(uvicorn.Server):
    """Ends the studio's event streams and long polls as soon as Ctrl+C
    arrives. Uvicorn runs the app's shutdown only after every connection is
    gone, and the web UI always holds an event stream open: without this,
    shutdown waits out the timeout and then cancels the stream with a
    traceback."""

    def __init__(self, config: uvicorn.Config, studio: Studio) -> None:
        super().__init__(config)
        self._studio = studio

    def handle_exit(self, sig: int, frame: FrameType | None) -> None:
        self._studio.begin_closing()
        super().handle_exit(sig, frame)


def main() -> None:
    parser = argparse.ArgumentParser(description="Local studio for game assets.")
    parser.add_argument("--config", type=Path, default=DEFAULT_CONFIG_PATH)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)
    arguments = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(name)s: %(message)s")
    # The dispatcher polls every model server every few seconds; don't log each poll.
    logging.getLogger("httpx2").setLevel(logging.WARNING)
    studio = Studio(load_config(arguments.config))
    app = create_app(studio, extra_hosts=[arguments.host], web_dir=WEB_DIR)
    # The timeout still bounds shutdown for connections the studio does not
    # control (e.g. an MCP client's open stream).
    config = uvicorn.Config(
        app,
        host=arguments.host,
        port=arguments.port,
        timeout_graceful_shutdown=_GRACEFUL_SHUTDOWN_S,
    )
    # Uvicorn re-raises Ctrl+C after a clean shutdown; that is not an error.
    with contextlib.suppress(KeyboardInterrupt):
        _Server(config, studio).run()


if __name__ == "__main__":
    main()
