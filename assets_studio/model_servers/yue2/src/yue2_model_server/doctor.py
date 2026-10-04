"""Print the runtime environment and whether the weights are downloaded."""

import json

import torch
from huggingface_hub import try_to_load_from_cache

from yue2_model_server.server import MODEL_REPO, MODEL_REVISION, VAE_REPO, VAE_REVISION

CHECK = [
    (MODEL_REPO, MODEL_REVISION, "model.safetensors"),
    (MODEL_REPO, MODEL_REVISION, "qwen.tiktoken"),
    (VAE_REPO, VAE_REVISION, "model.safetensors"),
]


def main() -> None:
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "bf16": torch.cuda.is_available() and torch.cuda.is_bf16_supported(),
        "files_missing": [
            f"{repo}/{name}"
            for repo, revision, name in CHECK
            if not isinstance(try_to_load_from_cache(repo, name, revision=revision), str)
        ],
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
