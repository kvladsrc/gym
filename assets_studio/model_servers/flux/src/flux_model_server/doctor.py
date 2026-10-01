"""Print the runtime environment, access to the gated weights, what is
downloaded and the state of the offload store."""

import json
import shutil
from pathlib import Path

import torch
from huggingface_hub import get_token, try_to_load_from_cache

from flux_model_server.server import BASE_REPO, BASE_REVISION, GGUF_FILE, GGUF_REPO, GGUF_REVISION
from model_server_sdk import offload

BASE_CHECK = [
    "text_encoder/model.safetensors",
    "text_encoder_2/model-00001-of-00002.safetensors",
    "text_encoder_2/model-00002-of-00002.safetensors",
    "vae/diffusion_pytorch_model.safetensors",
    "tokenizer/vocab.json",
    "tokenizer_2/spiece.model",
]


def cached(repo: str, revision: str, filename: str) -> bool:
    return isinstance(try_to_load_from_cache(repo, filename, revision=revision), str)


def _existing(path: Path) -> Path:
    """The nearest existing directory at or above ``path``."""
    while not path.exists():
        path = path.parent
    return path


def main() -> None:
    store = offload.base_directory("flux")
    stores = (
        sorted(path.name for path in store.iterdir() if path.is_dir()) if store.is_dir() else []
    )
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        # FLUX.1-schnell is gated: the token's account must accept its licence.
        "hf_token_found": get_token() is not None,
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
