# Text and result formats

## JSONL

One line per page. `page` is a positive source page number and `text` is a string.

```json
{"title":"Game book","text":"Page text","author":"Author","page":20,"total_pages":300,"extraction":"pdf-text","source":"/books/book.pdf"}
```

The title and author come from the first record, or from `--title` and `--author`.
`chapter`, `page_label`, `source`, `source_sha256` and provenance data are optional.
Page numbers are unique. With `total_pages`, the import requires every page from
1 to that number. Empty pages are stored without embeddings.

`chapter` is part of the embedding input (`{title}\n{chapter}\n\n{text}`) and is
returned in results. Nested chapters read well as a path:
`Part › Chapter › Section`. `page_label` is the printed page number (`xiv`, `23`),
used to cite the paper edition.

`extraction` is `pdf-text` or `ocr-text`; without the field, `ocr-text` is assumed.
A recognized page can carry:

```json
{"title":"Game book","page":20,"text":"Recognized text","ocr":{"job_id":"job-id","model":{"revision":"revision"},"meta":{"truncated":false}}}
```

A page with `ocr.meta.truncated: true` is rejected. Provenance is stored in the
database with the text. The format is not tied to one OCR server.

## TXT

UTF-8 text without page markup gets page number 0: citations give a character
range, not an invented page. A form feed (`\f`) splits explicitly paginated TXT.
BOM and line endings are normalized; paragraphs, Markdown tables and code
indentation are kept. JSONL is better for exact PDF citations.

Markdown headings `#` and `##` start sections in TXT. A chunk's chapter is the
heading path (`Command › Undo`), which carries across `\f` to the following pages.
A section shorter than 64 tokens joins the next one; the last one joins the
previous. In JSONL, headings do not affect chapters.

The book hash covers the bytes of the input TXT/JSONL, not the source PDF. A change
in the input file's formatting changes its identity, so keep the canonical input
unchanged between imports.

## Cleaning at import

The import (text format 2, `text_format` in `import_profile`) stores cleaned text.
Citations, offsets and page windows refer to it. YAQS does not keep the input file;
keep it yourself if you may need to re-import.

- Zero-width characters and trailing spaces are removed. Runs of spaces become one
  space and several blank lines become one. Leading indentation is kept, but on
  `pdf-text` pages each paragraph loses its common indentation, so the relative
  indentation of code survives.
- A running header or footer is one of the first two or last two non-empty
  lines of a page (up to 120 characters). It must repeat at the edge of at least
  three pages of one run, with gaps of at most four pages, differing only in
  numbers; a roman numeral counts as a number. A line without digits that ends
  like a sentence is never removed. The pass repeats while it removes something
  (up to four times), which also removes two-line headers. Page 0 is not
  checked.
- Removed lines are recorded in the page's `provenance.removed_lines`.
- A page or section shorter than 32 tokens (an empty page, a part title) stays
  readable but gets no chunk.

A change of the text format changes `import_profile`; re-import the old book with
`--rebuild` or rebuild the whole database.

## Context

`search` and `passage` return `{hits, context}`. In a block:

- `text`, `start_char`, `end_char`, `token_count`, `sha256` and `citation` describe
  the whole assembled range of source text;
- `id`, `anchor_chunk_id`, `ordinal` and the scores belong to the block's best
  source chunk;
- `chunk_ids` and `source_chunks` list the contributing chunks and their offsets;
- `book_id`, `title`, `author`, `page_number` and `source_path` let you verify the
  source. The PDF page number is physical, starting at 1. Page `provenance` is not
  repeated in every block: `page` returns it.

One SQLite snapshot serves ranking and reading of the source ranges. Different
pages and books are never merged, and gaps between ranges are not filled. A
candidate that is too large is skipped whole; text is never truncated. The block
limit and the text budget apply after merging.

`context` reports the number of candidates, selected chunks, finished blocks, used
tokens and omitted results. `max_tokens` accepts 256–16384. For neighbours
requested explicitly with `passage --neighbors N`, one block may use the whole
budget, with priority for the central chunk; the selected blocks come back in
reading order.

`page` returns a page in windows of at most `max_tokens` (8192 by default): `text`,
`start_char`, `end_char`, `total_chars`, `complete` and `next_start_char`. The next
window starts at `next_start_char`; windows cover the page without gaps or repeats.
Page 0 is read the same way, as one unsplit text.

## Compact view

The CLI and MCP answer compactly by default; `detail=full` returns everything listed
above. A compact block has `id`, `chunk_ids`, `book_id`, `title`, `author`,
`page_number`, `page_label` and `chapter` when present, `score` (up to three
decimals) and `text`. Page 0 adds `start_char` and `end_char`, and a book that is
not fully indexed adds `index_status`. `context` shrinks to `used_tokens` and
`omitted_blocks`; `query` and `score_note` are not repeated. A page keeps its source,
`text`, `complete` and `next_start_char`; a book keeps `id`, `title`, `author`,
`page_count`, `chunks`, plus `status` and `indexed_chunks` for a book that is not
ready.

The compact text drops zero-width characters and trailing spaces, and collapses
runs of spaces (including non-breaking and typographic ones) and of blank lines
to one. Leading indentation is removed only on `pdf-text` pages, where
`pdftotext -layout` uses it just to place words on the sheet. Elsewhere, for
example in code, indentation is kept. Offsets and the budget still refer to the
stored text, so `next_start_char` from a compact window continues reading exactly
as from a full one.

The vector `score` is cosine similarity, the lexical one is the FTS5 reciprocal
rank and the hybrid one is RRF. None of them is a probability of a correct answer.
A nearest chunk exists even for a question outside the library's subject.
