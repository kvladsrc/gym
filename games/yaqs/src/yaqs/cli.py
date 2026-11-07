"""YAQS command line: import books, search them and serve them to agents and people.

Results are printed to stdout as JSON; errors go to stderr as {"error": ...} with exit code 1.
"""

import argparse
import json
import sqlite3
import sys
from importlib import metadata
from pathlib import Path

from yaqs import DEFAULT_SEARCH_MODE, SEARCH_MODES, default_database
from yaqs.context import DEFAULT_CONTEXT_TOKENS, DEFAULT_PAGE_TOKENS
from yaqs.diagnostics import RUNTIME_HINT
from yaqs.present import DETAILS, compact_books

DEVICES = ("cuda", "cpu")


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(
        prog="yaqs", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    root.add_argument("--version", action="version", version=f"%(prog)s {_version()}")
    root.add_argument(
        "--db", type=Path, default=default_database(), help="database file (env: YAQS_DB)"
    )
    commands = root.add_subparsers(dest="command", required=True, metavar="COMMAND")

    def command(name: str, summary: str) -> argparse.ArgumentParser:
        return commands.add_parser(name, help=summary, description=summary)

    command("download", "download the pinned embedding model into the Hugging Face cache")
    command("init", "create an empty database")
    ingest = command("import", "import a UTF-8 TXT or JSONL book and embed its chunks")
    ingest.add_argument("source", type=Path, help="TXT or JSONL file")
    ingest.add_argument("--title", default="", help="override the title from the file")
    ingest.add_argument("--author", default="", help="override the author from the file")
    ingest.add_argument("--device", choices=DEVICES, default="cuda", help="default: cuda")
    ingest.add_argument("--batch-size", type=int, default=16, help="chunks per batch, 1-32")
    ingest.add_argument("--rebuild", action="store_true", help="replace an imported book")
    ingest.add_argument(
        "--wait-for-lock", action="store_true", help="wait for another writer to finish"
    )
    search = command("search", "search the library and print merged source blocks")
    search.add_argument("query")
    search.add_argument("--device", choices=DEVICES, default="cpu", help="default: cpu")
    search.add_argument("--limit", type=int, default=5, help="number of blocks, 1-20")
    search.add_argument(
        "--max-tokens",
        type=int,
        default=DEFAULT_CONTEXT_TOKENS,
        help="total text budget, 256-16384",
    )
    search.add_argument("--book", type=int, help="search one book id")
    search.add_argument(
        "--mode", choices=SEARCH_MODES, default=DEFAULT_SEARCH_MODE, help="default: %(default)s"
    )
    command("stats", "database size, counts and embedding profile")
    books = command("books", "list books and their indexing state")
    command("check", "check SQLite, FTS5, foreign key and vector integrity")
    command("doctor", "check packages, model cache and database without downloads")
    backup = command("backup", "write a consistent copy of the database to a new file")
    backup.add_argument("destination", type=Path)
    remove = command("remove", "remove a book, its chunks and vectors")
    remove.add_argument("book_id", type=int)
    remove.add_argument(
        "--wait-for-lock", action="store_true", help="wait for another writer to finish"
    )
    rebuild = command("rebuild", "re-chunk and re-embed every stored book into a new database")
    rebuild.add_argument("--output", required=True, type=Path, help="new database file")
    rebuild.add_argument("--device", choices=DEVICES, default="cuda", help="default: cuda")
    rebuild.add_argument("--batch-size", type=int, default=16, help="chunks per batch, 1-32")
    passage = command("passage", "read a chunk with neighboring context")
    passage.add_argument("chunk_id", type=int)
    passage.add_argument("--neighbors", type=int, default=0, help="chunks on each side, 0-3")
    passage.add_argument(
        "--max-tokens",
        type=int,
        default=DEFAULT_CONTEXT_TOKENS,
        help="total text budget, 256-16384",
    )
    page = command("page", "read a source page, in windows if it is long")
    page.add_argument("book_id", type=int)
    page.add_argument("page_number", type=int, help="0 for unpaginated text")
    page.add_argument("--start-char", type=int, default=0, help="continue at next_start_char")
    page.add_argument(
        "--max-tokens", type=int, default=DEFAULT_PAGE_TOKENS, help="window size, 256-16384"
    )
    for reader in (search, books, passage, page):
        reader.add_argument(
            "--detail", choices=DETAILS, default="compact", help="full adds every stored field"
        )
    serve = command("serve", "run the MCP server for agents")
    serve.add_argument(
        "--transport", choices=("stdio", "streamable-http"), default="streamable-http"
    )
    serve.add_argument("--host", default="127.0.0.1")
    serve.add_argument("--port", type=int, default=9033)
    serve.add_argument("--web", action="store_true", help="also serve the browser UI at /")
    web = command("web", "run the browser UI on its own port")
    web.add_argument("--host", default="127.0.0.1")
    web.add_argument("--port", type=int, default=9035)
    for subcommand in commands.choices.values():
        subcommand.add_argument("--db", type=Path, default=argparse.SUPPRESS, help="database file")
    return root


def _version() -> str:
    try:
        return metadata.version("yaqs")
    except metadata.PackageNotFoundError:
        return "unknown"


def main() -> None:
    args = parser().parse_args()
    try:
        result = execute(args)
    except KeyboardInterrupt as error:
        print(json.dumps({"error": "Interrupted"}), file=sys.stderr)
        raise SystemExit(130) from error
    except ImportError as error:
        print(
            json.dumps({"error": str(error), "hint": RUNTIME_HINT}),
            file=sys.stderr,
        )
        raise SystemExit(1) from error
    except (ValueError, RuntimeError, KeyError, OSError, sqlite3.Error) as error:
        print(json.dumps({"error": str(error)}, ensure_ascii=False), file=sys.stderr)
        raise SystemExit(1) from error
    if result is not None:
        print(json.dumps(result, ensure_ascii=False, indent=2))
        if args.command == "doctor" and not result["ready"]:
            raise SystemExit(1)


def execute(args: argparse.Namespace) -> object:
    """Run one command; heavy modules are imported only by the commands that need them."""
    if args.command == "doctor":
        # Reports missing packages itself, so nothing else is imported first.
        from yaqs.diagnostics import diagnose

        return diagnose(args.db)
    from yaqs.embedding import QwenEncoder, download
    from yaqs.store import INTEGRITY_OK, KnowledgeBase

    if args.command == "download":
        return {"model_directory": download()}
    if args.command == "init":
        return KnowledgeBase(args.db, create=True).stats()
    if args.command in {"backup", "rebuild"}:
        from yaqs.maintenance import backup_database, rebuild_database

        if args.command == "backup":
            return backup_database(args.db, args.destination)
        return rebuild_database(
            args.db, args.output, QwenEncoder(args.device), batch_size=args.batch_size
        )
    if args.command == "remove":
        from yaqs.locking import import_lock

        with import_lock(args.db, wait=args.wait_for_lock):
            return KnowledgeBase(args.db).remove_book(args.book_id)
    if args.command == "import":
        from yaqs.ingest import import_book

        return import_book(
            args.db,
            args.source,
            QwenEncoder(args.device),
            batch_size=args.batch_size,
            rebuild=args.rebuild,
            title=args.title,
            author=args.author,
            wait_for_lock=args.wait_for_lock,
        )
    if args.command in {"search", "serve", "web", "passage", "page"}:
        from yaqs.service import KnowledgeService

        service = KnowledgeService(args.db, device=getattr(args, "device", "cpu"))
        if args.command == "search":
            return service.search(
                args.query, args.limit, args.book, args.mode, args.max_tokens, args.detail
            )
        if args.command == "passage":
            return service.passage(args.chunk_id, args.neighbors, args.max_tokens, args.detail)
        if args.command == "page":
            return service.page(
                args.book_id, args.page_number, args.start_char, args.max_tokens, args.detail
            )
        if args.command == "web":
            from yaqs.web import run

            run(service, args.host, args.port)
            return None
        from yaqs.mcp import build_server

        server = build_server(service)
        if args.web:
            if args.transport == "stdio":
                raise ValueError("--web needs the streamable-http transport")
            from yaqs.web import mount

            mount(server, service, host=args.host)
        if args.transport == "stdio":
            server.run(transport="stdio")
        else:
            server.run(transport="streamable-http", host=args.host, port=args.port)
        return None
    store = KnowledgeBase(args.db)
    if args.command == "stats":
        return store.stats()
    if args.command == "books":
        return compact_books(store.books()) if args.detail == "compact" else store.books()
    if args.command == "check":
        result = store.integrity()
        if result != INTEGRITY_OK:
            raise RuntimeError(f"Index integrity check failed: {result}")
        return result
    raise AssertionError(f"Unhandled command {args.command}")
