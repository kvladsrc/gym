"""SQLite catalog, exact cosine KNN with sqlite-vec, and FTS5 fused by rank.

One database file holds books, page text, chunks, their vectors and a full-text index.
Triggers keep the FTS5 index and the vectors in step with the chunks table. Every read
that combines several queries runs in one transaction, so it sees a single snapshot even
while a writer replaces a book.
"""

import json
import math
import re
import sqlite3
from collections.abc import Iterator, Sequence
from contextlib import contextmanager
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import sqlite_vec

from yaqs import DEFAULT_SEARCH_MODE, SCHEMA_VERSION, SEARCH_MODES
from yaqs.chunking import Chunk
from yaqs.context import DEFAULT_CONTEXT_TOKENS, assemble_context, validate_budget
from yaqs.documents import Document
from yaqs.embedding import DIMENSIONS, PROFILE

MAX_QUERY_CHARS = 4000
MAX_SEARCH_LIMIT = 20
MAX_NEIGHBORS = 3
RRF_K = 60  # reciprocal rank fusion constant from the original RRF paper
# What integrity() returns for a healthy, fully indexed database.
INTEGRITY_OK = {
    "sqlite": "ok",
    "foreign_keys": [],
    "orphan_vectors": 0,
    "fts": "ok",
    "pending_chunks": 0,
}

SCHEMA = f"""
CREATE TABLE IF NOT EXISTS config(key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS books(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sha256 TEXT NOT NULL UNIQUE,
    source_path TEXT NOT NULL,
    title TEXT NOT NULL,
    author TEXT NOT NULL,
    page_count INTEGER NOT NULL,
    import_profile TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'extracted',
    created_at TEXT NOT NULL,
    indexed_at TEXT
);
CREATE TABLE IF NOT EXISTS pages(
    book_id INTEGER NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    page_number INTEGER NOT NULL,
    page_label TEXT,
    chapter TEXT NOT NULL,
    text TEXT NOT NULL,
    extraction TEXT NOT NULL,
    provenance TEXT NOT NULL DEFAULT '{{}}',
    PRIMARY KEY(book_id, page_number)
);
CREATE TABLE IF NOT EXISTS chunks(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    book_id INTEGER NOT NULL,
    page_number INTEGER NOT NULL,
    ordinal INTEGER NOT NULL,
    chapter TEXT NOT NULL,
    text TEXT NOT NULL,
    start_char INTEGER NOT NULL,
    end_char INTEGER NOT NULL,
    token_count INTEGER NOT NULL,
    sha256 TEXT NOT NULL,
    UNIQUE(book_id, page_number, ordinal),
    FOREIGN KEY(book_id, page_number) REFERENCES pages(book_id, page_number) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS chunks_book ON chunks(book_id, page_number, ordinal);
CREATE VIRTUAL TABLE IF NOT EXISTS chunk_vectors USING vec0(
    chunk_id INTEGER PRIMARY KEY,
    embedding float[{DIMENSIONS}] distance_metric=cosine,
    book_id INTEGER
);
CREATE VIRTUAL TABLE IF NOT EXISTS chunks_fts USING fts5(
    text, chapter, content='chunks', content_rowid='id',
    tokenize='unicode61 remove_diacritics 2'
);
CREATE TRIGGER IF NOT EXISTS chunks_insert AFTER INSERT ON chunks BEGIN
    INSERT INTO chunks_fts(rowid, text, chapter) VALUES(new.id, new.text, new.chapter);
END;
CREATE TRIGGER IF NOT EXISTS chunks_delete AFTER DELETE ON chunks BEGIN
    INSERT INTO chunks_fts(chunks_fts, rowid, text, chapter)
        VALUES('delete', old.id, old.text, old.chapter);
    DELETE FROM chunk_vectors WHERE chunk_id=old.id;
END;
CREATE TRIGGER IF NOT EXISTS chunks_update AFTER UPDATE ON chunks BEGIN
    INSERT INTO chunks_fts(chunks_fts, rowid, text, chapter)
        VALUES('delete', old.id, old.text, old.chapter);
    INSERT INTO chunks_fts(rowid, text, chapter) VALUES(new.id, new.text, new.chapter);
    DELETE FROM chunk_vectors WHERE chunk_id=old.id;
END;
"""


def utc_now() -> str:
    return datetime.now(UTC).isoformat()


def fts_expression(query: str) -> str:
    """User words are literals, not executable FTS5 query syntax."""
    words = re.findall(r"[^\W_]+(?:[-_][^\W_]+)*", query, flags=re.UNICODE)[:32]
    return " OR ".join('"' + word.replace('"', '""') + '"' for word in words)


def fuse_ranks(dense: Sequence[int], lexical: Sequence[int]) -> list[tuple[int, float]]:
    """Reciprocal rank fusion of two rankings; ties go to the lower chunk id."""
    scores: dict[int, float] = {}
    for ranking in (dense, lexical):
        for rank, chunk_id in enumerate(dict.fromkeys(ranking), 1):
            scores[chunk_id] = scores.get(chunk_id, 0.0) + 1 / (RRF_K + rank)
    return sorted(scores.items(), key=lambda item: (-item[1], item[0]))


def validate_search(query: str, limit: int, mode: str) -> None:
    if not query.strip() or len(query) > MAX_QUERY_CHARS:
        raise ValueError(f"Query must contain 1 to {MAX_QUERY_CHARS} characters")
    if mode not in SEARCH_MODES:
        raise ValueError(f"mode must be one of {', '.join(SEARCH_MODES)}")
    if not 1 <= limit <= MAX_SEARCH_LIMIT:
        raise ValueError(f"limit must be between 1 and {MAX_SEARCH_LIMIT}")


def validate_neighbors(radius: int) -> None:
    if not 0 <= radius <= MAX_NEIGHBORS:
        raise ValueError(f"neighbors must be between 0 and {MAX_NEIGHBORS}")


class KnowledgeBase:
    """The library database. Opening checks the schema version and the embedding profile.

    Each method opens its own connection, so one instance can serve several threads.
    Writers must hold yaqs.locking.import_lock; readers need no lock (WAL).
    """

    def __init__(self, path: Path, *, create: bool = False) -> None:
        self.path = path.expanduser().resolve()
        if create:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            with self.connect() as db:
                version = db.execute("PRAGMA user_version").fetchone()[0]
                if version not in (0, SCHEMA_VERSION):
                    raise ValueError(
                        f"Database schema {version} requires yaqs rebuild --output NEW_DB"
                    )
                db.execute("PRAGMA journal_mode=WAL")
                db.executescript(SCHEMA)
                with db:
                    db.execute(f"PRAGMA user_version={SCHEMA_VERSION}")
                    db.execute(
                        "INSERT OR IGNORE INTO config(key,value) VALUES('embedding_profile',?)",
                        (json.dumps(PROFILE, sort_keys=True),),
                    )
        elif not self.path.is_file():
            raise FileNotFoundError(f"No database at {self.path}; import a book first")
        with self.connect() as db:
            version = db.execute("PRAGMA user_version").fetchone()[0]
            if version != SCHEMA_VERSION:
                raise ValueError(f"Database schema {version} requires yaqs rebuild --output NEW_DB")
            row = db.execute("SELECT value FROM config WHERE key='embedding_profile'").fetchone()
            if row is None or json.loads(row[0]) != PROFILE:
                raise ValueError("Embedding profile differs; use a separate database or rebuild it")

    @contextmanager
    def connect(self) -> Iterator[sqlite3.Connection]:
        db = sqlite3.connect(self.path, timeout=30)
        db.row_factory = sqlite3.Row
        try:
            db.enable_load_extension(True)
            try:
                sqlite_vec.load(db)
            finally:
                db.enable_load_extension(False)
            db.execute("PRAGMA foreign_keys=ON")
            yield db
        finally:
            db.close()

    def book_by_hash(self, sha256: str) -> dict[str, Any] | None:
        with self.connect() as db:
            row = db.execute("SELECT * FROM books WHERE sha256=?", (sha256,)).fetchone()
            return dict(row) if row else None

    def add_document(
        self, document: Document, chunks: Sequence[Chunk], *, rebuild: bool = False
    ) -> int:
        """Add a book, or return the existing id for the same file hash.

        rebuild replaces the stored book atomically; triggers clean both indexes.
        """
        with self.connect() as db, db:
            old = db.execute("SELECT * FROM books WHERE sha256=?", (document.sha256,)).fetchone()
            if old is not None and not rebuild:
                if old["import_profile"] != document.import_profile:
                    raise ValueError("Import settings changed; use --rebuild for this book")
                return int(old["id"])
            if old is not None:
                book_id = int(old["id"])
                db.execute("DELETE FROM pages WHERE book_id=?", (book_id,))
                db.execute(
                    "UPDATE books SET source_path=?,title=?,author=?,page_count=?,import_profile=?,"
                    "status='extracted',indexed_at=NULL WHERE id=?",
                    (
                        document.path,
                        document.title,
                        document.author,
                        len(document.pages),
                        document.import_profile,
                        book_id,
                    ),
                )
            else:
                cursor = db.execute(
                    "INSERT INTO books(sha256,source_path,title,author,page_count,"
                    "import_profile,created_at)"
                    " VALUES(?,?,?,?,?,?,?)",
                    (
                        document.sha256,
                        document.path,
                        document.title,
                        document.author,
                        len(document.pages),
                        document.import_profile,
                        utc_now(),
                    ),
                )
                book_id = int(cursor.lastrowid)
            db.executemany(
                "INSERT INTO pages(book_id,page_number,page_label,chapter,text,extraction,"
                "provenance)"
                " VALUES(?,?,?,?,?,?,?)",
                [
                    (
                        book_id,
                        page.number,
                        page.label,
                        page.chapter,
                        page.text,
                        page.extraction,
                        json.dumps(page.provenance, ensure_ascii=False),
                    )
                    for page in document.pages
                ],
            )
            db.executemany(
                "INSERT INTO chunks(book_id,page_number,ordinal,chapter,text,start_char,end_char,"
                "token_count,sha256) VALUES(?,?,?,?,?,?,?,?,?)",
                [
                    (
                        book_id,
                        chunk.page_number,
                        chunk.ordinal,
                        chunk.chapter,
                        chunk.text,
                        chunk.start_char,
                        chunk.end_char,
                        chunk.token_count,
                        chunk.sha256,
                    )
                    for chunk in chunks
                ],
            )
            return book_id

    def pending_chunks(self, book_id: int) -> list[dict[str, Any]]:
        with self.connect() as db:
            return [
                dict(row)
                for row in db.execute(
                    "SELECT c.*, b.title FROM chunks c JOIN books b ON b.id=c.book_id "
                    "LEFT JOIN chunk_vectors v ON v.chunk_id=c.id WHERE c.book_id=? "
                    "AND v.chunk_id IS NULL ORDER BY c.page_number,c.ordinal",
                    (book_id,),
                )
            ]

    def add_vectors(self, rows: Sequence[dict[str, Any]], vectors: Any) -> None:
        """Store unit-length vectors for pending_chunks() rows in one transaction."""
        import numpy as np

        matrix = np.asarray(vectors, dtype=np.float32)
        if matrix.shape != (len(rows), DIMENSIONS) or not np.isfinite(matrix).all():
            raise ValueError("Invalid embedding shape or values")
        if not np.allclose(np.linalg.norm(matrix, axis=1), 1, atol=1e-4):
            raise ValueError("Embeddings must be normalized")
        with self.connect() as db, db:
            for row, vector in zip(rows, matrix, strict=True):
                db.execute(
                    "INSERT INTO chunk_vectors(chunk_id,embedding,book_id) VALUES(?,?,?)",
                    (row["id"], vector.astype("<f4").tobytes(), row["book_id"]),
                )
            if rows:
                db.execute("UPDATE books SET status='indexing' WHERE id=?", (rows[0]["book_id"],))

    def finish_index(self, book_id: int) -> None:
        if self.pending_chunks(book_id):
            raise ValueError("The book still has chunks without embeddings")
        with self.connect() as db, db:
            db.execute(
                "UPDATE books SET status='ready',indexed_at=? WHERE id=?", (utc_now(), book_id)
            )

    def books(self) -> list[dict[str, Any]]:
        with self.connect() as db:
            return [
                dict(row)
                for row in db.execute(
                    "SELECT b.*, (SELECT count(*) FROM chunks c WHERE c.book_id=b.id) AS chunks, "
                    "(SELECT count(*) FROM chunk_vectors v WHERE v.book_id=b.id) AS indexed_chunks "
                    "FROM books b ORDER BY b.id"
                )
            ]

    def stats(self) -> dict[str, Any]:
        with self.connect() as db:
            counts = {
                table: db.execute(f"SELECT count(*) FROM {table}").fetchone()[0]
                for table in ("books", "pages", "chunks", "chunk_vectors")
            }
            return {
                "path": str(self.path),
                "schema": SCHEMA_VERSION,
                "counts": counts,
                "pending_chunks": counts["chunks"] - counts["chunk_vectors"],
                "sqlite": sqlite3.sqlite_version,
                "sqlite_vec": db.execute("SELECT vec_version()").fetchone()[0],
                "embedding": PROFILE,
                "database_bytes": self.path.stat().st_size,
            }

    def passage(self, chunk_id: int) -> dict[str, Any]:
        with self.connect() as db:
            return self._passage(db, chunk_id)

    @staticmethod
    def _passage(db: sqlite3.Connection, chunk_id: int) -> dict[str, Any]:
        # Page provenance is long and repeats for every hit; get_page returns it.
        row = db.execute(
            "SELECT c.*, b.title, b.author, b.source_path, b.sha256 AS book_sha256, "
            "b.status AS index_status, p.page_label, p.extraction FROM chunks c "
            "JOIN books b ON b.id=c.book_id JOIN pages p ON p.book_id=c.book_id "
            "AND p.page_number=c.page_number WHERE c.id=?",
            (chunk_id,),
        ).fetchone()
        if row is None:
            raise KeyError(f"Unknown chunk {chunk_id}")
        result = dict(row)
        location = (
            f"page {row['page_number']}"
            if row["page_number"]
            else f"text offsets {row['start_char']}–{row['end_char']}"
        )
        result["citation"] = f"{row['title']}, {location}"
        return result

    def page(self, book_id: int, number: int) -> dict[str, Any]:
        with self.connect() as db:
            return self._page(db, book_id, number)

    @staticmethod
    def _page(db: sqlite3.Connection, book_id: int, number: int) -> dict[str, Any]:
        row = db.execute(
            "SELECT p.*,b.title,b.author,b.source_path FROM pages p "
            "JOIN books b ON b.id=p.book_id WHERE p.book_id=? AND p.page_number=?",
            (book_id, number),
        ).fetchone()
        if row is None:
            raise KeyError(f"Unknown page {number} of book {book_id}")
        result = dict(row)
        result["provenance"] = json.loads(result["provenance"])
        return result

    def neighbors(self, chunk_id: int, radius: int = 1) -> list[dict[str, Any]]:
        """The chunk and up to radius chunks before and after it in the same book."""
        validate_neighbors(radius)
        with self.connect() as db:
            db.execute("BEGIN")
            return self._neighbors(db, chunk_id, radius)

    def _neighbors(
        self,
        db: sqlite3.Connection,
        chunk_id: int,
        radius: int,
    ) -> list[dict[str, Any]]:
        center = self._passage(db, chunk_id)
        key = (center["book_id"], center["page_number"], center["ordinal"], radius)
        before = [
            r[0]
            for r in db.execute(
                "SELECT id FROM chunks WHERE book_id=? AND (page_number,ordinal)<(?,?) "
                "ORDER BY page_number DESC,ordinal DESC LIMIT ?",
                key,
            )
        ]
        after = [
            r[0]
            for r in db.execute(
                "SELECT id FROM chunks WHERE book_id=? AND (page_number,ordinal)>(?,?) "
                "ORDER BY page_number,ordinal LIMIT ?",
                key,
            )
        ]
        return [self._passage(db, i) for i in [*reversed(before), chunk_id, *after]]

    def context_neighbors(
        self,
        chunk_id: int,
        radius: int,
        tokenizer: Any,
        *,
        max_tokens: int = DEFAULT_CONTEXT_TOKENS,
    ) -> dict[str, Any]:
        """Neighbors merged into exact source blocks, in reading order (see yaqs.context)."""
        validate_budget(max_tokens)
        validate_neighbors(radius)
        with self.connect() as db:
            db.execute("BEGIN")
            hits = self._neighbors(db, chunk_id, radius)
            # Center has priority if a small budget cannot fit the whole neighborhood.
            hits.sort(key=lambda hit: hit["id"] != chunk_id)
            result = assemble_context(
                hits,
                lambda book, page: self._page(db, book, page)["text"],
                tokenizer,
                limit=len(hits),
                max_tokens=max_tokens,
                max_block_tokens=max_tokens,
            )
        # The selected blocks are returned in reading order, not by priority.
        result["hits"].sort(key=lambda hit: (hit["page_number"], hit["start_char"]))
        return result

    def search(
        self,
        query: str,
        vector: Sequence[float] | None = None,
        *,
        limit: int = 5,
        book_id: int | None = None,
        mode: str = DEFAULT_SEARCH_MODE,
    ) -> list[dict[str, Any]]:
        """Ranked chunks as stored; context_search merges them into source blocks."""
        validate_search(query, limit, mode)
        with self.connect() as db:
            db.execute("BEGIN")
            return self._search_candidates(db, query, vector, book_id, mode, _depth(limit))[:limit]

    def context_search(
        self,
        query: str,
        vector: Sequence[float] | None,
        tokenizer: Any,
        *,
        limit: int = 5,
        book_id: int | None = None,
        mode: str = DEFAULT_SEARCH_MODE,
        max_tokens: int = DEFAULT_CONTEXT_TOKENS,
    ) -> dict[str, Any]:
        """Search and assemble up to limit non-overlapping source blocks within max_tokens."""
        validate_search(query, limit, mode)
        validate_budget(max_tokens)
        with self.connect() as db:
            db.execute("BEGIN")
            candidates = self._search_candidates(db, query, vector, book_id, mode, _depth(limit))
            return assemble_context(
                candidates,
                lambda book, page: self._page(db, book, page)["text"],
                tokenizer,
                limit=limit,
                max_tokens=max_tokens,
            )

    def _search_candidates(
        self,
        db: sqlite3.Connection,
        query: str,
        vector: Sequence[float] | None,
        book_id: int | None,
        mode: str,
        depth: int,
    ) -> list[dict[str, Any]]:
        dense: list[int] = []
        lexical: list[int] = []
        similarities: dict[int, float] = {}
        if mode != "lexical":
            if vector is None or len(vector) != DIMENSIONS:
                raise ValueError(f"A {DIMENSIONS}-dimensional query embedding is required")
            if not all(math.isfinite(v) for v in vector):
                raise ValueError("Non-finite query embedding")
            sql = "SELECT chunk_id,distance FROM chunk_vectors WHERE embedding MATCH ? AND k=?"
            params: list[Any] = [sqlite_vec.serialize_float32(vector), depth]
            if book_id is not None:
                sql += " AND book_id=?"
                params.append(book_id)
            for row in db.execute(sql + " ORDER BY distance", params):
                dense.append(row["chunk_id"])
                similarities[row["chunk_id"]] = 1 - row["distance"]
        expression = fts_expression(query)
        if mode != "semantic" and expression:
            sql = (
                "SELECT c.id FROM chunks_fts JOIN chunks c ON c.id=chunks_fts.rowid "
                "WHERE chunks_fts MATCH ?"
            )
            params = [expression]
            if book_id is not None:
                sql += " AND c.book_id=?"
                params.append(book_id)
            params.append(depth)
            lexical = [
                r[0] for r in db.execute(sql + " ORDER BY bm25(chunks_fts),c.id LIMIT ?", params)
            ]
        ranking = (
            [(chunk_id, similarities[chunk_id]) for chunk_id in dense]
            if mode == "semantic"
            else fuse_ranks(dense, lexical)
        )
        dense_rank = {chunk_id: rank for rank, chunk_id in enumerate(dense, 1)}
        lexical_rank = {chunk_id: rank for rank, chunk_id in enumerate(lexical, 1)}
        hits = []
        for chunk_id, score in ranking[:depth]:
            hit = self._passage(db, chunk_id)
            hit.update(
                score=score,
                semantic_similarity=similarities.get(chunk_id),
                semantic_rank=dense_rank.get(chunk_id),
                lexical_rank=lexical_rank.get(chunk_id),
            )
            hits.append(hit)
        return hits

    def remove_book(self, book_id: int) -> dict[str, Any]:
        """Delete only index data; source files remain owned by their caller."""
        with self.connect() as db, db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("SELECT * FROM books WHERE id=?", (book_id,)).fetchone()
            if row is None:
                raise KeyError(f"Unknown book {book_id}")
            counts = {
                table: db.execute(
                    f"SELECT count(*) FROM {table} WHERE book_id=?", (book_id,)
                ).fetchone()[0]
                for table in ("pages", "chunks", "chunk_vectors")
            }
            db.execute("DELETE FROM books WHERE id=?", (book_id,))
            return {"book_id": book_id, "title": row["title"], "deleted": counts}

    def integrity(self) -> dict[str, Any]:
        """SQLite, foreign key, FTS5 and vector checks; INTEGRITY_OK when all pass."""
        with self.connect() as db:
            check = db.execute("PRAGMA integrity_check").fetchone()[0]
            foreign_keys = [tuple(row) for row in db.execute("PRAGMA foreign_key_check")]
            db.execute("INSERT INTO chunks_fts(chunks_fts,rank) VALUES('integrity-check',1)")
            db.commit()
            orphans = db.execute(
                "SELECT count(*) FROM chunk_vectors v LEFT JOIN chunks c ON c.id=v.chunk_id "
                "WHERE c.id IS NULL OR c.book_id!=v.book_id"
            ).fetchone()[0]
        return {
            "sqlite": check,
            "foreign_keys": foreign_keys,
            "orphan_vectors": orphans,
            "fts": "ok",
            "pending_chunks": self.stats()["pending_chunks"],
        }


def _depth(limit: int) -> int:
    """Candidates ranked per search: merging overlaps can turn several into one block."""
    return max(30, limit * 5)
