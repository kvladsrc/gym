"""FastAPI application: REST API, server-sent events and the MCP endpoint.

The application owns the studio's lifecycle: dispatching starts with the
application and stops with it. Errors always have the shape
``{"detail": "<message>"}``.
"""

import asyncio
import json
import logging
from collections.abc import AsyncGenerator, Callable, Iterable, Sequence
from contextlib import asynccontextmanager
from pathlib import Path
from typing import Annotated

from fastapi import Body, FastAPI, HTTPException, Query, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import FileResponse, JSONResponse, PlainTextResponse, StreamingResponse
from fastapi.staticfiles import StaticFiles
from starlette.concurrency import run_in_threadpool
from starlette.middleware.trustedhost import TrustedHostMiddleware

from studio.api.schemas import (
    AssetOut,
    AssetUpdate,
    ImportUrl,
    JobCreate,
    JobOut,
    ParentOut,
    ServerOut,
)
from studio.core import Studio
from studio.domain import FILE_EXTENSIONS, Asset, AssetKind, Dependency, JobStatus
from studio.mcp.server import build_mcp
from studio.services.generation import JobRequestError, JobStateError
from studio.services.library import AssetInUse, BadTag, ImportError_, Unset

logger = logging.getLogger("studio.api")

# How often the event stream checks server status and client disconnects.
_EVENT_TICK_S = 1.0
_MAX_WAIT_S = 600.0
# The studio serves only this machine. Checking Host defeats DNS rebinding: a
# web page cannot talk to the API through a hostname that resolves to 127.0.0.1.
LOCAL_HOSTS = ("127.0.0.1", "localhost", "[::1]")


def create_app(
    studio: Studio, *, extra_hosts: Iterable[str] = (), web_dir: Path | None = None
) -> FastAPI:
    """``extra_hosts``: further accepted Host names, e.g. the address the server binds to.
    ``web_dir``: the built web UI (``web/dist``) to serve at ``/``."""
    allowed_hosts = sorted(
        {*LOCAL_HOSTS, *(host for host in extra_hosts if host not in ("0.0.0.0", "::"))}  # noqa: S104
    )
    mcp = build_mcp(studio)
    # Served at exactly /mcp: some MCP clients do not follow redirects on POST.
    mcp_app = mcp.streamable_http_app(streamable_http_path="/mcp")

    @asynccontextmanager
    async def lifespan(_: FastAPI) -> AsyncGenerator[None]:
        studio.start()
        try:
            async with mcp.session_manager.run():
                yield
        finally:
            studio.close()

    app = FastAPI(title="Asset studio", lifespan=lifespan)
    app.add_middleware(TrustedHostMiddleware, allowed_hosts=allowed_hosts)

    @app.exception_handler(RequestValidationError)
    async def validation_error(_: Request, error: RequestValidationError) -> JSONResponse:  # pyright: ignore[reportUnusedFunction]
        parts = [
            f"{'.'.join(str(part) for part in item['loc'] if part != 'body')}: {item['msg']}"
            for item in error.errors()
        ]
        return JSONResponse({"detail": "; ".join(parts)}, status_code=422)

    def job_or_404(job_id: str) -> JobOut:
        job = studio.generation.job(job_id)
        if job is None:
            raise HTTPException(404, f"job {job_id} not found")
        return JobOut.of(job)

    def asset_or_404(asset_id: str) -> Asset:
        asset = studio.library.asset(asset_id)
        if asset is None:
            raise HTTPException(404, f"asset {asset_id} not found")
        return asset

    def servers() -> list[ServerOut]:
        return [ServerOut.of(server, studio) for server in studio.config.servers]

    @app.get("/api/servers")
    def list_servers() -> list[ServerOut]:  # pyright: ignore[reportUnusedFunction]
        return servers()

    @app.post("/api/jobs", status_code=201)
    def create_job(request: JobCreate) -> JobOut:  # pyright: ignore[reportUnusedFunction]
        try:
            job = studio.generation.submit(
                request.server,
                request.task,
                prompt=request.prompt,
                params=request.params,
                count=request.count,
                seed=request.seed,
                inputs=request.inputs,
                dependencies={
                    role: Dependency(dependency.job_id, dependency.output_index)
                    for role, dependency in request.dependencies.items()
                },
                idempotency_key=request.idempotency_key,
            )
        except JobRequestError as error:
            raise HTTPException(422, str(error)) from error
        return JobOut.of(job)

    @app.get("/api/jobs")
    def list_jobs(  # pyright: ignore[reportUnusedFunction]
        status: Annotated[list[JobStatus] | None, Query()] = None,
        limit: Annotated[int, Query(ge=1, le=1000)] = 100,
    ) -> list[JobOut]:
        return [JobOut.of(job) for job in studio.generation.jobs(statuses=status, limit=limit)]

    @app.get("/api/jobs/{job_id}")
    def get_job(job_id: str) -> JobOut:  # pyright: ignore[reportUnusedFunction]
        return job_or_404(job_id)

    @app.post("/api/jobs/{job_id}/wait")
    async def wait_job(  # pyright: ignore[reportUnusedFunction]
        job_id: str, timeout_s: Annotated[float, Query(gt=0, le=_MAX_WAIT_S)] = 60
    ) -> JobOut:
        """Long poll: return when the job is finished, the timeout passes or
        the studio shuts down."""
        job_or_404(job_id)
        return JobOut.of(await run_in_threadpool(studio.wait, job_id, timeout_s))

    @app.post("/api/jobs/{job_id}/cancel")
    def cancel_job(job_id: str) -> JobOut:  # pyright: ignore[reportUnusedFunction]
        job_or_404(job_id)
        try:
            return JobOut.of(studio.generation.cancel(job_id))
        except JobStateError as error:
            raise HTTPException(409, str(error)) from error

    @app.delete("/api/jobs/{job_id}")
    def delete_job(job_id: str) -> JobOut:  # pyright: ignore[reportUnusedFunction]
        """Remove a failed or cancelled job from the history."""
        job_or_404(job_id)
        try:
            return JobOut.of(studio.generation.delete(job_id))
        except JobStateError as error:
            raise HTTPException(409, str(error)) from error

    @app.post("/api/jobs/{job_id}/retry", status_code=201)
    def retry_job(job_id: str) -> JobOut:  # pyright: ignore[reportUnusedFunction]
        job_or_404(job_id)
        try:
            return JobOut.of(studio.generation.retry(job_id))
        except JobStateError as error:
            raise HTTPException(409, str(error)) from error
        except JobRequestError as error:
            raise HTTPException(422, str(error)) from error

    @app.get("/api/assets")
    def list_assets(  # pyright: ignore[reportUnusedFunction]
        kind: AssetKind | None = None,
        favorite: bool | None = None,
        tag: Annotated[list[str] | None, Query()] = None,
        min_rating: Annotated[int | None, Query(ge=0, le=5)] = None,
        unrated: bool = False,
        limit: Annotated[int, Query(ge=1, le=1000)] = 100,
    ) -> list[AssetOut]:
        """Assets matching all filters; ``tag`` may repeat (all must match)."""
        assets = studio.library.assets(
            kind=kind,
            favorite=favorite,
            tags=[t.strip().lower() for t in tag or []],
            min_rating=min_rating,
            unrated=unrated,
            limit=limit,
        )
        return [AssetOut.of(asset) for asset in assets]

    @app.post("/api/assets", status_code=201)
    def upload_asset(  # pyright: ignore[reportUnusedFunction]
        data: Annotated[bytes, Body(media_type="application/octet-stream")],
        title: str | None = None,
    ) -> AssetOut:
        """Upload a file as the raw request body; the format is detected by content."""
        try:
            return AssetOut.of(studio.library.import_bytes(data, title=title))
        except ImportError_ as error:
            raise HTTPException(422, str(error)) from error

    @app.post("/api/assets/import-url", status_code=201)
    def import_url(request: ImportUrl) -> AssetOut:  # pyright: ignore[reportUnusedFunction]
        try:
            return AssetOut.of(studio.library.import_url(request.url, title=request.title))
        except ImportError_ as error:
            raise HTTPException(422, str(error)) from error

    @app.get("/api/assets/{asset_id}")
    def get_asset(asset_id: str) -> AssetOut:  # pyright: ignore[reportUnusedFunction]
        return AssetOut.of(asset_or_404(asset_id))

    @app.delete("/api/assets/{asset_id}")
    def delete_asset(asset_id: str) -> AssetOut:  # pyright: ignore[reportUnusedFunction]
        """Remove from the library (ADR-004); jobs and lineage still show it."""
        try:
            asset = studio.library.delete(asset_id)
        except AssetInUse as error:
            raise HTTPException(409, str(error)) from error
        if asset is None:
            raise HTTPException(404, f"asset {asset_id} not found")
        return AssetOut.of(asset)

    @app.patch("/api/assets/{asset_id}")
    def update_asset(asset_id: str, request: AssetUpdate) -> AssetOut:  # pyright: ignore[reportUnusedFunction]
        asset_or_404(asset_id)
        try:
            updated = studio.library.update(
                asset_id,
                title=request.title,
                favorite=request.favorite,
                rating=request.rating if "rating" in request.model_fields_set else Unset,
                tags=request.tags,
            )
        except BadTag as error:
            raise HTTPException(422, str(error)) from error
        assert updated is not None
        return AssetOut.of(updated)

    @app.get("/api/assets/{asset_id}/file")
    def asset_file(asset_id: str) -> FileResponse:  # pyright: ignore[reportUnusedFunction]
        asset = asset_or_404(asset_id)
        if asset.deleted_at is not None:
            raise HTTPException(410, f"asset {asset_id} was deleted")
        path = studio.library.file(asset)
        if not path.is_file():
            raise HTTPException(410, f"the file of asset {asset_id} is missing")
        return FileResponse(
            path,
            media_type=asset.mime,
            filename=_download_name(asset),
            content_disposition_type="inline",
            # Served as stored: a text asset must never be sniffed into HTML.
            headers={"X-Content-Type-Options": "nosniff"},
        )

    @app.get("/api/assets/{asset_id}/lineage")
    def lineage(asset_id: str) -> list[ParentOut]:  # pyright: ignore[reportUnusedFunction]
        asset_or_404(asset_id)
        return [
            ParentOut(asset_id=parent, relation=relation)
            for parent, relation in studio.library.parents(asset_id)
        ]

    @app.get("/api/events")
    async def events(request: Request) -> StreamingResponse:  # pyright: ignore[reportUnusedFunction]
        """Server-sent events: ``job`` on job changes, ``servers`` when a server's
        state changes (and once on connect). Rapid changes of one job are
        coalesced into one event with its latest state."""
        return StreamingResponse(
            _event_stream(request, studio, servers),
            media_type="text/event-stream",
            headers={"Cache-Control": "no-cache"},
        )

    @app.api_route(
        "/api/{path:path}",
        methods=["GET", "POST", "PUT", "PATCH", "DELETE"],
        include_in_schema=False,
    )
    def unknown_api_path(path: str) -> None:  # pyright: ignore[reportUnusedFunction]
        """Keep the error shape for unknown API paths (the root mount answers in text)."""
        raise HTTPException(404, f"no such API endpoint: /api/{path}")

    _serve_web_ui(app, web_dir)
    # Last: the MCP app is mounted at the root and serves only /mcp; every
    # route defined above takes precedence.
    app.mount("/", mcp_app)
    return app


async def _event_stream(
    request: Request, studio: Studio, servers: Callable[[], list[ServerOut]]
) -> AsyncGenerator[str]:
    loop = asyncio.get_running_loop()
    # A set, not a queue: memory stays bounded by the number of jobs that
    # changed, however slow the client is.
    pending: set[str] = set()
    wakeup = asyncio.Event()

    def mark(job_id: str) -> None:
        pending.add(job_id)
        wakeup.set()

    def listener(job_id: str) -> None:  # called from dispatcher threads
        if not loop.is_closed():
            loop.call_soon_threadsafe(mark, job_id)

    # Subscribing inside the generator ties the subscription to the generator's
    # lifetime: ``finally`` runs even if the client leaves at once.
    unsubscribe = studio.subscribe(listener)
    try:
        last_servers: str | None = None
        while not (await request.is_disconnected() or studio.closing.is_set()):
            snapshot = await run_in_threadpool(servers)
            # checked_at changes on every poll; send only real changes.
            comparable = json.dumps(
                [server.model_dump(mode="json", exclude={"checked_at"}) for server in snapshot]
            )
            if comparable != last_servers:
                last_servers = comparable
                current = json.dumps([server.model_dump(mode="json") for server in snapshot])
                yield f"event: servers\ndata: {current}\n\n"
            try:
                await asyncio.wait_for(wakeup.wait(), _EVENT_TICK_S)
            except TimeoutError:
                yield ": keep-alive\n\n"
                continue
            wakeup.clear()
            changed = sorted(pending)
            pending.clear()
            for job in await run_in_threadpool(_jobs, studio, changed):
                yield f"event: job\ndata: {job.model_dump_json()}\n\n"
    finally:
        unsubscribe()


def _serve_web_ui(app: FastAPI, web_dir: Path | None) -> None:
    index = None if web_dir is None else web_dir / "index.html"

    @app.get("/", include_in_schema=False, response_model=None)
    def web_ui() -> FileResponse | PlainTextResponse:  # pyright: ignore[reportUnusedFunction]
        if index is None or not index.is_file():
            return PlainTextResponse(
                "The web UI is not built. Run: just assets_studio web-build", status_code=503
            )
        return FileResponse(index, headers={"Cache-Control": "no-cache"})

    if web_dir is not None and (web_dir / "assets").is_dir():
        # Vite puts hashed bundles here; the names change with the content.
        app.mount("/assets", StaticFiles(directory=web_dir / "assets"), name="web-assets")


def _jobs(studio: Studio, job_ids: Sequence[str]) -> list[JobOut]:
    jobs = (studio.generation.job(job_id) for job_id in job_ids)
    return [JobOut.of(job) for job in jobs if job is not None]


def _download_name(asset: Asset) -> str:
    """A readable file name; the stored blob is named by its hash."""
    stem = asset.title or f"{asset.kind}-{asset.id}"
    safe = "".join(char if char.isalnum() or char in "-_ ." else "_" for char in stem).strip()
    return f"{safe or asset.kind}{FILE_EXTENSIONS.get(asset.mime, '')}"
