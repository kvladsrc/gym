"""Print the runtime environment, the native modules and the downloaded weights."""

import importlib.util
import json

import torch

from unirig_model_server.server import CHECKPOINTS, source_directory


def main() -> None:
    source = source_directory()
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "source": (source / "run.py").is_file(),
        **{
            name: importlib.util.find_spec(name) is not None
            for name in ("flash_attn", "spconv", "torch_scatter", "torch_cluster", "bpy")
        },
        "weights": all((source / "experiments" / name).is_file() for name in CHECKPOINTS),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
