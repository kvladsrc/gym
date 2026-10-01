"""MCP tools: the studio's scenarios for agents on the same machine.

Tools mirror scenarios rather than endpoints: see what models are available,
queue a generation, wait for it, and get the resulting files by local path.
They call the same services as the HTTP API.
"""

from pathlib import Path
from typing import Any

from mcp.server.mcpserver import MCPServer
from mcp.server.mcpserver.exceptions import ToolError
from pydantic import BaseModel, Field

from studio.api.schemas import AssetOut, JobOut, ServerOut
from studio.core import Studio
from studio.domain import AssetKind, Dependency, Job
from studio.services.generation import JobRequestError, JobStateError
from studio.services.library import ImportError_

INSTRUCTIONS = """\
Local studio for game assets (images, 3D models, speech, sounds).
Each server runs one model; the user starts it manually. Call list_servers to see
which models are ready and what tasks and parameters they accept, then
generate, then wait_for_job; results are assets with a local file path.
Jobs wait in a queue while their model server is not running."""


class AssetView(AssetOut):
    """An asset with the absolute path of its file on this machine (None once
    the asset is deleted: the file may be gone)."""

    path: str | None


class JobView(JobOut):
    """A job with the local paths of its output files, in output order (None
    for a deleted output)."""

    output_paths: list[str | None]


class InputJob(BaseModel):
    """Output ``output_index`` of job ``job_id``, used as an input."""

    job_id: str
    output_index: int = Field(default=0, ge=0)


def build_mcp(studio: Studio) -> MCPServer:
    mcp = MCPServer("assets-studio", instructions=INSTRUCTIONS)

    def job_view(job: Job) -> JobView:
        paths = [asset_view(asset_id).path for asset_id in job.outputs]
        return JobView(**JobOut.of(job).model_dump(), output_paths=paths)

    def asset_view(asset_id: str) -> AssetView:
        asset = studio.library.asset(asset_id)
        if asset is None:
            raise ToolError(f"asset {asset_id} not found")
        path = None if asset.deleted_at else str(studio.library.file(asset))
        return AssetView(**AssetOut.of(asset).model_dump(), path=path)

    @mcp.tool()
    def list_servers() -> list[ServerOut]:  # pyright: ignore[reportUnusedFunction]
        """Model servers with their state (ready, loading, unavailable…), the
        model, and for each task: prompt usage, input roles and a JSON Schema
        of parameters. A stopped server keeps the tasks it last declared."""
        return [ServerOut.of(server, studio) for server in studio.config.servers]

    @mcp.tool()
    def generate(  # pyright: ignore[reportUnusedFunction]
        server: str,
        task: str,
        prompt: str | None = None,
        params: dict[str, Any] | None = None,
        count: int = 1,
        seed: int | None = None,
        input_assets: dict[str, str] | None = None,
        input_jobs: dict[str, InputJob] | None = None,
        idempotency_key: str | None = None,
    ) -> JobView:
        """Queue a generation and return the job (then call wait_for_job).

        input_assets maps an input role to an asset id. input_jobs maps a role
        to an output of another job, {"job_id": ..., "output_index": 0}; the
        new job waits for it. Use idempotency_key to make a retried call safe.
        """
        dependencies = {
            role: Dependency(reference.job_id, reference.output_index)
            for role, reference in (input_jobs or {}).items()
        }
        try:
            job = studio.generation.submit(
                server,
                task,
                prompt=prompt,
                params=params,
                count=count,
                seed=seed,
                inputs=input_assets,
                dependencies=dependencies,
                idempotency_key=idempotency_key,
            )
        except JobRequestError as error:
            raise ToolError(str(error)) from error
        return job_view(job)

    @mcp.tool()
    def wait_for_job(job_id: str, timeout_s: float = 60) -> JobView:  # pyright: ignore[reportUnusedFunction]
        """Wait until the job has succeeded, failed or been cancelled, or until
        timeout_s passes, and return it. If its status is still queued,
        waiting_model or running, call again. output_paths are the local
        files of the results."""
        try:
            return job_view(studio.wait(job_id, min(timeout_s, 600)))
        except JobRequestError as error:
            raise ToolError(str(error)) from error

    @mcp.tool()
    def get_job(job_id: str) -> JobView:  # pyright: ignore[reportUnusedFunction]
        """The current state of a job."""
        job = studio.generation.job(job_id)
        if job is None:
            raise ToolError(f"job {job_id} not found")
        return job_view(job)

    @mcp.tool()
    def cancel_job(job_id: str) -> JobOut:  # pyright: ignore[reportUnusedFunction]
        """Cancel a job that has not been sent to its model server yet."""
        try:
            return JobOut.of(studio.generation.cancel(job_id))
        except (JobRequestError, JobStateError) as error:
            raise ToolError(str(error)) from error

    @mcp.tool()
    def retry_job(job_id: str) -> JobOut:  # pyright: ignore[reportUnusedFunction]
        """Queue a failed job again (a new job; failed upstream jobs are retried too)."""
        try:
            return JobOut.of(studio.generation.retry(job_id))
        except (JobRequestError, JobStateError) as error:
            raise ToolError(str(error)) from error

    @mcp.tool()
    def list_assets(kind: AssetKind | None = None, limit: int = 20) -> list[AssetView]:  # pyright: ignore[reportUnusedFunction]
        """Newest assets first (deleted ones are not listed); kind is image,
        mesh, audio, text or video."""
        return [asset_view(asset.id) for asset in studio.library.assets(kind=kind, limit=limit)]

    @mcp.tool()
    def get_asset(asset_id: str) -> AssetView:  # pyright: ignore[reportUnusedFunction]
        """An asset with its local file path and metadata (seed, mesh stats…)."""
        return asset_view(asset_id)

    @mcp.tool()
    def import_asset(  # pyright: ignore[reportUnusedFunction]
        path: str | None = None, url: str | None = None, title: str | None = None
    ) -> AssetView:
        """Add a local file (path) or a downloaded file (url) to the library,
        e.g. to use it as an input. PNG, JPEG, WebP, WAV and GLB are supported."""
        if (path is None) == (url is None):
            raise ToolError("give exactly one of path or url")
        try:
            if path is not None:
                file = Path(path).expanduser()
                limit = studio.config.max_import_bytes
                if file.stat().st_size > limit:
                    raise ToolError(f"file is larger than {limit} bytes")
                asset = studio.library.import_bytes(file.read_bytes(), title=title)
            else:
                assert url is not None
                asset = studio.library.import_url(url, title=title)
        except (ImportError_, OSError) as error:
            raise ToolError(str(error)) from error
        return asset_view(asset.id)

    return mcp
