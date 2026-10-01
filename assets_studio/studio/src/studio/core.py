"""The studio assembled: storage, services and the dispatcher."""

import fcntl
import logging
import threading
import time
from collections.abc import Callable

from studio.config import StudioConfig
from studio.dispatch.dispatcher import Dispatcher, ServerStatus
from studio.domain import FINISHED, Job
from studio.services.generation import Generation, JobRequestError
from studio.services.library import Library
from studio.storage.blobs import BlobStore
from studio.storage.db import Database
from studio.storage.repository import Repository

logger = logging.getLogger("studio")


class DataDirectoryBusy(RuntimeError):
    """Another studio process uses the same data directory."""


class Studio:
    """Entry point for the HTTP API and MCP. Call ``start`` to begin dispatching."""

    def __init__(self, config: StudioConfig) -> None:
        self.config = config
        config.data_dir.mkdir(parents=True, exist_ok=True)
        # One process per data directory: restart recovery (interrupted jobs)
        # would otherwise fail jobs another process is still running.
        self._lock = (config.data_dir / ".lock").open("w")
        try:
            fcntl.flock(self._lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            self._lock.close()
            raise DataDirectoryBusy(f"another studio is already using {config.data_dir}") from None
        try:
            self.database = Database(config.data_dir / "studio.sqlite3")
        except BaseException:
            self._lock.close()
            raise
        repository = Repository(self.database)
        blobs = BlobStore(config.data_dir / "blobs")
        self._listeners: list[Callable[[str], None]] = []
        self._listeners_lock = threading.Lock()
        # Set on close: long waits (API long-poll, MCP wait_for_job) return early.
        self.closing = threading.Event()
        self._waiters: set[threading.Event] = set()
        self.dispatcher = Dispatcher(config, repository, blobs, on_change=self._job_changed)
        self.library = Library(config, repository, blobs)
        self.generation = Generation(
            config, repository, wake=self.dispatcher.wake, on_change=self._job_changed
        )

    def start(self) -> None:
        self.dispatcher.start()

    def begin_closing(self) -> None:
        """End long waits and event streams now; ``close`` follows. Safe to
        call from a signal handler and more than once."""
        self.closing.set()
        with self._listeners_lock:
            waiters = list(self._waiters)
        for waiter in waiters:
            waiter.set()

    def close(self) -> None:
        self.begin_closing()
        self.dispatcher.stop()
        self.database.close()
        self._lock.close()  # releases the directory lock

    def wait(self, job_id: str, timeout_s: float) -> Job:
        """Block until the job is finished, ``timeout_s`` passes or the studio
        closes; return the job's state at that moment."""
        changed = threading.Event()

        def listener(changed_id: str) -> None:
            if changed_id == job_id:
                changed.set()

        unsubscribe = self.subscribe(listener)
        with self._listeners_lock:
            self._waiters.add(changed)
        try:
            deadline = time.monotonic() + timeout_s
            while True:
                changed.clear()
                job = self.generation.job(job_id)
                if job is None:
                    raise JobRequestError(f"job {job_id} not found")
                remaining = deadline - time.monotonic()
                if job.status in FINISHED or remaining <= 0 or self.closing.is_set():
                    return job
                changed.wait(remaining)
        finally:
            with self._listeners_lock:
                self._waiters.discard(changed)
            unsubscribe()

    def server_status(self, server_id: str) -> ServerStatus:
        return self.dispatcher.status(server_id)

    def subscribe(self, listener: Callable[[str], None]) -> Callable[[], None]:
        """Call ``listener(job_id)`` on every job change (from dispatcher threads).

        Returns a function that unsubscribes.
        """
        with self._listeners_lock:
            self._listeners.append(listener)

        def unsubscribe() -> None:
            with self._listeners_lock:
                self._listeners.remove(listener)

        return unsubscribe

    def _job_changed(self, job_id: str) -> None:
        with self._listeners_lock:
            listeners = list(self._listeners)
        for listener in listeners:
            try:
                listener(job_id)
            except Exception:
                # A broken subscriber (e.g. a closed event stream) must not
                # disturb the dispatcher.
                logger.exception("job change listener failed")
