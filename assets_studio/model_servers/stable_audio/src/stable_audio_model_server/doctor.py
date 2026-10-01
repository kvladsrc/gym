"""Print the runtime environment, access to the gated weights and what is downloaded."""

import json

import torch
from huggingface_hub import get_token, try_to_load_from_cache

from stable_audio_model_server.server import REPO, REVISION

CHECK = [
    "transformer/diffusion_pytorch_model.safetensors",
    "vae/diffusion_pytorch_model.safetensors",
    "text_encoder/model.safetensors",
    "projection_model/diffusion_pytorch_model.safetensors",
    "tokenizer/spiece.model",
    "scheduler/scheduler_config.json",
]


def main() -> None:
    missing = [
        name
        for name in CHECK
        if not isinstance(try_to_load_from_cache(REPO, name, revision=REVISION), str)
    ]
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        # Stable Audio Open is gated: the token's account must accept its licence.
        "hf_token_found": get_token() is not None,
        "files_missing": missing,
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
