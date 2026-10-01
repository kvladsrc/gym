"""All SQL of the studio. Callers work with domain objects only.

f-strings in queries splice only constant SQL fragments and ``?``
placeholders; values are always passed as parameters.
"""

# ruff: noqa: S608

import json
import sqlite3
from collections.abc import Iterable, Mapping, Sequence
from dataclasses import replace
from datetime import UTC, datetime
from typing import Any

from studio.domain import PENDING, Asset, AssetKind, Dependency, Job, JobStatus, Origin
from studio.storage.db import Database

_PENDING_SQL = ", ".join(f"'{status}'" for status in PENDING)
# A pending job whose dependencies have all succeeded can be sent.
_READY_SQL = f"""
  j.status IN ({_PENDING_SQL})
  AND NOT EXISTS (
    SELECT 1 FROM job_dependencies d JOIN jobs u ON u.id = d.depends_on
    WHERE d.job_id = j.id AND u.status != 'succeeded'
  )
"""


def now() -> str:
    return datetime.now(UTC).isoformat(timespec="milliseconds")


def _placeholders(values: Sequence[object]) -> str:
    return ", ".join("?" for _ in values)


def _json(value: object) -> str | None:
    return None if value is None else json.dumps(value, ensure_ascii=False)


def _load(value: str | None) -> Any:
    return None if value is None else json.loads(value)


class Repository:
    def __init__(self, database: Database) -> None:
        self.db = database

    # Assets

    def add_asset(self, asset: Asset, connection: sqlite3.Connection | None = None) -> None:
        (connection or self.db.connection).execute(
            """INSERT INTO assets (id, kind, blob_sha256, mime, size_bytes, origin, source_url,
                                   title, favorite, meta, created_at)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                asset.id,
                asset.kind,
                asset.blob_sha256,
                asset.mime,
                asset.size_bytes,
                asset.origin,
                asset.source_url,
                asset.title,
                int(asset.favorite),
                _json(asset.meta),
                asset.created_at,
            ),
        )

    def asset(self, asset_id: str) -> Asset | None:
        row = self.db.connection.execute(
            "SELECT * FROM assets WHERE id = ?", (asset_id,)
        ).fetchone()
        return None if row is None else _asset(row)

    def assets(
        self, *, kind: AssetKind | None = None, favorite: bool | None = None, limit: int = 100
    ) -> list[Asset]:
        query = "SELECT * FROM assets"
        conditions: list[str] = ["deleted_at IS NULL"]
        arguments: list[object] = []
        if kind is not None:
            conditions.append("kind = ?")
            arguments.append(kind)
        if favorite is not None:
            conditions.append("favorite = ?")
            arguments.append(int(favorite))
        query += " WHERE " + " AND ".join(conditions)
        query += " ORDER BY id DESC LIMIT ?"
        arguments.append(limit)
        return [_asset(row) for row in self.db.connection.execute(query, arguments)]

    def update_asset(
        self, asset_id: str, *, title: str | None = None, favorite: bool | None = None
    ) -> Asset | None:
        """Change the editable fields that are given; return the updated asset."""
        if title is not None:
            self.db.connection.execute(
                "UPDATE assets SET title = ? WHERE id = ?", (title or None, asset_id)
            )
        if favorite is not None:
            self.db.connection.execute(
                "UPDATE assets SET favorite = ? WHERE id = ?", (int(favorite), asset_id)
            )
        return self.asset(asset_id)

    def delete_asset(self, asset_id: str) -> tuple[Asset, bool] | None:
        """Mark an asset deleted; return it and whether its blob is now unused.

        Refused (``AssetInUse``) while a job that has not finished needs it as
        an input. Already deleted assets are returned as they are.
        """
        with self.db.transaction() as connection:
            row = connection.execute("SELECT * FROM assets WHERE id = ?", (asset_id,)).fetchone()
            if row is None:
                return None
            asset = _asset(row)
            if asset.deleted_at is not None:
                return asset, False
            # An input of an unfinished job, directly or as the output of the
            # upstream job it depends on.
            waiting = connection.execute(
                f"""SELECT j.id FROM job_inputs i JOIN jobs j ON j.id = i.job_id
                    WHERE i.asset_id = ? AND j.status IN ({_PENDING_SQL}, 'running')
                    UNION
                    SELECT j.id FROM job_outputs o
                    JOIN job_dependencies d
                      ON d.depends_on = o.job_id AND d.output_index = o.position
                    JOIN jobs j ON j.id = d.job_id
                    WHERE o.asset_id = ? AND j.status IN ({_PENDING_SQL}, 'running')
                    ORDER BY 1 LIMIT 1""",
                (asset_id, asset_id),
            ).fetchone()
            if waiting is not None:
                raise AssetInUse(f"job {waiting['id']} still needs this asset as an input")
            deleted_at = now()
            connection.execute(
                "UPDATE assets SET deleted_at = ? WHERE id = ?", (deleted_at, asset_id)
            )
            shared = connection.execute(
                """SELECT 1 FROM assets WHERE blob_sha256 = ? AND mime = ?
                   AND deleted_at IS NULL LIMIT 1""",
                (asset.blob_sha256, asset.mime),
            ).fetchone()
        return replace(asset, deleted_at=deleted_at), shared is None

    def parents(self, asset_id: str) -> list[tuple[str, str]]:
        """(parent asset id, relation) pairs of an asset."""
        rows = self.db.connection.execute(
            "SELECT parent_id, relation FROM lineage WHERE child_id = ? ORDER BY parent_id",
            (asset_id,),
        )
        return [(row["parent_id"], row["relation"]) for row in rows]

    # Jobs: creation and reading

    def add_job(self, job: Job) -> Job:
        """Insert a job; with an existing idempotency key, return that job instead.

        If an upstream job failed or was cancelled after the caller checked it,
        the job is stored as failed (``dependency_failed``): the check here runs
        inside the same write transaction as ``fail`` and ``cancel``.
        """
        with self.db.transaction() as connection:
            if job.idempotency_key is not None:
                existing = connection.execute(
                    "SELECT id FROM jobs WHERE idempotency_key = ?", (job.idempotency_key,)
                ).fetchone()
                if existing is not None:
                    found = self.job(existing["id"])
                    assert found is not None
                    return found
            # Re-checked here: a deletion committed after the caller looked
            # must not leave a queued job with a deleted input.
            inputs = list(job.inputs.values())
            gone = connection.execute(
                f"""SELECT id FROM assets WHERE id IN ({_placeholders(inputs)})
                    AND deleted_at IS NOT NULL ORDER BY id LIMIT 1""",
                inputs,
            ).fetchone()
            if gone is not None:
                raise InputDeleted(f"asset {gone['id']} was deleted")
            connection.execute(
                """INSERT INTO jobs (id, server, task, prompt, params, count, seed, status,
                                     idempotency_key, retry_of, created_at)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                (
                    job.id,
                    job.server,
                    job.task,
                    job.prompt,
                    _json(job.params),
                    job.count,
                    job.seed,
                    job.status,
                    job.idempotency_key,
                    job.retry_of,
                    job.created_at,
                ),
            )
            connection.executemany(
                "INSERT INTO job_inputs (job_id, role, asset_id) VALUES (?, ?, ?)",
                [(job.id, role, asset_id) for role, asset_id in job.inputs.items()],
            )
            connection.executemany(
                """INSERT INTO job_dependencies (job_id, depends_on, output_index, input_role)
                   VALUES (?, ?, ?, ?)""",
                [
                    (job.id, dependency.job_id, dependency.output_index, role)
                    for role, dependency in job.dependencies.items()
                ],
            )
            upstream = [dependency.job_id for dependency in job.dependencies.values()]
            dead = connection.execute(
                f"""SELECT id FROM jobs WHERE id IN ({_placeholders(upstream)})
                    AND status IN ('failed', 'cancelled') ORDER BY id LIMIT 1""",
                upstream,
            ).fetchone()
            if dead is not None:
                connection.execute(
                    """UPDATE jobs SET version = version + 1,
                        status = 'failed', error_code = 'dependency_failed',
                                       error_message = ?, finished_at = ?
                       WHERE id = ?""",
                    (f"upstream job {dead['id']} did not succeed", now(), job.id),
                )
        found = self.job(job.id)
        assert found is not None
        return found

    def job(self, job_id: str) -> Job | None:
        connection = self.db.connection
        row = connection.execute("SELECT * FROM jobs WHERE id = ?", (job_id,)).fetchone()
        if row is None:
            return None
        inputs = {
            item["role"]: item["asset_id"]
            for item in connection.execute(
                "SELECT role, asset_id FROM job_inputs WHERE job_id = ?", (job_id,)
            )
        }
        dependencies = {
            item["input_role"]: Dependency(item["depends_on"], item["output_index"])
            for item in connection.execute(
                "SELECT * FROM job_dependencies WHERE job_id = ?", (job_id,)
            )
        }
        outputs = [
            item["asset_id"]
            for item in connection.execute(
                "SELECT asset_id FROM job_outputs WHERE job_id = ? ORDER BY position", (job_id,)
            )
        ]
        return _job(row, inputs, dependencies, outputs)

    def jobs(self, *, statuses: Iterable[JobStatus] | None = None, limit: int = 100) -> list[Job]:
        query = "SELECT id FROM jobs"
        arguments: list[object] = []
        if statuses is not None:
            wanted = list(statuses)
            query += f" WHERE status IN ({_placeholders(wanted)})"
            arguments += wanted
        query += " ORDER BY id DESC LIMIT ?"
        arguments.append(limit)
        ids = [row["id"] for row in self.db.connection.execute(query, arguments)]
        return [job for job_id in ids if (job := self.job(job_id)) is not None]

    def next_ready(self, servers: Sequence[str]) -> Job | None:
        """The earliest pending job of these servers whose dependencies have succeeded.

        Ids are strictly increasing, so ordering by id is first-in, first-out
        even if the wall clock steps back.
        """
        row = self.db.connection.execute(
            f"""SELECT id FROM jobs j WHERE j.server IN ({_placeholders(servers)})
                AND {_READY_SQL} ORDER BY j.id LIMIT 1""",
            list(servers),
        ).fetchone()
        return None if row is None else self.job(row["id"])

    def latest_retry(self, job_id: str) -> str:
        """The newest job in the retry chain that starts at ``job_id``."""
        while True:
            row = self.db.connection.execute(
                "SELECT id FROM jobs WHERE retry_of = ? ORDER BY id DESC LIMIT 1", (job_id,)
            ).fetchone()
            if row is None:
                return job_id
            job_id = row["id"]

    def output_asset(self, job_id: str, position: int) -> str | None:
        row = self.db.connection.execute(
            "SELECT asset_id FROM job_outputs WHERE job_id = ? AND position = ?",
            (job_id, position),
        ).fetchone()
        return None if row is None else row["asset_id"]

    # Jobs: state transitions. Each is guarded by the expected current state.

    def mark_waiting(self, servers: Sequence[str]) -> list[str]:
        """Ready jobs of these servers wait for them to start; return their ids."""
        rows = self.db.connection.execute(
            f"""UPDATE jobs AS j SET version = version + 1,
                status = 'waiting_model'
                WHERE j.server IN ({_placeholders(servers)}) AND j.status = 'queued'
                AND {_READY_SQL}
                RETURNING id""",
            list(servers),
        ).fetchall()
        return sorted(row["id"] for row in rows)

    def start(self, job_id: str) -> bool:
        """Pending → running; False if the job was cancelled or taken meanwhile."""
        return (
            self.db.connection.execute(
                f"""UPDATE jobs SET version = version + 1,
                    status = 'running', started_at = ?
                    WHERE id = ? AND status IN ({_PENDING_SQL})""",
                (now(), job_id),
            ).rowcount
            == 1
        )

    def requeue(self, job_id: str, *, retryable_failures: int) -> None:
        """Running → waiting for the model again (busy, retryable error, not sent)."""
        self.db.connection.execute(
            """UPDATE jobs SET version = version + 1,
                status = 'waiting_model', started_at = NULL,
                               retryable_failures = ?
               WHERE id = ? AND status = 'running'""",
            (retryable_failures, job_id),
        )

    def fail(self, job_id: str, code: str, message: str) -> list[str]:
        """Fail an unfinished job and its pending dependents; return the changed ids."""
        with self.db.transaction() as connection:
            failed = connection.execute(
                """UPDATE jobs SET version = version + 1,
                    status = 'failed', error_code = ?, error_message = ?,
                                   finished_at = ?
                   WHERE id = ? AND status NOT IN ('succeeded', 'failed', 'cancelled')""",
                (code, message, now(), job_id),
            ).rowcount
            return [job_id, *_fail_dependents(connection, job_id)] if failed else []

    def cancel(self, job_id: str) -> list[str]:
        """Cancel a job that has not been sent yet and fail its dependents.

        Returns the changed ids; empty if the job is not pending.
        """
        with self.db.transaction() as connection:
            cancelled = connection.execute(
                f"""UPDATE jobs SET version = version + 1,
                    status = 'cancelled', finished_at = ?
                    WHERE id = ? AND status IN ({_PENDING_SQL})""",
                (now(), job_id),
            ).rowcount
            return [job_id, *_fail_dependents(connection, job_id)] if cancelled else []

    def interrupt_running(self) -> list[str]:
        """After a restart, jobs left running have lost their response.

        Only correct while a single studio process uses the database, which the
        data directory lock guarantees. Returns the changed ids.
        """
        ids = [
            row["id"]
            for row in self.db.connection.execute("SELECT id FROM jobs WHERE status = 'running'")
        ]
        changed: list[str] = []
        for job_id in ids:
            changed += self.fail(
                job_id, "interrupted", "the studio stopped while the job was running"
            )
        return changed

    def succeed(
        self,
        job_id: str,
        outputs: Sequence[Asset],
        *,
        parents: Sequence[str],
        seed: int,
        model_snapshot: Mapping[str, Any],
        effective_params: Mapping[str, Any],
        timing: Mapping[str, Any],
    ) -> None:
        """Record outputs, their lineage and the snapshot in one transaction.

        ``seed`` is the seed the server actually used (it picks one when the
        job did not specify it).
        """
        with self.db.transaction() as connection:
            for position, asset in enumerate(outputs):
                self.add_asset(asset, connection)
                connection.execute(
                    "INSERT INTO job_outputs (job_id, position, asset_id) VALUES (?, ?, ?)",
                    (job_id, position, asset.id),
                )
                connection.executemany(
                    """INSERT OR IGNORE INTO lineage (parent_id, child_id, relation, job_id)
                       VALUES (?, ?, 'input', ?)""",
                    [(parent, asset.id, job_id) for parent in parents],
                )
            updated = connection.execute(
                """UPDATE jobs SET version = version + 1,
                    status = 'succeeded', seed = ?, model_snapshot = ?,
                                   effective_params = ?, timing = ?, finished_at = ?,
                                   error_code = NULL, error_message = NULL
                   WHERE id = ? AND status = 'running'""",
                (
                    seed,
                    _json(model_snapshot),
                    _json(effective_params),
                    _json(timing),
                    now(),
                    job_id,
                ),
            ).rowcount
            if updated != 1:
                # Raising rolls back the outputs: they must not attach to a job
                # that is no longer running.
                raise JobNotRunning(f"job {job_id} is no longer running")

    # Servers: what each said last, so its tasks are known while it is down.

    def remember_server(self, server_id: str, info: Mapping[str, Any]) -> None:
        self.db.connection.execute(
            """INSERT INTO servers (id, info, seen_at) VALUES (?, ?, ?)
               ON CONFLICT (id) DO UPDATE SET info = excluded.info, seen_at = excluded.seen_at""",
            (server_id, _json(info), now()),
        )

    def remembered_servers(self) -> dict[str, dict[str, Any]]:
        """The last /v1/info of each server that ever answered (unreadable
        rows are skipped: the server's next answer replaces them)."""
        remembered: dict[str, dict[str, Any]] = {}
        for row in self.db.connection.execute("SELECT id, info FROM servers"):
            try:
                remembered[row["id"]] = json.loads(row["info"])
            except ValueError:
                continue
        return remembered


class JobNotRunning(RuntimeError):
    pass


class AssetInUse(RuntimeError):
    pass


class InputDeleted(ValueError):
    pass


def _fail_dependents(connection: sqlite3.Connection, job_id: str) -> list[str]:
    """Fail pending jobs that (transitively) depend on ``job_id``; return their ids."""
    rows = connection.execute(
        f"""WITH RECURSIVE downstream(id) AS (
              SELECT job_id FROM job_dependencies WHERE depends_on = ?
              UNION
              SELECT d.job_id FROM job_dependencies d
              JOIN downstream ON d.depends_on = downstream.id
            )
            UPDATE jobs SET version = version + 1,
                status = 'failed', error_code = 'dependency_failed',
                            error_message = ?, finished_at = ?
            WHERE id IN (SELECT id FROM downstream) AND status IN ({_PENDING_SQL})
            RETURNING id""",
        (job_id, f"upstream job {job_id} did not succeed", now()),
    ).fetchall()
    return sorted(row["id"] for row in rows)


def _asset(row: sqlite3.Row) -> Asset:
    return Asset(
        id=row["id"],
        kind=AssetKind(row["kind"]),
        mime=row["mime"],
        size_bytes=row["size_bytes"],
        blob_sha256=row["blob_sha256"],
        origin=Origin(row["origin"]),
        created_at=row["created_at"],
        title=row["title"],
        source_url=row["source_url"],
        favorite=bool(row["favorite"]),
        meta=json.loads(row["meta"]),
        deleted_at=row["deleted_at"],
    )


def _job(
    row: sqlite3.Row,
    inputs: dict[str, str],
    dependencies: dict[str, Dependency],
    outputs: list[str],
) -> Job:
    return Job(
        id=row["id"],
        server=row["server"],
        task=row["task"],
        status=JobStatus(row["status"]),
        count=row["count"],
        created_at=row["created_at"],
        prompt=row["prompt"],
        params=json.loads(row["params"]),
        seed=row["seed"],
        inputs=inputs,
        dependencies=dependencies,
        outputs=outputs,
        error_code=row["error_code"],
        error_message=row["error_message"],
        retryable_failures=row["retryable_failures"],
        version=row["version"],
        model_snapshot=_load(row["model_snapshot"]),
        effective_params=_load(row["effective_params"]),
        timing=_load(row["timing"]),
        idempotency_key=row["idempotency_key"],
        retry_of=row["retry_of"],
        started_at=row["started_at"],
        finished_at=row["finished_at"],
    )
