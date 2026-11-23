"""Build the Audio2Face-3D SDK's library and the server's runner (`just setup`).

Runs in the CUDA shell: the SDK needs the CUDA toolkit (12.8 or newer) and a
host compiler. TensorRT comes from this environment's wheels; its headers,
which the wheels do not carry, from TensorRT's open-source repository at the
same version. The SDK's own fetcher (packman) brings CMake, Ninja and the
libraries the SDK builds against.

Environment: AUDIO2FACE_SOURCE (the pinned SDK checkout), AUDIO2FACE_BUILD
(where everything is built), CUDA_ARCH (e.g. 8.6), MAX_JOBS.
"""

import os
import subprocess
from pathlib import Path

from audio2face_model_server.server import (
    build_directory,
    runner_path,
    sdk_library,
    source_directory,
)

TENSORRT_REPOSITORY = "https://github.com/NVIDIA/TensorRT.git"
# Tag v10.13.3, the version of the tensorrt-cu12 wheels. A Git revision, not a secret.
TENSORRT_REVISION = "94e2b9ef6d2cce74c76cdad499cca36cc4949197"  # pragma: allowlist secret
TENSORRT_VERSION = (10, 13, 3)
TENSORRT_LIBRARIES = ("nvinfer", "nvinfer_plugin", "nvonnxparser")


def run(*command: str | Path, cwd: Path | None = None, env: dict[str, str] | None = None) -> None:
    print("+", " ".join(str(part) for part in command), flush=True)
    subprocess.run([str(part) for part in command], cwd=cwd, env=env, check=True)  # noqa: S603


def tensorrt_library_directory() -> Path:
    """Where the tensorrt-cu12-libs wheel keeps libnvinfer.so.10 and the rest."""
    import tensorrt_libs

    return Path(tensorrt_libs.__file__).parent


def tensorrt_root() -> Path:
    """A TensorRT directory as the SDK's CMake looks for it: include/ with the
    headers, lib/ with unversioned names of the wheels' libraries."""
    root = build_directory() / "tensorrt"
    headers = root / "source"
    if not (headers / ".git").is_dir():
        run("git", "init", "--quiet", headers)
        run("git", "-C", headers, "remote", "add", "origin", TENSORRT_REPOSITORY)
        run("git", "-C", headers, "sparse-checkout", "set", "include")
    run("git", "-C", headers, "fetch", "--quiet", "--depth", "1", "origin", TENSORRT_REVISION)
    run("git", "-C", headers, "checkout", "--quiet", "--detach", TENSORRT_REVISION)
    # NV_TENSORRT_MAJOR and the others are defined through other macros.
    defines = {
        parts[1]: parts[2]
        for parts in (
            line.split() for line in (headers / "include/NvInferVersion.h").read_text().splitlines()
        )
        if len(parts) >= 3 and parts[0] == "#define"
    }

    def value(name: str) -> int:
        while not defines[name].isdigit():
            name = defines[name]
        return int(defines[name])

    found = tuple(value(f"NV_TENSORRT_{part}") for part in ("MAJOR", "MINOR", "PATCH"))
    if found != TENSORRT_VERSION:
        raise RuntimeError(f"TensorRT headers are {found}, the wheels {TENSORRT_VERSION}")
    include = root / "include"
    if not include.is_symlink():
        include.symlink_to(headers / "include")
    libraries = tensorrt_library_directory()
    (root / "lib").mkdir(parents=True, exist_ok=True)
    for name in TENSORRT_LIBRARIES:
        link = root / "lib" / f"lib{name}.so"
        link.unlink(missing_ok=True)
        link.symlink_to(libraries / f"lib{name}.so.{TENSORRT_VERSION[0]}")
    return root


def compilers() -> list[str]:
    """C and C++ by the CUDA host compiler, when the shell names one: the
    SDK's C++ and its CUDA code then share a compiler, and the SDK's prebuilt
    headers (refl-cpp) predate GCC 15's leaner standard headers."""
    host = os.environ.get("CUDAHOSTCXX")
    if not host:
        return []
    cc = Path(host).with_name("cc")
    return [f"-DCMAKE_CXX_COMPILER={host}", f"-DCMAKE_CUDA_HOST_COMPILER={host}"] + (
        [f"-DCMAKE_C_COMPILER={cc}"] if cc.exists() else []
    )


def main() -> None:
    source = source_directory()
    if not (source / "CMakeLists.txt").is_file():
        raise SystemExit(f"Audio2Face-3D SDK source not found in {source}")
    jobs = os.environ.get("MAX_JOBS", "2")
    architecture = os.environ.get("CUDA_ARCH", "8.6").replace(".", "")
    trt = tensorrt_root()

    # CMake, Ninja and the libraries the SDK builds against, into source/_deps.
    run(source / "fetch_deps.sh", "release", cwd=source)
    cmake = source / "_deps/build-deps/cmake/bin/cmake"
    ninja = source / "_deps/build-deps/ninja/ninja"

    sdk = build_directory() / "sdk"
    # The SDK's FindZLIB prefers the system's zlib; its directory, /usr/include,
    # then brings the system's C headers in front of the toolchain's. The SDK
    # fetches its own zlib, so that one is named outright.
    zlib = source / "_deps/target-deps/zlib/release"
    run(
        cmake, "-S", source, "-B", sdk, "-G", "Ninja",
        f"-DCMAKE_MAKE_PROGRAM={ninja}",
        "-DCMAKE_BUILD_TYPE=Release",
        f"-DCMAKE_CUDA_ARCHITECTURES={architecture}",
        f"-DTENSORRT_ROOT_DIR={trt}",
        *compilers(),
        *(f"-DZLIB_INCLUDE_DIR_{kind}={zlib / 'include'}" for kind in ("RELEASE", "DEBUG")),
        *(f"-DZLIB_LIBRARY_{kind}={zlib / 'lib/libz.a'}" for kind in ("RELEASE", "DEBUG")),
    )  # fmt: skip
    run(cmake, "--build", sdk, "--target", "audio2x", "--parallel", jobs)

    runner = build_directory() / "runner"
    run(
        cmake, "-S", Path(__file__).resolve().parents[2] / "native", "-B", runner, "-G", "Ninja",
        f"-DCMAKE_MAKE_PROGRAM={ninja}",
        "-DCMAKE_BUILD_TYPE=Release",
        f"-DAUDIO2X_SDK={sdk_library().parents[1]}",
        f"-DTENSORRT_LIBRARY_DIR={tensorrt_library_directory()}",
        *compilers(),
    )  # fmt: skip
    run(cmake, "--build", runner, "--parallel", jobs)
    print("runner:", runner_path(), "library:", sdk_library(), flush=True)


if __name__ == "__main__":
    main()
