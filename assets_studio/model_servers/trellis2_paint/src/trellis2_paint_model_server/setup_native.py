"""Pinned source and native dependencies, outside the repository."""

import os
import shutil
import subprocess
import sys
from pathlib import Path

from trellis2_paint_model_server.server import SOURCE_REVISION, source_directory

NATIVE = (
    (
        "FlexGEMM",
        "6dd94a859c26ee8246888502eada3dd8ad85532e",
        "flex_gemm-",
        "https://github.com/JeffreyXiang/FlexGEMM.git",
    ),
    (
        "CuMesh",
        "12289e1062f0603f2f0d0771b02e1395d247f26f",
        "cumesh-",
        "https://github.com/JeffreyXiang/CuMesh.git",
    ),
    ("o-voxel", SOURCE_REVISION, "o_voxel-", ""),
    (
        "nvdiffrast",
        "253ac4fcea7de5f396371124af597e6cc957bfae",
        "nvdiffrast-",
        "https://github.com/NVlabs/nvdiffrast.git",
    ),
)


def run(
    arguments: list[str], *, check: bool = True, capture_output: bool = False
) -> subprocess.CompletedProcess[bytes]:
    executable = shutil.which(arguments[0])
    if executable is None:
        raise FileNotFoundError(arguments[0])
    # Fixed setup commands and pinned sources, passed as argv without a shell.
    return subprocess.run([executable, *arguments[1:]], check=check, capture_output=capture_output)  # noqa: S603


def checkout(url: str, revision: str, target: Path) -> None:
    if not (target / ".git").exists():
        run(["git", "clone", "--no-checkout", url, str(target)], check=True)
    if run(
        ["git", "-C", str(target), "cat-file", "-e", revision], check=False, capture_output=True
    ).returncode:
        run(["git", "-C", str(target), "fetch", "--quiet", "origin", revision], check=True)
    run(["git", "-C", str(target), "checkout", "--quiet", "--detach", revision], check=True)


def main() -> None:
    root = Path(os.environ.get("XDG_CACHE_HOME", Path.home() / ".cache")) / "assets-studio"
    source = source_directory()
    checkout("https://github.com/microsoft/TRELLIS.2.git", SOURCE_REVISION, source)
    # Current trimesh exposes cached normals as read-only. Geometry is unchanged.
    path = source / "trellis2/pipelines/trellis2_texturing.py"
    text = path.read_text()
    path.write_text(
        text.replace("normals = mesh.vertex_normals\n", "normals = mesh.vertex_normals.copy()\n")
    )
    wheels = root / "trellis2-paint-native/wheels"
    wheels.mkdir(parents=True, exist_ok=True)
    for name, revision, prefix, url in NATIVE:
        found = sorted(wheels.glob(prefix + "*cp311*linux_x86_64.whl"))
        if not found:
            if name == "o-voxel":
                target = source / "o-voxel"
                run(
                    ["git", "-C", str(source), "submodule", "update", "--init", "--recursive"],
                    check=True,
                )
            else:
                target = root / "trellis2-paint-native" / name
                checkout(url, revision, target)
                run(
                    ["git", "-C", str(target), "submodule", "update", "--init", "--recursive"],
                    check=True,
                )
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
                ],
                check=True,
            )
            found = sorted(wheels.glob(prefix + "*cp311*linux_x86_64.whl"))
        # o-voxel declares Git dependencies; never rebuild already pinned packages.
        run(
            ["uv", "pip", "install", "--python", sys.executable, "--no-deps", str(found[-1])],
            check=True,
        )


if __name__ == "__main__":
    main()
