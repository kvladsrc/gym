"""Studio core (M1b): storage, queue and dispatch against live model servers."""

import base64
import dataclasses
import io
import json
import socket
import sqlite3
import threading
import time
from collections.abc import Callable, Generator
from contextlib import contextmanager
from pathlib import Path
from typing import Any

import pytest
from fake_model_server import FakeModelServer
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse, Response
from fastapi.testclient import TestClient
from PIL import Image
from pydantic import BaseModel
from studio.config import Server, StudioConfig, load_config
from studio.core import DataDirectoryBusy, Studio
from studio.domain import Dependency, Job, JobStatus, new_id
from studio.services.generation import JobRequestError, JobStateError
from studio.services.library import ImportError_
from studio.storage.blobs import BlobStore
from studio.storage.repository import JobNotRunning, Repository
from support import live_server

from model_server_sdk import InputSpec, ModelInfo, ModelServer, Output, TaskSpec, create_app, media
from model_server_sdk import Job as ServerJob

POLL_S = 0.05


def free_port() -> int:
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        return probe.getsockname()[1]


def model_server(server_id: str, url: str, timeout_s: float | None = None) -> Server:
    return Server.model_validate({"id": server_id, "url": url, "timeout_s": timeout_s})


@contextmanager
def running_studio(data_dir: Path, *servers: Server) -> Generator[Studio]:
    studio = Studio(StudioConfig(data_dir=data_dir, servers=servers, poll_interval_s=POLL_S))
    studio.start()
    try:
        yield studio
    finally:
        studio.close()


def wait_for(studio: Studio, job_id: str, *statuses: JobStatus, timeout_s: float = 10) -> Job:
    deadline = time.monotonic() + timeout_s
    while True:
        job = studio.generation.job(job_id)
        assert job is not None
        if job.status in statuses:
            return job
        assert time.monotonic() < deadline, f"job is {job.status}, expected {statuses}"
        time.sleep(0.01)


def png(color: tuple[int, int, int] = (200, 30, 30)) -> bytes:
    return media.encode_png(8, 8, lambda x, y: color)


def test_job_runs_and_stores_outputs(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        job = studio.generation.submit("image", "text-to-image", prompt="мох", count=3, seed=7)
        done = wait_for(studio, job.id, JobStatus.SUCCEEDED)
        assert len(done.outputs) == 3
        assert done.model_snapshot is not None
        assert done.model_snapshot["id"] == "fake"
        assert done.effective_params == {
            "delay_s": 0.0,
            "fail": "none",
            "width": 256,
            "height": 256,
        }
        assert done.timing is not None
        assert done.seed == 7
        assets = [studio.library.asset(asset_id) for asset_id in done.outputs]
        assert [asset.meta["seed"] for asset in assets if asset] == [7, 8, 9]
        for asset in assets:
            assert asset is not None
            assert asset.kind == "image"
            assert studio.library.file(asset).read_bytes().startswith(b"\x89PNG")


def test_every_change_bumps_the_job_version(tmp_path: Path) -> None:
    seen: list[int] = []
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        studio.subscribe(lambda job_id: seen.append(studio.generation.job(job_id).version))  # pyright: ignore[reportOptionalMemberAccess]
        job = studio.generation.submit("image", "text-to-image", prompt="x")
        assert job.version == 0
        done = wait_for(studio, job.id, JobStatus.SUCCEEDED)
    assert done.version >= 2  # running, succeeded
    assert seen == sorted(seen)


def test_seed_chosen_by_server_is_recorded(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("audio", url)) as studio,
    ):
        job = studio.generation.submit(
            "audio", "text-to-audio", prompt="шаги", params={"duration_s": 0.1}
        )
        done = wait_for(studio, job.id, JobStatus.SUCCEEDED)
        asset = studio.library.asset(done.outputs[0])
        assert asset is not None
        assert done.seed == asset.meta["seed"]


def test_job_waits_for_a_server_that_is_not_running(tmp_path: Path) -> None:
    port = free_port()
    url = f"http://127.0.0.1:{port}"
    with running_studio(tmp_path, model_server("image", url)) as studio:
        job = studio.generation.submit("image", "text-to-image", prompt="мох")
        wait_for(studio, job.id, JobStatus.WAITING_MODEL)
        assert studio.server_status("image").unavailable is not None
        app = create_app(FakeModelServer())
        server = _serve_on(app, port)
        try:
            wait_for(studio, job.id, JobStatus.SUCCEEDED)
            # The status is "busy" until the next poll after the generation.
            deadline = time.monotonic() + 5
            while not studio.server_status("image").ready:
                assert time.monotonic() < deadline
                time.sleep(0.01)
        finally:
            server()


def test_job_waits_while_the_model_loads(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer(load_delay_s=1.0))) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        job = studio.generation.submit("image", "text-to-image", prompt="мох")
        wait_for(studio, job.id, JobStatus.WAITING_MODEL)
        wait_for(studio, job.id, JobStatus.SUCCEEDED)


def test_server_crash_fails_the_job_and_retry_creates_a_new_one(tmp_path: Path) -> None:
    with TestClient(create_app(FakeModelServer())) as client:
        info_body = client.get("/v1/info").json()
    info_body["status"] = "ready"
    with (
        _crashing_server(info_body) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        job = studio.generation.submit("image", "text-to-image", prompt="мох")
        failed = wait_for(studio, job.id, JobStatus.FAILED)
        assert failed.error_code == "connection_lost"
        retry = studio.generation.retry(job.id)
        assert retry.id != job.id
        assert retry.retry_of == job.id
        assert retry.prompt == "мох"
        original = studio.generation.job(job.id)
        assert original is not None
        assert original.status == JobStatus.FAILED  # history is kept


def test_busy_server_does_not_spend_retries(tmp_path: Path) -> None:
    calls: list[int] = []

    def busy_five_times(status: int, body: Any) -> tuple[int, Any]:
        calls.append(status)
        if len(calls) <= 5:
            return 409, {"error": {"code": "busy", "message": "busy", "retryable": True}}
        return status, body

    with (
        _proxy(busy_five_times) as app,
        live_server(app) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        job = studio.generation.submit("image", "text-to-image", prompt="мох")
        done = wait_for(studio, job.id, JobStatus.SUCCEEDED)
        assert done.retryable_failures == 0
        assert len(calls) == 6


def test_retryable_errors_fail_after_three_attempts(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        job = studio.generation.submit(
            "image", "text-to-image", prompt="мох", params={"fail": "retryable"}
        )
        failed = wait_for(studio, job.id, JobStatus.FAILED)
        assert failed.error_code == "generation_failed"
        assert failed.retryable_failures == 2  # the third error is final
        assert "transient" in (failed.error_message or "")


def test_non_retryable_error_is_recorded(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        job = studio.generation.submit("image", "text-to-image", prompt="мох", params={"colour": 1})
        failed = wait_for(studio, job.id, JobStatus.FAILED)
        assert failed.error_code == "invalid_request"
        assert "colour" in (failed.error_message or "")


def test_unsupported_task_fails_without_sending(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer(tasks=["text-to-image"]))) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        job = studio.generation.submit("image", "text-to-speech", prompt="привет")
        failed = wait_for(studio, job.id, JobStatus.FAILED)
        assert failed.error_code == "unsupported_task"


def test_chain_across_two_servers_records_lineage(tmp_path: Path) -> None:
    images = create_app(FakeModelServer(model_id="images", tasks=["text-to-image"]))
    meshes = create_app(FakeModelServer(model_id="meshes", tasks=["image-to-3d"]))
    with (
        live_server(images) as image_url,
        live_server(meshes) as mesh_url,
        running_studio(
            tmp_path, model_server("image", image_url), model_server("mesh", mesh_url)
        ) as studio,
    ):
        picture = studio.generation.submit("image", "text-to-image", prompt="гриб", count=2)
        model = studio.generation.submit(
            "mesh", "image-to-3d", dependencies={"image": Dependency(picture.id, output_index=1)}
        )
        done = wait_for(studio, model.id, JobStatus.SUCCEEDED)
        source = studio.generation.job(picture.id)
        assert source is not None
        assert done.model_snapshot is not None
        assert done.model_snapshot["id"] == "meshes"
        assert studio.library.parents(done.outputs[0]) == [(source.outputs[1], "input")]


def test_failed_dependency_fails_dependents_transitively(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("fake", url)) as studio,
    ):
        first = studio.generation.submit(
            "fake", "text-to-image", prompt="x", params={"fail": "generation", "delay_s": 0.3}
        )
        second = studio.generation.submit(
            "fake", "image-to-image", dependencies={"image": Dependency(first.id)}
        )
        third = studio.generation.submit(
            "fake", "image-to-3d", dependencies={"image": Dependency(second.id)}
        )
        wait_for(studio, first.id, JobStatus.FAILED)
        for job_id in (second.id, third.id):
            dependent = wait_for(studio, job_id, JobStatus.FAILED)
            assert dependent.error_code == "dependency_failed"


def test_waiting_dependency_does_not_block_independent_jobs(tmp_path: Path) -> None:
    images = create_app(FakeModelServer(tasks=["text-to-image"]))
    with live_server(create_app(FakeModelServer(tasks=["image-to-3d"]))) as mesh_url:
        port = free_port()
        with running_studio(
            tmp_path,
            model_server("image", f"http://127.0.0.1:{port}"),
            model_server("mesh", mesh_url),
        ) as studio:
            picture = studio.generation.submit("image", "text-to-image", prompt="x")
            blocked = studio.generation.submit(
                "mesh", "image-to-3d", dependencies={"image": Dependency(picture.id)}
            )
            asset = studio.library.import_bytes(png())
            free = studio.generation.submit("mesh", "image-to-3d", inputs={"image": asset.id})
            wait_for(studio, free.id, JobStatus.SUCCEEDED)
            assert studio.generation.job(blocked.id).status == JobStatus.QUEUED  # pyright: ignore[reportOptionalMemberAccess]
            stop = _serve_on(images, port)
            try:
                wait_for(studio, blocked.id, JobStatus.SUCCEEDED)
            finally:
                stop()


def test_one_job_at_a_time_per_server_address(tmp_path: Path) -> None:
    active, peak = [0], [0]
    lock = threading.Lock()

    class CountingServer(FakeModelServer):
        def generate(self, job: ServerJob) -> list[Output]:
            with lock:
                active[0] += 1
                peak[0] = max(peak[0], active[0])
            try:
                return super().generate(job)
            finally:
                with lock:
                    active[0] -= 1

    with (
        live_server(create_app(CountingServer())) as url,
        running_studio(
            tmp_path, model_server("image", url), model_server("variation", url)
        ) as studio,
    ):
        jobs = [
            studio.generation.submit(tab_id, "text-to-image", prompt="x", params={"delay_s": 0.2})
            for tab_id in ("image", "variation", "image", "variation")
        ]
        for job in jobs:
            done = wait_for(studio, job.id, JobStatus.SUCCEEDED)
            assert done.retryable_failures == 0
    assert peak[0] == 1


def test_idempotency_key_returns_the_same_job(tmp_path: Path) -> None:
    with running_studio(
        tmp_path, model_server("image", f"http://127.0.0.1:{free_port()}")
    ) as studio:
        first = studio.generation.submit("image", "text-to-image", prompt="x", idempotency_key="k1")
        again = studio.generation.submit("image", "text-to-image", prompt="y", idempotency_key="k1")
        assert again.id == first.id
        assert len(studio.generation.jobs()) == 1


def test_cancel_only_before_sending(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        slow = studio.generation.submit(
            "image", "text-to-image", prompt="x", params={"delay_s": 0.5}
        )
        queued = studio.generation.submit("image", "text-to-image", prompt="y")
        wait_for(studio, slow.id, JobStatus.RUNNING)
        cancelled = studio.generation.cancel(queued.id)
        assert cancelled.status == JobStatus.CANCELLED
        with pytest.raises(JobStateError):
            studio.generation.cancel(slow.id)
        wait_for(studio, slow.id, JobStatus.SUCCEEDED)
        assert studio.generation.job(queued.id).status == JobStatus.CANCELLED  # pyright: ignore[reportOptionalMemberAccess]
        with pytest.raises(JobStateError):
            studio.generation.retry(slow.id)


def test_submit_validation(tmp_path: Path) -> None:
    with running_studio(
        tmp_path, model_server("image", f"http://127.0.0.1:{free_port()}")
    ) as studio:
        asset = studio.library.import_bytes(png())
        parent = studio.generation.submit("image", "text-to-image", prompt="x", count=2)
        cases: list[tuple[dict[str, Any], str]] = [
            ({"server": "nope"}, "unknown server"),
            ({"count": 0}, "count"),
            ({"inputs": {"image": "missing"}}, "not found"),
            ({"dependencies": {"image": Dependency("missing")}}, "not found"),
            ({"dependencies": {"image": Dependency(parent.id, 2)}}, "no output 2"),
            (
                {"inputs": {"image": asset.id}, "dependencies": {"image": Dependency(parent.id)}},
                "filled twice",
            ),
        ]
        for overrides, fragment in cases:
            arguments: dict[str, Any] = {"server": "image", "task": "image-to-3d", **overrides}
            with pytest.raises(JobRequestError, match=fragment):
                studio.generation.submit(**arguments)
        studio.generation.cancel(parent.id)
        with pytest.raises(JobRequestError, match="did not succeed"):
            studio.generation.submit(
                "image", "image-to-3d", dependencies={"image": Dependency(parent.id)}
            )


class PngOnlyParams(BaseModel):
    pass


class PngOnlyServer(ModelServer):
    """Accepts only PNG input and echoes it back, to observe conversions."""

    model = ModelInfo(id="png-only", name="PNG only")
    tasks = (
        TaskSpec(
            "image-to-image",
            PngOnlyParams,
            ("image/png",),
            prompt="none",
            inputs=(InputSpec(role="image", mime=["image/png"]),),
        ),
    )

    def load(self) -> None: ...

    def generate(self, job: ServerJob) -> list[Output]:
        received = job.inputs["image"]
        assert received.mime == "image/png"
        return [Output("image/png", received.data) for _ in job.seeds]


def test_jpeg_input_is_converted_to_png(tmp_path: Path) -> None:
    buffer = io.BytesIO()
    Image.new("RGB", (4, 4), (10, 200, 30)).save(buffer, format="JPEG")
    with (
        live_server(create_app(PngOnlyServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        jpeg = studio.library.import_bytes(buffer.getvalue())
        assert jpeg.mime == "image/jpeg"
        job = studio.generation.submit("image", "image-to-image", inputs={"image": jpeg.id})
        done = wait_for(studio, job.id, JobStatus.SUCCEEDED)
        output = studio.library.asset(done.outputs[0])
        assert output is not None
        assert output.mime == "image/png"
        assert studio.library.parents(output.id) == [(jpeg.id, "input")]


def test_invalid_output_is_rejected(tmp_path: Path) -> None:
    def lying_mime(status: int, body: Any) -> tuple[int, Any]:
        if status == 200:
            body["outputs"][0]["data_b64"] = base64.b64encode(b"RIFF....WAVEfmt ").decode()
        return status, body

    with (
        _proxy(lying_mime) as app,
        live_server(app) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        job = studio.generation.submit("image", "text-to-image", prompt="x")
        failed = wait_for(studio, job.id, JobStatus.FAILED)
        assert failed.error_code == "invalid_response"


def test_timeout(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url, timeout_s=0.3)) as studio,
    ):
        job = studio.generation.submit("image", "text-to-image", prompt="x", params={"delay_s": 2})
        failed = wait_for(studio, job.id, JobStatus.FAILED)
        assert failed.error_code == "timeout"


def test_running_jobs_are_interrupted_on_restart(tmp_path: Path) -> None:
    url = f"http://127.0.0.1:{free_port()}"
    with running_studio(tmp_path, model_server("image", url)) as studio:
        job = studio.generation.submit("image", "text-to-image", prompt="x")
        dependent = studio.generation.submit(
            "image", "image-to-3d", dependencies={"image": Dependency(job.id)}
        )
    with sqlite3.connect(tmp_path / "studio.sqlite3") as connection:
        connection.execute("UPDATE jobs SET status = 'running' WHERE id = ?", (job.id,))
    with running_studio(tmp_path, model_server("image", url)) as studio:
        interrupted = studio.generation.job(job.id)
        assert interrupted is not None
        assert interrupted.error_code == "interrupted"
        assert studio.generation.job(dependent.id).error_code == "dependency_failed"  # pyright: ignore[reportOptionalMemberAccess]


def test_change_events(tmp_path: Path) -> None:
    events: list[str] = []
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        unsubscribe = studio.subscribe(events.append)
        job = studio.generation.submit("image", "text-to-image", prompt="x")
        wait_for(studio, job.id, JobStatus.SUCCEEDED)
        time.sleep(POLL_S)
        unsubscribe()
    assert events.count(job.id) >= 2  # running, succeeded


def test_raising_listener_does_not_disturb_dispatch(tmp_path: Path) -> None:
    def broken(_: str) -> None:
        raise RuntimeError("event stream closed")

    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        studio.subscribe(broken)
        job = studio.generation.submit("image", "text-to-image", prompt="x")
        wait_for(studio, job.id, JobStatus.SUCCEEDED)


def test_unexpected_error_after_start_fails_the_job(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def disk_full(*_: object) -> str:
        raise OSError(28, "No space left on device")

    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        monkeypatch.setattr(BlobStore, "put", disk_full)
        job = studio.generation.submit("image", "text-to-image", prompt="x")
        failed = wait_for(studio, job.id, JobStatus.FAILED)
        assert failed.error_code == "internal"
        assert "No space left" in (failed.error_message or "")
        assert failed.outputs == []


def test_dependency_that_failed_meanwhile_fails_the_new_job(tmp_path: Path) -> None:
    """add_job re-checks upstream jobs inside its transaction."""
    with running_studio(
        tmp_path, model_server("image", f"http://127.0.0.1:{free_port()}")
    ) as studio:
        upstream = studio.generation.submit("image", "text-to-image", prompt="x")
        repository = Repository(studio.database)
        repository.fail(upstream.id, "generation_failed", "boom")
        stored = repository.add_job(
            Job(
                id=new_id(),
                server="image",
                task="image-to-3d",
                status=JobStatus.QUEUED,
                count=1,
                created_at="2026-09-29T00:00:00.000+00:00",
                dependencies={"image": Dependency(upstream.id)},
            )
        )
        assert stored.status == JobStatus.FAILED
        assert stored.error_code == "dependency_failed"


def test_outputs_are_not_attached_to_a_job_that_is_not_running(tmp_path: Path) -> None:
    with running_studio(
        tmp_path, model_server("image", f"http://127.0.0.1:{free_port()}")
    ) as studio:
        job = studio.generation.submit("image", "text-to-image", prompt="x")
        asset = studio.library.import_bytes(png())
        output = dataclasses.replace(asset, id=new_id())
        repository = Repository(studio.database)
        with pytest.raises(JobNotRunning):
            repository.succeed(
                job.id,
                [output],
                parents=[],
                seed=1,
                model_snapshot={},
                effective_params={},
                timing={},
            )
        assert studio.library.asset(output.id) is None
        assert studio.generation.job(job.id).outputs == []  # pyright: ignore[reportOptionalMemberAccess]


def test_events_cover_waiting_and_dependent_jobs(tmp_path: Path) -> None:
    events: list[str] = []
    with running_studio(
        tmp_path, model_server("image", f"http://127.0.0.1:{free_port()}")
    ) as studio:
        studio.subscribe(events.append)
        first = studio.generation.submit("image", "text-to-image", prompt="x")
        second = studio.generation.submit("image", "text-to-image", prompt="y")
        dependent = studio.generation.submit(
            "image", "image-to-3d", dependencies={"image": Dependency(first.id)}
        )
        wait_for(studio, second.id, JobStatus.WAITING_MODEL)
        assert {first.id, second.id} <= set(events)
        studio.generation.cancel(first.id)
        assert events[-2:] == [first.id, dependent.id]
        assert studio.generation.job(dependent.id).error_code == "dependency_failed"  # pyright: ignore[reportOptionalMemberAccess]


def test_retrying_a_chain_retries_its_failed_upstream(tmp_path: Path) -> None:
    port = free_port()
    with running_studio(tmp_path, model_server("fake", f"http://127.0.0.1:{port}")) as studio:
        picture = studio.generation.submit("fake", "text-to-image", prompt="x")
        model = studio.generation.submit(
            "fake", "image-to-3d", dependencies={"image": Dependency(picture.id)}
        )
        Repository(studio.database).fail(picture.id, "generation_failed", "boom")
        wait_for(studio, model.id, JobStatus.FAILED)
        retried = studio.generation.retry(model.id)
        new_picture = retried.dependencies["image"].job_id
        assert new_picture != picture.id
        assert studio.generation.job(new_picture).retry_of == picture.id  # pyright: ignore[reportOptionalMemberAccess]
        again = studio.generation.retry(model.id)  # the upstream retry is reused
        assert again.dependencies["image"].job_id == new_picture
        stop = _serve_on(create_app(FakeModelServer()), port)
        try:
            wait_for(studio, retried.id, JobStatus.SUCCEEDED)
        finally:
            stop()


def test_cancelled_upstream_is_not_retried_automatically(tmp_path: Path) -> None:
    with running_studio(
        tmp_path, model_server("fake", f"http://127.0.0.1:{free_port()}")
    ) as studio:
        picture = studio.generation.submit("fake", "text-to-image", prompt="x")
        model = studio.generation.submit(
            "fake", "image-to-3d", dependencies={"image": Dependency(picture.id)}
        )
        studio.generation.cancel(picture.id)
        with pytest.raises(JobStateError, match="was cancelled"):
            studio.generation.retry(model.id)


def test_refused_retry_submits_nothing(tmp_path: Path) -> None:
    with running_studio(
        tmp_path, model_server("fake", f"http://127.0.0.1:{free_port()}")
    ) as studio:
        failed_input = studio.generation.submit("fake", "text-to-image", prompt="x")
        cancelled_input = studio.generation.submit("fake", "text-to-audio", prompt="y")
        combined = studio.generation.submit(
            "fake",
            "image-to-3d",
            dependencies={
                "image": Dependency(failed_input.id),
                "audio": Dependency(cancelled_input.id),
            },
        )
        Repository(studio.database).fail(failed_input.id, "generation_failed", "boom")
        studio.generation.cancel(cancelled_input.id)
        wait_for(studio, combined.id, JobStatus.FAILED)
        before = len(studio.generation.jobs())
        with pytest.raises(JobStateError, match="was cancelled"):
            studio.generation.retry(combined.id)
        assert len(studio.generation.jobs()) == before


def test_missing_input_file_fails_the_job_without_blocking_the_queue(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("fake", url)) as studio,
    ):
        asset = studio.library.import_bytes(png())
        studio.library.file(asset).unlink()
        broken = studio.generation.submit("fake", "image-to-3d", inputs={"image": asset.id})
        independent = studio.generation.submit("fake", "text-to-image", prompt="x")
        failed = wait_for(studio, broken.id, JobStatus.FAILED)
        assert failed.error_code == "internal"
        assert "FileNotFoundError" in (failed.error_message or "")
        wait_for(studio, independent.id, JobStatus.SUCCEEDED)


def test_server_is_reported_busy_during_a_generation(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        job = studio.generation.submit(
            "image", "text-to-image", prompt="x", params={"delay_s": 0.5}
        )
        wait_for(studio, job.id, JobStatus.RUNNING)
        status = studio.server_status("image")
        assert status.info is not None
        assert status.info.status == "busy"
        wait_for(studio, job.id, JobStatus.SUCCEEDED)


def test_close_wakes_waiters(tmp_path: Path) -> None:
    studio = Studio(
        StudioConfig(data_dir=tmp_path, servers=(model_server("image", "http://127.0.0.1:9"),))
    )
    studio.start()
    job = studio.generation.submit("image", "text-to-image", prompt="x")
    result: list[Job] = []
    waiter = threading.Thread(target=lambda: result.append(studio.wait(job.id, 600)))
    waiter.start()
    time.sleep(0.2)
    started = time.monotonic()
    studio.close()
    waiter.join(5)
    assert time.monotonic() - started < 5
    assert result[0].status in (JobStatus.QUEUED, JobStatus.WAITING_MODEL)


def test_one_studio_per_data_directory(tmp_path: Path) -> None:
    first = Studio(StudioConfig(data_dir=tmp_path))
    try:
        with pytest.raises(DataDirectoryBusy):
            Studio(StudioConfig(data_dir=tmp_path))
    finally:
        first.close()
    Studio(StudioConfig(data_dir=tmp_path)).close()  # free again after close


# Library


def test_import_detects_format_and_deduplicates_files(tmp_path: Path) -> None:
    with running_studio(tmp_path) as studio:
        first = studio.library.import_bytes(png(), title="красный")
        second = studio.library.import_bytes(png())
        assert first.id != second.id
        assert first.blob_sha256 == second.blob_sha256
        assert len(list((tmp_path / "blobs").rglob("*.png"))) == 1
        assert first.kind == "image"
        assert first.title == "красный"
        glb = studio.library.import_bytes(media.encode_glb(*media.tetrahedron()))
        assert (glb.kind, glb.mime) == ("mesh", "model/gltf-binary")
        with pytest.raises(ImportError_, match="unsupported file format"):
            studio.library.import_bytes(b"\x00\x01 binary garbage")


def test_import_size_limit(tmp_path: Path) -> None:
    studio = Studio(StudioConfig(data_dir=tmp_path, max_import_bytes=100))
    try:
        with pytest.raises(ImportError_, match="larger than 100"):
            studio.library.import_bytes(png() + b"\0" * 200)
    finally:
        studio.close()


def test_import_from_url(tmp_path: Path) -> None:
    files = FastAPI()

    @files.get("/picture.png")
    def picture() -> Response:  # pyright: ignore[reportUnusedFunction]
        return Response(png(), media_type="image/png")

    @files.get("/large")
    def large() -> Response:  # pyright: ignore[reportUnusedFunction]
        return Response(b"\0" * 5000, media_type="application/octet-stream")

    with live_server(files) as url:
        studio = Studio(StudioConfig(data_dir=tmp_path, max_import_bytes=1000))
        try:
            asset = studio.library.import_url(f"{url}/picture.png")
            assert (asset.origin, asset.source_url) == ("url", f"{url}/picture.png")
            with pytest.raises(ImportError_, match="larger than 1000"):
                studio.library.import_url(f"{url}/large")
            with pytest.raises(ImportError_, match="cannot download"):
                studio.library.import_url(f"{url}/missing")
            with pytest.raises(ImportError_, match="http"):
                studio.library.import_url("file:///etc/passwd")
        finally:
            studio.close()


# Storage and configuration


def test_ids_are_strictly_increasing() -> None:
    ids = [new_id() for _ in range(2000)]
    assert ids == sorted(ids)
    assert len(set(ids)) == len(ids)


def test_migrations_are_applied_once(tmp_path: Path) -> None:
    first = Studio(StudioConfig(data_dir=tmp_path))
    version = first.database.schema_version
    first.close()
    second = Studio(StudioConfig(data_dir=tmp_path))
    migrations = Path(__file__).resolve().parents[1] / "studio/src/studio/storage/migrations"
    assert second.database.schema_version == version == len(list(migrations.glob("*.sql")))
    second.close()


def test_config_file(tmp_path: Path) -> None:
    path = tmp_path / "studio.toml"
    path.write_text(
        f'data_dir = "{tmp_path}"\n'
        '[[servers]]\nid = "image"\ntitle = "SDXL"\nurl = "http://127.0.0.1:9101"\n'
        '[[servers]]\nid = "mesh"\nurl = "http://127.0.0.1:9102/"\ntimeout_s = 600\n'
    )
    config = load_config(path)
    assert [server.id for server in config.servers] == ["image", "mesh"]
    assert (config.server("image").title, config.server("mesh").title) == ("SDXL", None)
    assert config.server("mesh").base_url == "http://127.0.0.1:9102"
    assert config.server("mesh").timeout_s == 600
    assert load_config(tmp_path / "missing.toml").servers == ()
    path.write_text('[[servers]]\nid = "a"\nurl = "http://x"\n' * 2)
    with pytest.raises(ValueError, match="unique"):
        load_config(path)


# Helpers


def _serve_on(app: FastAPI, port: int) -> Callable[[], None]:
    """Serve on a fixed port (unlike ``live_server``); return a stop function."""
    import uvicorn

    server = uvicorn.Server(uvicorn.Config(app, host="127.0.0.1", port=port, log_level="warning"))
    thread = threading.Thread(target=server.run, daemon=True)
    thread.start()

    def stop() -> None:
        server.should_exit = True
        thread.join(timeout=10)

    return stop


@contextmanager
def _proxy(mutate: Callable[[int, Any], tuple[int, Any]]) -> Generator[FastAPI]:
    """Forwards to a fake server and lets the test rewrite generate responses."""
    with TestClient(create_app(FakeModelServer())) as inner:
        while inner.get("/v1/info").json()["status"] != "ready":
            time.sleep(0.01)
        app = FastAPI()

        @app.get("/v1/info")
        def info() -> Any:  # pyright: ignore[reportUnusedFunction]
            return inner.get("/v1/info").json()

        @app.post("/v1/generate")
        async def generate(request: Request) -> JSONResponse:  # pyright: ignore[reportUnusedFunction]
            response = inner.post("/v1/generate", json=await request.json())
            status, body = mutate(response.status_code, response.json())
            return JSONResponse(body, status_code=status)

        yield app


@contextmanager
def _crashing_server(info: dict[str, Any]) -> Generator[str]:
    """Answers /v1/info, then drops the connection on /v1/generate like a crash."""
    listener = socket.socket()
    listener.bind(("127.0.0.1", 0))
    listener.listen()
    listener.settimeout(0.1)
    stopped = threading.Event()
    body = json.dumps(info).encode()

    def serve() -> None:
        while not stopped.is_set():
            try:
                connection, _ = listener.accept()
            except TimeoutError:
                continue
            with connection:
                request = connection.recv(65536)
                if request.startswith(b"GET /v1/info"):
                    connection.sendall(
                        b"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n"
                        + f"Content-Length: {len(body)}\r\nConnection: close\r\n\r\n".encode()
                        + body
                    )
                # Anything else: close without answering.

    thread = threading.Thread(target=serve, daemon=True)
    thread.start()
    try:
        yield f"http://127.0.0.1:{listener.getsockname()[1]}"
    finally:
        stopped.set()
        thread.join(timeout=5)
        listener.close()
