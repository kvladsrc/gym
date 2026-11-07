"""Page-local token windows with overlap, source offsets and sentence-aware ends.

TXT input has no page chapters, so its Markdown headings (# and ##) start sections and name
the chapter of their chunks; the heading path carries over form-feed pages. Sections shorter
than SECTION_TOKENS join a neighbour. A page or section shorter than MIN_CHUNK_TOKENS
(blank-page notices, part titles, publisher pages) stays readable but gets no chunk.
"""

import bisect
import hashlib
import re
from collections.abc import Iterator
from dataclasses import dataclass
from typing import Any

from yaqs import CHUNK_TOKENS, MIN_CHUNK_TOKENS, OVERLAP_TOKENS, SECTION_TOKENS
from yaqs.documents import Document, Page

HEADING = re.compile(r"^(#{1,2})[ \t]+(\S[^\n]*?)[ \t#]*$", re.MULTILINE)
# A chunk prefers to end after a paragraph or a sentence (Latin or Cyrillic capital next).
BOUNDARY = re.compile(r"\n\s*\n|(?<=[.!?])\s+(?=[A-ZА-ЯЁ])")
BOUNDARY_LOOKBACK = 128  # tokens at the end of a window searched for a boundary


@dataclass(frozen=True)
class Chunk:
    """A token window of one page; text == page.text[start_char:end_char]."""

    page_number: int
    ordinal: int
    chapter: str
    text: str
    start_char: int
    end_char: int
    token_count: int
    sha256: str


def _sections(page: Page, offsets: list[Any], path: list[str]) -> list[list[Any]]:
    """[first token, end token, chapter] of a page; path is the carried heading path."""
    if page.extraction != "text":
        return [[0, len(offsets), page.chapter]]
    starts = [start for start, _ in offsets]
    cuts = [(0, " › ".join(filter(None, path)) or page.chapter)]
    for match in HEADING.finditer(page.text):
        level = len(match[1])
        del path[level - 1 :]
        path.extend([""] * (level - 1 - len(path)))
        path.append(match[2])
        cuts.append((bisect.bisect_left(starts, match.start()), " › ".join(filter(None, path))))
    sections = [
        [first, end, chapter]
        for (first, chapter), (end, _) in zip(cuts, [*cuts[1:], (len(offsets), "")], strict=True)
        if end > first
    ]
    merged: list[list[Any]] = []
    for section in sections:
        if merged and merged[-1][1] - merged[-1][0] < SECTION_TOKENS:
            # A heading with little text joins the next section, which names the chapter.
            section = [merged.pop()[0], section[1], section[2]]
        merged.append(section)
    if len(merged) > 1 and merged[-1][1] - merged[-1][0] < SECTION_TOKENS:
        tail = merged.pop()
        merged[-1][1] = tail[1]
    return merged


def chunk_document(
    document: Document,
    tokenizer: Any,
    *,
    size: int = CHUNK_TOKENS,
    overlap: int = OVERLAP_TOKENS,
    min_tokens: int = MIN_CHUNK_TOKENS,
) -> Iterator[Chunk]:
    """Yield chunks of at most size tokens; neighbors within a section share overlap tokens.

    The tokenizer is called like a Hugging Face fast tokenizer and must return
    offset_mapping. Chunks never cross a page, so every chunk cites exactly one page.
    """
    if size < 32 or not 0 <= overlap < size // 2:
        raise ValueError("Invalid chunk size or overlap")
    path: list[str] = []
    for page in document.pages:
        if not page.text.strip():
            continue
        encoded = tokenizer(page.text, add_special_tokens=False, return_offsets_mapping=True)
        offsets = encoded["offset_mapping"]
        if not offsets:
            continue
        ends = [end for _, end in offsets]
        boundaries = [match.end() for match in BOUNDARY.finditer(page.text)]
        ordinal = 0
        for first, last, chapter in _sections(page, offsets, path):
            if last - first < min_tokens:
                continue
            start = first
            while start < last:
                end = min(start + size, last)
                if end < last:
                    earliest = ends[max(start, end - BOUNDARY_LOOKBACK)]
                    candidates = [b for b in boundaries if earliest <= b <= ends[end - 1]]
                    if candidates:
                        end = min(end, max(start + 1, bisect.bisect_right(ends, candidates[-1])))
                char_start, char_end = offsets[start][0], offsets[end - 1][1]
                text = page.text[char_start:char_end]
                if text.strip():
                    yield Chunk(
                        page.number,
                        ordinal,
                        chapter,
                        text,
                        char_start,
                        char_end,
                        end - start,
                        hashlib.sha256(text.encode()).hexdigest(),
                    )
                    ordinal += 1
                if end == last:
                    break
                start = max(start + 1, end - overlap)
