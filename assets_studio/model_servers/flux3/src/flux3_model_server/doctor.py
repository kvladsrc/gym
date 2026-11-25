"""Report PyTorch, the CUDA device, llama.cpp, the weights and the offload store.

python -m flux3_model_server.doctor [--download]
"""

import argparse
import json
import shutil
from pathlib import Path

from huggingface_hub import hf_hub_download, snapshot_download, try_to_load_from_cache

from flux3_model_server.server import (
    BASE_FILES,
    BASE_REPO,
    BASE_REVISION,
    GGUF_FILE,
    GGUF_REPO,
    GGUF_REVISION,
    UPSAMPLER_FILE,
    UPSAMPLER_PROJECTOR,
    UPSAMPLER_PROMPTS,
    UPSAMPLER_REPO,
    UPSAMPLER_REVISION,
)
from model_server_sdk import offload

BASE_CHECK = [
    "text_encoder/model-00001-of-00002.safetensors",
    "text_encoder/model-00002-of-00002.safetensors",
    "vae/diffusion_pytorch_model.safetensors",
    "transformer/config.json",
]
UPSAMPLER_FILES = [UPSAMPLER_FILE, UPSAMPLER_PROJECTOR, UPSAMPLER_PROMPTS]


def cached(repo: str, revision: str, filename: str) -> bool:
    return isinstance(try_to_load_from_cache(repo, filename, revision=revision), str)


def download() -> None:
    """Everything the server loads (~26 GB); the transformer as GGUF only."""
    base = snapshot_download(BASE_REPO, revision=BASE_REVISION, allow_patterns=BASE_FILES)
    print(base, flush=True)
    print(hf_hub_download(GGUF_REPO, GGUF_FILE, revision=GGUF_REVISION), flush=True)
    for name in UPSAMPLER_FILES:
        print(hf_hub_download(UPSAMPLER_REPO, name, revision=UPSAMPLER_REVISION), flush=True)


def _existing(path: Path) -> Path:
    """The nearest existing directory at or above ``path``."""
    while not path.exists():
        path = path.parent
    return path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--download", action="store_true", help="fetch the weights")
    if parser.parse_args().download:
        download()
        return

    import torch
    from llm_model_server import runtime

    store = offload.base_directory("flux3")
    stores = sorted(p.name for p in store.iterdir() if p.is_dir()) if store.is_dir() else []
    report = {
        "torch": torch.__version__,
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "llama.cpp": f"{runtime.BUILD} installed" if runtime.installed() else "not installed",
        "base_files_missing": [f for f in BASE_CHECK if not cached(BASE_REPO, BASE_REVISION, f)],
        "transformer_q8_cached": cached(GGUF_REPO, GGUF_REVISION, GGUF_FILE),
        "upsampler_files_missing": [
            f for f in UPSAMPLER_FILES if not cached(UPSAMPLER_REPO, UPSAMPLER_REVISION, f)
        ],
        "offload_stores": {
            name: "complete" if (store / name / "complete").is_file() else "incomplete"
            for name in stores
        },
        "free_disk_gb": round(shutil.disk_usage(_existing(store)).free / 1e9),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
