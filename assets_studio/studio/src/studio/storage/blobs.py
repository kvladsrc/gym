"""Content-addressed file storage: files are named by the SHA-256 of their bytes."""

import hashlib
import os
import tempfile
import threading
from pathlib import Path

from studio.domain import FILE_EXTENSIONS


class BlobStore:
    def __init__(self, root: Path) -> None:
        self.root = root
        # Held from storing a file until its asset is recorded, and from
        # deleting an asset until its file is gone: files are shared by
        # content, and a file must not vanish under an asset just recorded.
        self.lock = threading.RLock()

    def path(self, sha256: str, mime: str) -> Path:
        return self.root / sha256[:2] / f"{sha256}{FILE_EXTENSIONS[mime]}"

    def put(self, data: bytes, mime: str) -> str:
        """Store ``data`` once; return its SHA-256. Existing files are never rewritten."""
        sha256 = hashlib.sha256(data).hexdigest()
        target = self.path(sha256, mime)
        if target.exists():
            return sha256
        target.parent.mkdir(parents=True, exist_ok=True)
        # Write next to the target, then rename atomically: readers never see
        # a partial file.
        descriptor, temporary = tempfile.mkstemp(dir=target.parent, prefix=".incoming-")
        try:
            with os.fdopen(descriptor, "wb") as file:
                file.write(data)
                file.flush()
                os.fsync(file.fileno())
            os.replace(temporary, target)
        except BaseException:
            Path(temporary).unlink(missing_ok=True)
            raise
        return sha256

    def remove(self, sha256: str, mime: str) -> None:
        self.path(sha256, mime).unlink(missing_ok=True)

    def read(self, sha256: str, mime: str) -> bytes:
        return self.path(sha256, mime).read_bytes()
