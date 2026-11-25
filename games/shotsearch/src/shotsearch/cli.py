"""Shot Search command line: build the index, search it, serve it to agents and people.

Results are printed to stdout as JSON; errors go to stderr as {"error": ...} with exit code 1.
Everything runs on the CPU.
"""

import argparse
import json
import os
import re
import sys
from pathlib import Path

from shotsearch import RANKINGS, SORTS, default_home

SHOT_ID = re.compile(r"^\d+_\d{2}$")
DEFAULT_PORT = 9150


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(
        prog="shotsearch", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    root.add_argument(
        "--index",
        type=Path,
        default=default_home() / "index",
        help="index directory (default: $SHOTSEARCH_HOME/index, ~/.cache/shotsearch/index)",
    )
    commands = root.add_subparsers(dest="command", required=True, metavar="COMMAND")

    def command(name: str, summary: str) -> argparse.ArgumentParser:
        return commands.add_parser(name, help=summary, description=summary)

    command("download", "download both models into the Hugging Face cache")
    build = command("build", "build the index from a scoring run (see shotsearch.index)")
    build.add_argument("run", type=Path, help="run folder with siglip/, dino/ and studio/")
    build.add_argument("shots", type=Path, help="folder of the screenshots (APPID_NN.jpg)")
    build.add_argument("labels", type=Path, nargs="+", help="games, JSON lines")
    command("info", "index counts, models and calibration")

    search = command("search", "search and print the results")
    search.add_argument("queries", nargs="*", help="image paths, URLs or shot ids (APPID_NN)")
    search.add_argument("--text", default="")
    search.add_argument("--ranking", choices=RANKINGS, default="both")
    search.add_argument("--sort", choices=SORTS, default="similarity")
    search.add_argument("--limit", type=int, default=10)
    search.add_argument("--pool", type=int, default=100)
    search.add_argument("--year-from", type=int)
    search.add_argument("--year-to", type=int)
    search.add_argument("--tags", default="", help="comma-separated, all required")
    search.add_argument("--genres", default="", help="comma-separated, all required")
    search.add_argument("--min-reviews", type=int, default=0)
    search.add_argument("--unseen-only", action="store_true")
    search.add_argument("--all-shots", action="store_true", help="several shots per game")
    search.add_argument("--sheet", action="store_true", help="also render a contact sheet")

    serve = command("serve", "serve MCP at /mcp and the browser UI at /")
    serve.add_argument("--host", default="127.0.0.1")
    serve.add_argument("--port", type=int, default=DEFAULT_PORT)
    serve.add_argument(
        "--idle", type=float, default=300, help="seconds before unused models are dropped"
    )
    return root


def _split(value: str) -> tuple[str, ...]:
    return tuple(v.strip() for v in value.split(",") if v.strip())


def run(args: argparse.Namespace):
    if args.command == "download":
        from shotsearch.encoders import download

        download()
        return None
    if args.command == "build":
        from shotsearch.index import build

        return build(
            args.run, args.labels, args.shots, args.index, log=lambda m: print(m, file=sys.stderr)
        )

    from shotsearch.service import ShotSearch

    if args.command == "serve":
        from shotsearch.mcp import build_server
        from shotsearch.web import mount

        service = ShotSearch(args.index, idle_s=args.idle)
        server = build_server(service)
        mount(server, service, host=args.host)
        print(f"shotsearch: http://{args.host}:{args.port}/ (MCP at /mcp)", file=sys.stderr)
        server.run(transport="streamable-http", host=args.host, port=args.port)
        return None
    service = ShotSearch(args.index, idle_s=0)
    if args.command == "info":
        return service.info()
    from shotsearch.search import Filters

    shots = [q for q in args.queries if SHOT_ID.match(q)]
    images = [q for q in args.queries if not SHOT_ID.match(q)]
    filters = Filters(
        year_from=args.year_from,
        year_to=args.year_to,
        tags=_split(args.tags),
        genres=_split(args.genres),
        min_reviews=args.min_reviews,
        unseen_only=args.unseen_only,
    )
    return service.search(
        images,
        shots,
        args.text,
        ranking=args.ranking,
        sort=args.sort,
        limit=args.limit,
        pool=args.pool,
        per_game=not args.all_shots,
        filters=filters,
        sheet=args.sheet,
    )


def main(argv: list[str] | None = None) -> int:
    # Queries run on the CPU; keep CUDA out of reach so the GPU stays free for other work.
    os.environ["CUDA_VISIBLE_DEVICES"] = ""
    args = parser().parse_args(argv)
    try:
        result = run(args)
    except (ValueError, KeyError, FileNotFoundError, OSError) as error:
        message = error.args[0] if isinstance(error, KeyError) and error.args else str(error)
        print(json.dumps({"error": message}), file=sys.stderr)
        return 1
    if result is not None:
        print(json.dumps(result, ensure_ascii=False, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
