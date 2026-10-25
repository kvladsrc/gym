"""Print the runtime environment, diso and the downloaded weights."""

import importlib.util
import json

import torch
from huggingface_hub import try_to_load_from_cache

from triposg_model_server.server import (
    WEIGHTS_REPO,
    WEIGHTS_REVISION,
    source_directory,
)


def main() -> None:
    transformer = try_to_load_from_cache(
        WEIGHTS_REPO,
        "transformer/diffusion_pytorch_model.safetensors",
        revision=WEIGHTS_REVISION,
    )
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "source": (source_directory().expanduser() / "triposg").is_dir(),
        "diso": importlib.util.find_spec("diso") is not None,
        "weights": isinstance(transformer, str),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
