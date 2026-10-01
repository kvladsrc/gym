"""SQLite access: one connection per thread, WAL, numbered migrations."""

import sqlite3
import threading
from collections.abc import Generator
from contextlib import contextmanager
from importlib import resources
from pathlib import Path


class Database:
    """Opens a connection per thread; applies pending migrations on creation."""

    def __init__(self, path: Path) -> None:
        self.path = path
        path.parent.mkdir(parents=True, exist_ok=True)
        self._local = threading.local()
        self._migrate()

    @property
    def connection(self) -> sqlite3.Connection:
        connection: sqlite3.Connection | None = getattr(self._local, "connection", None)
        if connection is None:
            # Autocommit mode: transactions are explicit (see ``transaction``).
            connection = sqlite3.connect(self.path, isolation_level=None, timeout=30)
            connection.row_factory = sqlite3.Row
            connection.execute("PRAGMA journal_mode = WAL")
            connection.execute("PRAGMA foreign_keys = ON")
            connection.execute("PRAGMA synchronous = NORMAL")
            self._local.connection = connection
        return connection

    @contextmanager
    def transaction(self) -> Generator[sqlite3.Connection]:
        """A write transaction that takes the lock up front (no upgrade deadlocks)."""
        connection = self.connection
        connection.execute("BEGIN IMMEDIATE")
        try:
            yield connection
        except BaseException:
            connection.execute("ROLLBACK")
            raise
        connection.execute("COMMIT")

    def close(self) -> None:
        connection: sqlite3.Connection | None = getattr(self._local, "connection", None)
        if connection is not None:
            connection.close()
            self._local.connection = None

    @property
    def schema_version(self) -> int:
        return self.connection.execute("PRAGMA user_version").fetchone()[0]

    def _migrate(self) -> None:
        migrations = sorted(
            (
                item
                for item in resources.files(__package__).joinpath("migrations").iterdir()
                if item.name.endswith(".sql")
            ),
            key=lambda item: item.name,
        )
        for number, migration in enumerate(migrations, start=1):
            if not migration.name.startswith(f"{number:04d}_"):
                raise RuntimeError(f"migration {migration.name} is out of sequence")
            if number <= self.schema_version:
                continue
            self._apply(migration.name, migration.read_text(), number)

    def _apply(self, name: str, script: str, number: int) -> None:
        """One migration in one transaction, with foreign keys off while it runs.

        This is SQLite's procedure for rebuilding a table (the only way to
        change a CHECK constraint): with foreign keys on, dropping the old
        table would cascade into the tables that reference it. The pragma has
        no effect inside a transaction, so it is switched around it, and the
        references are verified before the commit.
        """
        connection = self.connection
        connection.execute("PRAGMA foreign_keys = OFF")
        try:
            connection.executescript(f"BEGIN IMMEDIATE;\n{script}\nPRAGMA user_version = {number};")
            broken = connection.execute("PRAGMA foreign_key_check").fetchall()
            if broken:
                raise RuntimeError(f"migration {name} breaks {len(broken)} references")
            connection.execute("COMMIT")
        except BaseException:
            if connection.in_transaction:
                connection.execute("ROLLBACK")
            raise
        finally:
            connection.execute("PRAGMA foreign_keys = ON")
