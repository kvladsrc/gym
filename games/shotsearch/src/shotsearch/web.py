"""Browser UI over ShotSearch, served next to /mcp by `shotsearch serve`.

Requests must name a local host (or the host the server was bound to), so a web page from
elsewhere cannot use the server through DNS rebinding. Screenshots are served only by their
index id, uploads only by their content hash.
"""

from collections.abc import Awaitable, Callable
from pathlib import Path
from typing import Any

from starlette.concurrency import run_in_threadpool
from starlette.requests import Request
from starlette.responses import FileResponse, JSONResponse, Response

from shotsearch.search import Filters
from shotsearch.service import MAX_IMAGE_BYTES, ShotSearch

STATIC = Path(__file__).with_name("static")
FILES = {"/": "index.html", "/app.js": "app.js", "/app.css": "app.css"}
LOCAL_HOSTS = frozenset({"127.0.0.1", "localhost", "::1"})
SECURITY_HEADERS = {
    "Content-Security-Policy": "default-src 'none'; script-src 'self'; style-src 'self'; "
    "connect-src 'self'; img-src 'self' blob: data:; base-uri 'none'; form-action 'self'; "
    "frame-ancestors 'none'",
    "X-Content-Type-Options": "nosniff",
    "Referrer-Policy": "no-referrer",
}
NO_STORE = {**SECURITY_HEADERS, "Cache-Control": "no-store"}
IMMUTABLE = {**SECURITY_HEADERS, "Cache-Control": "max-age=86400"}

Endpoint = Callable[[Request], Awaitable[Response]]


def _int(request: Request, name: str, default: int | None = None) -> int | None:
    value = request.query_params.get(name, "").strip()
    if not value:
        return default
    try:
        return int(value)
    except ValueError as error:
        raise ValueError(f"{name} must be an integer") from error


def _flag(request: Request, name: str, default: bool) -> bool:
    value = request.query_params.get(name, "")
    return default if value == "" else value in ("1", "true", "yes", "on")


def _list(request: Request, name: str) -> list[str]:
    return [v.strip() for v in request.query_params.get(name, "").split(",") if v.strip()]


def endpoints(service: ShotSearch, *, host: str = "127.0.0.1") -> dict[str, tuple[str, Endpoint]]:
    """Routes by path: (method, endpoint)."""
    allowed = LOCAL_HOSTS | {host.strip("[]")}

    def guard(request: Request) -> Response | None:
        if request.url.hostname not in allowed:
            return JSONResponse({"error": "Host not allowed"}, 403, headers=NO_STORE)
        return None

    def api(handler: Callable[[Request], Awaitable[Any]]) -> Endpoint:
        async def endpoint(request: Request) -> Response:
            if refused := guard(request):
                return refused
            try:
                result = await handler(request)
            except KeyError as error:
                return JSONResponse({"error": str(error.args[0])}, 404, headers=NO_STORE)
            except ValueError as error:
                return JSONResponse({"error": str(error)}, 400, headers=NO_STORE)
            except (RuntimeError, ImportError, OSError) as error:
                return JSONResponse({"error": str(error)}, 500, headers=NO_STORE)
            if isinstance(result, Response):
                return result
            return JSONResponse(result, headers=NO_STORE)

        return endpoint

    def static(name: str) -> Endpoint:
        async def endpoint(request: Request) -> Response:
            return guard(request) or FileResponse(STATIC / name, headers=NO_STORE)

        return endpoint

    async def search(request: Request) -> Any:
        params = request.query_params
        filters = Filters(
            year_from=_int(request, "year_from"),
            year_to=_int(request, "year_to"),
            tags=tuple(_list(request, "tags")),
            genres=tuple(_list(request, "genres")),
            min_reviews=_int(request, "min_reviews", 0),
            unseen_only=_flag(request, "unseen_only", False),
        )
        images = params.getlist("upload") + params.getlist("image")
        return await run_in_threadpool(
            service.search,
            images,
            params.getlist("shot"),
            params.get("text", ""),
            ranking=params.get("ranking", "both"),
            sort=params.get("sort", "similarity"),
            limit=_int(request, "limit", 24),
            pool=_int(request, "pool", 100),
            per_game=_flag(request, "per_game", True),
            include_query_games=_flag(request, "include_query_games", False),
            filters=filters,
        )

    async def upload(request: Request) -> Any:
        data = await request.body()
        if len(data) > MAX_IMAGE_BYTES:
            raise ValueError("Image is larger than 40 MB")
        return await run_in_threadpool(service.upload, data)

    async def fetch(request: Request) -> Any:
        return await run_in_threadpool(service.upload_url, request.query_params.get("url", ""))

    async def game(request: Request) -> Any:
        return await run_in_threadpool(service.game, int(request.path_params["appid"]))

    async def info(request: Request) -> Any:
        return service.info()

    async def shot(request: Request) -> Any:
        row = service.index.row(request.path_params["shot"])
        return FileResponse(service.index.shot_path(row), headers=IMMUTABLE)

    async def uploaded(request: Request) -> Any:
        path = service.upload_path(request.path_params["upload_id"])
        return FileResponse(path, headers=IMMUTABLE)

    routes: dict[str, tuple[str, Endpoint]] = {
        path: ("GET", static(name)) for path, name in FILES.items()
    }
    routes |= {
        "/api/info": ("GET", api(info)),
        "/api/search": ("GET", api(search)),
        "/api/upload": ("POST", api(upload)),
        "/api/fetch": ("POST", api(fetch)),
        "/api/game/{appid:int}": ("GET", api(game)),
        "/shot/{shot}": ("GET", api(shot)),
        "/upload/{upload_id}": ("GET", api(uploaded)),
    }
    return routes


def mount(server: Any, service: ShotSearch, *, host: str = "127.0.0.1") -> None:
    """Add the UI routes to an MCPServer before it starts serving streamable HTTP."""
    for path, (method, endpoint) in endpoints(service, host=host).items():
        server.custom_route(path, methods=[method])(endpoint)


def build_app(service: ShotSearch, *, host: str = "127.0.0.1"):
    """The UI alone, as a Starlette app (tests)."""
    from starlette.applications import Starlette
    from starlette.routing import Route

    routes = endpoints(service, host=host)
    return Starlette(
        routes=[Route(path, endpoint, methods=[m]) for path, (m, endpoint) in routes.items()]
    )
