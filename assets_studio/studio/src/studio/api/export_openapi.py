"""Write the API's OpenAPI document: ``python -m studio.api.export_openapi PATH``.

The web UI generates its TypeScript types from it, so the UI and the API
cannot drift apart unnoticed.
"""

import json
import sys
import tempfile
from pathlib import Path

from studio.api.app import create_app
from studio.config import StudioConfig
from studio.core import Studio


def main() -> None:
    target = Path(sys.argv[1])
    with tempfile.TemporaryDirectory() as data_dir:
        studio = Studio(StudioConfig(data_dir=Path(data_dir)))
        try:
            document = create_app(studio).openapi()
        finally:
            studio.close()
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n")


if __name__ == "__main__":
    main()
