"""The pinned llama.cpp server build: download, verify and locate it.

llama.cpp publishes Linux builds with CUDA; no compiler is needed. The build
is pinned with the checksums GitHub reports for its release files. Installing
is safe when two servers start at once: it runs under a lock, unpacks into a
temporary directory and moves that into place whole.
"""

import contextlib
import fcntl
import hashlib
import os
import shutil
import tarfile
import tempfile
import urllib.request
from collections.abc import Iterator
from dataclasses import dataclass
from pathlib import Path

BUILD = "b11312"
_RELEASE = f"https://github.com/ggml-org/llama.cpp/releases/download/{BUILD}"
_DOWNLOAD_TIMEOUT_S = 60  # per read, not for the whole file


@dataclass(frozen=True)
class _Archive:
    name: str
    sha256: str
    # The directory the archive unpacks to.
    directory: str


ARCHIVES = (
    _Archive(
        f"llama-{BUILD}-bin-ubuntu-cuda-12.8-x64.tar.gz",
        # pragma: allowlist nextline secret
        "375b995c91f84007f8189aa7acc476d348397a5cc431c31f649b8f76467a16b4",
        f"llama-{BUILD}",
    ),
    # The CUDA runtime libraries (cudart, cuBLAS) the build links against.
    _Archive(
        f"cudart-llama-{BUILD}-bin-ubuntu-cuda-12.8-x64.tar.gz",
        # pragma: allowlist nextline secret
        "3441758db8ec3fbccb9c86fbd8cff8dd50518473f3054727cb18de6f5e25f5da",
        f"cudart-llama-{BUILD}-bin-ubuntu-cuda-12.8-x64",
    ),
)


def directory() -> Path:
    cache = Path(os.environ.get("XDG_CACHE_HOME") or Path.home() / ".cache")
    return cache / "assets-studio" / "llama.cpp" / BUILD


def server_binary() -> Path:
    return directory() / ARCHIVES[0].directory / "llama-server"


def library_path() -> str:
    """LD_LIBRARY_PATH for the binary: its libraries and the CUDA runtime first."""
    own = [str(directory() / archive.directory) for archive in ARCHIVES]
    inherited = os.environ.get("LD_LIBRARY_PATH")
    return os.pathsep.join([*own, inherited] if inherited else own)


def installed() -> bool:
    return server_binary().is_file() and (directory() / ".complete").is_file()


def install(release: str = _RELEASE) -> Path:
    """Download, verify and unpack the build unless it is there."""
    target = directory()
    target.parent.mkdir(parents=True, exist_ok=True)
    with _locked(target.parent / f".{BUILD}.lock"):
        if installed():
            return server_binary()
        with tempfile.TemporaryDirectory(dir=target.parent, prefix=f".{BUILD}-") as scratch:
            staging = Path(scratch) / "build"
            staging.mkdir()
            for archive in ARCHIVES:
                path = Path(scratch) / archive.name
                print(f"downloading {archive.name}", flush=True)
                digest = _download(f"{release}/{archive.name}", path)
                if digest != archive.sha256:
                    raise RuntimeError(
                        f"{archive.name}: checksum {digest}, expected {archive.sha256}"
                    )
                with tarfile.open(path) as tar:
                    tar.extractall(staging, filter="data")
            (staging / ".complete").write_text("ok\n")
            shutil.rmtree(target, ignore_errors=True)  # a partial earlier attempt
            os.replace(staging, target)
    return server_binary()


def _download(url: str, path: Path) -> str:
    """Stream ``url`` to ``path``; return its sha256."""
    digest = hashlib.sha256()
    with (
        # The fixed https release URL (tests pass a file:// copy).
        urllib.request.urlopen(url, timeout=_DOWNLOAD_TIMEOUT_S) as response,  # noqa: S310
        path.open("wb") as out,
    ):
        while chunk := response.read(1 << 20):
            digest.update(chunk)
            out.write(chunk)
    return digest.hexdigest()


@contextlib.contextmanager
def _locked(path: Path) -> Iterator[None]:
    with path.open("w") as handle:
        fcntl.flock(handle, fcntl.LOCK_EX)
        yield


if __name__ == "__main__":
    print(install())
