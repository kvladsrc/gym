"""The native packages LATO.2's input preparation imports, outside the repository.

LATO.2 finds the voxels a surface occupies with TRELLIS.2's o-voxel, which
imports CuMesh, FlexGEMM and nvdiffrast. The pins, the checkouts and the wheel
cache are those of the TRELLIS.2 Paint server (same Python, same torch), so
whichever server is set up first builds them and the other installs them.
"""

import os
import shutil
import subprocess
import sys
from pathlib import Path

# Public commits, not secrets.
TRELLIS2_REVISION = "75fbf0183001ed9876c8dbb35de6b68552ee08bd"  # pragma: allowlist secret
NATIVE = (
    (
        "FlexGEMM",
        "6dd94a859c26ee8246888502eada3dd8ad85532e",  # pragma: allowlist secret
        "flex_gemm-",
        "https://github.com/JeffreyXiang/FlexGEMM.git",
    ),
    (
        "CuMesh",
        "12289e1062f0603f2f0d0771b02e1395d247f26f",  # pragma: allowlist secret
        "cumesh-",
        "https://github.com/JeffreyXiang/CuMesh.git",
    ),
    ("o-voxel", TRELLIS2_REVISION, "o_voxel-", "https://github.com/microsoft/TRELLIS.2.git"),
    (
        "nvdiffrast",
        "253ac4fcea7de5f396371124af597e6cc957bfae",  # pragma: allowlist secret
        "nvdiffrast-",
        "https://github.com/NVlabs/nvdiffrast.git",
    ),
)


def run(arguments: list[str], *, check: bool = True) -> int:
    executable = shutil.which(arguments[0])
    if executable is None:
        raise FileNotFoundError(arguments[0])
    # Fixed setup commands and pinned sources, passed as argv without a shell.
    command = [executable, *arguments[1:]]
    return subprocess.run(command, check=check, capture_output=not check).returncode  # noqa: S603


def checkout(url: str, revision: str, target: Path) -> None:
    if not (target / ".git").exists():
        run(["git", "clone", "--no-checkout", url, str(target)])
    if run(["git", "-C", str(target), "cat-file", "-e", revision], check=False):
        run(["git", "-C", str(target), "fetch", "--quiet", "origin", revision])
    run(["git", "-C", str(target), "checkout", "--quiet", "--detach", revision])
    run(["git", "-C", str(target), "submodule", "update", "--init", "--recursive"])


def main() -> None:
    root = Path(os.environ.get("XDG_CACHE_HOME") or Path.home() / ".cache") / "assets-studio"
    wheels = root / "trellis2-paint-native/wheels"
    wheels.mkdir(parents=True, exist_ok=True)
    for name, revision, prefix, url in NATIVE:
        found = sorted(wheels.glob(prefix + "*cp311*linux_x86_64.whl"))
        if not found:
            if name == "o-voxel":
                checkout(url, revision, root / "trellis2-source")
                target = root / "trellis2-source/o-voxel"
            else:
                target = root / "trellis2-paint-native" / name
                checkout(url, revision, target)
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
                    str(target),
                ]
            )
            found = sorted(wheels.glob(prefix + "*cp311*linux_x86_64.whl"))
        # o-voxel declares Git dependencies; never rebuild already pinned packages.
        run(["uv", "pip", "install", "--python", sys.executable, "--no-deps", str(found[-1])])


if __name__ == "__main__":
    main()
