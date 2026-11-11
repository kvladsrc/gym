"""Read-only operations shared by the CLI, the MCP server and the browser UI."""

from contextlib import AbstractContextManager, nullcontext
from pathlib import Path
from typing import Any

from yaqs import DEFAULT_SEARCH_MODE
from yaqs.context import DEFAULT_CONTEXT_TOKENS, DEFAULT_PAGE_TOKENS, page_window, validate_budget
from yaqs.embedding import QwenEncoder
from yaqs.present import check_detail, compact_books, compact_page, compact_passage, compact_search
from yaqs.querylog import PYTHON, Caller, Entry, QueryLog, blocks, window
from yaqs.store import KnowledgeBase, validate_neighbors, validate_search

SCORE_NOTES = {
    "semantic": "Cosine similarity; higher is closer, not a probability",
    "lexical": "Reciprocal keyword rank, not confidence or probability",
    "hybrid": "RRF rank-fusion score, not confidence or probability",
}


class KnowledgeService:
    """Search and reading over one database with a lazily loaded embedder.

    detail="full" returns stored records; "compact" is the agent view (yaqs.present).
    Arguments are validated before the model runs. With query_log, every call appends a
    record naming its caller (yaqs.querylog); without it nothing is logged.
    """

    def __init__(
        self, database: Path, *, device: str = "cpu", query_log: Path | None = None
    ) -> None:
        self.store = KnowledgeBase(database)
        self.encoder = QwenEncoder(device)
        self.log = QueryLog(query_log) if query_log else None

    def _record(
        self, caller: Caller, operation: str, arguments: dict[str, Any]
    ) -> AbstractContextManager[Entry]:
        if self.log is None:
            return nullcontext(Entry())
        return self.log.record(caller, operation, arguments)

    def search(
        self,
        query: str,
        limit: int = 5,
        book_id: int | None = None,
        mode: str = DEFAULT_SEARCH_MODE,
        max_tokens: int = DEFAULT_CONTEXT_TOKENS,
        detail: str = "full",
        *,
        caller: Caller = PYTHON,
    ) -> dict[str, Any]:
        arguments = {
            "query": query,
            "limit": limit,
            "book_id": book_id,
            "mode": mode,
            "max_tokens": max_tokens,
            "detail": detail,
        }
        with self._record(caller, "search", arguments) as entry:
            check_detail(detail)
            validate_search(query, limit, mode)
            validate_budget(max_tokens)
            vector = (
                None if mode == "lexical" else self.encoder.encode([query], query=True)[0].tolist()
            )
            result = {
                "query": query,
                "mode": mode,
                **self.store.context_search(
                    query,
                    vector,
                    self.encoder.tokenizer,
                    limit=limit,
                    book_id=book_id,
                    mode=mode,
                    max_tokens=max_tokens,
                ),
                "score_note": SCORE_NOTES[mode],
            }
            entry.result = blocks(result)
        return compact_search(result) if detail == "compact" else result

    def passage(
        self,
        chunk_id: int,
        neighbors: int = 0,
        max_tokens: int = DEFAULT_CONTEXT_TOKENS,
        detail: str = "full",
        *,
        caller: Caller = PYTHON,
    ) -> dict[str, Any]:
        arguments = {
            "chunk_id": chunk_id,
            "neighbors": neighbors,
            "max_tokens": max_tokens,
            "detail": detail,
        }
        with self._record(caller, "passage", arguments) as entry:
            check_detail(detail)
            validate_budget(max_tokens)
            validate_neighbors(neighbors)
            result = self.store.context_neighbors(
                chunk_id,
                neighbors,
                self.encoder.tokenizer,
                max_tokens=max_tokens,
            )
            entry.result = blocks(result)
        return compact_passage(result) if detail == "compact" else result

    def page(
        self,
        book_id: int,
        page_number: int,
        start_char: int = 0,
        max_tokens: int = DEFAULT_PAGE_TOKENS,
        detail: str = "full",
        *,
        caller: Caller = PYTHON,
    ) -> dict[str, Any]:
        arguments = {
            "book_id": book_id,
            "page_number": page_number,
            "start_char": start_char,
            "max_tokens": max_tokens,
            "detail": detail,
        }
        with self._record(caller, "page", arguments) as entry:
            check_detail(detail)
            validate_budget(max_tokens)
            result = page_window(
                self.store.page(book_id, page_number),
                self.encoder.tokenizer,
                start_char=start_char,
                max_tokens=max_tokens,
            )
            entry.result = window(result)
        return compact_page(result) if detail == "compact" else result

    def books(self, detail: str = "full", *, caller: Caller = PYTHON) -> list[dict[str, Any]]:
        with self._record(caller, "books", {"detail": detail}) as entry:
            check_detail(detail)
            books = self.store.books()
            entry.result = {"books": len(books)}
        return compact_books(books) if detail == "compact" else books

    def stats(self, *, caller: Caller = PYTHON) -> dict[str, Any]:
        with self._record(caller, "stats", {}) as entry:
            stats = self.store.stats()
            entry.result = {}
        return stats
