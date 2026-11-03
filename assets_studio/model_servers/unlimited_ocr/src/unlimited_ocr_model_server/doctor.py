"""Print CUDA availability and whether every pinned weight shard is cached."""

import json

import torch
from huggingface_hub import try_to_load_from_cache

from unlimited_ocr_model_server.server import REPO, REVISION


def main() -> None:
    files = ["model-00001-of-00002.safetensors", "model-00002-of-00002.safetensors"]
    print(
        json.dumps(
            {
                "model": REPO,
                "revision": REVISION,
                "torch": torch.__version__,
                "cuda": torch.cuda.is_available(),
                "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
                "weights": {
                    name: isinstance(try_to_load_from_cache(REPO, name, revision=REVISION), str)
                    for name in files
                },
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
