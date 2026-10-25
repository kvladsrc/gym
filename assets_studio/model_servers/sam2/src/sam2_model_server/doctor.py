"""Check the environment and local checkpoint without loading the model."""

import shutil

import torch
from huggingface_hub import try_to_load_from_cache

from sam2_model_server.server import REPO, REVISION


def main() -> None:
    print("PyTorch:", torch.__version__)
    available = torch.cuda.is_available()
    print("CUDA:", torch.cuda.get_device_name(0) if available else "unavailable")
    print("ffmpeg:", shutil.which("ffmpeg") or "missing")
    weights = try_to_load_from_cache(REPO, "model.safetensors", revision=REVISION)
    print("Weights:", weights if isinstance(weights, str) else "not downloaded")


if __name__ == "__main__":
    main()
