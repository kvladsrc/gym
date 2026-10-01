"""Ctrl+C on the studio: a quick, quiet exit with the web UI's event stream open."""

import signal
import socket
import subprocess
import sys
import time
from pathlib import Path

import httpx2


def test_ctrl_c_ends_event_streams_quietly(tmp_path: Path) -> None:
    config = tmp_path / "studio.toml"
    config.write_text(f'data_dir = "{tmp_path / "data"}"\n')
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    process = subprocess.Popen(
        [sys.executable, "-m", "studio", "--config", str(config), "--port", str(port)],
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
    )
    try:
        url = f"http://127.0.0.1:{port}"
        deadline = time.monotonic() + 20
        while True:
            try:
                httpx2.get(f"{url}/api/servers", timeout=1)
                break
            except httpx2.TransportError:
                assert time.monotonic() < deadline, "the studio did not start"
                time.sleep(0.1)
        with httpx2.stream("GET", f"{url}/api/events", timeout=10) as events:
            next(events.iter_lines())  # the stream is open (first "servers" event)
            started = time.monotonic()
            process.send_signal(signal.SIGINT)
            output, _ = process.communicate(timeout=10)
            elapsed = time.monotonic() - started
    finally:
        if process.poll() is None:
            process.kill()
    assert process.returncode == 0, output
    assert elapsed < 2.5, f"shutdown took {elapsed:.1f} s"
    assert "Traceback" not in output, output
    assert "CancelledError" not in output, output
