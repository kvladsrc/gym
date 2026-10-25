"""A separate local service; talks to studio only through its public HTTP API."""

import asyncio
import contextlib
from collections.abc import AsyncGenerator
from contextlib import asynccontextmanager
from pathlib import Path
from typing import Any

import httpx2
from fastapi import FastAPI, HTTPException, Query
from fastapi.responses import FileResponse, Response
from fastapi.staticfiles import StaticFiles

from studio_flow.engine import Engine, GraphError, preflight
from studio_flow.models import FlowUpdate, Graph, StartRun
from studio_flow.store import Conflict, Store


async def checked(response: httpx2.Response) -> Any:
    if response.is_error:
        raise HTTPException(response.status_code, response.text[:2000])
    return response.json()


def create_app(
    *,
    database: Path,
    studio_url: str = "http://127.0.0.1:9000",
    web_dir: Path | None = None,
    transport: httpx2.AsyncBaseTransport | None = None,
) -> FastAPI:
    store = Store(database)
    client = httpx2.AsyncClient(base_url=studio_url.rstrip("/"), timeout=30, transport=transport)
    engine = Engine(store, client)

    @asynccontextmanager
    async def lifespan(_app: FastAPI) -> AsyncGenerator[None]:
        task = asyncio.create_task(engine.pump())
        try:
            yield
        finally:
            task.cancel()
            with contextlib.suppress(asyncio.CancelledError):
                await task
            await client.aclose()

    app = FastAPI(title="Studio flow", lifespan=lifespan)
    app.state.store, app.state.engine = store, engine

    @app.exception_handler(httpx2.HTTPError)
    async def upstream_error(_request: Any, error: httpx2.HTTPError) -> Response:
        from fastapi.responses import JSONResponse

        return JSONResponse(status_code=502, content={"detail": f"Studio is unavailable: {error}"})

    def get_flow(identity: str) -> dict[str, Any]:
        flow = store.flow(identity)
        if flow is None:
            raise HTTPException(404, "workflow not found")
        return flow

    @app.get("/flow-api/config")
    async def config() -> dict[str, str]:
        return {"studio_url": studio_url}

    @app.get("/flow-api/flows")
    async def list_flows() -> list[dict[str, Any]]:
        return store.flows()

    @app.post("/flow-api/flows", status_code=201)
    async def create_flow(graph: Graph) -> dict[str, Any]:
        return store.save(graph)

    @app.get("/flow-api/flows/{identity}")
    async def read_flow(identity: str) -> dict[str, Any]:
        return get_flow(identity)

    @app.put("/flow-api/flows/{identity}")
    async def update_flow(identity: str, update: FlowUpdate) -> dict[str, Any]:
        get_flow(identity)
        try:
            return store.save(update.graph, identity, update.revision)
        except Conflict as error:
            raise HTTPException(409, str(error)) from error

    @app.delete("/flow-api/flows/{identity}", status_code=204)
    async def delete_flow(identity: str) -> Response:
        get_flow(identity)
        store.delete(identity)
        return Response(status_code=204)

    @app.post("/flow-api/flows/{identity}/runs", status_code=202)
    async def start(identity: str, request: StartRun) -> dict[str, Any]:
        flow = get_flow(identity)
        try:
            await preflight(Graph.model_validate(flow["graph"]), client)
            run = store.new_run(flow, request.request_id)
        except GraphError as error:
            raise HTTPException(422, str(error)) from error
        except Conflict as error:
            raise HTTPException(409, str(error)) from error
        return run

    @app.get("/flow-api/flows/{identity}/runs")
    async def list_runs(identity: str) -> list[dict[str, Any]]:
        # Run snapshots remain accessible even after their saved graph is deleted.
        return store.runs(identity)

    @app.get("/flow-api/runs/{identity}")
    async def read_run(identity: str) -> dict[str, Any]:
        run = await engine.view(identity)
        if run is None:
            raise HTTPException(404, "run not found")
        return run

    @app.post("/flow-api/runs/{identity}/cancel")
    async def cancel_run(identity: str) -> dict[str, Any]:
        async with engine.lock:
            run = store.run(identity)
            if run is None:
                raise HTTPException(404, "run not found")
            store.receipt(identity, run["jobs"], state="cancelling")
        await engine.cancel(identity)
        result = await engine.view(identity)
        assert result
        return result

    @app.get("/flow-api/studio/servers")
    async def servers() -> Any:
        return await checked(await client.get("/api/servers"))

    @app.get("/flow-api/studio/assets")
    async def assets(limit: int = Query(default=200, ge=1, le=1000)) -> Any:
        return await checked(await client.get("/api/assets", params={"limit": limit}))

    @app.get("/flow-api/studio/assets/{identity}/file")
    async def asset_file(identity: str) -> Response:
        response = await client.get(f"/api/assets/{identity}/file")
        if response.is_error:
            raise HTTPException(response.status_code, "asset file unavailable")
        disposition = response.headers.get("content-disposition", "")
        parameters = disposition.partition(";")[2]
        download = (
            "attachment;" + parameters if parameters else f'attachment; filename="{identity}"'
        )
        return Response(
            response.content,
            media_type=response.headers.get("content-type"),
            headers={"Content-Disposition": download},
        )

    if web_dir is None:
        web_dir = Path(__file__).resolve().parents[3] / "web/dist"
    if (web_dir / "assets").is_dir():
        app.mount("/assets", StaticFiles(directory=web_dir / "assets"), name="assets")

    @app.get("/", include_in_schema=False)
    async def index() -> FileResponse:
        index_file = web_dir / "flow.html"
        if not index_file.is_file():
            raise HTTPException(503, "Build the flow UI with just web-build")
        return FileResponse(index_file, headers={"Cache-Control": "no-cache"})

    return app
