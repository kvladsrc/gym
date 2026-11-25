"""MCP tools over ShotSearch; every client of one server shares its models and index."""

from functools import partial
from typing import Any, Literal

from mcp.server.mcpserver import MCPServer
from starlette.concurrency import run_in_threadpool

from shotsearch.search import Filters
from shotsearch.service import ShotSearch

INSTRUCTIONS = """\
Search about 500,000 Steam store screenshots (about 127,000 games, 2013-2026) for ones like a
query, each with its game's real Steam reviews and a review forecast for that screenshot.

Query with images (local file paths or http(s) URLs) and/or screenshots of the index (shot ids
such as 967050_00, from earlier results: "more like this one"); several are averaged into one
query. Or query with one English text alone: it searches meaning only, and its results carry a
raw similarity (about 0.05-0.2) instead of closeness.

ranking chooses what "like" means:
- look: DINOv2 on the shot in grey. How the frame is built: layout, camera, depth, forms, light
  and dark, screen composition (e.g. a scene above and a row of cards below). Ignores colour.
- meaning: SigLIP 2. What is shown: setting, characters, genre, mood. Text queries use it.
- both (default): the mean of the two closenesses.

closeness: 0 = two random Steam screenshots, 1 = two screenshots of the same game (median);
above 1 is unusually close. The nearest results of a typical query lie around 0.8-1.3.

sort orders the `pool` nearest games (one shot per game unless per_game=false): similarity
(default), reviews (real review count: what found an audience) or forecast (the model's
expected review class). forecast classes: 0 none, 1 = 1-2 reviews, 2 = 3-9, 3 = 10-29,
4 = 30-99, 5 = 100-299, 6 = 300-999, 7 = 1,000-2,999, 8 = 3,000-9,999, 9 = 10,000+. The
forecaster trained on most of these games (game_seen_in_training=true): their forecast leans on
the answer it learnt; unseen_only=true keeps games it never saw.

Each result gives `image`, a local path you can open to look at the screenshot; sheet=true also
renders one labelled contact sheet of the query and results and returns its path: look at that
one image rather than many. Screenshots are store images of other people's games: use them as
references, never as assets."""


def build_server(service: ShotSearch) -> MCPServer:
    server = MCPServer("shotsearch", instructions=INSTRUCTIONS)

    @server.tool()
    async def search_screenshots(
        images: list[str] | None = None,
        shots: list[str] | None = None,
        text: str = "",
        ranking: Literal["both", "look", "meaning"] = "both",
        sort: Literal["similarity", "reviews", "forecast"] = "similarity",
        limit: int = 10,
        pool: int = 100,
        year_from: int | None = None,
        year_to: int | None = None,
        tags: list[str] | None = None,
        genres: list[str] | None = None,
        min_reviews: int = 0,
        unseen_only: bool = False,
        per_game: bool = True,
        include_query_games: bool = False,
        sheet: bool = False,
    ) -> dict[str, Any]:
        """Find Steam screenshots like the query images, shots or text.

        Returns up to `limit` results ordered by `sort` among the `pool` nearest. tags and genres
        (Steam's, e.g. "Horror", "Card Battler"; case-insensitive) must all be present.
        include_query_games=false leaves out the games of query shots.
        """
        filters = Filters(
            year_from=year_from,
            year_to=year_to,
            tags=tuple(tags or ()),
            genres=tuple(genres or ()),
            min_reviews=min_reviews,
            unseen_only=unseen_only,
        )
        call = partial(
            service.search,
            images or [],
            shots or [],
            text,
            ranking=ranking,
            sort=sort,
            limit=limit,
            pool=pool,
            per_game=per_game,
            include_query_games=include_query_games,
            filters=filters,
            sheet=sheet,
        )
        return await run_in_threadpool(call)

    @server.tool()
    async def get_game(appid: int) -> dict[str, Any]:
        """A game of the index: name, year, genres, tags, real reviews and its screenshots."""
        return await run_in_threadpool(service.game, appid)

    @server.tool()
    async def index_info() -> dict[str, Any]:
        """Counts, models and the calibration behind closeness."""
        return service.info()

    return server
