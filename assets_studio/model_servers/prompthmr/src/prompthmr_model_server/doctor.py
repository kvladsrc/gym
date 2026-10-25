"""Check local files without loading neural weights."""

import json

from prompthmr_model_server.server import CHECKPOINTS, source_directory, worker_python


def main() -> None:
    print(
        json.dumps(
            {
                "worker": worker_python().is_file(),
                "files": {name: (source_directory() / name).is_file() for name in CHECKPOINTS},
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
