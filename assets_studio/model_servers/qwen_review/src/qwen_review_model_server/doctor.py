"""Print CUDA availability, the Triton kernels and whether the weights and adapter are in place."""

import argparse
import importlib.util
import json

import torch
from huggingface_hub import try_to_load_from_cache

from qwen_review_model_server.server import BASES, DEFAULT_BASE, adapter_digest, adapter_directory


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", choices=sorted(BASES), default=DEFAULT_BASE)
    base = parser.parse_args().base
    repo, revision = BASES[base].repo, BASES[base].revision
    index = try_to_load_from_cache(repo, "model.safetensors.index.json", revision=revision)
    shards = []
    if isinstance(index, str):
        with open(index, encoding="utf-8") as handle:
            shards = sorted(set(json.load(handle)["weight_map"].values()))
    adapter = adapter_directory(base)
    print(
        json.dumps(
            {
                "model": repo,
                "revision": revision,
                "torch": torch.__version__,
                "cuda": torch.cuda.is_available(),
                "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
                "flash_linear_attention": importlib.util.find_spec("fla") is not None,
                "weights": {
                    name: isinstance(try_to_load_from_cache(repo, name, revision=revision), str)
                    for name in ["model.safetensors.index.json", *shards]
                },
                "adapter": str(adapter),
                "adapter_sha256": adapter_digest(adapter),
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
