"""A small synthetic scoring run and its index: six games of four shots, plus one unknown game.

Each game has its own direction in each space; meaning and look directions differ, so the two
spaces disagree about which games are near. Game 10 is close to 11 by meaning and to 12 by look.
"""

import json
from pathlib import Path

import numpy as np
import pytest
from PIL import Image

from shotsearch.index import build
from shotsearch.service import ShotSearch

GAMES = [
    # appid, year, tags, reviews, split
    (10, 2020, ["Horror", "Adventure"], 500, "val"),
    (11, 2021, ["Horror"], 20000, "train"),
    (12, 2024, ["Card Battler"], 50, "val"),
    (13, 2016, ["Horror", "Puzzle"], 3000, "train"),
    (14, 2026, ["Puzzle"], 5, "test"),
    (15, 2019, ["Adventure"], 0, "val"),
]
UNKNOWN = 99
DIM_MEANING, DIM_LOOK = 8, 6


def _directions(rng, dim, pulls):
    base = {appid: rng.normal(size=dim) for appid, *_ in GAMES}
    base[UNKNOWN] = rng.normal(size=dim)
    for a, b, weight in pulls:
        base[a] = base[a] + weight * base[b]
    return base


def make_run(root: Path) -> tuple[Path, Path, list[Path]]:
    rng = np.random.default_rng(0)
    meaning = _directions(rng, DIM_MEANING, [(10, 11, 3.0)])
    look = _directions(rng, DIM_LOOK, [(10, 12, 3.0)])
    run, shots = root / "run", root / "shots"
    for folder in (run / "siglip", run / "dino", run / "studio", shots):
        folder.mkdir(parents=True)
    names, emb, gray = [], [], []
    for appid in [g[0] for g in GAMES] + [UNKNOWN]:
        for shot in range(4):
            name = f"{appid}_{shot:02d}.jpg"
            names.append(name)
            emb.append(meaning[appid] + 0.2 * rng.normal(size=DIM_MEANING))
            gray.append(look[appid] + 0.2 * rng.normal(size=DIM_LOOK))
            Image.new("RGB", (64, 36), (appid * 7 % 255, shot * 50, 90)).save(shots / name)
    half = len(names) // 2
    for k, part in enumerate((slice(0, half), slice(half, None))):
        np.savez(
            run / "siglip" / f"chunk_{k:05d}.npz",
            names=np.array(names[part]),
            emb=np.array(emb[part], dtype=np.float16),
        )
        np.savez(
            run / "dino" / f"chunk_{k:05d}.npz",
            names=np.array(names[part]),
            gray_mean=np.array(gray[part], dtype=np.float16),
            cls=np.zeros((len(names[part]), 2), dtype=np.float16),
        )
    with open(run / "studio" / "eval_val_last_all_predictions.jsonl", "w") as out:
        for appid, *_ in GAMES:
            for shot in range(3):  # the fourth shot has no forecast
                row = {"appid": appid, "shot": shot, "review_class": appid / 3, "positive": 80}
                out.write(json.dumps(row) + "\n")
    labels = root / "labels.jsonl"
    extra = root / "labels_extra.jsonl"
    with open(labels, "w") as out:
        for appid, year, tags, reviews, split in GAMES:
            row = {
                "appid": appid,
                "name": f"Game {appid}",
                "year": year,
                "genres": ["Indie"],
                "tags": tags,
                "reviews": reviews,
                "positive": 90,
                "split": split,
            }
            out.write(json.dumps(row) + "\n")
    with open(extra, "w") as out:  # 14 was trained on in the second file
        out.write(json.dumps({"appid": 14, "name": "Other name", "split": "train"}) + "\n")
    return run, shots, [labels, extra]


@pytest.fixture(scope="session")
def index_dir(tmp_path_factory) -> Path:
    root = tmp_path_factory.mktemp("shotsearch")
    run, shots, labels = make_run(root)
    build(run, labels, shots, root / "index", log=lambda message: None)
    return root / "index"


@pytest.fixture
def service(index_dir, tmp_path) -> ShotSearch:
    return ShotSearch(index_dir, home=tmp_path, idle_s=0)
