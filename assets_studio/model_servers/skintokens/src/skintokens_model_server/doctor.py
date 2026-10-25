"""Print the runtime environment, flash-attn, bpy and the downloaded weights."""

import importlib.util
import json

import torch

from skintokens_model_server.server import (
    CHECKPOINT,
    LLM_DIRECTORY,
    VAE_CHECKPOINT,
    source_directory,
)


def main() -> None:
    source = source_directory()
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "source": (source / "src/model/tokenrig.py").is_file(),
        "flash_attn": importlib.util.find_spec("flash_attn") is not None,
        "bpy": importlib.util.find_spec("bpy") is not None,
        "weights": all((source / p).is_file() for p in (CHECKPOINT, VAE_CHECKPOINT)),
        "llm_config": (source / LLM_DIRECTORY / "config.json").is_file(),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
