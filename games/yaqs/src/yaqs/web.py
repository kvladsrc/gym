"""Read-only browser UI over KnowledgeService: search, passages, pages and books.

`yaqs web` serves it on its own port; `yaqs serve --web` adds the same routes next to /mcp,
so the browser and agents share one embedder. Calls to the service run one at a time in a
worker thread. Requests must name a local host (or the host the server was bound to), so a
web page from elsewhere cannot read the library through DNS rebinding. The page renders
book text as text, never as HTML. API calls reach the query log as interface "web".
"""

import sqlite3
import threading
from collections.abc import Awaitable, Callable
from pathlib import Path
from typing import Any

from starlette.applications import Starlette
from starlette.concurrency import run_in_threadpool
from starlette.requests import Request
from starlette.responses import FileResponse, JSONResponse, Response
from starlette.routing import Route

from yaqs import DEFAULT_SEARCH_MODE, SEARCH_MODES
from yaqs.context import DEFAULT_CONTEXT_TOKENS, DEFAULT_PAGE_TOKENS
from yaqs.querylog import Caller
from yaqs.service import KnowledgeService

STATIC = Path(__file__).with_name("static")
FILES = {"/": "index.html", "/app.js": "app.js", "/app.css": "app.css"}
LOCAL_HOSTS = frozenset({"127.0.0.1", "localhost", "::1"})
WEB = Caller("web")
SECURITY_HEADERS = {
    "Cache-Control": "no-store",
    "Content-Security-Policy": "default-src 'none'; script-src 'self'; style-src 'self'; "
    "connect-src 'self'; img-src 'self' data:; base-uri 'none'; form-action 'self'; "
    "frame-ancestors 'none'",
    "X-Content-Type-Options": "nosniff",
    "Referrer-Policy": "no-referrer",
}

Endpoint = Callable[[Request], Awaitable[Response]]


def _integer(request: Request, name: str, default: int | None = None) -> int | None:
    value = request.query_params.get(name, "")
    if not value:
        return default
    try:
        return int(value)
    except ValueError as error:
        raise ValueError(f"{name} must be an integer") from error


def endpoints(service: KnowledgeService, *, host: str = "127.0.0.1") -> dict[str, Endpoint]:
    """GET routes of the UI by path; the same endpoints mount on the MCP server."""
    lock = threading.Lock()
    allowed = LOCAL_HOSTS | {host.strip("[]")}

    def locked(function: Callable[..., Any], *args: Any, **kwargs: Any) -> Any:
        with lock:
            return function(*args, **kwargs)

    def guard(request: Request) -> Response | None:
        if request.url.hostname not in allowed:
            return JSONResponse({"error": "Host not allowed"}, 403, headers=SECURITY_HEADERS)
        return None

    def api(handler: Callable[[Request], tuple[Callable[..., Any], tuple]]) -> Endpoint:
        async def endpoint(request: Request) -> Response:
            if refused := guard(request):
                return refused
            try:
                function, args = handler(request)
                result = await run_in_threadpool(locked, function, *args, caller=WEB)
            except KeyError as error:
                return JSONResponse({"error": error.args[0]}, 404, headers=SECURITY_HEADERS)
            except ValueError as error:
                return JSONResponse({"error": str(error)}, 400, headers=SECURITY_HEADERS)
            except (RuntimeError, ImportError, OSError, sqlite3.Error) as error:
                # A missing model or a broken runtime: show the reason, not a bare 500.
                return JSONResponse({"error": str(error)}, 500, headers=SECURITY_HEADERS)
            return JSONResponse(result, headers=SECURITY_HEADERS)

        return endpoint

    def static(name: str) -> Endpoint:
        async def endpoint(request: Request) -> Response:
            return guard(request) or FileResponse(STATIC / name, headers=SECURITY_HEADERS)

        return endpoint

    def search(request: Request):
        query = request.query_params.get("q", "")
        mode = request.query_params.get("mode", DEFAULT_SEARCH_MODE)
        if mode not in SEARCH_MODES:
            raise ValueError(f"mode must be one of {', '.join(SEARCH_MODES)}")
        limit = _integer(request, "limit", 10)
        book = _integer(request, "book")
        budget = DEFAULT_CONTEXT_TOKENS * 2
        return service.search, (query, limit, book, mode, budget, "compact")

    def passage(request: Request):
        chunk = int(request.path_params["chunk_id"])
        neighbors = _integer(request, "neighbors", 1)
        return service.passage, (chunk, neighbors, DEFAULT_CONTEXT_TOKENS, "compact")

    def page(request: Request):
        book, number = int(request.path_params["book_id"]), int(request.path_params["page"])
        start = _integer(request, "start", 0)
        return service.page, (book, number, start, DEFAULT_PAGE_TOKENS, "compact")

    routes = {path: static(name) for path, name in FILES.items()}
    routes |= {
        "/api/books": api(lambda request: (service.books, ("compact",))),
        "/api/stats": api(lambda request: (service.stats, ())),
        "/api/search": api(search),
        "/api/passage/{chunk_id:int}": api(passage),
        "/api/page/{book_id:int}/{page:int}": api(page),
    }
    return routes


def build_app(service: KnowledgeService, *, host: str = "127.0.0.1") -> Starlette:
    routes = endpoints(service, host=host)
    return Starlette(routes=[Route(path, endpoint) for path, endpoint in routes.items()])


def mount(server: Any, service: KnowledgeService, *, host: str = "127.0.0.1") -> None:
    """Add the UI routes to an MCPServer before it starts serving streamable HTTP."""
    for path, endpoint in endpoints(service, host=host).items():
        server.custom_route(path, methods=["GET"])(endpoint)


def run(service: KnowledgeService, host: str, port: int) -> None:
    """Serve the UI until interrupted (yaqs web)."""
    import uvicorn

    uvicorn.run(build_app(service, host=host), host=host, port=port, log_level="warning")
