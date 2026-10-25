"""Print the runtime environment, Blender and whether the weights are downloaded."""

import json
import subprocess

import torch
from huggingface_hub import try_to_load_from_cache

from mocapanything_model_server.server import (
    WEIGHTS_FILE,
    WEIGHTS_REPO,
    WEIGHTS_REVISION,
    blender,
    source_directory,
)


def main() -> None:
    path = blender()
    version = None
    if path:
        done = subprocess.run(  # noqa: S603 -- Blender from PATH or $BLENDER
            [path, "--version"], capture_output=True, text=True, check=False
        )
        version = done.stdout.splitlines()[0]
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "source": (source_directory() / "inference/video2pose2rot.py").is_file(),
        "blender": version,
        "weights": isinstance(
            try_to_load_from_cache(WEIGHTS_REPO, WEIGHTS_FILE, revision=WEIGHTS_REVISION), str
        ),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
