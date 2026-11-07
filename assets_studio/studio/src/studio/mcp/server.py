"""MCP tools: the studio's scenarios for agents on the same machine.

Tools mirror scenarios rather than endpoints: see what models are available,
queue a generation, wait for it, and get the resulting files by local path.
They call the same services as the HTTP API.
"""

# Handlers are registered by their decorators, inside the factory function.
# pyright: reportUnusedFunction=false

from pathlib import Path
from typing import Any

from mcp.server.mcpserver import MCPServer
from mcp.server.mcpserver.exceptions import ToolError
from pydantic import BaseModel, Field

from studio.api.schemas import AssetOut, JobOut, ProjectOut, ServerOut
from studio.core import Studio
from studio.domain import DEFAULT_PROJECT, AssetKind, Dependency, Job
from studio.services.generation import JobRequestError, JobStateError
from studio.services.library import AssetImportError, BadTag, Unset
from studio.services.projects import ProjectConflict, ProjectError

INSTRUCTIONS = """\
Local studio for game assets: images, 3D models, rigs and animation, sound,
text and video. Each server runs one model. Call list_servers to see what tasks
and parameters they accept, then generate, then wait_for_job; results are
assets with a local file path. A job's output can be the next job's input
before it exists (generate's input_jobs). A server shown as unavailable is not
running: submit the job anyway, it waits in the queue until the server is
started (the launcher starts one GPU model at a time, so a job can wait
minutes). Every asset and job belongs to a project (list_projects); without
one, new ones go to "default"."""


class AssetView(AssetOut):
    """An asset with the absolute path of its file on this machine (None once
    the asset is deleted: the file may be gone)."""

    path: str | None
    # The job that made it (get_job tells its prompt, parameters and inputs);
    # None for an imported file.
    job_id: str | None


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
        producer = studio.generation.producer(asset_id)
        return AssetView(
            **AssetOut.of(asset).model_dump(),
            path=path,
            job_id=None if producer is None else producer.id,
        )

    @mcp.tool()
    def list_servers() -> list[ServerOut]:
        """Model servers with their state (ready, loading, unavailable…), the
        model, and for each task: prompt usage, input roles and a JSON Schema
        of parameters. A stopped server keeps the tasks it last declared."""
        return [ServerOut.of(server, studio) for server in studio.config.servers]

    @mcp.tool()
    def list_projects() -> list[ProjectOut]:
        """Projects, the parts of the library that assets and jobs belong to, with how many
        assets each has. "default" always exists."""
        return [ProjectOut.of(project) for project in studio.projects.all()]

    @mcp.tool()
    def create_project(title: str, id: str | None = None) -> ProjectOut:
        """A new project; id is a slug (lowercase letters, digits, - or _), by default made
        from the title."""
        try:
            return ProjectOut.of(studio.projects.create(title, id))
        except (ProjectError, ProjectConflict) as error:
            raise ToolError(str(error)) from error

    @mcp.tool()
    def generate(
        server: str,
        task: str,
        prompt: str | None = None,
        params: dict[str, Any] | None = None,
        count: int = 1,
        seed: int | None = None,
        input_assets: dict[str, str] | None = None,
        input_jobs: dict[str, InputJob] | None = None,
        idempotency_key: str | None = None,
        project: str = DEFAULT_PROJECT,
    ) -> JobView:
        """Queue a generation and return the job (then call wait_for_job).

        input_assets maps an input role to an asset id (of any project).
        input_jobs maps a role to an output of another job, {"job_id": ...,
        "output_index": 0}; the new job waits for it. The job and its results
        belong to project. Use idempotency_key to make a retried call safe.
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
                project=project,
            )
        except JobRequestError as error:
            raise ToolError(str(error)) from error
        return job_view(job)

    @mcp.tool()
    def wait_for_job(job_id: str, timeout_s: float = 60) -> JobView:
        """Wait until the job has succeeded, failed or been cancelled, or until
        timeout_s passes, and return it. If its status is still queued,
        waiting_model or running, call again. output_paths are the local
        files of the results."""
        try:
            return job_view(studio.wait(job_id, min(timeout_s, 600)))
        except JobRequestError as error:
            raise ToolError(str(error)) from error

    @mcp.tool()
    def get_job(job_id: str) -> JobView:
        """The current state of a job."""
        job = studio.generation.job(job_id)
        if job is None:
            raise ToolError(f"job {job_id} not found")
        return job_view(job)

    @mcp.tool()
    def cancel_job(job_id: str) -> JobOut:
        """Cancel a job that has not been sent to its model server yet."""
        try:
            return JobOut.of(studio.generation.cancel(job_id))
        except (JobRequestError, JobStateError) as error:
            raise ToolError(str(error)) from error

    @mcp.tool()
    def retry_job(job_id: str) -> JobOut:
        """Queue a failed job again (a new job; failed upstream jobs are retried too)."""
        try:
            return JobOut.of(studio.generation.retry(job_id))
        except (JobRequestError, JobStateError) as error:
            raise ToolError(str(error)) from error

    @mcp.tool()
    def list_assets(
        kind: AssetKind | None = None,
        tags: list[str] | None = None,
        min_rating: int | None = None,
        unrated: bool = False,
        limit: int = 20,
        project: str | None = None,
    ) -> list[AssetView]:
        """Newest assets first (deleted ones are not listed); kind is image,
        mesh, audio, text, video or motion (SMPL-X or SOMA); tags: having all of them (e.g.
        "style:cartoon"); min_rating 0-5; unrated: not rated yet; project: only its assets
        (every project's when not given)."""
        if project is not None and studio.projects.find(project) is None:
            raise ToolError(f"project {project} not found")
        assets = studio.library.assets(
            project=project,
            kind=kind,
            tags=tags or [],
            min_rating=min_rating,
            unrated=unrated,
            limit=limit,
        )
        return [asset_view(asset.id) for asset in assets]

    @mcp.tool()
    def rate_asset(
        asset_id: str, rating: int | None = None, tags: list[str] | None = None
    ) -> AssetView:
        """Rate an asset 0-5 (None leaves the rating) and/or replace its tags."""
        if rating is not None and not 0 <= rating <= 5:
            raise ToolError("rating must be 0-5")
        try:
            updated = studio.library.update(
                asset_id, rating=Unset if rating is None else rating, tags=tags
            )
        except BadTag as error:
            raise ToolError(str(error)) from error
        if updated is None:
            raise ToolError(f"asset {asset_id} not found")
        return asset_view(asset_id)

    @mcp.tool()
    def move_asset(asset_id: str, project: str) -> AssetView:
        """Move an asset to another project. The job that made it stays where it ran."""
        try:
            updated = studio.library.update(asset_id, project=project)
        except ProjectError as error:
            raise ToolError(str(error)) from error
        if updated is None:
            raise ToolError(f"asset {asset_id} not found")
        return asset_view(asset_id)

    @mcp.tool()
    def get_asset(asset_id: str) -> AssetView:
        """An asset with its local file path, metadata (seed, mesh stats…) and
        the job that made it (get_job: its prompt, parameters and inputs)."""
        return asset_view(asset_id)

    @mcp.tool()
    def import_asset(
        path: str | None = None,
        url: str | None = None,
        title: str | None = None,
        project: str = DEFAULT_PROJECT,
    ) -> AssetView:
        """Add a local file (path) or a downloaded file (url) to the library
        of project, e.g. to use it as an input: images (PNG, JPEG, WebP), sound
        (WAV, FLAC, OGG, MP3), 3D (GLB, binary FBX), video (MP4, WebM) and text."""
        if (path is None) == (url is None):
            raise ToolError("give exactly one of path or url")
        try:
            if path is not None:
                file = Path(path).expanduser()
                limit = studio.config.max_import_bytes
                if file.stat().st_size > limit:
                    raise ToolError(f"file is larger than {limit} bytes")
                asset = studio.library.import_bytes(file.read_bytes(), title=title, project=project)
            else:
                assert url is not None
                asset = studio.library.import_url(url, title=title, project=project)
        except (AssetImportError, ProjectError, OSError) as error:
            raise ToolError(str(error)) from error
        return asset_view(asset.id)

    return mcp
