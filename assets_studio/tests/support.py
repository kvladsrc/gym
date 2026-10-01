"""Helpers shared by the tests."""

import socket
import threading
import time
from collections.abc import Generator
from contextlib import contextmanager

import uvicorn
from fastapi import FastAPI
from fastapi.testclient import TestClient


def wait_until_loaded(client: TestClient, timeout_s: float = 5.0) -> str:
    deadline = time.monotonic() + timeout_s
    while True:
        status = client.get("/v1/info").json()["status"]
        if status != "loading" or time.monotonic() > deadline:
            return status
        time.sleep(0.01)


@contextmanager
def live_server(app: FastAPI) -> Generator[str]:
    """Serve ``app`` on a free loopback port in a background thread."""
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    server = uvicorn.Server(uvicorn.Config(app, host="127.0.0.1", port=port, log_level="warning"))
    thread = threading.Thread(target=server.run, daemon=True)
    thread.start()
    deadline = time.monotonic() + 10
    while not server.started:
        if time.monotonic() > deadline:
            raise RuntimeError("live server did not start")
        time.sleep(0.01)
    try:
        yield f"http://127.0.0.1:{port}"
    finally:
        server.should_exit = True
        thread.join(timeout=10)
