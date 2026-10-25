"""Print the runtime environment, the source and the downloaded weights."""

import json

import torch

from hi3d_model_server.server import model_directory, source_directory


def main() -> None:
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "capability": torch.cuda.get_device_capability(0) if torch.cuda.is_available() else None,
        "source": (source_directory() / "sparc3d/pipelines").is_dir(),
        "weights": (model_directory() / "transformer_1024/model.safetensors").is_file(),
    }
    for module in ("flash_attn", "spconv", "triton"):
        try:
            report[module] = __import__(module).__version__
        except ImportError as error:
            report[module] = f"missing: {error}"
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
