"""JSON CLI and shared local MCP endpoint for the game development library."""

import argparse
import json
import sqlite3
import sys
from pathlib import Path

from yaqs import DEFAULT_SEARCH_MODE, SEARCH_MODES, default_database
from yaqs.context import DEFAULT_CONTEXT_TOKENS


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--db", type=Path, default=default_database())
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("download")
    commands.add_parser("init")
    ingest = commands.add_parser("import")
    ingest.add_argument("source", type=Path)
    ingest.add_argument("--title", default="")
    ingest.add_argument("--author", default="")
    ingest.add_argument("--device", choices=("cuda", "cpu"), default="cuda")
    ingest.add_argument("--batch-size", type=int, default=16)
    ingest.add_argument("--rebuild", action="store_true")
    ingest.add_argument("--wait-for-lock", action="store_true")
    search = commands.add_parser("search")
    search.add_argument("query")
    search.add_argument("--device", choices=("cuda", "cpu"), default="cpu")
    search.add_argument("--limit", type=int, default=5)
    search.add_argument("--max-tokens", type=int, default=DEFAULT_CONTEXT_TOKENS)
    search.add_argument("--book", type=int)
    search.add_argument("--mode", choices=SEARCH_MODES, default=DEFAULT_SEARCH_MODE)
    commands.add_parser("stats")
    commands.add_parser("books")
    commands.add_parser("check")
    commands.add_parser("doctor")
    backup = commands.add_parser("backup")
    backup.add_argument("destination", type=Path)
    remove = commands.add_parser("remove")
    remove.add_argument("book_id", type=int)
    remove.add_argument("--wait-for-lock", action="store_true")
    rebuild = commands.add_parser("rebuild")
    rebuild.add_argument("--output", required=True, type=Path)
    rebuild.add_argument("--device", choices=("cuda", "cpu"), default="cuda")
    rebuild.add_argument("--batch-size", type=int, default=16)
    passage = commands.add_parser("passage")
    passage.add_argument("chunk_id", type=int)
    passage.add_argument("--neighbors", type=int, default=0)
    passage.add_argument("--max-tokens", type=int, default=DEFAULT_CONTEXT_TOKENS)
    page = commands.add_parser("page")
    page.add_argument("book_id", type=int)
    page.add_argument("page_number", type=int)
    serve = commands.add_parser("serve")
    serve.add_argument(
        "--transport", choices=("stdio", "streamable-http"), default="streamable-http"
    )
    serve.add_argument("--host", default="127.0.0.1")
    serve.add_argument("--port", type=int, default=9033)
    for command in commands.choices.values():
        command.add_argument("--db", type=Path, default=argparse.SUPPRESS)
    args = parser.parse_args()
    try:
        result = execute(args)
    except KeyboardInterrupt as error:
        print(json.dumps({"error": "Interrupted"}), file=sys.stderr)
        raise SystemExit(130) from error
    except ImportError as error:
        print(
            json.dumps({"error": str(error), "hint": "Use Nix/just; run just yaqs setup"}),
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


def execute(args: argparse.Namespace):
    from yaqs.embedding import QwenEncoder, download
    from yaqs.store import KnowledgeBase

    if args.command == "download":
        return {"model_directory": download()}
    if args.command == "init":
        return KnowledgeBase(args.db, create=True).stats()
    if args.command == "doctor":
        from yaqs.diagnostics import diagnose

        return diagnose(args.db)
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
    if args.command in {"search", "serve", "passage"}:
        from yaqs.service import KnowledgeService

        service = KnowledgeService(args.db, device=getattr(args, "device", "cpu"))
        if args.command == "search":
            return service.search(args.query, args.limit, args.book, args.mode, args.max_tokens)
        if args.command == "passage":
            return service.passage(args.chunk_id, args.neighbors, args.max_tokens)
        from yaqs.mcp import build_server

        server = build_server(service)
        if args.transport == "stdio":
            server.run(transport="stdio")
        else:
            server.run(transport="streamable-http", host=args.host, port=args.port)
        return None
    store = KnowledgeBase(args.db)
    if args.command == "stats":
        return store.stats()
    if args.command == "books":
        return store.books()
    if args.command == "check":
        result = store.integrity()
        if result != {
            "sqlite": "ok",
            "foreign_keys": [],
            "orphan_vectors": 0,
            "fts": "ok",
            "pending_chunks": 0,
        }:
            raise RuntimeError(f"Index integrity check failed: {result}")
        return result
    if args.command == "page":
        return store.page(args.book_id, args.page_number)
