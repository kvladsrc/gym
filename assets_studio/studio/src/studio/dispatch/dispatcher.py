"""Sends queued jobs to model servers (ADR-002).

One worker thread per server address: a server handles one generation at a
time, and several configured servers may share an address. Each worker polls
``/v1/info``, which also keeps the server status current for the UI, and
remembers what the server declared, so that its tasks are known after a
restart while it is down (ADR-004).
"""

import base64
import logging
import threading
from collections.abc import Callable
from dataclasses import dataclass
from typing import Any

from model_server_sdk.contract import GenerateResponse, Info, TaskInfo, mime_matches
from pydantic import ValidationError

from model_server_sdk import media as signatures
from studio.config import StudioConfig
from studio.dispatch.client import GenerateFailure, ModelClient, NotSent, ServerUnavailable
from studio.domain import MAX_RETRYABLE_FAILURES, MIME_KINDS, Asset, Job, Origin, new_id
from studio.media import UnsupportedMedia, normalize_text, to_canonical
from studio.storage.blobs import BlobStore
from studio.storage.repository import Repository, now

logger = logging.getLogger("studio.dispatch")


@dataclass(frozen=True)
class ServerStatus:
    """What the last poll of a server returned."""

    url: str
    info: Info | None
    unavailable: str | None
    checked_at: str
    # The last answer the server gave, kept while it is down: its tasks let the
    # UI and agents queue jobs for a server that is not running yet.
    last_seen: Info | None = None

    @property
    def ready(self) -> bool:
        return self.info is not None and self.info.status == "ready"


def _declaration(info: Info | None) -> str | None:
    """What a server declares about itself (model, tasks), not its momentary state."""
    if info is None:
        return None
    return info.model_dump_json(include={"contract", "model", "tasks"})


def _remembered(remembered: dict[str, dict[str, Any]], servers: list[str]) -> Info | None:
    for server in servers:
        if server in remembered:
            try:
                return Info.model_validate(remembered[server])
            except ValidationError:
                logger.warning("ignoring an unreadable remembered declaration of %s", server)
    return None


class _JobProblem(Exception):
    """The job cannot be sent as it is; it fails with this code."""

    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.message = message


class _Worker:
    def __init__(self, url: str, servers: list[str], remembered: Info | None) -> None:
        self.url = url
        self.servers = servers
        self.wakeup = threading.Event()
        self.status = ServerStatus(url, None, "not checked yet", now(), remembered)
        # What is stored for these servers (model and tasks), to write only
        # changes. None at first: the first answer is written for every id of
        # this address, whatever an id had stored before.
        self.stored: str | None = None
        self.thread: threading.Thread | None = None

    def observe(self, info: Info | ServerUnavailable) -> None:
        last_seen = info if isinstance(info, Info) else self.status.last_seen
        self.status = (
            ServerStatus(self.url, None, info.reason, now(), last_seen)
            if isinstance(info, ServerUnavailable)
            else ServerStatus(self.url, info, None, now(), last_seen)
        )


class Dispatcher:
    def __init__(
        self,
        config: StudioConfig,
        repository: Repository,
        blobs: BlobStore,
        *,
        on_change: Callable[[str], None] = lambda _: None,
    ) -> None:
        """``on_change(job_id)`` is called after every job state transition."""
        self._config = config
        self._repository = repository
        self._blobs = blobs
        self._on_change = on_change
        self._stop = threading.Event()
        groups: dict[str, list[str]] = {}
        for server in config.servers:
            groups.setdefault(server.base_url, []).append(server.id)
        remembered = repository.remembered_servers()
        self._workers = {
            url: _Worker(url, ids, _remembered(remembered, ids)) for url, ids in groups.items()
        }
        self._by_server = {
            server: worker for worker in self._workers.values() for server in worker.servers
        }

    def start(self) -> None:
        self._changed(self._repository.interrupt_running())
        for worker in self._workers.values():
            worker.thread = threading.Thread(
                target=self._work, args=(worker,), name=f"dispatch {worker.url}", daemon=True
            )
            worker.thread.start()

    def stop(self) -> None:
        """Stop polling. A generation in progress is abandoned (ADR-002: interrupted)."""
        self._stop.set()
        self.wake()
        for worker in self._workers.values():
            if worker.thread is not None:
                worker.thread.join(timeout=10)

    def wake(self) -> None:
        """Re-check the queue now (a job was created, cancelled or finished)."""
        for worker in self._workers.values():
            worker.wakeup.set()

    def status(self, server_id: str) -> ServerStatus:
        return self._by_server[server_id].status

    def _work(self, worker: _Worker) -> None:
        client = ModelClient(worker.url)
        try:
            while not self._stop.is_set():
                worker.wakeup.clear()
                pause = self._step(worker, client)
                if pause:
                    worker.wakeup.wait(self._config.poll_interval_s)
        finally:
            client.close()

    def _step(self, worker: _Worker, client: ModelClient) -> bool:
        """Do one unit of work; return True to pause before the next one."""
        try:
            info = client.info()
            worker.observe(info)
            if isinstance(info, Info) and info.status != "loading":
                self._remember(worker, info)
            job = self._repository.next_ready(worker.servers)
            if job is None:
                return True
            if not worker.status.ready:
                self._changed(self._repository.mark_waiting(worker.servers))
                return True
            assert worker.status.info is not None
            return self._run(job, worker, client)
        except Exception:
            # A bug must not kill the worker silently; log it and keep serving.
            logger.exception("dispatcher step failed for %s", worker.url)
            return True

    def _remember(self, worker: _Worker, info: Info) -> None:
        declaration = _declaration(info)
        if declaration == worker.stored:
            return
        for server in worker.servers:
            self._repository.remember_server(server, info.model_dump(mode="json"))
        worker.stored = declaration

    def _run(self, job: Job, worker: _Worker, client: ModelClient) -> bool:
        info = worker.status.info
        assert info is not None
        try:
            request, parents = self._request(job, info)
        except _JobProblem as problem:
            self._finished(self._repository.fail(job.id, problem.code, problem.message))
            return False
        except Exception as error:
            # E.g. a missing blob or an image too large to decode. Left pending,
            # the job would be picked again forever and block the whole queue.
            logger.exception("cannot prepare job %s", job.id)
            message = f"cannot prepare inputs: {type(error).__name__}: {error}"
            self._finished(self._repository.fail(job.id, "internal", message))
            return False
        if not self._repository.start(job.id):
            return False  # cancelled meanwhile
        # While this thread waits for the generation it cannot poll /v1/info;
        # the server is known to be busy with this job.
        busy = info.model_copy(update={"status": "busy"})
        worker.observe(busy)
        try:
            return self._send(job, request, parents, client)
        except Exception as error:
            # Whatever went wrong (a listener, the disk, the database), the job
            # must not stay "running" with nobody working on it.
            logger.exception("job %s failed unexpectedly", job.id)
            self._finished(
                self._repository.fail(job.id, "internal", f"{type(error).__name__}: {error}")
            )
            return True

    def _send(
        self, job: Job, request: dict[str, Any], parents: list[str], client: ModelClient
    ) -> bool:
        self._on_change(job.id)
        timeout_s = self._config.server(job.server).timeout_s
        try:
            response = client.generate(request, timeout_s=timeout_s)
        except NotSent:
            self._repository.requeue(job.id, retryable_failures=job.retryable_failures)
            self._on_change(job.id)
            return True
        except GenerateFailure as failure:
            return self._handle_failure(job, failure)
        with self._blobs.lock:  # stored files and their assets, together
            try:
                outputs = self._outputs(job, response)
            except _JobProblem as problem:
                self._finished(self._repository.fail(job.id, problem.code, problem.message))
                return False
            self._repository.succeed(
                job.id,
                outputs,
                parents=parents,
                seed=response.seed,
                model_snapshot=response.model.model_dump(mode="json"),
                effective_params=response.params,
                timing=response.timing.model_dump(mode="json"),
            )
        self._finished([job.id])
        return False

    def _handle_failure(self, job: Job, failure: GenerateFailure) -> bool:
        if failure.code == "busy":
            # Normal state (another client is using the server): no retry budget spent.
            self._repository.requeue(job.id, retryable_failures=job.retryable_failures)
        elif failure.retryable and job.retryable_failures + 1 < MAX_RETRYABLE_FAILURES:
            self._repository.requeue(job.id, retryable_failures=job.retryable_failures + 1)
        else:
            self._finished(self._repository.fail(job.id, failure.code, failure.message))
            return False
        self._on_change(job.id)
        return True

    def _changed(self, job_ids: list[str]) -> None:
        for job_id in job_ids:
            self._on_change(job_id)

    def _finished(self, job_ids: list[str]) -> None:
        """Jobs finished: tell listeners and let dependent jobs proceed."""
        self._changed(job_ids)
        self.wake()

    def _request(self, job: Job, info: Info) -> tuple[dict[str, Any], list[str]]:
        task = next((task for task in info.tasks if task.task == job.task), None)
        if task is None:
            raise _JobProblem(
                "unsupported_task", f"model {info.model.id} does not offer task {job.task}"
            )
        assets = dict(job.inputs)
        for role, dependency in job.dependencies.items():
            asset_id = self._repository.output_asset(dependency.job_id, dependency.output_index)
            if asset_id is None:
                raise _JobProblem(
                    "invalid_request",
                    f"job {dependency.job_id} has no output {dependency.output_index}",
                )
            assets[role] = asset_id
        inputs: list[dict[str, str]] = []
        for role, asset_id in sorted(assets.items()):
            asset = self._repository.asset(asset_id)
            if asset is None:
                raise _JobProblem("invalid_request", f"input asset {asset_id} not found")
            if asset.deleted_at is not None:
                # Deletion refuses inputs of unfinished jobs; this only guards
                # against anything that slips past that.
                raise _JobProblem("invalid_request", f"input asset {asset_id} was deleted")
            data, mime = self._encode_input(task, role, asset)
            inputs.append(
                {"role": role, "mime": mime, "data_b64": base64.b64encode(data).decode("ascii")}
            )
        request: dict[str, Any] = {
            "task": job.task,
            "inputs": inputs,
            "params": job.params,
            "count": job.count,
        }
        if job.prompt is not None:
            request["prompt"] = job.prompt
        if job.seed is not None:
            request["seed"] = job.seed
        return request, sorted(set(assets.values()))

    def _encode_input(self, task: TaskInfo, role: str, asset: Asset) -> tuple[bytes, str]:
        data = self._blobs.read(asset.blob_sha256, asset.mime)
        spec = next((spec for spec in task.inputs if spec.role == role), None)
        if spec is None or any(mime_matches(asset.mime, pattern) for pattern in spec.mime):
            return data, asset.mime  # an unknown role is for the server to reject
        try:
            return to_canonical(data, asset.mime)
        except UnsupportedMedia as error:
            raise _JobProblem("invalid_request", f"input {role}: {error}") from error

    def _outputs(self, job: Job, response: GenerateResponse) -> list[Asset]:
        assets: list[Asset] = []
        for index, output in enumerate(response.outputs):
            mime = output.mime
            try:
                data = base64.b64decode(output.data_b64, validate=True)
            except ValueError as error:
                raise _JobProblem("invalid_response", f"output {index}: {error}") from error
            # The declared type is checked, not guessed: a text that starts with
            # "ID3" is still text.
            if mime not in MIME_KINDS:
                raise _JobProblem("invalid_response", f"output {index}: unsupported type {mime}")
            if not signatures.looks_like(mime, data):
                raise _JobProblem(
                    "invalid_response", f"output {index} claims {mime} but is not one"
                )
            if mime == "text/plain":
                data = normalize_text(data)  # as imported text is
            sha256 = self._blobs.put(data, mime)
            assets.append(
                Asset(
                    id=new_id(),
                    kind=MIME_KINDS[mime],
                    mime=mime,
                    size_bytes=len(data),
                    blob_sha256=sha256,
                    origin=Origin.GENERATED,
                    created_at=now(),
                    meta=output.meta,
                    # A readable default name; the user can rename the asset.
                    title=_title(job.prompt, index if len(response.outputs) > 1 else None),
                )
            )
        return assets


_TITLE_LENGTH = 80


def _title(prompt: str | None, variant: int | None = None) -> str | None:
    """A default name from the prompt; variants of one job are numbered."""
    if not prompt or not prompt.strip():
        return None
    text = " ".join(prompt.split())
    if variant is not None:
        suffix = f" · {variant + 1}"
        limit = _TITLE_LENGTH - len(suffix)
        return (text if len(text) <= limit else text[: limit - 1] + "…") + suffix
    return text if len(text) <= _TITLE_LENGTH else text[: _TITLE_LENGTH - 1] + "…"
