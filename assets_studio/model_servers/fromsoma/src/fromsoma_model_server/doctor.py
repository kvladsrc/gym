"""Print the runtime environment, the files in the root directory and the readers' training
(fromsoma's, frommhr's and fromanny's)."""

import json

import torch

from fromsoma_model_server import fromanny, frommhr
from fromsoma_model_server.server import MODEL_FILE, describe, missing_files, root_directory


def main() -> None:
    root = root_directory()
    report = {
        "torch": torch.__version__,
        "threads": torch.get_num_threads(),
        "root": str(root),
        "missing": missing_files(root),
    }
    if (root / MODEL_FILE).is_file():
        package = torch.load(root / MODEL_FILE, map_location="cpu", weights_only=False)
        report["reader"] = describe(package)
        report["validation"] = package.get("training", {}).get("validation")
    for server in (frommhr, fromanny):
        name = server.MODEL_FILE.removesuffix(".pt")
        report[name] = {"missing": server.missing_files(root)}
        if (root / server.MODEL_FILE).is_file():
            package = torch.load(root / server.MODEL_FILE, map_location="cpu", weights_only=False)
            report[name]["reader"] = describe(package)
            report[name]["validation"] = package.get("training", {}).get("validation")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
