import pytest
from helpers import ATTACK, ECONOMY, CharacterEncoder

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
