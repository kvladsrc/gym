"""Print the runtime environment, the source, the kernels and the downloaded weights."""

import json

import torch

from deepmesh_model_server.server import WEIGHTS_FILE, source_directory, weights_directory


def main() -> None:
    cuda = torch.cuda.is_available()
    report = {
        "torch": torch.__version__,
        "cuda": cuda,
        "gpu": torch.cuda.get_device_name(0) if cuda else None,
        "capability": torch.cuda.get_device_capability(0) if cuda else None,
        "source": (source_directory() / "lit_gpt/model_cache.py").is_file(),
        "weights": (weights_directory() / WEIGHTS_FILE).is_file(),
    }
    for module in ("flash_attn", "xformers", "rotary_emb", "dropout_layer_norm", "lightning"):
        try:
            report[module] = getattr(__import__(module), "__version__", "present")
        except ImportError as error:
            report[module] = f"missing: {error}"
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
