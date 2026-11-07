"""Compact views of results for agents; the full view keeps every stored field.

A compact view keeps what an agent reads, cites or passes back: ids, book, author, page,
score, the text and the page-window cursor. It drops hashes, source paths, per-chunk offsets,
import profiles and page provenance, and collapses layout whitespace in the text. Offsets
such as next_start_char still refer to the stored text, so page windows chain as before.
"""

from typing import Any

from yaqs.cleaning import tidy

DETAILS = ("compact", "full")


def check_detail(detail: str) -> None:
    if detail not in DETAILS:
        raise ValueError(f"detail must be one of {', '.join(DETAILS)}")


def _source(record: dict[str, Any]) -> dict[str, Any]:
    view = {
        "book_id": record["book_id"],
        "title": record["title"],
        "author": record["author"],
        "page_number": record["page_number"],
    }
    if record.get("page_label"):
        view["page_label"] = record["page_label"]
    if record.get("chapter"):
        view["chapter"] = record["chapter"]
    return view


def _block(hit: dict[str, Any]) -> dict[str, Any]:
    view = {"id": hit["id"], "chunk_ids": hit["chunk_ids"], **_source(hit)}
    if not hit["page_number"]:  # unpaginated text is cited by character offsets
        view.update(start_char=hit["start_char"], end_char=hit["end_char"])
    if hit.get("index_status", "ready") != "ready":
        view["index_status"] = hit["index_status"]
    if "score" in hit:
        view["score"] = round(hit["score"], 3)
    view["text"] = tidy(hit["text"], layout=hit.get("extraction") == "pdf-text")
    return view


def _context(context: dict[str, Any]) -> dict[str, Any]:
    return {"used_tokens": context["used_tokens"], "omitted_blocks": context["omitted_blocks"]}


def compact_search(result: dict[str, Any]) -> dict[str, Any]:
    return {
        "mode": result["mode"],
        "hits": [_block(hit) for hit in result["hits"]],
        "context": _context(result["context"]),
    }


def compact_passage(result: dict[str, Any]) -> dict[str, Any]:
    return {
        "hits": [_block(hit) for hit in result["hits"]],
        "context": _context(result["context"]),
    }


def compact_page(page: dict[str, Any]) -> dict[str, Any]:
    return {
        **_source(page),
        "text": tidy(page["text"], layout=page["extraction"] == "pdf-text"),
        "complete": page["complete"],
        "next_start_char": page["next_start_char"],
    }


def compact_books(books: list[dict[str, Any]]) -> list[dict[str, Any]]:
    views = []
    for book in books:
        view = {key: book[key] for key in ("id", "title", "author", "page_count", "chunks")}
        if book["status"] != "ready":
            view.update(status=book["status"], indexed_chunks=book["indexed_chunks"])
        views.append(view)
    return views
