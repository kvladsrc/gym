"""Local game development knowledge with source-page citations."""

import os
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[2]
SCHEMA_VERSION = 2
DEFAULT_SEARCH_MODE = "semantic"
SEARCH_MODES = ("semantic", "lexical", "hybrid")
CHUNK_TOKENS = 512
OVERLAP_TOKENS = 128


def default_database() -> Path:
    """Keep checkout data beside the project; installed wheels use user data."""
    if location := os.environ.get("YAQS_DB"):
        return Path(location).expanduser()
    if (PROJECT / "pyproject.toml").is_file():
        return PROJECT / "data/knowledge.sqlite3"
    data = Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share")))
    return data / "yaqs/knowledge.sqlite3"


DEFAULT_DB = default_database()
