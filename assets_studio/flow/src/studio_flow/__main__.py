import argparse
import os
from pathlib import Path

import uvicorn

from studio_flow.app import create_app


def main() -> None:
    parser = argparse.ArgumentParser(description="Saved job graphs for the asset studio")
    parser.add_argument("--studio", default="http://127.0.0.1:9000")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=9001)
    data = Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share")))
    parser.add_argument("--database", type=Path, default=data / "assets-studio/flow.sqlite3")
    args = parser.parse_args()
    uvicorn.run(
        create_app(database=args.database, studio_url=args.studio), host=args.host, port=args.port
    )


if __name__ == "__main__":
    main()
