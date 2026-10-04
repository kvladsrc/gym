"""Creating, cancelling and retrying generation jobs."""

from collections.abc import Callable, Iterable, Mapping
from typing import Any

from studio.config import StudioConfig
from studio.domain import Dependency, Job, JobStatus, new_id
from studio.storage.repository import InputDeleted, Repository, now


class JobRequestError(ValueError):
    """The job cannot be created as requested."""


class JobStateError(ValueError):
    """The operation is not possible in the job's current state."""


class Generation:
    def __init__(
        self,
        config: StudioConfig,
        repository: Repository,
        *,
        wake: Callable[[], None],
        on_change: Callable[[str], None] = lambda _: None,
    ) -> None:
        self._config = config
        self._repository = repository
        self._wake = wake
        self._on_change = on_change

    def submit(
        self,
        server: str,
        task: str,
        *,
        prompt: str | None = None,
        params: Mapping[str, Any] | None = None,
        count: int = 1,
        seed: int | None = None,
        inputs: Mapping[str, str] | None = None,
        dependencies: Mapping[str, Dependency] | None = None,
        idempotency_key: str | None = None,
        retry_of: str | None = None,
    ) -> Job:
        """Queue a job. Each input role is filled either by an asset or by the
        output of another job, never both. Parameters are validated by the
        model server when the job is sent."""
        inputs = dict(inputs or {})
        dependencies = dict(dependencies or {})
        try:
            self._config.server(server)
        except KeyError:
            raise JobRequestError(f"unknown server: {server}") from None
        if count < 1:
            raise JobRequestError("count must be at least 1")
        both = sorted(set(inputs) & set(dependencies))
        if both:
            raise JobRequestError(f"roles filled twice: {', '.join(both)}")
        for role, asset_id in inputs.items():
            asset = self._repository.asset(asset_id)
            if asset is None:
                raise JobRequestError(f"input {role}: asset {asset_id} not found")
            if asset.deleted_at is not None:
                raise JobRequestError(f"input {role}: asset {asset_id} was deleted")
        for role, dependency in dependencies.items():
            upstream = self._repository.job(dependency.job_id)
            if upstream is None:
                raise JobRequestError(f"input {role}: job {dependency.job_id} not found")
            if upstream.status in (JobStatus.FAILED, JobStatus.CANCELLED):
                raise JobRequestError(f"input {role}: job {dependency.job_id} did not succeed")
            if not 0 <= dependency.output_index < upstream.count:
                raise JobRequestError(
                    f"input {role}: job {dependency.job_id} has no output {dependency.output_index}"
                )
            output = self._repository.output_asset(dependency.job_id, dependency.output_index)
            produced = self._repository.asset(output) if output else None
            if produced is not None and produced.deleted_at is not None:
                raise JobRequestError(f"input {role}: its source, asset {output}, was deleted")
        try:
            job = self._repository.add_job(
                Job(
                    id=new_id(),
                    server=server,
                    task=task,
                    status=JobStatus.QUEUED,
                    count=count,
                    created_at=now(),
                    prompt=prompt,
                    params=dict(params or {}),
                    seed=seed,
                    inputs=inputs,
                    dependencies=dependencies,
                    idempotency_key=idempotency_key,
                    retry_of=retry_of,
                )
            )
        except InputDeleted as error:
            raise JobRequestError(f"an input {error}") from error
        self._on_change(job.id)
        self._wake()
        return job

    def cancel(self, job_id: str) -> Job:
        changed = self._repository.cancel(job_id)
        if not changed:
            raise JobStateError(f"job {job_id} is not waiting to be sent; it cannot be cancelled")
        for changed_id in changed:
            self._on_change(changed_id)
        self._wake()
        return self._get(job_id)

    def delete(self, job_id: str) -> Job:
        """Remove a failed or cancelled job from the history (ADR-004)."""
        if not self._repository.delete_job(job_id):
            job = self._get(job_id)
            state = "already deleted" if job.deleted_at else str(job.status)
            raise JobStateError(
                f"job {job_id} is {state}; only failed or cancelled jobs can be deleted"
            )
        self._on_change(job_id)
        return self._get(job_id)

    def retry(self, job_id: str) -> Job:
        """A new job with the same request; the failed one leaves the history
        (deleted, ADR-004: the retry chain still refers to it).

        Upstream jobs are taken from their retry chains: if an upstream job was
        already retried, the new job depends on that retry; if it failed and
        was not retried, it is retried too. A cancelled upstream job is not
        revived: the user stopped it on purpose.
        """
        job = self._get(job_id)
        if job.status not in (JobStatus.FAILED, JobStatus.CANCELLED):
            raise JobStateError(f"job {job_id} is {job.status}; only failed jobs can be retried")
        # Check the whole upstream chain first, so a refusal submits nothing.
        self._check_retryable_upstream(job)
        dependencies: dict[str, Dependency] = {}
        for role, dependency in job.dependencies.items():
            upstream = self._get(self._repository.latest_retry(dependency.job_id))
            if upstream.status is JobStatus.FAILED:
                upstream = self.retry(upstream.id)
            dependencies[role] = Dependency(upstream.id, dependency.output_index)
        retried = self.submit(
            job.server,
            job.task,
            prompt=job.prompt,
            params=job.params,
            count=job.count,
            seed=job.seed,
            inputs=job.inputs,
            dependencies=dependencies,
            retry_of=job.id,
        )
        if self._repository.delete_job(job.id):
            self._on_change(job.id)
        return retried

    def _check_retryable_upstream(self, job: Job) -> None:
        for role, dependency in job.dependencies.items():
            upstream = self._get(self._repository.latest_retry(dependency.job_id))
            if upstream.status is JobStatus.CANCELLED:
                raise JobStateError(
                    f"input {role}: job {upstream.id} was cancelled; retry it first"
                )
            if upstream.status is JobStatus.FAILED:
                self._check_retryable_upstream(upstream)

    def job(self, job_id: str) -> Job | None:
        return self._repository.job(job_id)

    def jobs(self, *, statuses: Iterable[JobStatus] | None = None, limit: int = 100) -> list[Job]:
        return self._repository.jobs(statuses=statuses, limit=limit)

    def _get(self, job_id: str) -> Job:
        job = self._repository.job(job_id)
        if job is None:
            raise JobRequestError(f"job {job_id} not found")
        return job
