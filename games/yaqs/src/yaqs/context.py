"""Assemble exact source ranges; keep indexed chunks and their vectors untouched."""

import hashlib
from collections.abc import Callable, Sequence
from typing import Any

DEFAULT_CONTEXT_TOKENS = 4096
DEFAULT_PAGE_TOKENS = 8192
MAX_BLOCK_TOKENS = 1024


def validate_budget(max_tokens: int) -> None:
    if not 256 <= max_tokens <= 16384:
        raise ValueError("max_tokens must be between 256 and 16384")


def page_window(
    page: dict[str, Any],
    tokenizer: Any,
    *,
    start_char: int = 0,
    max_tokens: int = DEFAULT_PAGE_TOKENS,
) -> dict[str, Any]:
    """Return page text from start_char within max_tokens; long pages are read in windows.

    Consecutive windows cover the page exactly: the next one starts at next_start_char.
    """
    validate_budget(max_tokens)
    text = page["text"]
    if start_char < 0 or (text and start_char >= len(text)) or (not text and start_char):
        raise ValueError(f"start_char must be between 0 and {max(len(text) - 1, 0)}")
    offsets = tokenizer(text[start_char:], add_special_tokens=False, return_offsets_mapping=True)[
        "offset_mapping"
    ]
    if len(offsets) > max_tokens:
        end, tokens = start_char + offsets[max_tokens - 1][1], max_tokens
    else:
        end, tokens = len(text), len(offsets)
    window = text[start_char:end]
    result = dict(page)
    result.update(
        text=window,
        start_char=start_char,
        end_char=end,
        total_chars=len(text),
        token_count=tokens,
        sha256=hashlib.sha256(window.encode()).hexdigest(),
        complete=start_char == 0 and end == len(text),
        next_start_char=end if end < len(text) else None,
    )
    return result


def assemble_context(
    candidates: Sequence[dict[str, Any]],
    page_text: Callable[[int, int], str],
    tokenizer: Any,
    *,
    limit: int,
    max_tokens: int = DEFAULT_CONTEXT_TOKENS,
    max_block_tokens: int = MAX_BLOCK_TOKENS,
) -> dict[str, Any]:
    """Merge only touching/overlapping ranges of one source page, in rank order.

    A candidate that would exceed the block cap is skipped in its entirety.
    Final blocks retain the best contributing chunk's score; scores are never
    added together. Limit and total budget are applied after merging.
    """
    validate_budget(max_tokens)
    blocks: list[dict[str, Any]] = []
    pages: dict[tuple[int, int], str] = {}
    seen: set[int] = set()
    skipped = 0
    cap = min(max_block_tokens, max_tokens)

    def count(text: str) -> int:
        return len(tokenizer(text, add_special_tokens=False)["input_ids"])

    for rank, hit in enumerate(candidates, 1):
        if hit["id"] in seen:
            continue
        seen.add(hit["id"])
        key = (hit["book_id"], hit["page_number"])
        if key not in pages:
            pages[key] = page_text(*key)
        source = pages[key]
        start, end = hit["start_char"], hit["end_char"]
        if not 0 <= start < end <= len(source) or source[start:end] != hit["text"]:
            raise RuntimeError(f"Chunk {hit['id']} does not match its source-page offsets")
        overlapping = [
            block
            for block in blocks
            if block["key"] == key and block["start"] <= end and start <= block["end"]
        ]
        members = [(rank, hit)]
        for block in overlapping:
            start = min(start, block["start"])
            end = max(end, block["end"])
            members.extend(block["members"])
        tokens = count(source[start:end])
        if tokens > cap:
            skipped += 1
            continue
        blocks = [block for block in blocks if block not in overlapping]
        blocks.append(
            {
                "key": key,
                "start": start,
                "end": end,
                "tokens": tokens,
                "members": members,
                "rank": min(r for r, _ in members),
            }
        )

    selected, used = [], 0
    omitted = 0
    for block in sorted(blocks, key=lambda item: item["rank"]):
        if len(selected) == limit or used + block["tokens"] > max_tokens:
            omitted += 1
            continue
        members = sorted(block["members"], key=lambda member: member[1]["start_char"])
        anchor = min(members, key=lambda member: member[0])[1]
        result = dict(anchor)
        text = pages[block["key"]][block["start"] : block["end"]]
        location = (
            f"page {anchor['page_number']}"
            if anchor["page_number"]
            else f"text offsets {block['start']}–{block['end']}"
        )
        result.update(
            text=text,
            start_char=block["start"],
            end_char=block["end"],
            token_count=block["tokens"],
            sha256=hashlib.sha256(text.encode()).hexdigest(),
            citation=f"{anchor['title']}, {location}",
            anchor_chunk_id=anchor["id"],
            chunk_ids=[hit["id"] for _, hit in members],
            source_chunks=[
                {
                    "id": hit["id"],
                    "start_char": hit["start_char"],
                    "end_char": hit["end_char"],
                    "candidate_rank": rank,
                }
                for rank, hit in members
            ],
        )
        selected.append(result)
        used += block["tokens"]
    return {
        "hits": selected,
        "context": {
            "max_tokens": max_tokens,
            "used_tokens": used,
            "budget_scope": "passage text only, counted by the Qwen tokenizer",
            "max_block_tokens": cap,
            "candidate_count": len(seen),
            "selected_chunk_count": sum(len(block["chunk_ids"]) for block in selected),
            "block_count": len(selected),
            "skipped_overlong_candidates": skipped,
            "omitted_blocks": omitted,
        },
    }
