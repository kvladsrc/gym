"""Print the runtime environment, what is downloaded and the state of the
offload store."""

import json
import shutil
from pathlib import Path

import torch
from huggingface_hub import try_to_load_from_cache

from model_server_sdk import offload
from qwen_image_model_server.server import (
    BASE_REPO,
    BASE_REVISION,
    GGUF_FILE,
    GGUF_REPO,
    GGUF_REVISION,
)

BASE_CHECK = [
    "text_encoder/model-00001-of-00004.safetensors",
    "text_encoder/model-00002-of-00004.safetensors",
    "text_encoder/model-00003-of-00004.safetensors",
    "text_encoder/model-00004-of-00004.safetensors",
    "vae/diffusion_pytorch_model.safetensors",
    "processor/tokenizer.json",
]


def cached(repo: str, revision: str, filename: str) -> bool:
    return isinstance(try_to_load_from_cache(repo, filename, revision=revision), str)


def _existing(path: Path) -> Path:
    """The nearest existing directory at or above ``path``."""
    while not path.exists():
        path = path.parent
    return path


def main() -> None:
    store = offload.base_directory("qwen_image")
    stores = (
        sorted(path.name for path in store.iterdir() if path.is_dir()) if store.is_dir() else []
    )
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "base_files_missing": [f for f in BASE_CHECK if not cached(BASE_REPO, BASE_REVISION, f)],
        "transformer_q8_cached": cached(GGUF_REPO, GGUF_REVISION, GGUF_FILE),
        "offload_stores": {
            name: "complete" if (store / name / "complete").is_file() else "incomplete"
            for name in stores
        },
        "free_disk_gb": round(shutil.disk_usage(_existing(store)).free / 1e9),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
