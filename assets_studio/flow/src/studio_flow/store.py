"""Only graph definitions and submission receipts live here; jobs stay in studio."""

import json
import sqlite3
import uuid
from collections.abc import Generator
from contextlib import contextmanager
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from studio_flow.models import Graph


class Conflict(ValueError):
    pass


def now() -> str:
    return datetime.now(UTC).isoformat()


class Store:
    def __init__(self, path: Path) -> None:
        self.path = path
        path.parent.mkdir(parents=True, exist_ok=True)
        with self.connect() as db:
            db.executescript("""
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS flows (
                    id TEXT PRIMARY KEY, revision INTEGER NOT NULL,
                    created_at TEXT NOT NULL, updated_at TEXT NOT NULL, graph TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS runs (
                    id TEXT PRIMARY KEY, request_id TEXT NOT NULL UNIQUE,
                    flow_id TEXT NOT NULL, revision INTEGER NOT NULL, created_at TEXT NOT NULL,
                    graph TEXT NOT NULL, state TEXT NOT NULL, jobs TEXT NOT NULL DEFAULT '{}',
                    error TEXT);
            """)

    @contextmanager
    def connect(self) -> Generator[sqlite3.Connection]:
        db = sqlite3.connect(self.path, timeout=10)
        db.row_factory = sqlite3.Row
        try:
            with db:
                yield db
        finally:
            db.close()

    @staticmethod
    def unpack(row: sqlite3.Row) -> dict[str, Any]:
        data = dict(row)
        data["graph"] = json.loads(data["graph"])
        if "jobs" in data:
            data["jobs"] = json.loads(data["jobs"])
        return data

    def flows(self) -> list[dict[str, Any]]:
        with self.connect() as db:
            return [
                self.unpack(r) for r in db.execute("SELECT * FROM flows ORDER BY updated_at DESC")
            ]

    def flow(self, identity: str) -> dict[str, Any] | None:
        with self.connect() as db:
            row = db.execute("SELECT * FROM flows WHERE id=?", (identity,)).fetchone()
            return self.unpack(row) if row is not None else None

    def save(
        self, graph: Graph, identity: str | None = None, revision: int | None = None
    ) -> dict[str, Any]:
        timestamp = now()
        with self.connect() as db:
            if identity is None:
                identity = uuid.uuid4().hex
                db.execute(
                    "INSERT INTO flows VALUES (?,1,?,?,?)",
                    (identity, timestamp, timestamp, graph.model_dump_json()),
                )
            else:
                cursor = db.execute(
                    "UPDATE flows SET revision=revision+1,updated_at=?,graph=? "
                    "WHERE id=? AND revision=?",
                    (timestamp, graph.model_dump_json(), identity, revision),
                )
                if cursor.rowcount != 1:
                    raise Conflict("The workflow changed in another window; reload before saving")
        result = self.flow(identity)
        assert result
        return result

    def delete(self, identity: str) -> None:
        with self.connect() as db:
            db.execute("DELETE FROM flows WHERE id=?", (identity,))

    def new_run(self, flow: dict[str, Any], request_id: str) -> dict[str, Any]:
        with self.connect() as db:
            row = db.execute("SELECT * FROM runs WHERE request_id=?", (request_id,)).fetchone()
            if row:
                result = self.unpack(row)
                assert result
                if result["flow_id"] != flow["id"] or result["revision"] != flow["revision"]:
                    raise Conflict("This run request id belongs to a different workflow revision")
                return result
            identity = uuid.uuid4().hex
            db.execute(
                "INSERT INTO runs (id,request_id,flow_id,revision,created_at,graph,state) "
                "VALUES (?,?,?,?,?,?,?)",
                (
                    identity,
                    request_id,
                    flow["id"],
                    flow["revision"],
                    now(),
                    json.dumps(flow["graph"]),
                    "submitting",
                ),
            )
        result = self.run(identity)
        assert result
        return result

    def run(self, identity: str) -> dict[str, Any] | None:
        with self.connect() as db:
            row = db.execute("SELECT * FROM runs WHERE id=?", (identity,)).fetchone()
            return self.unpack(row) if row is not None else None

    def runs(self, flow_id: str | None = None, *, pending: bool = False) -> list[dict[str, Any]]:
        with self.connect() as db:
            rows = db.execute(
                "SELECT * FROM runs WHERE (? IS NULL OR flow_id=?) "
                "AND (?=0 OR state IN ('submitting','cancelling')) "
                "ORDER BY created_at DESC LIMIT 100",
                (flow_id, flow_id, int(pending)),
            )
            return [self.unpack(row) for row in rows]

    def receipt(
        self,
        identity: str,
        jobs: dict[str, str],
        *,
        state: str = "submitting",
        error: str | None = None,
    ) -> None:
        with self.connect() as db:
            db.execute(
                "UPDATE runs SET jobs=?,state=?,error=? WHERE id=?",
                (json.dumps(jobs), state, error, identity),
            )
