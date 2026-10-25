"""Print the runtime environment, Kimodo, bpy and the downloaded weights."""

import importlib.util
import json

import torch
from huggingface_hub import try_to_load_from_cache

from kimodo_model_server.server import LLAMA_REPO, MODEL_NAME, data_directory


def main() -> None:
    data = data_directory()
    llama = try_to_load_from_cache(LLAMA_REPO, "config.json")
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "kimodo": importlib.util.find_spec("kimodo") is not None,
        "motion_correction": importlib.util.find_spec("motion_correction") is not None,
        "bpy": importlib.util.find_spec("bpy") is not None,
        "weights": (data / "checkpoints" / MODEL_NAME / "model.safetensors").is_file(),
        "encoder_adapters": (data / "encoder-peft" / "adapter_model.safetensors").is_file(),
        "llama": isinstance(llama, str),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
