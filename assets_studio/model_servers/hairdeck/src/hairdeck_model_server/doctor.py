"""Print the runtime environment, the source and the downloaded weights."""

import json

import torch

from hairdeck_model_server.server import missing_files, root_directory, source_directory


def main() -> None:
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "gpu_memory_gib": (
            round(torch.cuda.get_device_properties(0).total_memory / 2**30, 1)
            if torch.cuda.is_available()
            else None
        ),
        "source": (source_directory() / "hairdeck/infer/image.py").is_file(),
        "weights_missing": missing_files(root_directory()),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
