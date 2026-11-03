import numpy as np
import pytest

from yaqs.embedding import DIMENSIONS
from yaqs.ingest import import_book
from yaqs.store import KnowledgeBase


class CharacterEncoder:
    def tokenizer(self, text, **kwargs):
        return {
            "input_ids": list(range(len(text))),
            "offset_mapping": [(i, i + 1) for i in range(len(text))],
        }

    def encode(self, texts, **kwargs):
        vectors = np.zeros((len(texts), DIMENSIONS), dtype=np.float32)
        vectors[:, 0] = 1
        return vectors


@pytest.fixture
def encoder():
    return CharacterEncoder()


@pytest.fixture
def library(tmp_path, encoder):
    source = tmp_path / "source.txt"
    source.write_text("Enemy attack anticipation.\fCurrency sources and sinks.")
    path = tmp_path / "library.sqlite3"
    import_book(path, source, encoder, title="Game reference", author="Author")
    return KnowledgeBase(path)
