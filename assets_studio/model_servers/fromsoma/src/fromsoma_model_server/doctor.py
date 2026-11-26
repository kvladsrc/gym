"""Print the runtime environment, the files in the root directory and the readers' training
(fromsoma's and frommhr's), read from the packages' metadata alone."""

import json

import torch

from fromsoma_model_server import frommhr, server
from fromsoma_model_server.package import metadata


def main() -> None:
    root = server.root_directory()
    report = {"torch": torch.__version__, "threads": torch.get_num_threads(), "root": str(root)}
    for name, module in (("fromsoma", server), ("frommhr", frommhr)):
        entry: dict = {"missing": module.missing_files(root)}
        if (root / module.MODEL_FILE).is_file():
            package = metadata(root / module.MODEL_FILE)
            entry["reader"] = server.describe(package)
            entry["validation"] = package.get("training", {}).get("validation")
        report[name] = entry
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
