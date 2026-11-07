# YAQS architecture

## Data path

```text
TXT / JSONL → Document / Page → cleaning → 512/128 chunks → Qwen → SQLite
                                                                  ↓
                        CLI / MCP / web → KnowledgeService → cited context
```

- `documents.py` validates the prepared text and keeps page provenance.
- `cleaning.py` collapses layout whitespace and removes running headers, footers
  and page numbers at import.
- `chunking.py` builds token windows within a page with exact offsets; in TXT,
  Markdown headings split the text into sections and set the chapter.
- `embedding.py` holds the pinned model profile, the cache and lazy loading.
- `ingest.py` handles resumable import and transactional vector batches.
- `locking.py` serializes writers, including book removal.
- `store.py` owns SQL, indexes, citations and consistent reads.
- `context.py` merges only exact overlaps of source text.
- `maintenance.py` makes consistent backups and full rebuilds.
- `diagnostics.py` checks readiness without downloads or inference.
- `service.py` is the shared read layer for `cli.py`, `mcp.py` and `web.py`.
- `web.py` and `static/` are a read-only browser UI: search, context, pages and
  the book list.
- `present.py` builds the compact answer view for agents; it never changes
  stored data.

## Data and invariants

Schema 2: `books`, `pages`, `chunks`, `chunk_vectors`, `chunks_fts`, `config`.
`books.id` and `chunks.id` use `AUTOINCREMENT`, so a deleted ID is never given to
a new source or text in the same database. Rebuilding one book keeps its identity
but creates new chunk IDs.

Foreign keys cascade deletes to pages and chunks. Triggers keep FTS5 in sync and
remove stale vectors. Vectors have 1024 dimensions and unit L2 norm. Page text and
chunk ranges are the basis for citations; reading never needs the source PDF. The
model profile is checked when the database is opened.

Qwen3-Embedding-0.6B is pinned to revision
`97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3`, without quantization. Document input:
`{title}\n{chapter}\n\n{text}`. A query gets a retrieval instruction. CPU uses FP32,
CUDA uses BF16, and the stored normalized vector is float32. A change of the model
profile requires a full rebuild; vectors of different profiles are never mixed.

A full rebuild reads schemas 1 and 2 only as a text source. Normal operation uses
schema 2. The new database is published after an integrity check, and the source
stays available for rollback. Backups use the SQLite backup API so that WAL content
is included; an open file is never copied byte by byte.

## Boundaries

YAQS does not extract text from PDFs, run OCR, manage a collection of source
files or an import queue, translate queries or generate LLM answers. These tasks
belong to the tools that prepare its TXT and JSONL input, and must not leak into
the storage layer or MCP. Such a tool also decides which sources may be imported
again after `remove`. It can call the Python API directly through
`read_document`, `import_book` or `import_document`, `KnowledgeBase` and
`import_lock`.

The runtime is a uv environment on uv's own Python; it needs no Nix shell and no
other service. YAQS does not coordinate GPU use with other programs: a tool that
shares the GPU decides when to import on CUDA and when on the CPU.

## Limitations

- A multilingual model does not guarantee the same ranking for translations of a
  question.
- FTS5 matches tokens without translation; the hybrid mode needs its own quality
  evaluation.
- YAQS does not extract chapters or printed page numbers: the tool that prepares
  the JSONL passes them in `chapter` and `page_label`. Header and footer removal is
  heuristic. A line must repeat at the page edge, so a header whose characters
  differ on neighbouring pages (OCR errors) can survive. Removed lines are listed
  in `removed_lines`.
- On a short page (a figure caption, notes) the chapter in the embedding input
  noticeably affects the score: such pages rank higher for queries about the
  chapter's topic.
- Identical text on different pages stays as separate sources; context assembly
  removes overlapping ranges, not repeated content across a whole book.
- Import identity is defined by the bytes of the input file; no semantic duplicate
  detection of books is done.
- After a full rebuild the new file has its own citation numbering.
