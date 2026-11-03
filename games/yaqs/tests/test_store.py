import dataclasses
import hashlib
import sqlite3

import numpy as np
import pytest

from yaqs.chunking import Chunk
from yaqs.documents import Document, Page
from yaqs.embedding import DIMENSIONS
from yaqs.store import KnowledgeBase, fts_expression, fuse_ranks


def document(name="combat", words=("Enemy attack anticipation", "Currency sources and sinks")):
    pages = [Page(i, None, f"Chapter {i}", text) for i, text in enumerate(words, 1)]
    return Document(
        f"/{name}.txt",
        hashlib.sha256(name.encode()).hexdigest(),
        name,
        "Author",
        pages,
        "test-profile-v1",
    )


def chunks(doc):
    return [
        Chunk(
            p.number,
            0,
            p.chapter,
            p.text,
            0,
            len(p.text),
            10,
            hashlib.sha256(p.text.encode()).hexdigest(),
        )
        for p in doc.pages
    ]


def vectors(indices):
    result = np.zeros((len(indices), DIMENSIONS), dtype=np.float32)
    for row, index in enumerate(indices):
        result[row, index] = 1
    return result


@pytest.fixture
def store(tmp_path):
    return KnowledgeBase(tmp_path / "knowledge.sqlite3", create=True)


def populated(store):
    doc = document()
    book_id = store.add_document(doc, chunks(doc))
    rows = store.pending_chunks(book_id)
    store.add_vectors(rows, vectors([0, 1]))
    store.finish_index(book_id)
    return book_id, rows


def test_hybrid_returns_cited_source_and_combines_two_rankings(store):
    book, rows = populated(store)
    hits = store.search("attack", vectors([0])[0].tolist(), mode="hybrid")
    assert hits[0]["id"] == rows[0]["id"]
    assert hits[0]["lexical_rank"] == hits[0]["semantic_rank"] == 1
    assert hits[0]["page_number"] == 1
    assert hits[0]["book_id"] == book
    assert "page 1" in hits[0]["citation"]
    assert hits[0]["source_path"] == "/combat.txt"


def test_book_filter_applies_to_knn_before_limiting(store):
    first, _ = populated(store)
    doc = document("other", ("Different attack advice",))
    second = store.add_document(doc, chunks(doc))
    rows = store.pending_chunks(second)
    store.add_vectors(rows, vectors([2]))
    hits = store.search("attack", vectors([0])[0].tolist(), book_id=second, limit=1)
    assert hits and hits[0]["book_id"] == second != first


def test_lexical_search_does_not_need_a_model_or_vector(store):
    populated(store)
    assert store.search("sources", mode="lexical")[0]["page_number"] == 2
    assert store.search('" OR ( ) :', mode="lexical") == []
    assert store.search("!!!", mode="lexical") == []
    assert fts_expression('GOAP AND "utility"') == '"GOAP" OR "AND" OR "utility"'


def test_interrupted_index_resumes_only_missing_chunks(store):
    doc = document()
    book = store.add_document(doc, chunks(doc))
    pending = store.pending_chunks(book)
    store.add_vectors(pending[:1], vectors([0]))
    with pytest.raises(ValueError, match="without embeddings"):
        store.finish_index(book)
    assert [r["id"] for r in store.pending_chunks(book)] == [pending[1]["id"]]
    assert store.books()[0]["status"] == "indexing"
    store.add_vectors(pending[1:], vectors([1]))
    store.finish_index(book)
    assert store.integrity()["pending_chunks"] == 0


def test_duplicate_import_is_a_noop_and_does_not_break_citations(store):
    book, before = populated(store)
    assert store.add_document(document(), chunks(document())) == book
    assert store.stats()["counts"]["chunks"] == 2
    assert store.pending_chunks(book) == []
    assert store.passage(before[0]["id"])["text"] == "Enemy attack anticipation"


def test_rebuild_removes_stale_fts_and_vectors_transactionally(store):
    book, _ = populated(store)
    doc = document(words=("New controls and camera advice",))
    store.add_document(doc, chunks(doc), rebuild=True)
    assert store.stats()["counts"]["chunk_vectors"] == 0
    assert store.search("Currency", mode="lexical") == []
    assert store.search("camera", mode="lexical")[0]["book_id"] == book
    assert store.integrity()["orphan_vectors"] == 0


def test_failed_vector_batch_rolls_back_every_vector(store):
    doc = document()
    book = store.add_document(doc, chunks(doc))
    row = store.pending_chunks(book)[0]
    with pytest.raises(sqlite3.Error):
        store.add_vectors([row, row], vectors([0, 1]))
    assert store.stats()["counts"]["chunk_vectors"] == 0


def test_invalid_vectors_do_not_mutate_the_index(store):
    doc = document()
    book = store.add_document(doc, chunks(doc))
    rows = store.pending_chunks(book)
    with pytest.raises(ValueError, match="normalized"):
        store.add_vectors(rows, np.zeros((2, DIMENSIONS)))
    assert store.stats()["pending_chunks"] == 2


def test_model_profile_changes_are_rejected(store):
    with store.connect() as db, db:
        db.execute("UPDATE config SET value='{}' WHERE key='embedding_profile'")
    with pytest.raises(ValueError, match="profile differs"):
        KnowledgeBase(store.path)


def test_changed_import_profile_requires_explicit_rebuild(store):
    populated(store)
    changed = dataclasses.replace(document(), import_profile="test-profile-v2")
    with pytest.raises(ValueError, match="settings changed"):
        store.add_document(changed, chunks(changed))


def test_neighbors_do_not_cross_book_boundaries(store):
    book, rows = populated(store)
    doc = document("other")
    store.add_document(doc, chunks(doc))
    assert [hit["book_id"] for hit in store.neighbors(rows[0]["id"], 3)] == [book, book]


def test_rank_fusion_promotes_agreement_and_deduplicates():
    assert fuse_ranks([1, 2, 2], [2, 3])[0][0] == 2
    assert len(fuse_ranks([1, 2, 2], [2, 3])) == 3


def test_semantic_default_reports_cosine_without_keyword_fusion(store):
    _, rows = populated(store)
    # The keyword points at page 2, while the vector points at page 1.
    hits = store.search("Currency", vectors([0])[0].tolist())
    assert hits[0]["id"] == rows[0]["id"]
    assert hits[0]["score"] == pytest.approx(1)
    assert hits[0]["lexical_rank"] is None


def test_removal_cleans_both_indexes_and_never_reuses_citation_ids(store):
    book, old = populated(store)
    removed = store.remove_book(book)
    assert removed["deleted"] == {"pages": 2, "chunks": 2, "chunk_vectors": 2}
    assert store.search("attack", mode="lexical") == []
    assert store.stats()["counts"] == {"books": 0, "pages": 0, "chunks": 0, "chunk_vectors": 0}
    replacement = document("replacement")
    new_book = store.add_document(replacement, chunks(replacement))
    assert new_book > book
    assert min(r["id"] for r in store.pending_chunks(new_book)) > max(r["id"] for r in old)
    with pytest.raises(KeyError, match="Unknown chunk"):
        store.passage(old[0]["id"])
    assert store.integrity()["orphan_vectors"] == 0


def test_rebuild_never_retargets_old_chunk_citations(store):
    book, old = populated(store)
    doc = document(words=("New camera text",))
    store.add_document(doc, chunks(doc), rebuild=True)
    assert store.pending_chunks(book)[0]["id"] > max(r["id"] for r in old)
    with pytest.raises(KeyError, match="Unknown chunk"):
        store.passage(old[0]["id"])


def test_missing_book_removal_leaves_library_unchanged(store):
    populated(store)
    before = store.stats()["counts"]
    with pytest.raises(KeyError, match="Unknown book"):
        store.remove_book(999)
    assert store.stats()["counts"] == before
