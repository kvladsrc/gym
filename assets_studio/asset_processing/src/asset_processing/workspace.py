"""Temporary job files, optionally retained for operator diagnostics."""

import logging
import os
import tempfile
from collections.abc import Generator
from contextlib import contextmanager
from pathlib import Path


@contextmanager
def job_workspace(prefix: str) -> Generator[Path]:
    """Set ASSETS_STUDIO_DEBUG_DIR to retain inputs/intermediates, even on failure.

    Disabled by default: ordinary jobs remove their working files. A retained
    directory is unique to a request; it is never reused as an inference cache.
    """
    directory = os.environ.get("ASSETS_STUDIO_DEBUG_DIR")
    if directory:
        root = Path(directory).expanduser()
        root.mkdir(parents=True, exist_ok=True)
        work = Path(tempfile.mkdtemp(prefix=prefix, dir=root))
        logging.getLogger(__name__).info("retaining job diagnostics in %s", work)
        yield work
    else:
        with tempfile.TemporaryDirectory(prefix=prefix) as temporary:
            yield Path(temporary)
