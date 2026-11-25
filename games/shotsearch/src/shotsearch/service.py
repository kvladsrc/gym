"""Operations shared by the command line, the MCP server and the browser UI.

A query is any mix of images (local paths, http(s) URLs or uploaded files), screenshots of the
index (APPID_NN) and one text. Each item gives a unit vector per space it has (a text gives meaning
only); a space's query vector is the normalised mean of its items' vectors.
"""

import hashlib
import io
import re
import threading
from collections import OrderedDict
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, UnidentifiedImageError

from shotsearch import SPACES, default_home
from shotsearch.encoders import Encoders, unit
from shotsearch.index import Index
from shotsearch.search import Filters, Searcher
from shotsearch.sheet import render

MAX_IMAGE_BYTES = 40 * 1024 * 1024
MAX_ITEMS = 16
UPLOAD_ID = re.compile(r"^[0-9a-f]{16}$")
CACHED_VECTORS = 256

NOTES = {
    "closeness": "Per space: 0 = two random Steam screenshots, 1 = two screenshots of one game "
    "(median); above 1 is closer than usual for one game. A text query has no closeness: its "
    "similarity to screenshots is on another scale (about 0.05-0.2); compare it within one query.",
    "forecast": "Expected Steam review-count class predicted from this screenshot with its game's "
    "year, genres and tags; game_seen_in_training=true means the forecaster learnt this game's "
    "answer, so its forecast is biased towards the truth.",
}


def _open_image(data: bytes) -> Image.Image:
    if len(data) > MAX_IMAGE_BYTES:
        raise ValueError("Image is larger than 40 MB")
    try:
        image = Image.open(io.BytesIO(data))
        image.load()
    except (UnidentifiedImageError, OSError) as error:
        raise ValueError(f"Not a readable image: {error}") from error
    return image.convert("RGB")


class ShotSearch:
    def __init__(self, index: Path, *, home: Path | None = None, idle_s: float = 300) -> None:
        self.index = Index.load(index)
        self.searcher = Searcher(self.index)
        self.encoders = Encoders(idle_s=idle_s)
        self.home = home or default_home()
        self.uploads = self.home / "uploads"
        self.sheets = self.home / "sheets"
        self.cache: OrderedDict[str, dict[str, np.ndarray]] = OrderedDict()
        self.lock = threading.Lock()

    # Query items -------------------------------------------------------------------------------

    def _image_bytes(self, source: str) -> bytes:
        if source.startswith(("http://", "https://")):
            import httpx

            with httpx.stream("GET", source, follow_redirects=True, timeout=30) as response:
                response.raise_for_status()
                data = b""
                for part in response.iter_bytes():
                    data += part
                    if len(data) > MAX_IMAGE_BYTES:
                        raise ValueError(f"{source} is larger than 40 MB")
            return data
        path = Path(source).expanduser()
        if UPLOAD_ID.match(source):
            path = self.upload_path(source)
        if not path.is_file():
            raise ValueError(f"No image file {source}")
        return path.read_bytes()

    def _vectors(self, data: bytes) -> dict[str, np.ndarray]:
        digest = hashlib.sha256(data).hexdigest()
        if digest in self.cache:
            self.cache.move_to_end(digest)
            return self.cache[digest]
        vectors = {s: v[0] for s, v in self.encoders.images([_open_image(data)]).items()}
        self.cache[digest] = vectors
        if len(self.cache) > CACHED_VECTORS:
            self.cache.popitem(last=False)
        return vectors

    def upload(self, data: bytes) -> dict[str, Any]:
        """Keep an uploaded image under its content hash; the id can then be used as an image."""
        image = _open_image(data)
        upload_id = hashlib.sha256(data).hexdigest()[:16]
        self.uploads.mkdir(parents=True, exist_ok=True)
        path = self.uploads / f"{upload_id}.jpg"
        if not path.exists():
            image.save(path, quality=92)
        return {"id": upload_id, "width": image.width, "height": image.height}

    def upload_url(self, url: str) -> dict[str, Any]:
        """Download an http(s) image and keep it as an upload."""
        if not url.startswith(("http://", "https://")):
            raise ValueError("Give an http(s) URL")
        return {**self.upload(self._image_bytes(url)), "url": url}

    def upload_path(self, upload_id: str) -> Path:
        if not UPLOAD_ID.match(upload_id):
            raise ValueError("Bad upload id")
        path = self.uploads / f"{upload_id}.jpg"
        if not path.exists():
            raise KeyError(f"Unknown upload {upload_id}")
        return path

    # Operations --------------------------------------------------------------------------------

    def search(
        self,
        images: list[str] = (),
        shots: list[str] = (),
        text: str = "",
        *,
        ranking: str = "both",
        sort: str = "similarity",
        limit: int = 10,
        pool: int = 100,
        per_game: bool = True,
        include_query_games: bool = False,
        filters: Filters | None = None,
        sheet: bool = False,
    ) -> dict[str, Any]:
        images, shots, text = list(images), list(shots), text.strip()
        if not images and not shots and not text:
            raise ValueError("Give images, shots or text")
        if text and (images or shots):
            raise ValueError("Search by text alone: text and images are on different scales")
        if len(images) + len(shots) > MAX_ITEMS:
            raise ValueError(f"At most {MAX_ITEMS} images and shots in one query")
        with self.lock:
            per_space: dict[str, list[np.ndarray]] = {s: [] for s in SPACES}
            pictures: list[tuple[str, Image.Image]] = []
            for source in images:
                data = self._image_bytes(source)
                for s, v in self._vectors(data).items():
                    per_space[s].append(v)
                if sheet:
                    pictures.append(("query", _open_image(data)))
            rows = [self.index.row(shot) for shot in shots]
            for row in rows:
                for s in SPACES:
                    per_space[s].append(self.index.vectors[s][row].astype(np.float32))
                if sheet:
                    pictures.append(("query " + self.index.names[row][:-4], self._shot(row)))
            if text:
                per_space["meaning"].append(self.encoders.text(text))
            queries = {s: unit(np.mean(v, axis=0)) for s, v in per_space.items() if v}
            exclude_games = (
                frozenset()
                if include_query_games
                else frozenset(
                    int(self.index.game[row]) for row in rows if self.index.game[row] >= 0
                )
            )
            results = self.searcher.search(
                queries,
                ranking=ranking,
                sort=sort,
                limit=limit,
                pool=pool,
                per_game=per_game,
                filters=filters or Filters(),
                exclude_rows=frozenset(rows),
                exclude_games=exclude_games,
                uncalibrated=frozenset({"meaning"}) if text else frozenset(),
            )
            answer = {
                "query": {"images": images, "shots": shots, "text": text, "spaces": list(queries)},
                "ranking": ranking,
                "sort": sort,
                "pool": max(pool, limit),
                "results": results,
                "notes": NOTES,
            }
            if sheet:
                title = f"ranking {ranking}, sorted by {sort} among the {max(pool, limit)} nearest"
                if text:
                    title = f'"{text[:60]}": ' + title
                if filters and filters.active():
                    title += "; " + filters.describe()
                path = render(pictures, results, self.sheets, title)
                answer["sheet"] = str(path)
        return answer

    def _shot(self, row: int) -> Image.Image:
        with Image.open(self.index.shot_path(row)) as image:
            return image.convert("RGB")

    def game(self, appid: int) -> dict[str, Any]:
        """A game of the index with each of its screenshots and their forecasts."""
        game_row = self.index.game_row(appid)
        g = self.index.games[game_row]
        shots = []
        for row in np.flatnonzero(self.index.game == game_row):
            described = self.searcher.describe(int(row), {})
            shots.append({k: described[k] for k in ("shot", "image", "forecast")})
        return {
            **g,
            "steam_url": f"https://store.steampowered.com/app/{appid}/",
            "shots": shots,
            "notes": {"forecast": NOTES["forecast"]},
        }

    def info(self) -> dict[str, Any]:
        m = self.index.manifest
        return {
            "shots": m["shots"],
            "games": m["games"],
            "forecasts": m["forecasts"],
            "built": m["built"],
            "models": m["models"],
            "calibration": m["calibration"],
            "loaded_models": self.encoders.loaded(),
            "notes": NOTES,
        }
