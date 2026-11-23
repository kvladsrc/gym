"""Simple baseline: ridge regression on year/genre tags and SigLIP fingerprints.

    python baseline.py LABELS_JSONL EMBED_DIR REPORT_JSON [--folds 5]

Three variants are trained on one screenshot per game (the sampled one):
context only (year + genre tags), screenshot only, and both. Predictions are
out-of-fold (games never share folds). Reported per variant:

- pairwise accuracy: for two games of the same year (of first availability)
  and the same primary genre whose window review counts (+1) differ at least
  twofold, how often the model ranks the more reviewed one higher; "clear"
  pairs differ at least fourfold and the more reviewed game has 10+ reviews;
- Spearman correlation of predicted and true log review counts;
- % positive error (games with at least 10 reviews) against the mean of the
  game's year-and-genre group;
- a time split: trained up to 2024, tested on 2025-2026 releases.

Control games (full galleries) are scored on every screenshot to measure how
much the prediction depends on which screenshot is shown.
"""

import argparse
import json
import math
from collections import defaultdict
from pathlib import Path

import numpy as np
from scipy.stats import spearmanr
from sklearn.linear_model import RidgeCV
from sklearn.model_selection import KFold
from sklearn.preprocessing import StandardScaler

TOP_GENRES = (
    "Action",
    "Adventure",
    "Casual",
    "Puzzle",
    "RPG",
    "Racing",
    "Simulation",
    "Sports",
    "Strategy",
)
MAX_TAGS = 10
MIN_TAG_GAMES = 20
ALPHAS = np.logspace(-1, 5, 13)
MIN_RATIO = math.log(2) - 1e-9
CLEAR_RATIO = math.log(4) - 1e-9
SEED = 20261119


def primary_genre(row: dict) -> str:
    for name in row["tags"] + row["genres"]:
        if name in TOP_GENRES:
            return name
    return "Other"


def year(row: dict) -> int:
    return int(row["anchor"][:4])


def context_tokens(row: dict) -> list[str]:
    tokens = [f"year={year(row)}"]
    tokens += [f"tag={t}" for t in row["tags"][:MAX_TAGS]]
    tokens += [f"genre={g}" for g in row["genres"]]
    return tokens


def load_embeddings(path: Path) -> dict[str, np.ndarray]:
    vectors = {}
    for chunk in sorted(path.glob("chunk_*.npz")):
        data = np.load(chunk)
        for name, vec in zip(data["names"], data["emb"], strict=True):
            vectors[str(name)] = vec.astype(np.float32)
    return vectors


def shot_name(appid: int, index: int) -> str:
    return f"{appid}_{index:02d}.jpg"


def pairwise(
    pred: np.ndarray,
    true: np.ndarray,
    groups: list,
    ratio: float = MIN_RATIO,
    floor: float = 0.0,
) -> tuple[float, int]:
    by_group = defaultdict(list)
    for i, g in enumerate(groups):
        by_group[g].append(i)
    hits = total = 0
    for members in by_group.values():
        idx = np.array(members)
        if len(idx) < 2:
            continue
        t, p = true[idx], pred[idx]
        dt = t[:, None] - t[None, :]
        dp = p[:, None] - p[None, :]
        mask = (dt >= ratio) & (t[:, None] >= floor)
        total += int(mask.sum())
        hits += int((dp[mask] > 0).sum()) + 0.5 * int((dp[mask] == 0).sum())
    return (hits / total if total else float("nan")), total


def fit_predict(x_train, y_train, x_test):
    scaler = StandardScaler().fit(x_train)
    model = RidgeCV(alphas=ALPHAS).fit(scaler.transform(x_train), y_train)
    return model.predict(scaler.transform(x_test)), float(model.alpha_)


def evaluate(pred, rows, y, pos_mask, pos_true, pos_pred, pos_group_mean):
    groups = [(year(r), primary_genre(r)) for r in rows]
    acc, pairs = pairwise(pred, y, groups)
    clear, clear_pairs = pairwise(pred, y, groups, CLEAR_RATIO, math.log1p(10))
    rho = spearmanr(pred, y).statistic
    out = {
        "pairwise_same_year_genre": acc,
        "pairs": pairs,
        "pairwise_clear": clear,
        "clear_pairs": clear_pairs,
        "spearman": rho,
    }
    if pos_pred is not None:
        out["positive_mae"] = float(np.mean(np.abs(pos_pred - pos_true)))
        out["positive_mae_group_mean"] = float(
            np.mean(np.abs(pos_group_mean - pos_true))
        )
        out["positive_spearman"] = spearmanr(pos_pred, pos_true).statistic
        out["positive_games"] = int(pos_mask.sum())
    return out


def group_means(rows, values, train_idx, test_idx):
    sums = defaultdict(lambda: [0.0, 0])
    overall = float(np.mean(values[train_idx]))
    for i in train_idx:
        key = (year(rows[i]), primary_genre(rows[i]))
        sums[key][0] += values[i]
        sums[key][1] += 1
    out = []
    for i in test_idx:
        s, n = sums.get((year(rows[i]), primary_genre(rows[i])), (0.0, 0))
        out.append(s / n if n >= 5 else overall)
    return np.array(out)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("labels", type=Path)
    parser.add_argument("embeddings", type=Path)
    parser.add_argument("report", type=Path)
    parser.add_argument("--folds", type=int, default=5)
    args = parser.parse_args()

    vectors = load_embeddings(args.embeddings)
    rows = []
    for line in args.labels.read_text(encoding="utf-8").splitlines():
        row = json.loads(line)
        if row["old_system"] or shot_name(row["appid"], row["pick"]) not in vectors:
            continue
        rows.append(row)
    n = len(rows)
    y = np.log1p(np.array([r["window_reviews"] for r in rows], dtype=float))
    pos = np.array(
        [
            r["window_positive"] if r["window_positive"] is not None else np.nan
            for r in rows
        ]
    )
    has_pos = ~np.isnan(pos)

    tag_games = defaultdict(int)
    for r in rows:
        for t in set(context_tokens(r)):
            tag_games[t] += 1
    vocab = sorted(t for t, c in tag_games.items() if c >= MIN_TAG_GAMES)
    column = {t: i for i, t in enumerate(vocab)}
    ctx = np.zeros((n, len(vocab)), dtype=np.float32)
    for i, r in enumerate(rows):
        for t in context_tokens(r):
            if t in column:
                ctx[i, column[t]] = 1.0
    img = np.stack([vectors[shot_name(r["appid"], r["pick"])] for r in rows])
    img /= np.linalg.norm(img, axis=1, keepdims=True)
    variants = {"context": ctx, "screenshot": img, "both": np.hstack([ctx, img])}

    report = {
        "games": n,
        "games_with_positive": int(has_pos.sum()),
        "context_columns": len(vocab),
        "embedding_dim": int(img.shape[1]),
        "tag_count_spearman": spearmanr([len(r["tags"]) for r in rows], y).statistic,
        "raw_tag_count_spearman": spearmanr([r["all_tags"] for r in rows], y).statistic,
        "variants": {},
    }
    folds = list(KFold(args.folds, shuffle=True, random_state=SEED).split(np.arange(n)))
    oof = {}
    for name, x in variants.items():
        pred = np.zeros(n)
        pos_pred = np.full(n, np.nan)
        pos_base = np.full(n, np.nan)
        alphas = []
        for train_idx, test_idx in folds:
            pred[test_idx], alpha = fit_predict(x[train_idx], y[train_idx], x[test_idx])
            alphas.append(alpha)
            tr = train_idx[has_pos[train_idx]]
            te = test_idx[has_pos[test_idx]]
            pos_pred[te], _ = fit_predict(x[tr], pos[tr], x[te])
            pos_base[te] = group_means(rows, pos, tr, te)
        oof[name] = pred
        result = evaluate(
            pred, rows, y, has_pos, pos[has_pos], pos_pred[has_pos], pos_base[has_pos]
        )
        result["alphas"] = alphas

        early = np.array([year(r) <= 2024 for r in rows])
        late = ~early
        late_pred, _ = fit_predict(x[early], y[early], x[late])
        late_rows = [r for r, keep in zip(rows, late, strict=True) if keep]
        acc, pairs = pairwise(
            late_pred, y[late], [(year(r), primary_genre(r)) for r in late_rows]
        )
        result["time_split"] = {
            "train_games": int(early.sum()),
            "test_games": int(late.sum()),
            "pairwise_same_year_genre": acc,
            "pairs": pairs,
            "spearman": spearmanr(late_pred, y[late]).statistic,
        }
        report["variants"][name] = result
        print(name, json.dumps(result, default=float))

    # Control games: score every screenshot with the fold model that never saw the game.
    control = {}
    for train_idx, test_idx in folds:
        x_train = variants["both"][train_idx]
        scaler = StandardScaler().fit(x_train)
        model = RidgeCV(alphas=ALPHAS).fit(scaler.transform(x_train), y[train_idx])
        img_model_scaler = StandardScaler().fit(img[train_idx])
        img_model = RidgeCV(alphas=ALPHAS).fit(
            img_model_scaler.transform(img[train_idx]), y[train_idx]
        )
        for i in test_idx:
            r = rows[i]
            if not r["control"]:
                continue
            names = [
                shot_name(r["appid"], k)
                for k in r["shots"]
                if shot_name(r["appid"], k) in vectors
            ]
            if len(names) < 2:
                continue
            vecs = np.stack([vectors[k] for k in names])
            vecs /= np.linalg.norm(vecs, axis=1, keepdims=True)
            both = np.hstack([np.repeat(ctx[i : i + 1], len(vecs), axis=0), vecs])
            control[i] = {
                "pick": names.index(shot_name(r["appid"], r["pick"])),
                "both": model.predict(scaler.transform(both)),
                "screenshot": img_model.predict(img_model_scaler.transform(vecs)),
            }
    if control:
        idx = np.array(sorted(control))
        consistency = {}
        for name in ("both", "screenshot"):
            per_game = [control[i][name] for i in idx]
            within = float(np.mean([p.std() for p in per_game]))
            between = float(np.std([p.mean() for p in per_game]))
            first = np.array([p[0] for p in per_game])
            picked = np.array([control[i][name][control[i]["pick"]] for i in idx])
            mean = np.array([p.mean() for p in per_game])
            groups = [(year(rows[i]), primary_genre(rows[i])) for i in idx]
            consistency[name] = {
                "games": len(idx),
                "within_game_std": within,
                "between_game_std": between,
                "pairwise_first_screenshot": pairwise(first, y[idx], groups)[0],
                "pairwise_sampled_screenshot": pairwise(picked, y[idx], groups)[0],
                "pairwise_gallery_mean": pairwise(mean, y[idx], groups)[0],
            }
        report["control"] = consistency
        print("control", json.dumps(consistency))

    order = np.argsort(oof["screenshot"])
    report["screenshot_extremes"] = {
        "lowest": [
            (
                rows[i]["appid"],
                rows[i]["pick"],
                round(float(oof["screenshot"][i]), 2),
                rows[i]["window_reviews"],
            )
            for i in order[:50]
        ],
        "highest": [
            (
                rows[i]["appid"],
                rows[i]["pick"],
                round(float(oof["screenshot"][i]), 2),
                rows[i]["window_reviews"],
            )
            for i in order[-50:][::-1]
        ],
    }
    args.report.write_text(
        json.dumps(report, indent=1, default=float), encoding="utf-8"
    )
    print(f"report -> {args.report}")


if __name__ == "__main__":
    main()
