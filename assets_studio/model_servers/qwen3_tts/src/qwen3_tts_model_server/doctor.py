"""Report PyTorch, the CUDA device and which models are downloaded.

python -m qwen3_tts_model_server.doctor [--download design|voice|clone|all]
"""

import argparse
import json

from qwen3_tts_model_server.server import VARIANTS, cached, weights


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--download", choices=[*sorted(VARIANTS), "all"])
    arguments = parser.parse_args()
    if arguments.download:
        names = sorted(VARIANTS) if arguments.download == "all" else [arguments.download]
        for name in names:
            print(name, weights(VARIANTS[name]), flush=True)
        return

    import torch

    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "device": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "downloaded": {name: cached(variant) for name, variant in VARIANTS.items()},
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
