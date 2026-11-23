"""Print the runtime environment, the sources and the downloaded weights."""

import json

import torch

from lato2_model_server.server import (
    CHECKPOINTS,
    dinov2_directory,
    source_directory,
    weights_directory,
)


def main() -> None:
    cuda = torch.cuda.is_available()
    report = {
        "torch": torch.__version__,
        "cuda": cuda,
        "gpu": torch.cuda.get_device_name(0) if cuda else None,
        "capability": torch.cuda.get_device_capability(0) if cuda else None,
        "source": (source_directory() / "scripts/e2e_inference.py").is_file(),
        "dinov2 source": (dinov2_directory() / "facebookresearch_dinov2_main/hubconf.py").is_file(),
        "weights": all((weights_directory() / name).is_file() for name in CHECKPOINTS),
    }
    for module in ("flash_attn", "spconv", "torch_scatter", "open3d", "o_voxel"):
        try:
            report[module] = getattr(__import__(module), "__version__", "present")
        except ImportError as error:
            report[module] = f"missing: {error}"
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
