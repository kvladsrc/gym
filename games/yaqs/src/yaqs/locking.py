"""Serialize all library writers; readers remain available through SQLite WAL."""

import fcntl
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path


@contextmanager
def import_lock(path: Path, *, wait: bool = False) -> Iterator[None]:
    path = path.expanduser().resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.with_suffix(".import.lock").open("a") as handle:
        try:
            fcntl.flock(handle, fcntl.LOCK_EX | (0 if wait else fcntl.LOCK_NB))
        except BlockingIOError as error:
            raise RuntimeError(
                "Another writer owns this database; retry after it finishes"
            ) from error
        yield
