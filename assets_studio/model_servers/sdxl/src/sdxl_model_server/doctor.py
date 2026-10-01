"""Print the runtime environment and whether the weights are downloaded."""

import json

import torch
from huggingface_hub import try_to_load_from_cache

from sdxl_model_server.server import BASE_REPO, BASE_REVISION, VAE_REPO, VAE_REVISION


def cached(repo: str, revision: str, filename: str) -> bool:
    """Whether this file of this revision is on disk (works with no cache at all)."""
    return isinstance(try_to_load_from_cache(repo, filename, revision=revision), str)


def main() -> None:
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "unet_weights_cached": cached(
            BASE_REPO, BASE_REVISION, "unet/diffusion_pytorch_model.fp16.safetensors"
        ),
        "vae_cached": cached(VAE_REPO, VAE_REVISION, "diffusion_pytorch_model.safetensors"),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
