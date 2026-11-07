import sqlite3
from contextlib import closing

import pytest
from helpers import ATTACK

from yaqs import SCHEMA_VERSION
from yaqs.maintenance import backup_database, rebuild_database
from yaqs.store import KnowledgeBase


def test_backup_contains_committed_wal_and_is_standalone(library, tmp_path):
    # Hold an older read snapshot while a new committed version stays in WAL.
    with library.connect() as keeper:
        keeper.execute("PRAGMA wal_autocheckpoint=0")
        keeper.execute("BEGIN")
        keeper.execute("SELECT author FROM books").fetchone()
        with library.connect() as writer, writer:
            writer.execute("UPDATE books SET author='Updated author'")
        assert library.path.with_name(library.path.name + "-wal").stat().st_size > 0
        target = tmp_path / "backup.sqlite3"
        backup_database(library.path, target)
        assert not target.with_name(target.name + "-wal").exists()
    copy = KnowledgeBase(target)
    assert copy.books()[0]["author"] == "Updated author"
    assert copy.stats()["counts"] == library.stats()["counts"]
    assert copy.integrity()["pending_chunks"] == 0


def test_backup_refuses_overwrite_or_self_copy(library, tmp_path):
    target = tmp_path / "existing"
    target.write_text("keep me")
    for path in (target, library.path):
        with pytest.raises(FileExistsError):
            backup_database(library.path, path)
    assert target.read_text() == "keep me"


def test_rebuild_legacy_schema_uses_stored_text_without_source_files(library, encoder, tmp_path):
    old = library.books()[0]
    (tmp_path / "source.txt").unlink()
    with library.connect() as db, db:
        db.execute("PRAGMA user_version=1")
    target = tmp_path / "rebuilt.sqlite3"
    result = rebuild_database(library.path, target, encoder)
    assert result["stats"]["schema"] == SCHEMA_VERSION
    rebuilt = KnowledgeBase(target)
    book = rebuilt.books()[0]
    assert (book["title"], book["author"], book["sha256"]) == (
        old["title"],
        old["author"],
        old["sha256"],
    )
    assert rebuilt.page(book["id"], 1)["text"] == ATTACK
    assert result["integrity"]["pending_chunks"] == 0
    with closing(sqlite3.connect(library.path)) as db:
        assert db.execute("PRAGMA user_version").fetchone()[0] == 1


def test_failed_rebuild_does_not_publish_partial_database(library, encoder, tmp_path):
    def fail(*args, **kwargs):
        raise RuntimeError("inference failed")

    encoder.encode = fail
    target = tmp_path / "rebuilt.sqlite3"
    with pytest.raises(RuntimeError, match="inference failed"):
        rebuild_database(library.path, target, encoder)
    assert not target.exists()
    assert library.integrity()["pending_chunks"] == 0
    assert library.books()[0]["title"] == "Game reference"
