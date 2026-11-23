import hashlib
import json

import pytest
from helpers import ATTACK, ECONOMY, CharacterEncoder

from yaqs.chunking import Chunk
from yaqs.documents import Document, Page
from yaqs.ingest import import_book
from yaqs.store import KnowledgeBase


@pytest.fixture
def encoder():
    return CharacterEncoder()


@pytest.fixture
def library(tmp_path, encoder):
    source = tmp_path / "source.txt"
    source.write_text(f"{ATTACK}\f{ECONOMY}")
    path = tmp_path / "library.sqlite3"
    import_book(path, source, encoder, title="Game reference", author="Author")
    return KnowledgeBase(path)


@pytest.fixture
def mixed_library(library, encoder):
    text = "Enemy attack: make the warning readable before dealing damage."
    doc = Document(
        "/rules/atoms.jsonl",
        "rules-hash",
        "Combat rules",
        "Contributors",
        [Page(0, None, "combat", text)],
        json.dumps({"input": "atomkb-jsonl"}),
    )
    library.add_document(
        doc,
        [
            Chunk(
                0,
                0,
                "combat",
                text,
                0,
                len(text),
                len(text),
                hashlib.sha256(text.encode()).hexdigest(),
            )
        ],
    )
    rows = library.pending_chunks(2)
    library.add_vectors(rows, encoder.encode([text]))
    library.finish_index(2)
    return library
