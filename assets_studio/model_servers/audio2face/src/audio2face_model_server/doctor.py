"""Print TensorRT's version, the GPU, the runner and what is downloaded."""

import json
import os
import subprocess
import sys

from audio2face_model_server.server import (
    HEADS,
    MODELS,
    head_path,
    library_path,
    runner_path,
    sdk_library,
    source_directory,
)


def main() -> None:
    import tensorrt_bindings

    gpu = subprocess.run(
        [sys.executable, "-c", "from audio2face_model_server.engine import gpu; print(gpu())"],
        capture_output=True,
        text=True,
        check=False,
    )
    report = {
        "tensorrt": tensorrt_bindings.__version__,
        "gpu": gpu.stdout.strip() or gpu.stderr.strip().splitlines()[-1:],
        "source": (source_directory() / "CMakeLists.txt").is_file(),
        "sdk_library": sdk_library().is_file(),
        "runner": runner_path().is_file(),
        "runner_libraries": None,
        "networks": {
            name: {
                "onnx": (model.directory / "network.onnx").is_file(),
                "engine": (model.directory / "network.trt").is_file(),
            }
            for name, model in MODELS.items()
        },
        "heads": {scene: head_path(scene).is_file() for scene in HEADS},
    }
    if runner_path().is_file():
        # Every library the runner needs, found? (ldd lists the missing ones as "not found".)
        ldd = subprocess.run(  # noqa: S603 -- fixed argv, no shell
            ["ldd", str(runner_path())],  # noqa: S607 -- the system's ldd
            env={**os.environ, "LD_LIBRARY_PATH": library_path()},
            capture_output=True,
            text=True,
            check=False,
        )
        missing = [line.split()[0] for line in ldd.stdout.splitlines() if "not found" in line]
        report["runner_libraries"] = "all found" if not missing else {"missing": missing}
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
