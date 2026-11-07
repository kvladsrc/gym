import hashlib
import json

import pytest
from helpers import CharacterTokenizer

from yaqs.chunking import chunk_document
from yaqs.documents import Document, Page, read_document


def test_chunk_offsets_cover_the_page_and_stay_inside_the_source():
    text = ("A player sees an attack. They can dodge.\n\n" * 50).strip()
    doc = Document("/book.txt", "abc", "Test", "Author", [Page(1, None, "Combat", text)], "v1")
    result = list(chunk_document(doc, CharacterTokenizer(), size=128, overlap=16))
    covered = set()
    for chunk in result:
        assert chunk.text == text[chunk.start_char : chunk.end_char]
        assert chunk.token_count <= 128
        assert chunk.sha256 == hashlib.sha256(chunk.text.encode()).hexdigest()
        covered.update(range(chunk.start_char, chunk.end_char))
    assert covered == set(range(len(text)))


def test_chunks_preserve_page_boundaries_and_empty_pages():
    doc = Document(
        "/book.txt",
        "abc",
        "Test",
        "Author",
        [
            Page(1, None, "Intro", "first page"),
            Page(2, None, "", ""),
            Page(3, None, "Combat", "third page"),
        ],
        "v1",
    )
    result = list(chunk_document(doc, CharacterTokenizer(), min_tokens=1))
    assert [(c.page_number, c.text) for c in result] == [(1, "first page"), (3, "third page")]


def write_pages(path, rows):
    path.write_text("\n".join(json.dumps(row) for row in rows))


def test_ocr_text_preserves_markdown_code_and_normalizes_newlines(tmp_path):
    path = tmp_path / "book.txt"
    path.write_bytes(b"\xef\xbb\xbf# Heading\r\n\r\n```\r\n  x = 1\r\n```\r\n")
    doc = read_document(path, title="Game book", author="Author")
    assert doc.title == "Game book"
    assert doc.pages[0].number == 0
    assert doc.pages[0].text == "# Heading\n\n```\n  x = 1\n```"


def test_form_feeds_preserve_explicit_text_page_boundaries(tmp_path):
    path = tmp_path / "book.txt"
    path.write_text("First page\fSecond page\f")
    assert [(p.number, p.text) for p in read_document(path).pages] == [
        (1, "First page"),
        (2, "Second page"),
    ]


def test_page_jsonl_retains_source_numbers_and_ocr_provenance(tmp_path):
    path = tmp_path / "book.jsonl"
    write_pages(
        path,
        [
            {
                "page": 20,
                "title": "Game Feel",
                "author": "Steve Swink",
                "chapter": "Defining game feel",
                "text": "Real-time control",
                "ocr": {"job_id": "job-20", "model": {"revision": "pinned"}},
            }
        ],
    )
    doc = read_document(path)
    assert doc.title == "Game Feel"
    assert doc.pages[0].number == 20
    assert doc.pages[0].chapter == "Defining game feel"
    assert doc.pages[0].provenance["ocr"]["job_id"] == "job-20"


def test_incomplete_ocr_book_is_rejected_before_import(tmp_path):
    path = tmp_path / "book.jsonl"
    write_pages(path, [{"page": 1, "total_pages": 2, "text": "First page"}])
    with pytest.raises(ValueError, match="incomplete"):
        read_document(path)


def test_truncated_ocr_is_not_indexed_as_a_complete_page(tmp_path):
    path = tmp_path / "book.jsonl"
    write_pages(path, [{"page": 1, "text": "cut off", "ocr": {"meta": {"truncated": True}}}])
    with pytest.raises(ValueError, match="truncated"):
        read_document(path)


def test_duplicate_source_pages_are_rejected(tmp_path):
    path = tmp_path / "book.jsonl"
    write_pages(path, [{"page": 1, "text": "first"}, {"page": 1, "text": "duplicate"}])
    with pytest.raises(ValueError, match="Duplicate"):
        read_document(path)


def test_non_text_input_is_rejected(tmp_path):
    path = tmp_path / "unsupported.bin"
    path.write_bytes(b"unsupported")
    with pytest.raises(ValueError, match="only UTF-8 TXT"):
        read_document(path)


def test_native_pdf_text_retains_extraction_method_and_original_source(tmp_path):
    path = tmp_path / "book.jsonl"
    write_pages(
        path,
        [
            {
                "page": 1,
                "total_pages": 1,
                "text": "Native text",
                "extraction": "pdf-text",
                "source": "/books/source.pdf",
                "source_sha256": "original-hash",
            }
        ],
    )
    page = read_document(path).pages[0]
    assert page.extraction == "pdf-text"
    assert page.provenance["source"] == "/books/source.pdf"
    assert page.provenance["source_sha256"] == "original-hash"
