"""Domain entities and rules of the studio. No I/O."""

import secrets
import threading
import time
from dataclasses import dataclass, field
from enum import StrEnum
from typing import Any, Literal


class AssetKind(StrEnum):
    IMAGE = "image"
    MESH = "mesh"
    AUDIO = "audio"
    TEXT = "text"
    VIDEO = "video"


class Origin(StrEnum):
    GENERATED = "generated"
    UPLOAD = "upload"
    URL = "url"


class JobStatus(StrEnum):
    QUEUED = "queued"
    WAITING_MODEL = "waiting_model"
    RUNNING = "running"
    SUCCEEDED = "succeeded"
    FAILED = "failed"
    CANCELLED = "cancelled"


# Jobs that have not been sent to a model server yet.
PENDING = (JobStatus.QUEUED, JobStatus.WAITING_MODEL)
FINISHED = (JobStatus.SUCCEEDED, JobStatus.FAILED, JobStatus.CANCELLED)

# Error codes set by the studio itself, in addition to the contract's codes.
StudioErrorCode = Literal[
    "dependency_failed", "connection_lost", "timeout", "interrupted", "invalid_response", "internal"
]

# A job fails after this many consecutive retryable errors ("busy" does not count).
MAX_RETRYABLE_FAILURES = 3

# Formats the studio stores, all playable in the browser. Each kind has a
# canonical format that every model server accepts (ADR-001, ADR-003); others
# are converted before sending. Content detection tries them in this order:
# plain text last, as almost any header without NUL bytes would pass it.
MIME_KINDS: dict[str, AssetKind] = {
    "image/png": AssetKind.IMAGE,
    "image/jpeg": AssetKind.IMAGE,
    "image/webp": AssetKind.IMAGE,
    "audio/wav": AssetKind.AUDIO,
    "audio/flac": AssetKind.AUDIO,
    "audio/ogg": AssetKind.AUDIO,
    "model/gltf-binary": AssetKind.MESH,
    "video/mp4": AssetKind.VIDEO,
    "video/webm": AssetKind.VIDEO,
    "audio/mpeg": AssetKind.AUDIO,  # a weak signature (a sync word): late
    "text/plain": AssetKind.TEXT,
}
FILE_EXTENSIONS: dict[str, str] = {
    "image/png": ".png",
    "image/jpeg": ".jpg",
    "image/webp": ".webp",
    "audio/wav": ".wav",
    "audio/flac": ".flac",
    "audio/ogg": ".ogg",
    "audio/mpeg": ".mp3",
    "model/gltf-binary": ".glb",
    "video/mp4": ".mp4",
    "video/webm": ".webm",
    "text/plain": ".txt",
}
CANONICAL_MIME: dict[AssetKind, str] = {
    AssetKind.IMAGE: "image/png",
    AssetKind.AUDIO: "audio/wav",
    AssetKind.MESH: "model/gltf-binary",
    AssetKind.TEXT: "text/plain",
    AssetKind.VIDEO: "video/mp4",
}


_id_lock = threading.Lock()
_last_id_ms = 0


def new_id() -> str:
    """Time-sortable identifier: 12 hex digits of milliseconds + 10 random.

    Strictly increasing within the process, even for ids created in the same
    millisecond: the queue relies on id order for first-in, first-out.
    """
    global _last_id_ms
    with _id_lock:
        _last_id_ms = max(time.time_ns() // 1_000_000, _last_id_ms + 1)
        milliseconds = _last_id_ms
    return f"{milliseconds:012x}{secrets.token_hex(5)}"


@dataclass(frozen=True)
class Asset:
    id: str
    kind: AssetKind
    mime: str
    size_bytes: int
    blob_sha256: str
    origin: Origin
    created_at: str
    title: str | None = None
    source_url: str | None = None
    favorite: bool = False
    meta: dict[str, Any] = field(default_factory=dict[str, Any])
    # Deleted assets leave the library; jobs and lineage still refer to them.
    deleted_at: str | None = None


@dataclass(frozen=True)
class Dependency:
    """Feed output ``output_index`` of job ``job_id`` into an input role."""

    job_id: str
    output_index: int = 0


@dataclass(frozen=True)
class Job:
    id: str
    server: str
    task: str
    status: JobStatus
    count: int
    created_at: str
    prompt: str | None = None
    params: dict[str, Any] = field(default_factory=dict[str, Any])
    seed: int | None = None
    inputs: dict[str, str] = field(default_factory=dict[str, str])  # role -> asset id
    dependencies: dict[str, Dependency] = field(default_factory=dict[str, Dependency])
    outputs: list[str] = field(default_factory=list[str])  # asset ids in position order
    error_code: str | None = None
    error_message: str | None = None
    retryable_failures: int = 0
    version: int = 0  # bumped on every change; newer state wins
    model_snapshot: dict[str, Any] | None = None
    effective_params: dict[str, Any] | None = None
    timing: dict[str, Any] | None = None
    idempotency_key: str | None = None
    retry_of: str | None = None
    started_at: str | None = None
    finished_at: str | None = None
