"""Data for Shot Call (formerly «Сколько отзывов?»: which of two games is more popular), base v2 (2026-11-25).

Every screenshot of the held-out 2026 games (labels_2026c split val: the Studio model full1_2026 never
trained on them) with more than 30 reviews: 763 games, 3,052 shots. The 120 adult or sexual games (Steam's
adult flag or content descriptors, plus EXCLUDE) come last, ids s2573 on, marked x=1: the page shows
them only when the player turns «Игры 18+» on (off by default). They were appended after the first
deploy of this base, so the earlier ids and answers stay valid.
The model's forecast is its own per-shot prediction from the holdout eval. Base v1 (500 shots, one per
game, all review classes) is retired; its players stay in the D1 tables v1_*.

Writes data/shots/sNNNN.jpg (hard links into qwen_review/data), data/data.json (what the page shows, plus
the answers lightly scrambled for the round scores) and data/key.json (never published). The ids must not
change: the players' answers refer to them."""

import base64
import json
import os
from pathlib import Path

HERE = os.path.dirname(os.path.abspath(__file__))
QR = os.path.join(os.path.dirname(os.path.dirname(HERE)), "qwen_review")
DATA = os.path.join(HERE, "data")
SHOTS = f"{QR}/data/full/data/shots"
SEXUAL = {1, 3, 4}  # Steam content descriptors with nudity/sexual content
MIN_REVIEWS = 31  # «больше 30 отзывов»
# appids dropped after looking at the v1 contact sheets (sexualised art the Steam flags missed)
EXCLUDE = {3982380, 3919190, 4593980, 3124920}
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


def cls(n):
    for i, b in enumerate([0, 2, 9, 29, 99, 299, 999, 2999, 9999]):
        if n <= b:
            return i
    return 9


argmax = lambda pr: max(range(10), key=lambda c: pr[c])
preds = {}
with open(f"{QR}/data/runs/full1_2026/eval_val_last-1658_all_predictions.jsonl") as f:
    for line in f:
        p = json.loads(line)
        preds[(p["appid"], p["shot"])] = p
nsfw = lambda g: bool(
    g["adult"] or SEXUAL & set(g["content_descriptors"]) or g["appid"] in EXCLUDE
)
games = []
with open(f"{QR}/data/full/labels_2026c.jsonl") as f:
    for line in f:
        g = json.loads(line)
        if g["split"] == "val" and g["reviews"] >= MIN_REVIEWS:
            games.append(g)
games.sort(key=lambda g: (nsfw(g), g["appid"]))

picks = []
for gi, g in enumerate(games):
    pg = next((n for n in g["tags"] + g["genres"] if n in TOP_GENRES), "Other")
    for shot in sorted(g["shots"]):
        src = f"{SHOTS}/{g['appid']}_{shot:02d}.jpg"
        if not os.path.exists(src):
            continue
        p = preds[(g["appid"], shot)]
        picks.append(
            {
                "id": f"s{len(picks) + 1:04d}",
                "gi": gi,
                "nsfw": nsfw(g),
                "appid": g["appid"],
                "name": g["name"],
                "year": g["year"],
                "genres": g["genres"],
                "tags": g["tags"][:5],
                "pg": pg,
                "shot": shot,
                "reviews": g["reviews"],
                "true_class": cls(g["reviews"]),
                "positive": g["positive"],
                "model_class": round(p["review_class"], 3),
                "model_top": argmax(p["review_probs"]),
                "model_probs": p["review_probs"],
            }
        )

# data/shots holds exactly this base
os.makedirs(f"{DATA}/shots", exist_ok=True)
want = {k["id"] + ".jpg" for k in picks}
for f in os.listdir(f"{DATA}/shots"):
    if f not in want:
        os.remove(f"{DATA}/shots/{f}")
for k in picks:
    dst = f"{DATA}/shots/{k['id']}.jpg"
    if os.path.exists(dst):
        os.remove(dst)
    os.link(f"{SHOTS}/{k['appid']}_{k['shot']:02d}.jpg", dst)

Path(f"{DATA}/key.json").write_text(
    json.dumps({"version": 2, "picks": picks}, ensure_ascii=False, indent=1)
)
# the page draws random same-genre pairs itself, so it gets every shot's true class (t) and the
# model's expected class x100 (e), lightly scrambled so they don't show in the page source at a glance
answers = json.dumps(
    {
        "t": [k["true_class"] for k in picks],
        "e": [round(k["model_class"] * 100) for k in picks],
    },
    separators=(",", ":"),
)
SALT = "qwen-review-2026"
scrambled = base64.b64encode(
    bytes(
        ord(c) ^ ord(SALT[i % len(SALT)]) ^ (i * 7 & 255) for i, c in enumerate(answers)
    )
).decode()
shown = [
    {
        "id": k["id"],
        "gi": k["gi"],
        "g": k["genres"],
        "t": k["tags"],
        "pg": k["pg"],
        **({"x": 1} if k["nsfw"] else {}),
    }
    for k in picks
]
Path(f"{DATA}/data.json").write_text(
    json.dumps(
        {"shots": shown, "k": scrambled}, ensure_ascii=False, separators=(",", ":")
    )
)
print(len(games), "games,", len(picks), "shots, 18+:", sum(k["nsfw"] for k in picks))
print("per class", {c: sum(k["true_class"] == c for k in picks) for c in range(10)})
print(
    "per genre", {g: sum(k["pg"] == g for k in picks) for g in TOP_GENRES + ("Other",)}
)
