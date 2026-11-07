"""Restartable book import: read, clean, chunk, store, then embed in resumable batches.

Progress goes to stderr as one JSON object per line; the result is returned to the caller.
"""

import json
import sys
import time
from pathlib import Path
from typing import Any

from yaqs.chunking import chunk_document
from yaqs.cleaning import clean_document
from yaqs.documents import Document, read_document
from yaqs.embedding import QwenEncoder, document_text
from yaqs.locking import import_lock
from yaqs.store import KnowledgeBase


def import_book(
    database: Path,
    source: Path,
    encoder: QwenEncoder,
    *,
    batch_size: int = 16,
    rebuild: bool = False,
    title: str = "",
    author: str = "",
    wait_for_lock: bool = False,
) -> dict[str, Any]:
    """Import a TXT or JSONL file; importing the same bytes again resumes missing vectors.

    title and author override the file's own metadata. rebuild replaces a book that is
    already in the database, keeping its id but giving it new chunk ids.
    """
    document = read_document(source, title=title, author=author)
    return import_document(
        database,
        document,
        encoder,
        batch_size=batch_size,
        rebuild=rebuild,
        title=title,
        author=author,
        wait_for_lock=wait_for_lock,
    )


def import_document(
    database: Path,
    document: Document,
    encoder: QwenEncoder,
    *,
    batch_size: int = 16,
    rebuild: bool = False,
    title: str = "",
    author: str = "",
    wait_for_lock: bool = False,
) -> dict[str, Any]:
    """Import an already read document; see import_book."""
    if not 1 <= batch_size <= 32:
        raise ValueError("batch_size must be between 1 and 32")
    started = time.perf_counter()
    with import_lock(database, wait=wait_for_lock):
        store = KnowledgeBase(database, create=True)
        existing = store.book_by_hash(document.sha256)
        if existing is None or rebuild:
            document = clean_document(document)
            chunks = list(chunk_document(document, encoder.tokenizer))
            if not chunks:
                raise ValueError("The document contains no searchable text")
            book_id = store.add_document(document, chunks, rebuild=rebuild)
            print(
                json.dumps({"extracted_pages": len(document.pages), "chunks": len(chunks)}),
                file=sys.stderr,
                flush=True,
            )
        else:
            if existing["import_profile"] != document.import_profile:
                raise ValueError("Import settings changed; use --rebuild for this book")
            if (title and existing["title"] != title) or (author and existing["author"] != author):
                raise ValueError(
                    "Book metadata changed; use --rebuild to re-embed with the new title"
                )
            book_id = existing["id"]
        pending = store.pending_chunks(book_id)
        initial_pending = len(pending)
        for start in range(0, len(pending), batch_size):
            rows = pending[start : start + batch_size]
            texts = [document_text(row["title"], row["chapter"], row["text"]) for row in rows]
            store.add_vectors(rows, encoder.encode(texts))
            progress = {"embedded": start + len(rows), "pending_at_start": initial_pending}
            print(json.dumps(progress), file=sys.stderr, flush=True)
        store.finish_index(book_id)
        book = next(book for book in store.books() if book["id"] == book_id)
        return {
            "book": book,
            "embedded_now": initial_pending,
            "elapsed_s": round(time.perf_counter() - started, 3),
            "database": str(store.path),
        }
