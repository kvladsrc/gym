"""Print the runtime environment, the source and the downloaded weights."""

import json

import torch
from huggingface_hub import try_to_load_from_cache

from hunyuan3d_model_server.server import (
    SUBFOLDER,
    WEIGHTS_REPO,
    WEIGHTS_REVISION,
    source_directory,
)


def main() -> None:
    weights = try_to_load_from_cache(
        WEIGHTS_REPO, f"{SUBFOLDER}/model.fp16.safetensors", revision=WEIGHTS_REVISION
    )
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "source": (source_directory().expanduser() / "hy3dgen").is_dir(),
        "weights": isinstance(weights, str),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
