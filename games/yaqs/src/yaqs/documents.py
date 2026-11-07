"""Read prepared UTF-8 text: TXT, or JSONL with one page per line.

YAQS does not extract text from PDF or EPUB files and does not run OCR; do that first and
pass the result here. See docs/data-format.md for both formats.
"""

import hashlib
import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from yaqs import CHUNK_TOKENS, MIN_CHUNK_TOKENS, OVERLAP_TOKENS, SECTION_TOKENS, TEXT_FORMAT


@dataclass(frozen=True)
class Page:
    # Zero means unpaginated text, not a fabricated source-page citation.
    number: int
    # The printed page number ("xiv", "23"), when the extraction knows it.
    label: str | None
    chapter: str
    text: str
    extraction: str = "ocr-text"
    provenance: dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class Document:
    path: str
    sha256: str  # of the input file's bytes: the book's identity in the database
    title: str
    author: str
    pages: list[Page]
    import_profile: str  # JSON of the settings that shaped the chunks; see import_profile()


def fingerprint(path: Path) -> str:
    with path.open("rb") as file:
        return hashlib.file_digest(file, "sha256").hexdigest()


def normalize_text(text: str) -> str:
    """Normalize BOM and newlines; yaqs.cleaning tidies whitespace at import."""
    return text.removeprefix("\ufeff").replace("\r\n", "\n").replace("\r", "\n").strip()


def import_profile(suffix: str) -> str:
    """Settings that change stored text or chunks; a change requires --rebuild."""
    return json.dumps(
        {
            "input": suffix,
            "text_format": TEXT_FORMAT,
            "chunk_tokens": CHUNK_TOKENS,
            "overlap_tokens": OVERLAP_TOKENS,
            "min_chunk_tokens": MIN_CHUNK_TOKENS,
            "section_tokens": SECTION_TOKENS,
        },
        sort_keys=True,
    )


def read_document(path: Path, *, title: str = "", author: str = "") -> Document:
    """Validate a TXT or JSONL file; title and author override the file's own metadata."""
    path = path.expanduser().resolve(strict=True)
    if path.suffix.lower() not in {".txt", ".jsonl"}:
        raise ValueError("YAQS accepts only UTF-8 TXT or page JSONL text")
    text = path.read_text(encoding="utf-8-sig")
    if path.suffix.lower() == ".txt":
        parts = text.split("\f")
        if len(parts) > 1 and not parts[-1].strip():
            parts.pop()
        paginated = len(parts) > 1
        pages = [
            Page(i if paginated else 0, None, "", normalize_text(part), extraction="text")
            for i, part in enumerate(parts, 1)
        ]
    else:
        records = [json.loads(line) for line in text.splitlines() if line.strip()]
        if not records:
            raise ValueError("JSONL file has no pages")
        pages = []
        totals = set()
        for record in records:
            if not isinstance(record, dict):
                raise ValueError("Each JSONL record must be a page object")
            number = record.get("page")
            if type(number) is not int or number < 1 or not isinstance(record.get("text"), str):
                raise ValueError("Each JSONL page needs a positive integer page and a text string")
            if record.get("total_pages") is not None:
                total = record["total_pages"]
                if type(total) is not int or total < number:
                    raise ValueError("Invalid total_pages")
                totals.add(total)
            ocr = record.get("ocr", {})
            if not isinstance(ocr, dict):
                raise ValueError("OCR provenance must be an object")
            meta = ocr.get("meta", {})
            if not isinstance(meta, dict):
                raise ValueError("OCR metadata must be an object")
            if meta.get("truncated"):
                raise ValueError(f"OCR page {number} was truncated; recognize it again first")
            extraction = record.get("extraction", "ocr-text")
            if not isinstance(extraction, str) or not extraction:
                raise ValueError("Page extraction must be a nonempty string")
            provenance = {
                key: value
                for key, value in record.items()
                if key not in {"text", "chapter", "page_label"}
            }
            pages.append(
                Page(
                    number,
                    record.get("page_label"),
                    str(record.get("chapter", "")),
                    normalize_text(record["text"]),
                    extraction=extraction,
                    provenance=provenance,
                )
            )
        numbers = [page.number for page in pages]
        if len(set(numbers)) != len(numbers):
            raise ValueError("Duplicate page numbers")
        if totals and (len(totals) != 1 or set(numbers) != set(range(1, next(iter(totals)) + 1))):
            raise ValueError("Document is incomplete: total_pages declares missing pages")
        pages.sort(key=lambda page: page.number)
        title = title or str(records[0].get("title", ""))
        author = author or str(records[0].get("author", ""))
    return Document(
        str(path),
        fingerprint(path),
        title or path.stem,
        author,
        pages,
        import_profile(path.suffix.lower()),
    )
