"""The llama-server child process and the runtime installer, with stand-ins:
a small Python script plays llama-server, local archives play the release."""

import hashlib
import io
import os
import signal
import subprocess
import sys
import tarfile
import textwrap
import time
from pathlib import Path

import pytest
from llm_model_server import runtime, server
from llm_model_server.models import MODELS
from llm_model_server.server import LlmServer, Params

from model_server_sdk import GenerationError, Job

FAKE_LLAMA = textwrap.dedent(
    """\
    #!{python}
    import http.server, sys, time
    args = sys.argv[1:]
    port = int(args[args.index("--port") + 1])
    mode = {mode!r}
    if mode == "exit":
        sys.exit(3)
    started = time.monotonic()
    class Handler(http.server.BaseHTTPRequestHandler):
        def do_GET(self):
            ready = mode == "ok" and time.monotonic() - started > 0.3
            self.send_response(200 if ready else 503)
            self.end_headers()
        def log_message(self, *a):
            pass
    http.server.HTTPServer(("127.0.0.1", port), Handler).serve_forever()
    """
)


@pytest.fixture
def fake_llama(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    def make(mode: str) -> LlmServer:
        binary = tmp_path / f"llama-server-{mode}"
        binary.write_text(FAKE_LLAMA.format(python=sys.executable, mode=mode))
        binary.chmod(0o755)
        monkeypatch.setattr(runtime, "server_binary", lambda: binary)
        instance = LlmServer(MODELS["gemma-4-12b"])
        instance._weights = str(tmp_path / "model.gguf")
        return instance

    return make


def test_start_waits_for_health_and_close_stops(fake_llama) -> None:
    instance = fake_llama("ok")
    instance._start()
    process = instance._process
    assert process is not None
    assert process.poll() is None
    instance.close()
    assert process.poll() is not None


def test_early_exit_is_reported(fake_llama) -> None:
    with pytest.raises(RuntimeError, match="exited with code 3"):
        fake_llama("exit")._start()


def test_load_timeout_stops_the_child(fake_llama, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(server, "LOAD_TIMEOUT_S", 1)
    instance = fake_llama("never")
    with pytest.raises(RuntimeError, match="did not load"):
        instance._start()
    assert instance._process is not None
    assert instance._process.poll() is not None  # not left holding the GPU


def test_child_dies_with_a_killed_parent(tmp_path: Path) -> None:
    pid_file = tmp_path / "child.pid"
    parent = subprocess.Popen(
        [
            sys.executable,
            "-c",
            textwrap.dedent(
                f"""
                import subprocess, time
                from llm_model_server.server import _die_with_parent
                child = subprocess.Popen(["sleep", "60"], preexec_fn=_die_with_parent())
                open({str(pid_file)!r}, "w").write(str(child.pid))
                time.sleep(60)
                """
            ),
        ]
    )
    deadline = time.monotonic() + 10
    while not pid_file.is_file() or not pid_file.read_text():
        assert time.monotonic() < deadline
        time.sleep(0.05)
    child = int(pid_file.read_text())
    parent.send_signal(signal.SIGKILL)
    parent.wait()
    deadline = time.monotonic() + 5
    while _alive(child):
        assert time.monotonic() < deadline, "the child outlived its killed parent"
        time.sleep(0.05)


def _alive(pid: int) -> bool:
    try:
        stat = Path(f"/proc/{pid}/stat").read_text()
    except FileNotFoundError:
        return False
    return stat.split()[2] != "Z"  # a zombie waits only for reaping by init


# The installer


def _archive(path: Path, directory: str, files: dict[str, bytes]) -> str:
    with tarfile.open(path, "w:gz") as tar:
        for name, data in files.items():
            info = tarfile.TarInfo(f"{directory}/{name}")
            info.size = len(data)
            info.mode = 0o755
            tar.addfile(info, io.BytesIO(data))
    return hashlib.sha256(path.read_bytes()).hexdigest()


@pytest.fixture
def release(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    monkeypatch.setenv("XDG_CACHE_HOME", str(tmp_path / "cache"))
    files = tmp_path / "release"
    files.mkdir()
    archives = []
    for archive in runtime.ARCHIVES:
        digest = _archive(files / archive.name, archive.directory, {"llama-server": b"#!/bin/sh\n"})
        archives.append(runtime._Archive(archive.name, digest, archive.directory))
    monkeypatch.setattr(runtime, "ARCHIVES", tuple(archives))
    return files


def test_install_verifies_and_marks_complete(release: Path) -> None:
    binary = runtime.install(release.as_uri())
    assert binary.is_file()
    assert runtime.installed()
    assert runtime.install(release.as_uri()) == binary  # a second call does nothing


def test_install_rejects_a_wrong_checksum(release: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    first = runtime.ARCHIVES[0]
    monkeypatch.setattr(
        runtime,
        "ARCHIVES",
        (runtime._Archive(first.name, "0" * 64, first.directory), *runtime.ARCHIVES[1:]),
    )
    with pytest.raises(RuntimeError, match="checksum"):
        runtime.install(release.as_uri())
    assert not runtime.installed()


def test_library_path_keeps_the_inherited_one(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("LD_LIBRARY_PATH", "/opt/other")
    parts = runtime.library_path().split(os.pathsep)
    assert parts[-1] == "/opt/other"
    assert parts[0].endswith(runtime.ARCHIVES[0].directory)


def test_a_crashed_child_is_started_again(fake_llama, monkeypatch: pytest.MonkeyPatch) -> None:
    instance = fake_llama("ok")
    instance._start()
    first = instance._process
    assert first is not None
    first.kill()
    first.wait()
    replies: list[str] = []
    monkeypatch.setattr(
        instance, "_complete", lambda chat, params, seed: replies.append(instance._url)
    )
    instance.generate(Job(task="text-to-text", prompt="x", inputs={}, params=Params(), seeds=(1,)))
    assert instance._process is not first
    assert instance._process is not None
    assert instance._process.poll() is None
    assert replies
    instance.close()


def test_a_child_that_cannot_start_again_is_a_generation_error(fake_llama) -> None:
    instance = fake_llama("ok")
    instance._start()
    assert instance._process is not None
    instance._process.kill()
    instance._process.wait()
    instance._weights = ""
    runtime_binary = runtime.server_binary()
    runtime_binary.unlink()  # the binary is gone: Popen raises OSError
    with pytest.raises(GenerationError, match="cannot start again"):
        instance.generate(
            Job(task="text-to-text", prompt="x", inputs={}, params=Params(), seeds=(1,))
        )
