import asyncio
import sys

import pytest
from helpers import CharacterTokenizer

from yaqs import cli
from yaqs.chunking import Chunk
from yaqs.documents import Document, Page
from yaqs.mcp import build_server
from yaqs.present import tidy
from yaqs.service import KnowledgeService
from yaqs.store import KnowledgeBase

# pdftotext -layout text: a BOM, typographic spaces, a margin and a no-break space.
LAYOUT = (
    "\ufeff282\u2002\u2009◾\u2009\u2002Chapter 10   \n\n\n\n"
    "        A classic   example\u00a0of attack.\n"
)
CODE = "def wander():\n\n\n\n    return  steering   \n"


def service_for(tmp_path, pages):
    doc = Document("/source/book.jsonl", "book-hash", "Book", "Author", pages, "test")
    chunks = [
        Chunk(page.number, 0, page.chapter, page.text, 0, len(page.text), 1, "hash")
        for page in pages
    ]
    store = KnowledgeBase(tmp_path / "knowledge.sqlite3", create=True)
    store.add_document(doc, chunks)
    service = KnowledgeService(store.path)
    service.encoder._tokenizer = CharacterTokenizer()
    return service


def test_tidy_drops_layout_indentation_but_keeps_code_indentation():
    assert tidy(LAYOUT, layout=True) == "282 ◾ Chapter 10\n\nA classic example of attack."
    assert tidy(CODE, layout=False) == "def wander():\n\n    return steering"


def test_compact_search_keeps_citation_fields_and_drops_storage_details(tmp_path):
    service = service_for(
        tmp_path, [Page(7, "181", "Balance", LAYOUT, "pdf-text", {"tool": "pdftotext"})]
    )
    full = service.search("attack", mode="lexical", detail="full")
    compact = service.search("attack", mode="lexical", detail="compact")
    assert {"sha256", "source_path", "source_chunks", "book_sha256"} <= set(full["hits"][0])
    hit = compact["hits"][0]
    assert hit == {
        "id": full["hits"][0]["id"],
        "chunk_ids": full["hits"][0]["chunk_ids"],
        "book_id": 1,
        "title": "Book",
        "author": "Author",
        "page_number": 7,
        "page_label": "181",
        "chapter": "Balance",
        "index_status": "extracted",
        "score": round(full["hits"][0]["score"], 3),
        "text": "282 ◾ Chapter 10\n\nA classic example of attack.",
    }
    assert compact["context"] == {"used_tokens": len(LAYOUT), "omitted_blocks": 0}
    assert set(compact) == {"mode", "hits", "context"}


def test_unpaginated_compact_hits_keep_offsets_for_citation(tmp_path):
    service = service_for(tmp_path, [Page(0, None, "", CODE, "text")])
    hit = service.search("steering", mode="lexical", detail="compact")["hits"][0]
    assert (hit["page_number"], hit["start_char"], hit["end_char"]) == (0, 0, len(CODE))
    assert hit["text"] == "def wander():\n\n    return steering"
    passage = service.passage(hit["id"], 1, detail="compact")["hits"][0]
    assert "score" not in passage and passage["start_char"] == 0


def test_compact_page_windows_still_chain_over_the_stored_text(tmp_path):
    service = service_for(tmp_path, [Page(3, None, "", LAYOUT * 10, "pdf-text")])
    texts, start = [], 0
    while start is not None:
        window = service.page(1, 3, start, max_tokens=256, detail="compact")
        assert "provenance" not in window and "sha256" not in window
        texts.append(window["text"])
        start = window["next_start_char"]
    assert len(texts) == -(-len(LAYOUT * 10) // 256) and not window["complete"]
    assert service.page(1, 3, detail="full")["text"] == LAYOUT * 10


def test_compact_books_hide_files_and_hashes(tmp_path):
    service = service_for(tmp_path, [Page(1, None, "", "attack", "pdf-text")])
    (book,) = service.books("compact")
    assert book == {
        "id": 1,
        "title": "Book",
        "author": "Author",
        "page_count": 1,
        "chunks": 1,
        "status": "extracted",
        "indexed_chunks": 0,
    }
    assert "source_path" in service.books()[0]


def test_unknown_detail_is_rejected(tmp_path):
    service = service_for(tmp_path, [Page(1, None, "", "attack", "pdf-text")])
    with pytest.raises(ValueError, match="detail"):
        service.search("attack", mode="lexical", detail="brief")


def test_mcp_tools_answer_compactly_unless_asked_for_full(tmp_path):
    server = build_server(service_for(tmp_path, [Page(1, None, "", LAYOUT, "pdf-text")]))
    query = {"query": "attack", "mode": "lexical"}
    compact = asyncio.run(server.call_tool("search_knowledge", query)).structured_content
    full = asyncio.run(
        server.call_tool("search_knowledge", {**query, "detail": "full"})
    ).structured_content
    assert "sha256" not in compact["hits"][0] and "sha256" in full["hits"][0]
    assert full["hits"][0]["text"] == LAYOUT


@pytest.mark.parametrize(
    "command", [["search", "q"], ["books"], ["passage", "1"], ["page", "1", "2"]]
)
def test_cli_reads_compact_by_default(monkeypatch, capsys, command):
    monkeypatch.setattr(sys, "argv", ["yaqs", *command])
    monkeypatch.setattr(cli, "execute", lambda args: {"detail": args.detail})
    cli.main()
    assert '"compact"' in capsys.readouterr().out
