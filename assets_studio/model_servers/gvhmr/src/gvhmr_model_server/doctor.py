"""Report prerequisites without importing the worker's torch into the server."""

import json

from gvhmr_model_server.server import blender, missing_files, run, source_directory, worker_python


def main() -> None:
    report = {
        "source": str(source_directory()),
        "worker": str(worker_python()),
        "missing_checkpoints": missing_files(),
        "blender": blender(),
    }
    print(json.dumps(report, indent=2))
    if worker_python().is_file():
        print(
            run(
                [
                    str(worker_python()),
                    "-c",
                    "import torch; print('torch', torch.__version__); "
                    "print('cuda', torch.cuda.is_available())",
                ]
            )
        )


if __name__ == "__main__":
    main()
