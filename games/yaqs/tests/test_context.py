import dataclasses
import hashlib
import random

import pytest

from yaqs.chunking import Chunk
from yaqs.context import assemble_context
from yaqs.documents import Document, Page
from yaqs.service import KnowledgeService
from yaqs.store import KnowledgeBase


class CharacterTokenizer:
    def __call__(self, text, **kwargs):
        return {"input_ids": list(range(len(text)))}


TOKENIZER = CharacterTokenizer()
TEXT = "\n".join(f"Строка {i}: λ = {i}; 控制 🎮" for i in range(150))


def hit(identifier, start, end, *, book=1, page=1, text=TEXT):
    return {
        "id": identifier,
        "book_id": book,
        "page_number": page,
        "title": f"Book {book}",
        "start_char": start,
        "end_char": end,
        "text": text[start:end],
        "token_count": end - start,
        "score": 1 / (60 + identifier),
    }


def assemble(hits, **kwargs):
    return assemble_context(
        hits, lambda book, page: TEXT, TOKENIZER, limit=kwargs.pop("limit", 5), **kwargs
    )


@pytest.mark.parametrize(
    "ranges,expected",
    [
        ([(0, 512), (384, 896)], [(0, 896)]),
        ([(384, 896), (0, 512)], [(0, 896)]),
        ([(0, 512), (100, 200)], [(0, 512)]),
        ([(0, 300), (300, 600)], [(0, 600)]),
        ([(0, 200), (400, 600), (150, 450)], [(0, 600)]),
        ([(0, 200), (400, 600)], [(0, 200), (400, 600)]),
    ],
)
def test_exact_ranges_do_not_repeat_or_invent_source_text(ranges, expected):
    hits = [hit(i, start, end) for i, (start, end) in enumerate(ranges, 1)]
    result = assemble(hits)
    assert [(b["start_char"], b["end_char"]) for b in result["hits"]] == expected
    for block in result["hits"]:
        assert block["text"] == TEXT[block["start_char"] : block["end_char"]]
        assert block["sha256"] == hashlib.sha256(block["text"].encode()).hexdigest()
        assert block["token_count"] == len(block["text"])
    assert sum(len(b["text"]) for b in result["hits"]) == len(
        set(offset for start, end in ranges for offset in range(start, end))
    )


def test_overlapping_coordinates_never_merge_across_books_or_pages():
    result = assemble([hit(1, 0, 200), hit(2, 0, 200, book=2), hit(3, 0, 200, page=2)])
    assert len(result["hits"]) == 3


def test_rank_and_original_references_survive_merging_and_duplicate_ids():
    best = hit(4, 384, 896)
    result = assemble([best, hit(1, 0, 512), best])
    block = result["hits"][0]
    assert block["id"] == block["anchor_chunk_id"] == best["id"]
    assert block["score"] == best["score"]
    assert block["chunk_ids"] == [1, 4]
    assert [(r["id"], r["candidate_rank"]) for r in block["source_chunks"]] == [(1, 2), (4, 1)]
    assert result["context"]["selected_chunk_count"] == 2


def test_limit_is_applied_after_merging_so_other_results_fill_the_context():
    result = assemble([hit(1, 0, 400), hit(2, 300, 700), hit(3, 1100, 1400)], limit=2)
    assert [b["chunk_ids"] for b in result["hits"]] == [[1, 2], [3]]


def test_budget_skips_whole_blocks_and_can_select_a_smaller_later_result():
    result = assemble([hit(1, 0, 300), hit(2, 1000, 1500), hit(3, 2000, 2150)], max_tokens=500)
    assert [b["chunk_ids"] for b in result["hits"]] == [[1], [3]]
    assert result["context"]["used_tokens"] == 450
    assert result["context"]["omitted_blocks"] == 1
    assert result["hits"][0]["text"] == TEXT[:300]


def test_long_overlap_chains_cannot_consume_the_entire_context():
    result = assemble([hit(1, 0, 512), hit(2, 384, 896), hit(3, 768, 1280), hit(4, 2000, 2300)])
    assert [b["chunk_ids"] for b in result["hits"]] == [[1, 2], [4]]
    assert result["context"]["skipped_overlong_candidates"] == 1
    assert all(b["token_count"] <= 1024 for b in result["hits"])


def test_unpaginated_citation_uses_the_union_offsets():
    result = assemble([hit(1, 100, 500, page=0), hit(2, 400, 800, page=0)])
    assert result["hits"][0]["citation"] == "Book 1, text offsets 100–800"


def test_bad_offsets_fail_instead_of_returning_misquoted_source():
    candidate = hit(1, 0, 400) | {"text": "different text"}
    with pytest.raises(RuntimeError, match="source-page offsets"):
        assemble([candidate])


def test_random_ranges_preserve_exact_union_and_source_references():
    rng = random.Random(42)
    for _ in range(20):
        ranges = [
            (start := rng.randrange(0, 2500), start + rng.randrange(30, 300)) for _ in range(20)
        ]
        result = assemble(
            [hit(i, start, end) for i, (start, end) in enumerate(ranges, 1)],
            limit=30,
            max_tokens=16384,
            max_block_tokens=16384,
        )
        wanted = set(offset for start, end in ranges for offset in range(start, end))
        returned = [
            offset
            for block in result["hits"]
            for offset in range(block["start_char"], block["end_char"])
        ]
        assert len(returned) == len(set(returned))
        assert set(returned) == wanted
        identifiers = sorted(i for block in result["hits"] for i in block["chunk_ids"])
        assert identifiers == list(range(1, 21))


def corpus(tmp_path):
    text = "attack " * 130
    doc = Document(
        "/combat.txt",
        "source-hash",
        "Combat",
        "Author",
        [Page(0, None, "", text), Page(2, None, "", "attack elsewhere")],
        "test",
    )
    chunks = [
        Chunk(0, i, "", text[a:b], a, b, b - a, hashlib.sha256(text[a:b].encode()).hexdigest())
        for i, (a, b) in enumerate([(0, 400), (300, 700), (600, 900)])
    ]
    chunks.append(Chunk(2, 0, "", "attack elsewhere", 0, 16, 16, "hash"))
    store = KnowledgeBase(tmp_path / "knowledge.sqlite3", create=True)
    book = store.add_document(doc, chunks)
    return store, doc, book


def test_service_search_uses_merged_blocks_and_keeps_database_chunks(tmp_path):
    store, _, _ = corpus(tmp_path)
    service = KnowledgeService(store.path)
    service.encoder._tokenizer = TOKENIZER
    service.encoder.encode = lambda *a, **k: pytest.fail("Lexical search must not run embeddings")
    result = service.search("attack", limit=2, mode="lexical")
    assert len(result["hits"]) == 2
    merged = next(h for h in result["hits"] if h["page_number"] == 0)
    assert len(merged["chunk_ids"]) == 3
    assert merged["text"] == ("attack " * 130)[:900]
    assert result["context"]["used_tokens"] == 916
    assert store.stats()["counts"]["chunks"] == 4


def test_neighbors_merge_source_text_and_prioritize_the_requested_center(tmp_path):
    store, _, book = corpus(tmp_path)
    ids = [r["id"] for r in store.pending_chunks(book)]
    result = store.context_neighbors(ids[1], 1, TOKENIZER)
    assert result["hits"][0]["chunk_ids"] == ids[:3]
    assert result["hits"][0]["anchor_chunk_id"] == ids[1]
    limited = store.context_neighbors(ids[1], 1, TOKENIZER, max_tokens=500)
    assert limited["hits"][0]["chunk_ids"] == [ids[1]]
    assert limited["context"]["used_tokens"] == 400


def test_search_and_source_ranges_share_a_snapshot_during_rebuild(tmp_path, monkeypatch):
    store, doc, _ = corpus(tmp_path)
    original = store._search_candidates

    def rebuild_after_ranking(*args):
        hits = original(*args)
        changed = dataclasses.replace(doc, pages=[Page(0, None, "", "replacement")])
        other = KnowledgeBase(store.path)
        other.add_document(
            changed, [Chunk(0, 0, "", "replacement", 0, 11, 11, "hash")], rebuild=True
        )
        return hits

    monkeypatch.setattr(store, "_search_candidates", rebuild_after_ranking)
    result = store.context_search("attack", None, TOKENIZER, mode="lexical")
    assert any("attack" in hit["text"] for hit in result["hits"])
    assert store.page(1, 0)["text"] == "replacement"


@pytest.mark.parametrize("budget", [0, 255, 16385])
def test_bad_budgets_fail_before_searching(tmp_path, budget):
    store, _, _ = corpus(tmp_path)
    service = KnowledgeService(store.path)
    with pytest.raises(ValueError, match="max_tokens"):
        service.search("attack", mode="lexical", max_tokens=budget)
