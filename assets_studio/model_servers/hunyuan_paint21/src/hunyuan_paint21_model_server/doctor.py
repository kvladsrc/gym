"""Print the runtime environment, the native modules and the downloaded weights."""

import importlib.util
import json

import torch

from hunyuan_paint21_model_server.server import (
    dino_directory,
    paint_directory,
    realesrgan_path,
    source_directory,
)


def main() -> None:
    renderer = source_directory() / "hy3dpaint/DifferentiableRenderer"
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "source": (source_directory() / "hy3dpaint/textureGenPipeline.py").is_file(),
        "custom_rasterizer": importlib.util.find_spec("custom_rasterizer") is not None,
        "mesh_inpaint_processor": any(renderer.glob("mesh_inpaint_processor*.so")),
        "paint_weights": (paint_directory() / "unet").is_dir(),
        "dino_weights": (dino_directory() / "model.safetensors").is_file(),
        "realesrgan": realesrgan_path().is_file(),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
