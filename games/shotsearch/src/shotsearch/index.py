"""The index: unit embeddings of every screenshot in both spaces, their forecasts and the games.

`shotsearch build` makes it once from the outputs of a scoring run:

    RUN/siglip/chunk_*.npz      names + emb: pooled SigLIP 2 image embeddings
    RUN/dino/chunk_*.npz        names + gray_mean: DINOv2 mean patch token of the shot in grey
    RUN/studio/*_predictions.jsonl
                                appid, shot, review_class, positive: the forecast per shot
    LABELS (jsonl, one or more) a game per line: appid, name, year, genres, tags, reviews,
                                positive, split; the first file naming a game gives its data,
                                and split "train" in any file marks a game the forecaster saw

Layout of the index directory:

    manifest.json   counts, models, the shots directory and each space's calibration
    names.json      screenshot file names (APPID_NN.jpg) in index order
    meaning.npy     float16 (N, D) unit vectors, one per shot, likewise look.npy
    shots.npz       game (row in games.json, -1: unknown), forecast class and positive share
    games.json      the games: appid, name, year, genres, tags, reviews, positive, seen
"""

import json
import os
from dataclasses import dataclass, field
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import numpy as np

from shotsearch import LOOK_MODEL, MEANING_MODEL, SPACES

FORMAT = 1
SOURCES = {"meaning": ("siglip", "emb"), "look": ("dino", "gray_mean")}
CALIBRATION_PAIRS = 20000


def _chunks(folder: Path) -> list[Path]:
    chunks = sorted(folder.glob("chunk_*.npz"))
    if not chunks:
        raise FileNotFoundError(f"No chunk_*.npz in {folder}")
    return chunks


def _calibrate(vectors: np.ndarray, games: np.ndarray, seed: int = 0) -> dict[str, float]:
    """Median cosine of random shot pairs and of two shots of one game (neighbours in order)."""
    rng = np.random.default_rng(seed)
    n = len(vectors)
    a, b = rng.integers(0, n, CALIBRATION_PAIRS), rng.integers(0, n, CALIBRATION_PAIRS)
    random = (vectors[a].astype(np.float32) * vectors[b].astype(np.float32)).sum(1)
    pairs = np.sort(rng.integers(0, n - 1, CALIBRATION_PAIRS))
    pairs = pairs[(games[pairs] == games[pairs + 1]) & (games[pairs] >= 0)]
    if len(pairs) == 0:
        raise ValueError("No two shots of one game to calibrate with")
    same = (vectors[pairs].astype(np.float32) * vectors[pairs + 1].astype(np.float32)).sum(1)
    return {
        "random_median": float(np.median(random)),
        "random_p99": float(np.percentile(random, 99)),
        "same_game_median": float(np.median(same)),
        "same_game_p10": float(np.percentile(same, 10)),
    }


def _game_record(row: dict[str, Any]) -> dict[str, Any]:
    return {
        "appid": int(row["appid"]),
        "name": row.get("name") or "",
        "year": row.get("year"),
        "genres": list(row.get("genres") or []),
        "tags": list(row.get("tags") or []),
        "reviews": row.get("reviews"),
        "positive": row.get("positive"),
        "seen": False,
    }


def build(run: Path, labels: list[Path], shots: Path, out: Path, *, log=print) -> dict[str, Any]:
    """Write the index for RUN's embeddings and forecasts into OUT; returns the manifest."""
    folders = {space: _chunks(run / SOURCES[space][0]) for space in SPACES}
    if len(folders["meaning"]) != len(folders["look"]):
        raise ValueError("The two spaces have different numbers of chunks")
    names: list[str] = []
    dims = {}
    for space in SPACES:
        with np.load(folders[space][0]) as first:
            dims[space] = first[SOURCES[space][1]].shape[1]
    for chunk in folders["meaning"]:
        with np.load(chunk) as data:
            names += [str(n) for n in data["names"]]
    n = len(names)
    out.mkdir(parents=True, exist_ok=True)
    tmp = {space: out / f"{space}.tmp.npy" for space in SPACES}
    arrays = {
        space: np.lib.format.open_memmap(tmp[space], "w+", np.float16, (n, dims[space]))
        for space in SPACES
    }
    for space in SPACES:
        start = 0
        key = SOURCES[space][1]
        for k, chunk in enumerate(folders[space]):
            with np.load(chunk) as data:
                part = [str(x) for x in data["names"]]
                if part != names[start : start + len(part)]:
                    raise ValueError(f"{chunk} lists other shots than the SigLIP chunks")
                v = data[key].astype(np.float32)
            v /= np.linalg.norm(v, axis=1, keepdims=True)
            arrays[space][start : start + len(part)] = v.astype(np.float16)
            start += len(part)
            if (k + 1) % 100 == 0:
                log(f"{space}: {k + 1}/{len(folders[space])} chunks")
        arrays[space].flush()

    games: list[dict[str, Any]] = []
    rows: dict[int, int] = {}
    seen: set[int] = set()
    for path in labels:
        with open(path, encoding="utf-8") as lines:
            for line in lines:
                row = json.loads(line)
                appid = int(row["appid"])
                if row.get("split") == "train":
                    seen.add(appid)
                if appid not in rows:
                    rows[appid] = len(games)
                    games.append(_game_record(row))
    for appid in seen:
        if appid in rows:
            games[rows[appid]]["seen"] = True

    index_of = {name: i for i, name in enumerate(names)}
    game = np.array([rows.get(int(name.split("_")[0]), -1) for name in names], dtype=np.int32)
    forecast = np.full(n, np.nan, dtype=np.float32)
    positive = np.full(n, np.nan, dtype=np.float32)
    for path in sorted((run / "studio").glob("*_predictions.jsonl")):
        with open(path, encoding="utf-8") as lines:
            for line in lines:
                p = json.loads(line)
                i = index_of.get(f"{p['appid']}_{int(p['shot']):02d}.jpg")
                if i is not None:
                    forecast[i] = p["review_class"]
                    if p.get("positive") is not None:
                        positive[i] = p["positive"]

    calibration = {space: _calibrate(arrays[space], game) for space in SPACES}
    for space in SPACES:
        del arrays[space]
        os.replace(tmp[space], out / f"{space}.npy")
    np.savez(out / "shots.npz", game=game, forecast=forecast, positive=positive)
    (out / "names.json").write_text(json.dumps(names), encoding="utf-8")
    (out / "games.json").write_text(json.dumps(games, ensure_ascii=False), encoding="utf-8")
    manifest = {
        "format": FORMAT,
        "built": datetime.now(UTC).isoformat(timespec="seconds"),
        "shots": n,
        "games": len(games),
        "shots_without_game": int((game < 0).sum()),
        "forecasts": int(np.isfinite(forecast).sum()),
        "shots_dir": str(shots.resolve()),
        "models": {
            "meaning": {"name": MEANING_MODEL, "pooling": "pooler_output"},
            "look": {"name": LOOK_MODEL, "pooling": "mean patch token, grey, 448x252"},
        },
        "calibration": calibration,
        "source": {"run": str(run.resolve()), "labels": [str(p.resolve()) for p in labels]},
    }
    (out / "manifest.json").write_text(json.dumps(manifest, indent=1), encoding="utf-8")
    return manifest


@dataclass
class Index:
    """A built index, its embeddings memory-mapped."""

    root: Path
    manifest: dict[str, Any]
    names: list[str]
    vectors: dict[str, np.ndarray]
    game: np.ndarray
    forecast: np.ndarray
    positive: np.ndarray
    games: list[dict[str, Any]]
    shots_dir: Path
    _rows: dict[str, int] = field(default_factory=dict, repr=False)
    _game_rows: dict[int, int] = field(default_factory=dict, repr=False)

    @classmethod
    def load(cls, root: Path) -> "Index":
        manifest_path = root / "manifest.json"
        if not manifest_path.exists():
            raise FileNotFoundError(f"No index at {root}: run `shotsearch build` first")
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        if manifest.get("format") != FORMAT:
            raise ValueError(f"Index format {manifest.get('format')} is not {FORMAT}: rebuild")
        with np.load(root / "shots.npz") as shots:
            game, forecast, positive = shots["game"], shots["forecast"], shots["positive"]
        return cls(
            root=root,
            manifest=manifest,
            names=json.loads((root / "names.json").read_text(encoding="utf-8")),
            # Copy-on-write: writable for torch.from_numpy, never written back.
            vectors={s: np.load(root / f"{s}.npy", mmap_mode="c") for s in SPACES},
            game=game,
            forecast=forecast,
            positive=positive,
            games=json.loads((root / "games.json").read_text(encoding="utf-8")),
            shots_dir=Path(manifest["shots_dir"]),
        )

    def __len__(self) -> int:
        return len(self.names)

    def row(self, shot: str) -> int:
        """Index row of a shot given as APPID_NN or APPID_NN.jpg."""
        if not self._rows:
            self._rows = {name: i for i, name in enumerate(self.names)}
        name = shot if shot.endswith(".jpg") else f"{shot}.jpg"
        if name not in self._rows:
            raise KeyError(f"Unknown shot {shot}")
        return self._rows[name]

    def game_row(self, appid: int) -> int:
        if not self._game_rows:
            self._game_rows = {g["appid"]: i for i, g in enumerate(self.games)}
        if appid not in self._game_rows:
            raise KeyError(f"Unknown game {appid}")
        return self._game_rows[appid]

    def shot_path(self, row: int) -> Path:
        return self.shots_dir / self.names[row]
