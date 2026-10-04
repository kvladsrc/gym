"""Print the runtime environment, the rasterizer and the downloaded weights."""

import importlib.util
import json

import torch

from hunyuan_paint_model_server.server import (
    DELIGHT,
    PAINT,
    WEIGHTS_REPO,
    models_directory,
    source_directory,
)


def main() -> None:
    weights = models_directory() / WEIGHTS_REPO
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "source": (source_directory() / "hy3dgen/texgen").is_dir(),
        "custom_rasterizer": importlib.util.find_spec("custom_rasterizer") is not None,
        "paint_weights": (weights / PAINT / "unet/diffusion_pytorch_model.bin").is_file(),
        "delight_weights": (weights / DELIGHT / "model_index.json").is_file(),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
