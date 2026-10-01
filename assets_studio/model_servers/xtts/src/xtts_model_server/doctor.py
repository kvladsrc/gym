"""Print the runtime environment, whether the weights are downloaded, and
whether the built-in voices match the ones the server declares."""

import json
import typing

import torch
from huggingface_hub import try_to_load_from_cache

from xtts_model_server.server import REPO, REVISION, Speaker


def main() -> None:
    speakers_file = try_to_load_from_cache(REPO, "speakers_xtts.pth", revision=REVISION)
    report: dict[str, object] = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "weights_cached": isinstance(
            try_to_load_from_cache(REPO, "model.pth", revision=REVISION), str
        ),
    }
    if isinstance(speakers_file, str):
        shipped = set(torch.load(speakers_file, weights_only=False))
        declared = set(typing.get_args(Speaker))
        report["voices_match"] = shipped == declared
        report["voices_missing_in_declaration"] = sorted(shipped - declared)
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
