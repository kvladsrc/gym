"""Request and response bodies of the HTTP API (also used by the MCP tools)."""

from typing import Annotated, Any, Literal

from model_server_sdk.contract import ModelInfo, TaskInfo
from pydantic import BaseModel, ConfigDict, Field

from studio.config import Server
from studio.core import Studio
from studio.domain import Asset, Job

ServerState = Literal["unavailable", "loading", "ready", "busy", "error"]


class DependencyIn(BaseModel):
    model_config = ConfigDict(extra="forbid")

    job_id: str
    output_index: int = Field(default=0, ge=0)


class JobCreate(BaseModel):
    model_config = ConfigDict(extra="forbid")

    server: str
    task: str
    prompt: str | None = None
    params: dict[str, Any] = Field(default_factory=dict[str, Any])
    count: int = Field(default=1, ge=1)
    seed: int | None = Field(default=None, ge=0, lt=2**32)
    # Input role -> asset id.
    inputs: dict[str, str] = Field(default_factory=dict[str, str])
    # Input role -> output of another job.
    dependencies: dict[str, DependencyIn] = Field(default_factory=dict[str, DependencyIn])
    idempotency_key: str | None = None


class AssetUpdate(BaseModel):
    model_config = ConfigDict(extra="forbid")

    title: str | None = None
    favorite: bool | None = None
    # 0-5; an explicit null clears the rating (ADR-007).
    rating: Annotated[int, Field(ge=0, le=5)] | None = None
    # Replaces all tags, e.g. ["style:cartoon", "model:hunyuan3d"].
    tags: list[str] | None = None


class ImportUrl(BaseModel):
    model_config = ConfigDict(extra="forbid")

    url: str
    title: str | None = None


class DependencyOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    job_id: str
    output_index: int


class JobOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: str
    server: str
    task: str
    status: str
    count: int
    created_at: str
    prompt: str | None
    params: dict[str, Any]
    seed: int | None
    inputs: dict[str, str]
    dependencies: dict[str, DependencyOut]
    outputs: list[str]
    error_code: str | None
    error_message: str | None
    retryable_failures: int
    version: int
    model_snapshot: dict[str, Any] | None
    effective_params: dict[str, Any] | None
    timing: dict[str, Any] | None
    idempotency_key: str | None
    retry_of: str | None
    started_at: str | None
    finished_at: str | None
    # Set when a failed or cancelled job was deleted: it is gone from the
    # history, retries and dependents still refer to it (ADR-004).
    deleted_at: str | None

    @classmethod
    def of(cls, job: Job) -> "JobOut":
        return cls.model_validate(job)


class AssetOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: str
    kind: str
    mime: str
    size_bytes: int
    blob_sha256: str
    origin: str
    created_at: str
    title: str | None
    source_url: str | None
    favorite: bool
    meta: dict[str, Any]
    # Set when the asset was deleted: it is gone from the library, but jobs
    # and lineage still show where it was (ADR-004).
    deleted_at: str | None
    rating: int | None
    tags: list[str]
    file_url: str

    @classmethod
    def of(cls, asset: Asset) -> "AssetOut":
        return cls.model_validate({**vars(asset), "file_url": f"/api/assets/{asset.id}/file"})


class ParentOut(BaseModel):
    asset_id: str
    relation: str


class ServerOut(BaseModel):
    id: str
    title: str
    url: str
    state: ServerState
    message: str | None
    model: ModelInfo | None
    tasks: list[TaskInfo]
    checked_at: str
    # False until the server has answered once (then its tasks are remembered).
    seen: bool

    @classmethod
    def of(cls, server: Server, studio: Studio) -> "ServerOut":
        status = studio.server_status(server.id)
        info = status.info
        # While the server is down, its model and tasks as last seen, also
        # from before a restart (ADR-004): jobs can wait for it.
        known = info or status.last_seen
        return cls(
            id=server.id,
            title=server.title or (known.model.name if known else server.id),
            url=server.base_url,
            state="unavailable" if info is None else info.status,
            message=status.unavailable if info is None else info.status_message,
            model=known.model if known else None,
            tasks=known.tasks if known else [],
            checked_at=status.checked_at,
            seen=known is not None,
        )
