"""Import-time text cleaning: layout whitespace and running page furniture.

Stored page text is what YAQS cites, so cleaning runs once at import (text format 2) and
stored text, chunk offsets and page windows all refer to the cleaned text. The original
extraction stays with the caller. Removed header and footer lines are listed in the page
provenance under "removed_lines".
"""

import dataclasses
import re
from collections import defaultdict

from yaqs.documents import Document, Page

# Zero-width space, non-joiner, joiner, word joiner and BOM.
_INVISIBLE = re.compile("[\u200b\u200c\u200d\u2060\ufeff]")
# Space, tab, no-break space, the typographic spaces U+2000-U+200A, narrow no-break,
# medium mathematical and ideographic space.
_BLANKS = " \t\u00a0" + "".join(map(chr, range(0x2000, 0x200B))) + "\u202f\u205f\u3000"
_RUN = re.compile(f"[{_BLANKS}]+")
_SENTENCE_END = re.compile(r"[.!?][\"”’)]*$")
_DIGIT = re.compile(r"\d")
_ROMAN = re.compile(r"(?=[ivxlcdm])m{0,3}(?:c[md]|d?c{0,3})(?:x[cl]|l?x{0,3})(?:i[xv]|v?i{0,3})")

EDGE_LINES = 2  # lines at the top and at the bottom of a page that can be furniture
MAX_FURNITURE_CHARS = 120
MIN_REPEATS = 3  # a running line repeats on at least this many pages of one run
MAX_GAP = 4  # largest page number step inside one run (odd/even headers, figure pages)
MAX_PASSES = 4


def _indent(line: str) -> int:
    return len(line) - len(line.lstrip(_BLANKS))


def _dedent_blocks(lines: list[str]) -> list[str]:
    """Remove each paragraph block's common margin; relative indentation (code) stays."""
    result: list[str] = []
    block: list[str] = []
    for line in [*lines, ""]:
        if line:
            block.append(line)
            continue
        if block:
            margin = min(_indent(item) for item in block)
            result.extend(item[margin:] for item in block)
            block = []
        result.append("")
    return result[:-1]


def tidy(text: str, *, layout: bool) -> str:
    """Collapse whitespace that carries nothing for a reader.

    Runs of spaces inside a line become one space, trailing spaces and repeated blank lines
    go, and zero-width characters are removed. Leading indentation is kept, since it can be
    code or a nested list. In layout text (pdftotext -layout) the page margin only places
    words on the printed page, so each block of lines loses its common margin.
    """
    lines = [line.rstrip(_BLANKS) for line in _INVISIBLE.sub("", text).split("\n")]
    if layout:
        lines = _dedent_blocks(lines)
    tidied = []
    for line in lines:
        body = line.lstrip(_BLANKS)
        tidied.append(line[: len(line) - len(body)] + _RUN.sub(" ", body) if body else "")
    return re.sub(r"\n{3,}", "\n\n", "\n".join(tidied)).strip("\n")


def _key(line: str) -> str:
    """Running headers differ only by page and chapter numbers."""
    key = _RUN.sub(" ", line).strip().lower()
    return "#" if _ROMAN.fullmatch(key) else re.sub(r"\d+", "#", key)


def _edges(lines: list[str]) -> list[int]:
    filled = [index for index, line in enumerate(lines) if line.strip()]
    return sorted({*filled[:EDGE_LINES], *filled[-EDGE_LINES:]})


def _furniture(pages: list[Page]) -> dict[int, set[int]]:
    """Edge line indices to remove per page: lines repeated at page edges in runs."""
    seen: dict[str, dict[int, list[int]]] = defaultdict(dict)
    for page in pages:
        lines = page.text.split("\n")
        for index in _edges(lines):
            line = lines[index].strip()
            # The end of a sentence ("system.") can repeat at page edges; headers rarely do.
            sentence = _SENTENCE_END.search(line) and not _DIGIT.search(line)
            if len(line) > MAX_FURNITURE_CHARS or sentence:
                continue
            seen[_key(line)].setdefault(page.number, []).append(index)
    remove: dict[int, set[int]] = defaultdict(set)
    for where in seen.values():
        run: list[int] = []
        for number in [*sorted(where), None]:
            if run and (number is None or number - run[-1] > MAX_GAP):
                if len(run) >= MIN_REPEATS:
                    for page_number in run:
                        remove[page_number].update(where[page_number])
                run = []
            if number is not None:
                run.append(number)
    return remove


def strip_furniture(pages: list[Page]) -> list[Page]:
    """Remove running headers, footers and page numbers from paginated text.

    A line at a page edge is furniture when the same line, up to its numbers, sits at a page
    edge on at least three pages with at most three pages between repeats. A line that
    repeats only far apart, such as a chapter title in the table of contents, stays.
    Passes repeat until nothing changes (at most four), so two-line headers go too.
    """
    result = list(pages)
    for _ in range(MAX_PASSES):
        remove = _furniture([page for page in result if page.number])
        if not remove:
            break
        for position, page in enumerate(result):
            if page.number not in remove:
                continue
            lines = page.text.split("\n")
            removed = [lines[index].strip() for index in sorted(remove[page.number])]
            kept = [line for index, line in enumerate(lines) if index not in remove[page.number]]
            provenance = dict(page.provenance)
            provenance["removed_lines"] = [*provenance.get("removed_lines", []), *removed]
            text = re.sub(r"\n{3,}", "\n\n", "\n".join(kept)).strip("\n")
            result[position] = dataclasses.replace(page, text=text, provenance=provenance)
    return result


def clean_document(document: Document) -> Document:
    pages = [
        dataclasses.replace(page, text=tidy(page.text, layout=page.extraction == "pdf-text"))
        for page in document.pages
    ]
    return dataclasses.replace(document, pages=strip_furniture(pages))
