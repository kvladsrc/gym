"""Nearest screenshots for query vectors, with filters, one shot per game and a final ordering.

Closeness puts each space on one scale: 0 is the median cosine of two random screenshots and 1
the median of two screenshots of the same game (the index's calibration). Ranking "both" uses the
mean closeness of the two spaces. A text query is not an image: its cosines with screenshots lie
on another scale (about 0.05-0.2), so it gets no closeness and ranks by its raw similarity.
"""

from dataclasses import dataclass
from typing import Any

import numpy as np
import torch

from shotsearch import RANKINGS, SORTS, SPACES, review_range
from shotsearch.index import Index

MAX_LIMIT, MAX_POOL = 100, 2000


@dataclass(frozen=True)
class Filters:
    year_from: int | None = None
    year_to: int | None = None
    tags: tuple[str, ...] = ()
    genres: tuple[str, ...] = ()
    min_reviews: int = 0
    unseen_only: bool = False

    def active(self) -> bool:
        return self != Filters()

    def describe(self) -> str:
        parts = []
        if self.year_from or self.year_to:
            parts.append(f"years {self.year_from or ''}-{self.year_to or ''}")
        if self.tags:
            parts.append("tags " + ", ".join(self.tags))
        if self.genres:
            parts.append("genres " + ", ".join(self.genres))
        if self.min_reviews:
            parts.append(f"at least {self.min_reviews} reviews")
        if self.unseen_only:
            parts.append("games the forecaster never saw")
        return "; ".join(parts)


class Searcher:
    def __init__(self, index: Index) -> None:
        self.index = index
        games = index.games
        self.years = np.array([g["year"] or 0 for g in games], dtype=np.int32)
        self.reviews = np.array([g["reviews"] or 0 for g in games], dtype=np.int64)
        self.seen = np.array([bool(g["seen"]) for g in games])
        self.tags = [{t.lower() for t in g["tags"]} for g in games]
        self.genres = [{t.lower() for t in g["genres"]} for g in games]
        self.calibration = index.manifest["calibration"]

    def allowed_games(self, filters: Filters) -> np.ndarray:
        ok = np.ones(len(self.index.games), dtype=bool)
        if filters.year_from:
            ok &= self.years >= filters.year_from
        if filters.year_to:
            ok &= self.years <= filters.year_to
        if filters.min_reviews:
            ok &= self.reviews >= filters.min_reviews
        if filters.unseen_only:
            ok &= ~self.seen
        for wanted, have in ((filters.tags, self.tags), (filters.genres, self.genres)):
            want = {w.strip().lower() for w in wanted if w.strip()}
            if want:
                ok &= np.array([want <= h for h in have])
        return ok

    def similarities(self, space: str, query: np.ndarray) -> np.ndarray:
        """Cosine with every shot: a float16 product straight from the memory map (~0.05 s for
        500k shots; within 1e-4 of float32)."""
        matrix = torch.from_numpy(self.index.vectors[space])
        vector = torch.from_numpy(np.asarray(query, dtype=np.float16))
        return (matrix @ vector).float().numpy()

    def closeness(self, space: str, similarity):
        c = self.calibration[space]
        return (similarity - c["random_median"]) / (c["same_game_median"] - c["random_median"])

    def search(
        self,
        queries: dict[str, np.ndarray],
        *,
        ranking: str = "both",
        sort: str = "similarity",
        limit: int = 10,
        pool: int = 100,
        per_game: bool = True,
        filters: Filters | None = None,
        exclude_rows: frozenset[int] = frozenset(),
        exclude_games: frozenset[int] = frozenset(),
        uncalibrated: frozenset[str] = frozenset(),
    ) -> list[dict[str, Any]]:
        """Results ordered by `sort` among the `pool` nearest (games, or shots without per_game)."""
        if ranking not in RANKINGS:
            raise ValueError(f"ranking must be one of {', '.join(RANKINGS)}")
        if sort not in SORTS:
            raise ValueError(f"sort must be one of {', '.join(SORTS)}")
        if not 1 <= limit <= MAX_LIMIT:
            raise ValueError(f"limit must be 1-{MAX_LIMIT}")
        pool = max(pool, limit)
        if pool > MAX_POOL:
            raise ValueError(f"pool must be at most {MAX_POOL}")
        spaces = [s for s in SPACES if s in queries]
        if ranking != "both" and ranking not in queries:
            raise ValueError(f"The query has no {ranking} vector (text queries have meaning only)")
        sims = {s: self.similarities(s, queries[s]) for s in spaces}
        close = {s: sims[s] if s in uncalibrated else self.closeness(s, sims[s]) for s in spaces}
        key = (sum(close.values()) / len(close)) if ranking == "both" else close[ranking].copy()

        game = self.index.game
        allowed = self.allowed_games(filters or Filters())
        for g in exclude_games:
            allowed[g] = False
        mask = (game >= 0) & allowed[np.maximum(game, 0)]
        if exclude_rows:
            mask[list(exclude_rows)] = False
        key = np.where(mask, key, -np.inf)

        picked = self._nearest(key, pool, per_game)
        results = [self.describe(i, {s: sims[s][i] for s in spaces}, uncalibrated) for i in picked]
        if sort == "reviews":
            results.sort(key=lambda r: -(r["game"]["reviews"] or 0))
        elif sort == "forecast":
            results.sort(key=lambda r: -r["forecast"]["review_class"] if r["forecast"] else np.inf)
        return results[:limit]

    def _nearest(self, key: np.ndarray, pool: int, per_game: bool) -> list[int]:
        finite = int(np.isfinite(key).sum())
        if finite == 0:
            return []
        take = min(finite, pool * (12 if per_game else 1))
        while True:
            top = np.argpartition(-key, take - 1)[:take] if take < len(key) else np.arange(len(key))
            top = top[np.argsort(-key[top], kind="stable")]
            top = top[np.isfinite(key[top])]
            if not per_game:
                return [int(i) for i in top[:pool]]
            picked, games = [], set()
            for i in top:
                g = int(self.index.game[i])
                if g not in games:
                    games.add(g)
                    picked.append(int(i))
                    if len(picked) == pool:
                        return picked
            if take >= finite:
                return picked
            take = min(finite, take * 4)

    def describe(
        self, row: int, sims: dict[str, float], uncalibrated: frozenset[str] = frozenset()
    ) -> dict[str, Any]:
        index = self.index
        g = index.games[index.game[row]]
        forecast = None
        if np.isfinite(index.forecast[row]):
            positive = index.positive[row]
            forecast = {
                "review_class": round(float(index.forecast[row]), 2),
                "reviews": review_range(float(index.forecast[row])),
                "positive": round(float(positive), 1) if np.isfinite(positive) else None,
                "game_seen_in_training": g["seen"],
            }
        return {
            "shot": index.names[row].removesuffix(".jpg"),
            "image": str(index.shot_path(row)),
            "steam_url": f"https://store.steampowered.com/app/{g['appid']}/",
            "game": {
                "appid": g["appid"],
                "name": g["name"],
                "year": g["year"],
                "genres": g["genres"],
                "tags": g["tags"][:10],
                "reviews": g["reviews"],
                "positive": g["positive"],
            },
            "forecast": forecast,
            "closeness": {
                s: round(float(self.closeness(s, v)), 2)
                for s, v in sims.items()
                if s not in uncalibrated
            },
            "similarity": {s: round(float(v), 3) for s, v in sims.items()},
        }
