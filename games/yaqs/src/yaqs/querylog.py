"""Append-only log of read requests: what was asked, by whom and what came back.

`yaqs serve`, `yaqs web` and the reading commands of the CLI append one JSON line per
KnowledgeService call to NAME.queries.jsonl beside the database. YAQS_QUERY_LOG names
another file, or "off" turns the log off; the Python API logs only when given a path. A
record keeps the arguments and the ids, pages and scores of the answer, never the returned
text. Each line is written under flock, so servers and CLI calls can share one file. A
failed write prints one warning to stderr and never fails the request.
"""

import fcntl
import json
import os
import sys
import time
from collections.abc import Iterator
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class Caller:
    """Who asked: the interface and, for MCP, the client and its session when known."""

    interface: str
    client: str = ""
    session: str = ""


PYTHON = Caller("python")


def default_path(database: Path) -> Path | None:
    """YAQS_QUERY_LOG, or NAME.queries.jsonl beside the database; None when set to off."""
    value = os.environ.get("YAQS_QUERY_LOG", "").strip()
    if value.lower() == "off":
        return None
    if value:
        return Path(value).expanduser()
    return database.expanduser().with_suffix(".queries.jsonl")


class Entry:
    """A request being served; the service sets result before the record is written."""

    result: dict[str, Any] | None = None


class QueryLog:
    def __init__(self, path: Path) -> None:
        self.path = path.expanduser()
        self._warned = False

    @contextmanager
    def record(self, caller: Caller, operation: str, arguments: dict[str, Any]) -> Iterator[Entry]:
        """Write one line when the block ends, with the result or the error it raised."""
        entry = Entry()
        record: dict[str, Any] = {
            "time": datetime.now(UTC).isoformat(timespec="milliseconds").replace("+00:00", "Z"),
            "interface": caller.interface,
        }
        if caller.client:
            record["client"] = caller.client
        if caller.session:
            record["session"] = caller.session
        record |= {"operation": operation, "arguments": arguments}
        started = time.perf_counter()
        try:
            yield entry
        except BaseException as error:
            record["error"] = _message(error)
            raise
        else:
            record["result"] = entry.result
        finally:
            record["elapsed_ms"] = round((time.perf_counter() - started) * 1000, 1)
            self._append(record)

    def _append(self, record: dict[str, Any]) -> None:
        line = json.dumps(record, ensure_ascii=False, separators=(",", ":"), default=str) + "\n"
        try:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            with self.path.open("a", encoding="utf-8") as handle:
                fcntl.flock(handle, fcntl.LOCK_EX)
                handle.write(line)
        except OSError as error:
            if not self._warned:
                self._warned = True
                warning = {"warning": f"Query log {self.path} not written: {error}"}
                print(json.dumps(warning, ensure_ascii=False), file=sys.stderr)


def _message(error: BaseException) -> str:
    if isinstance(error, KeyError) and error.args:
        return str(error.args[0])  # str(KeyError) would add quotes
    return str(error) or type(error).__name__


def _hit(hit: dict[str, Any]) -> dict[str, Any]:
    view = {key: hit[key] for key in ("id", "chunk_ids", "book_id", "page_number")}
    if "score" in hit:
        view["score"] = round(hit["score"], 4)
    return view


def blocks(result: dict[str, Any]) -> dict[str, Any]:
    """Summary of a search or passage answer: its blocks without text, and the budget used."""
    context = result["context"]
    return {
        "hits": [_hit(hit) for hit in result["hits"]],
        "used_tokens": context["used_tokens"],
        "omitted_blocks": context["omitted_blocks"],
    }


def window(page: dict[str, Any]) -> dict[str, Any]:
    """Summary of a page window: where it ended and whether the page goes on."""
    keys = ("end_char", "total_chars", "complete", "next_start_char")
    return {"used_tokens": page["token_count"], **{key: page[key] for key in keys}}
