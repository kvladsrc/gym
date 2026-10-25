"""Print the runtime environment, the source and the downloaded weights."""

import json

import torch

from hunyuan3d21_model_server.server import (
    SUBFOLDER,
    WEIGHTS_REPO,
    models_directory,
    source_directory,
)


def main() -> None:
    weights = models_directory() / WEIGHTS_REPO / SUBFOLDER / "model.fp16.ckpt"
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "source": (source_directory().expanduser() / "hy3dshape/hy3dshape").is_dir(),
        "weights": weights.is_file(),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
