"""The on-disk store of offloaded model blocks (no torch import, unit-testable).

Servers that stream a model larger than RAM to the GPU block by block
(diffusers group offload with ``offload_to_disk_path``) keep the blocks here.

diffusers writes each offloaded group to ``group_<hash>.safetensors`` once and
reuses any file that exists, with no check of what is in it. That is only safe
if the files certainly belong to the current weights and library versions and
were written completely. So the store lives in a directory named after a
fingerprint of both, is written under a lock, and counts only once a
"complete" marker is in place; anything else is wiped and written again.
"""

import fcntl
import os
import shutil
from collections.abc import Generator
from contextlib import contextmanager
from pathlib import Path

_COMPLETE = "complete"
_LOCK = ".lock"


class OffloadError(RuntimeError):
    """The store cannot be prepared (another server holds it, or no disk space)."""


def base_directory(server: str) -> Path:
    """Where a server's offload stores live (outside Dropbox)."""
    cache = Path(os.environ.get("XDG_CACHE_HOME") or Path.home() / ".cache")
    return cache / "assets-studio" / f"{server}-offload"


def fingerprint(*parts: str) -> str:
    """A directory name from the weights' revisions and library versions."""
    return "-".join(part.replace("/", "_").replace("+", "_") for part in parts)


@contextmanager
def lock(base: Path) -> Generator[None]:
    """Hold the store exclusively while it is prepared and used by one server."""
    base.mkdir(parents=True, exist_ok=True)
    handle = (base / _LOCK).open("w")
    try:
        try:
            fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise OffloadError(f"another server is using {base}; stop it first") from None
        yield
    finally:
        handle.close()


def prepare(base: Path, name: str, *, needed_bytes: int) -> tuple[Path, bool]:
    """The store directory for ``name`` and whether it is complete.

    An incomplete store (interrupted first start) is wiped; stores of other
    fingerprints are removed. Before a new store is written, there must be
    ``needed_bytes`` of free disk space.
    """
    directory = base / name
    for sibling in base.iterdir():
        if sibling.is_dir() and sibling.name != name:
            shutil.rmtree(sibling)
    if (directory / _COMPLETE).is_file():
        return directory, True
    shutil.rmtree(directory, ignore_errors=True)
    free = shutil.disk_usage(base).free
    if free < needed_bytes:
        raise OffloadError(
            f"the offload store needs {needed_bytes / 1e9:.0f} GB free in {base}; "
            f"{free / 1e9:.0f} GB is free"
        )
    directory.mkdir(parents=True)
    return directory, False


def mark_complete(directory: Path) -> None:
    """Record, atomically, that every file of the store has been written."""
    os.sync()  # the blocks reach the disk before the marker does
    temporary = directory / f".{_COMPLETE}.tmp"
    temporary.write_text("ok\n")
    os.replace(temporary, directory / _COMPLETE)
