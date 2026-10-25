"""Print the runtime environment and which variants are downloaded."""

import json

import torch
from huggingface_hub import try_to_load_from_cache

from birefnet_model_server.server import VARIANTS


def main() -> None:
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "weights": {
            name: isinstance(
                try_to_load_from_cache(
                    variant.repo, "model.safetensors", revision=variant.revision
                ),
                str,
            )
            for name, variant in VARIANTS.items()
        },
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
