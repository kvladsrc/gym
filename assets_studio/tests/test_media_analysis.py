"""The text-only agent helper reuses results and preserves job provenance."""

import argparse
import importlib.util
from pathlib import Path
from typing import Any

import pytest


def load_tool() -> Any:
    path = Path(__file__).parents[1] / "agent_tools/describe_media.py"
    spec = importlib.util.spec_from_file_location("describe_media", path)
    assert spec is not None
    assert spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def options(path: Path) -> argparse.Namespace:
    return argparse.Namespace(
        studio="http://127.0.0.1:9000",
        server="qwen",
        path=str(path),
        asset=None,
        video=False,
        reference=None,
        prompt="What color?",
        max_tokens=128,
        frames=4,
        timeout=10,
        refresh=False,
    )


class Client:
    def __init__(self) -> None:
        self.submissions: list[dict[str, Any]] = []
        self.uploads: list[Path] = []
        self.revision = "vision.1"
        self.tasks = [{"task": "image-to-text"}]
        self.status = "succeeded"

    def call(self, path: str, data: Any = None) -> Any:
        if path == "/api/servers":
            return [{"id": "qwen", "model": {"revision": self.revision}, "tasks": self.tasks}]
        if path == "/api/jobs":
            self.submissions.append(data)
            return {
                "id": "job-1",
                "status": self.status,
                "outputs": ["text-1"],
                "error_message": "bad input",
            }
        if path == "/api/assets/text-1":
            return {"meta": {"analysis": "local_vlm_observation", "finish_reason": "stop"}}
        raise AssertionError(path)

    def upload(self, path: Path) -> str:
        self.uploads.append(path)
        return "image-1"

    def text(self, asset: str) -> str:
        assert asset == "text-1"
        return "Red.\n"


@pytest.fixture
def setup(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> tuple[Any, Client, argparse.Namespace]:
    tool = load_tool()
    client = Client()
    monkeypatch.setenv("XDG_CACHE_HOME", str(tmp_path / "cache"))

    def make_client(url: str) -> Client:
        return client

    monkeypatch.setattr(tool, "StudioClient", make_client)
    source = tmp_path / "fixture.png"
    source.write_bytes(b"synthetic bytes; mock local model")
    return tool, client, options(source)


def test_same_media_and_question_reuse_cached_text_without_another_upload(
    setup: tuple[Any, Client, argparse.Namespace],
) -> None:
    tool, client, args = setup
    first = tool.describe(args)
    second = tool.describe(args)
    assert first == second
    assert first["text"] == "Red.\n"
    assert first["job_id"] == "job-1"
    assert len(client.submissions) == 1
    assert len(client.uploads) == 1
    assert client.submissions[0]["params"]["thinking"] is False
    assert client.submissions[0]["idempotency_key"].startswith("local-media-")


def test_question_revision_and_bytes_each_invalidate_the_report(
    setup: tuple[Any, Client, argparse.Namespace],
) -> None:
    tool, client, args = setup
    tool.describe(args)
    args.prompt = "Where?"
    tool.describe(args)
    client.revision = "vision.2"
    tool.describe(args)
    Path(args.path).write_bytes(b"changed bytes")
    tool.describe(args)
    assert len(client.submissions) == 4
    assert len({r["idempotency_key"] for r in client.submissions}) == 4


def test_unavailable_local_capability_does_not_fall_back_or_submit(
    setup: tuple[Any, Client, argparse.Namespace],
) -> None:
    tool, client, args = setup
    client.tasks = []
    with pytest.raises(RuntimeError, match="has not advertised"):
        tool.describe(args)
    assert client.submissions == []
    assert client.uploads == []


def test_failed_generation_is_not_cached_as_a_success(
    setup: tuple[Any, Client, argparse.Namespace],
) -> None:
    tool, client, args = setup
    client.status = "failed"
    with pytest.raises(RuntimeError, match="job-1: bad input"):
        tool.describe(args)
    assert not list((Path(args.path).parent / "cache").rglob("*.json"))
