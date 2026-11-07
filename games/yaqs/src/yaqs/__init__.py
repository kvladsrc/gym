"""Local knowledge library with page citations, for people and coding agents.

The constants below shape how text is stored and chunked. They are recorded in each
book's import profile: after changing one, re-import books with --rebuild or rebuild the
whole database with yaqs rebuild. The embedding model is configured in yaqs.embedding.
"""

import os
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[2]  # the source checkout, when run from one
SCHEMA_VERSION = 2
DEFAULT_SEARCH_MODE = "semantic"
SEARCH_MODES = ("semantic", "lexical", "hybrid")
CHUNK_TOKENS = 512  # chunk size in embedding-model tokens
OVERLAP_TOKENS = 128  # tokens shared by neighboring chunks of one section
MIN_CHUNK_TOKENS = 32  # shorter pages and sections stay readable but get no chunk
SECTION_TOKENS = 64  # shorter TXT heading sections join a neighbor
TEXT_FORMAT = 2  # 2: cleaned at import (yaqs.cleaning), TXT sections by headings


def default_database() -> Path:
    """Keep checkout data beside the project; installed wheels use user data."""
    if location := os.environ.get("YAQS_DB"):
        return Path(location).expanduser()
    if (PROJECT / "pyproject.toml").is_file():
        return PROJECT / "data/knowledge.sqlite3"
    data = Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share")))
    return data / "yaqs/knowledge.sqlite3"


DEFAULT_DB = default_database()
