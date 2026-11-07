"""Consistent SQLite backups and complete rebuilds from stored source text."""

import hashlib
import json
import os
import sqlite3
import sys
import tempfile
import time
from contextlib import closing
from pathlib import Path
from typing import Any

import sqlite_vec

from yaqs import SCHEMA_VERSION
from yaqs.documents import Document, Page, import_profile
from yaqs.embedding import QwenEncoder
from yaqs.ingest import import_document
from yaqs.locking import import_lock
from yaqs.store import INTEGRITY_OK, KnowledgeBase


def backup_database(source: Path, destination: Path) -> dict[str, Any]:
    """Include committed WAL data and atomically publish a standalone SQLite file."""
    source = source.expanduser().resolve(strict=True)
    destination = destination.expanduser().resolve()
    if source == destination or destination.exists():
        raise FileExistsError(f"Backup destination already exists: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    descriptor, name = tempfile.mkstemp(prefix=f".{destination.name}.", dir=destination.parent)
    os.close(descriptor)
    temporary = Path(name)
    try:
        with (
            closing(sqlite3.connect(source.as_uri() + "?mode=ro", uri=True)) as src,
            closing(sqlite3.connect(temporary)) as dst,
        ):
            src.backup(dst)
            dst.execute("PRAGMA journal_mode=DELETE")
            dst.enable_load_extension(True)
            sqlite_vec.load(dst)
            dst.enable_load_extension(False)
            if dst.execute("PRAGMA quick_check").fetchone()[0] != "ok":
                raise RuntimeError("SQLite backup did not pass quick_check")
        # link refuses an existing destination even if another writer created it meanwhile.
        os.link(temporary, destination)
    finally:
        temporary.unlink(missing_ok=True)
    with destination.open("rb") as handle:
        digest = hashlib.file_digest(handle, "sha256").hexdigest()
    return {"path": str(destination), "bytes": destination.stat().st_size, "sha256": digest}


def stored_documents(path: Path) -> list[Document]:
    """Read schema 1 or 2 without needing the original TXT/JSONL files or PDFs."""
    with closing(sqlite3.connect(path.as_uri() + "?mode=ro", uri=True)) as db:
        db.row_factory = sqlite3.Row
        version = db.execute("PRAGMA user_version").fetchone()[0]
        if version not in (1, SCHEMA_VERSION):
            raise ValueError(f"Cannot rebuild database schema {version}")
        books = db.execute("SELECT * FROM books ORDER BY id").fetchall()
        documents = []
        for book in books:
            pages = [
                Page(
                    p["page_number"],
                    p["page_label"],
                    p["chapter"],
                    p["text"],
                    p["extraction"],
                    json.loads(p["provenance"]),
                )
                for p in db.execute(
                    "SELECT * FROM pages WHERE book_id=? ORDER BY page_number", (book["id"],)
                )
            ]
            if len(pages) != book["page_count"]:
                raise ValueError(f"Source text is incomplete for book {book['id']}")
            suffix = ".txt" if Path(book["source_path"]).suffix.lower() == ".txt" else ".jsonl"
            documents.append(
                Document(
                    book["source_path"],
                    book["sha256"],
                    book["title"],
                    book["author"],
                    pages,
                    import_profile(suffix),
                )
            )
        return documents


def rebuild_database(
    source: Path,
    destination: Path,
    encoder: QwenEncoder,
    *,
    batch_size: int = 16,
) -> dict[str, Any]:
    """Publish only a fully rebuilt index; source database is never replaced."""
    started = time.perf_counter()
    source = source.expanduser().resolve(strict=True)
    destination = destination.expanduser().resolve()
    if destination == source or destination.exists():
        raise FileExistsError(f"Rebuild requires a new output path: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    with (
        import_lock(destination),
        tempfile.TemporaryDirectory(prefix=".yaqs-rebuild-", dir=destination.parent) as folder,
    ):
        snapshot = Path(folder) / "source.sqlite3"
        backup_database(source, snapshot)
        documents = stored_documents(snapshot)
        rebuilt = Path(folder) / "rebuilt.sqlite3"
        store = KnowledgeBase(rebuilt, create=True)
        for index, document in enumerate(documents, 1):
            print(
                json.dumps({"book": document.title, "position": index, "total": len(documents)}),
                file=sys.stderr,
                flush=True,
            )
            import_document(rebuilt, document, encoder, batch_size=batch_size)
        integrity = store.integrity()
        if integrity != INTEGRITY_OK:
            raise RuntimeError(f"Rebuilt database failed integrity check: {integrity}")
        backup_database(rebuilt, destination)
    return {
        "source": str(source),
        "destination": str(destination),
        "books": len(documents),
        "elapsed_s": round(time.perf_counter() - started, 3),
        "stats": KnowledgeBase(destination).stats(),
        "integrity": integrity,
    }
