"""The two CUDA kernels DeepMesh imports besides flash-attn, outside the repository.

DeepMesh's transformer applies its rotary embedding and its RMS norm with
``rotary_emb`` and ``dropout_layer_norm``, small extensions that live in the
flash-attention repository next to the attention itself and have no wheels.
They are built once, from the tag of the flash-attn wheel the server installs,
and kept in the user's cache.
"""

import os
import shutil
import subprocess
import sys
from pathlib import Path

URL = "https://github.com/Dao-AILab/flash-attention.git"
# v2.7.4.post1. A public commit, not a secret.
REVISION = "5231d95fe13733fb534c01895f7ea88c6a6c7793"  # pragma: allowlist secret
KERNELS = (("rotary_emb", "csrc/rotary"), ("dropout_layer_norm", "csrc/layer_norm"))


def run(arguments: list[str], *, check: bool = True) -> int:
    executable = shutil.which(arguments[0])
    if executable is None:
        raise FileNotFoundError(arguments[0])
    # Fixed setup commands and a pinned source, passed as argv without a shell.
    command = [executable, *arguments[1:]]
    return subprocess.run(command, check=check, capture_output=not check).returncode  # noqa: S603


def checkout(target: Path) -> None:
    """The pinned commit without its submodules: the kernels need none."""
    if not (target / ".git").exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        run(["git", "clone", "--no-checkout", "--filter=blob:none", URL, str(target)])
    if run(["git", "-C", str(target), "cat-file", "-e", REVISION + "^{commit}"], check=False):
        run(["git", "-C", str(target), "fetch", "--quiet", "origin", REVISION])
    run(["git", "-C", str(target), "checkout", "--quiet", "--detach", REVISION])


def main() -> None:
    root = Path(os.environ.get("XDG_CACHE_HOME") or Path.home() / ".cache") / "assets-studio"
    wheels = root / "deepmesh-native/wheels"
    source = root / "deepmesh-native/flash-attention"
    wheels.mkdir(parents=True, exist_ok=True)
    for name, directory in KERNELS:
        found = sorted(wheels.glob(name + "-*cp311*linux_x86_64.whl"))
        if not found:
            checkout(source)
            run(
                [
                    "uv",
                    "build",
                    "--wheel",
                    "--no-build-isolation",
                    "--python",
                    sys.executable,
                    "--out-dir",
                    str(wheels),
                    str(source / directory),
                ]
            )
            found = sorted(wheels.glob(name + "-*cp311*linux_x86_64.whl"))
        run(["uv", "pip", "install", "--python", sys.executable, "--no-deps", str(found[-1])])


if __name__ == "__main__":
    main()
