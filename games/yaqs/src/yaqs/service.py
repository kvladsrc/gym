"""Read-only agent operations over the catalog and the two retrieval indexes."""

from pathlib import Path
from typing import Any

from yaqs import DEFAULT_SEARCH_MODE, SEARCH_MODES
from yaqs.context import DEFAULT_CONTEXT_TOKENS, validate_budget
from yaqs.embedding import QwenEncoder
from yaqs.store import KnowledgeBase


class KnowledgeService:
    def __init__(self, database: Path, *, device: str = "cpu") -> None:
        self.store = KnowledgeBase(database)
        self.encoder = QwenEncoder(device)

    def search(
        self,
        query: str,
        limit: int = 5,
        book_id: int | None = None,
        mode: str = DEFAULT_SEARCH_MODE,
        max_tokens: int = DEFAULT_CONTEXT_TOKENS,
    ) -> dict[str, Any]:
        if not query.strip() or len(query) > 4000:
            raise ValueError("Query must contain 1 to 4000 characters")
        if mode not in SEARCH_MODES or not 1 <= limit <= 20:
            raise ValueError("Invalid search mode or limit (1 to 20)")
        validate_budget(max_tokens)
        vector = None if mode == "lexical" else self.encoder.encode([query], query=True)[0].tolist()
        return {
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
            "score_note": {
                "semantic": "Cosine similarity; higher is closer, not a probability",
                "lexical": "Reciprocal keyword rank, not confidence or probability",
                "hybrid": "RRF rank-fusion score, not confidence or probability",
            }[mode],
        }

    def passage(
        self,
        chunk_id: int,
        neighbors: int = 0,
        max_tokens: int = DEFAULT_CONTEXT_TOKENS,
    ) -> dict[str, Any]:
        validate_budget(max_tokens)
        if not 0 <= neighbors <= 3:
            raise ValueError("neighbors must be between 0 and 3")
        return self.store.context_neighbors(
            chunk_id,
            neighbors,
            self.encoder.tokenizer,
            max_tokens=max_tokens,
        )
