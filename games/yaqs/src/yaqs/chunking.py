"""Page-local token windows with overlap, source offsets and sentence-aware ends."""

import bisect
import hashlib
import re
from collections.abc import Iterator
from dataclasses import dataclass
from typing import Any

from yaqs import CHUNK_TOKENS, OVERLAP_TOKENS
from yaqs.documents import Document


@dataclass(frozen=True)
class Chunk:
    page_number: int
    ordinal: int
    chapter: str
    text: str
    start_char: int
    end_char: int
    token_count: int
    sha256: str


def chunk_document(
    document: Document,
    tokenizer: Any,
    *,
    size: int = CHUNK_TOKENS,
    overlap: int = OVERLAP_TOKENS,
) -> Iterator[Chunk]:
    if size < 32 or not 0 <= overlap < size // 2:
        raise ValueError("Invalid chunk size or overlap")
    for page in document.pages:
        if not page.text.strip():
            continue
        encoded = tokenizer(page.text, add_special_tokens=False, return_offsets_mapping=True)
        offsets = encoded["offset_mapping"]
        if not offsets:
            continue
        ends = [end for _, end in offsets]
        boundaries = [
            m.end()
            for m in re.finditer(
                r"\n\s*\n|(?<=[.!?])\s+(?=[A-ZА-ЯЁ])",
                page.text,
            )
        ]
        start, ordinal = 0, 0
        while start < len(offsets):
            end = min(start + size, len(offsets))
            if end < len(offsets):
                candidates = [
                    b for b in boundaries if ends[max(start, end - 128)] <= b <= ends[end - 1]
                ]
                if candidates:
                    end = min(end, max(start + 1, bisect.bisect_right(ends, candidates[-1])))
            char_start, char_end = offsets[start][0], offsets[end - 1][1]
            text = page.text[char_start:char_end]
            if text.strip():
                yield Chunk(
                    page.number,
                    ordinal,
                    page.chapter,
                    text,
                    char_start,
                    char_end,
                    end - start,
                    hashlib.sha256(text.encode()).hexdigest(),
                )
                ordinal += 1
            if end == len(offsets):
                break
            start = max(start + 1, end - overlap)
