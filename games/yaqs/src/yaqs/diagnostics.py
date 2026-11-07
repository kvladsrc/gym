"""Offline readiness checks; never download weights or run model inference."""

import sqlite3
from contextlib import closing
from pathlib import Path
from typing import Any

from yaqs.embedding import cache_status

RUNTIME_HINT = (
    "Recreate the environment on uv's own Python, which loads the system C++ runtime "
    "and zlib; see Setup in the README"
)


def diagnose(database: Path) -> dict[str, Any]:
    """Report every missing part as JSON, including missing Python packages."""
    runtime: dict[str, Any] = {"ready": False}
    try:
        import numpy as np
        import sqlite_vec
        import torch

        with closing(sqlite3.connect(":memory:")) as db:
            db.enable_load_extension(True)
            sqlite_vec.load(db)
            db.enable_load_extension(False)
            db.execute("CREATE VIRTUAL TABLE probe USING fts5(text)")
            runtime.update(
                ready=True,
                sqlite=sqlite3.sqlite_version,
                sqlite_vec=db.execute("SELECT vec_version()").fetchone()[0],
                fts5=True,
                numpy=np.__version__,
                torch=torch.__version__,
                cuda_available=torch.cuda.is_available(),
            )
    except (ImportError, OSError, sqlite3.Error) as error:
        runtime["error"] = str(error)
        runtime["hint"] = RUNTIME_HINT
    try:
        model = cache_status()
    except ImportError as error:
        model = {"ready": False, "error": str(error)}
    if not model["ready"]:
        model["hint"] = "Run yaqs download"
    status: dict[str, Any] = {"path": str(database.expanduser().resolve()), "ready": False}
    try:
        from yaqs.store import KnowledgeBase

        stats = KnowledgeBase(database).stats()
        status.update(
            ready=stats["counts"]["books"] > 0 and stats["pending_chunks"] == 0, stats=stats
        )
        if not status["ready"]:
            status["hint"] = "Import text or resume unfinished imports"
    except ImportError as error:
        status["error"] = str(error)
        status["hint"] = RUNTIME_HINT
    except (OSError, ValueError, sqlite3.Error) as error:
        status["error"] = str(error)
        status["hint"] = "Import TXT/JSONL; use yaqs rebuild --output NEW_DB for an old schema"
    return {
        "ready": runtime["ready"] and model["ready"] and status["ready"],
        "runtime": runtime,
        "model": model,
        "database": status,
    }
