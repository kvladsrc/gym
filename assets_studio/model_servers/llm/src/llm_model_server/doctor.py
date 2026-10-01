"""Print the llama.cpp build, the devices it sees and which models are downloaded."""

import json
import os
import subprocess

from huggingface_hub import get_token, try_to_load_from_cache

from llm_model_server import runtime
from llm_model_server.models import MODELS


def main() -> None:
    devices = None
    if runtime.installed():
        listing = subprocess.run(  # noqa: S603 -- our pinned binary
            [str(runtime.server_binary()), "--list-devices"],
            capture_output=True,
            text=True,
            env={**os.environ, "LD_LIBRARY_PATH": runtime.library_path()},
            check=False,
        )
        devices = [line.strip() for line in listing.stdout.splitlines() if ":" in line][1:]
    report = {
        "llama_cpp": runtime.BUILD,
        "installed": runtime.installed(),
        "devices": devices,
        "hf_token_found": get_token() is not None,
        "models_downloaded": {
            key: isinstance(
                try_to_load_from_cache(spec.repo, spec.file, revision=spec.revision), str
            )
            for key, spec in MODELS.items()
        },
    }
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
