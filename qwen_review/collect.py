"""Collect Steam data for the qwen_review pilot.

Sources: the FronkonGames Steam Games Dataset (MIT) and Steam's public store
endpoints. Downloaded data is untrusted: run with ``python -I`` and keep
outputs under data/ (git-ignored), never next to the scripts.

    index  - stream games.json into a compact JSONL table
    sample - draw the pilot sample (or, with --all, every eligible game)
    fetch  - download review histograms, tags and screenshots
    labels - review count and % positive, and the train/val/test split

Tags came from SteamSpy in the pilot. SteamSpy has none for most games from
2025 on, so the full crawl takes them from Steam's store service instead
(--no-steamspy --store-tags): 20 tags per game in batches of 50 apps.

The pilot counted reviews in the first three months from review histories
(labels --reviews window). The full crawl uses the ready totals the store
service returns with the tags (--reviews store, the default): the count and %
the store page shows, without the history requests.
"""

from __future__ import annotations

import argparse
import datetime as dt
import http.client
import json
import random
import re
import ssl
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from collections import Counter
from pathlib import Path

USER_AGENT = "Mozilla/5.0 (compatible; qwen-review/0.1)"
HISTOGRAM_URL = (
    "https://store.steampowered.com/appreviewhistogram/{appid}"
    "?l=english&review_score_preference=0"
)
STEAMSPY_URL = "https://steamspy.com/api.php?request=appdetails&appid={appid}"
STORE_ITEMS_URL = (
    "https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?input_json={query}"
)
TAG_LIST_URL = (
    "https://api.steampowered.com/IStoreService/GetTagList/v1/?language=english"
)
STORE_BATCH = 50
MAX_DELAY = 6.0  # seconds per request after Steam refuses a faster pace
# Steam has no reviews before October 2010, so earlier releases have none in
# their first three months.
FIRST_DAY = dt.date(2011, 1, 1)
# Steam user reviews replaced recommendations in November 2013: the monthly
# volume tripled and the negative share went from ~4% to ~10%.
REVIEWS_SINCE = dt.date(2013, 11, 1)
MIN_SCORED = 10  # the store shows a % from 10 reviews on
SOFTWARE_GENRES = {
    "Accounting",
    "Animation & Modeling",
    "Audio Production",
    "Design & Illustration",
    "Education",
    "Game Development",
    "Photo Editing",
    "Software Training",
    "Utilities",
    "Video Production",
    "Web Publishing",
}
GAME_GENRES = {
    "Action",
    "Adventure",
    "Casual",
    "Massively Multiplayer",
    "RPG",
    "Racing",
    "Simulation",
    "Sports",
    "Strategy",
}
# Model input keeps genre, theme, player-mode and gameplay tags. Dropped:
# looks and camera (the screenshot has to carry them), moods and opinions,
# franchise hints, content descriptors and store meta tags.
VISUAL_TAGS = {
    "1980s",
    "1990's",
    "2.5D",
    "2D",
    "3D",
    "360 Video",
    "Abstract",
    "Anime",
    "Cartoon",
    "Cartoony",
    "Cinematic",
    "Colorful",
    "Comic Book",
    "Cute",
    "FMV",
    "First-Person",
    "Hand-drawn",
    "Isometric",
    "Minimalist",
    "Old School",
    "Pixel Graphics",
    "Realistic",
    "Retro",
    "Side Scroller",
    "Stylized",
    "Text-Based",
    "Third Person",
    "Top-Down",
    "Voxel",
}
OPINION_TAGS = {
    "Addictive",
    "Atmospheric",
    "Beautiful",
    "Classic",
    "Cozy",
    "Cult",
    "Cult Classic",
    "Dark",
    "Difficult",
    "Emotional",
    "Epic",
    "Funny",
    "Great Soundtrack",
    "Immersive",
    "Lore-Rich",
    "Masterpiece",
    "Memes",
    "Nostalgia",
    "Psychedelic",
    "Relaxing",
    "Replay Value",
    "Short",
    "Surreal",
    "Unforgiving",
    "Well-Written",
    "Wholesome",
}
OTHER_DROPPED_TAGS = {
    "Based On A Novel",
    "Benchmark",
    "Blood",
    "Gaming",
    "Gore",
    "Hardware",
    "Hentai",
    "Mod",
    "Nudity",
    "Reboot",
    "Remake",
    "Sequel",
    "Sexual Content",
    "Sexual Themes",
    "Soundtrack",
    "TrackIR",
    "Violent",
}
DROPPED_TAGS = VISUAL_TAGS | OPINION_TAGS | OTHER_DROPPED_TAGS
RENAMED_TAGS = {
    "2D Fighter": "Fighting",
    "3D Fighter": "Fighting",
    "2D Platformer": "Platformer",
    "3D Platformer": "Platformer",
}
EXCLUDED_NAME = re.compile(r"\b(playtest|demo)\b", re.IGNORECASE)
ADULT_NOTES = re.compile(r"sexual|nudity|nude|hentai", re.IGNORECASE)
DATE_FORMATS = ("%b %d, %Y", "%d %b, %Y", "%B %d, %Y", "%d %B, %Y", "%b %Y")
SIZE_SUFFIX = re.compile(r"\.\d+x\d+\.jpg$")


def parse_date(text: str | None) -> dt.date | None:
    text = (text or "").strip()
    for fmt in DATE_FORMATS:
        try:
            return dt.datetime.strptime(text, fmt).replace(tzinfo=dt.UTC).date()
        except ValueError:
            continue
    return None


def thumbnail(url: str) -> str:
    """Return the 600x338 store thumbnail for a screenshot URL."""
    base = url.split("?", 1)[0]
    return SIZE_SUFFIX.sub(".600x338.jpg", base)


def cmd_index(args: argparse.Namespace) -> None:
    import ijson  # games.json is about 1 GB, so it is streamed

    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    count = 0
    with open(args.games_json, "rb") as src, open(out, "w", encoding="utf-8") as dst:
        for appid, game in ijson.kvitems(src, "", use_float=True):
            date = parse_date(game.get("release_date"))
            row = {
                "appid": int(appid),
                "name": game.get("name") or "",
                "date": date.isoformat() if date else None,
                "genres": game.get("genres") or [],
                "adult": bool(ADULT_NOTES.search(game.get("notes") or "")),
                "price": game.get("price") or 0.0,
                "positive": game.get("positive") or 0,
                "negative": game.get("negative") or 0,
                "screenshots": game.get("screenshots") or [],
            }
            dst.write(json.dumps(row, ensure_ascii=False) + "\n")
            count += 1
    print(f"indexed {count} games -> {out}")


def eligible(row: dict, last_day: dt.date) -> str | None:
    """Return the reason a game is left out, or None if it is eligible."""
    if not row["date"]:
        return "no date"
    if not row["screenshots"]:
        return "no screenshots"
    day = dt.date.fromisoformat(row["date"])
    if day < FIRST_DAY:
        return "before 2011"
    if day > last_day:
        return "window not complete"
    if EXCLUDED_NAME.search(row["name"]):
        return "playtest or demo"
    genres = set(row["genres"])
    if genres & SOFTWARE_GENRES and not genres & GAME_GENRES:
        return "software"
    return None


def allocate(sizes: dict[int, int], total: int, floor: int) -> dict[int, int]:
    """Split ``total`` over years: at least ``floor`` each, the rest by size."""
    base = {year: min(n, floor) for year, n in sizes.items()}
    spare = {year: sizes[year] - base[year] for year in sizes}
    rest, pool = total - sum(base.values()), sum(spare.values())
    if rest <= 0 or pool == 0:
        return base
    shares = {year: rest * spare[year] / pool for year in sizes}
    extra = {year: int(shares[year]) for year in sizes}
    left = rest - sum(extra.values())
    by_remainder = sorted(sizes, key=lambda y: shares[y] - extra[y], reverse=True)
    for year in by_remainder[:left]:
        extra[year] += 1
    return {year: min(sizes[year], base[year] + extra[year]) for year in sizes}


def cmd_sample(args: argparse.Namespace) -> None:
    rng = random.Random(args.seed)
    last_day = dt.date.fromisoformat(args.last_day)
    since = dt.date.fromisoformat(args.since) if args.since else FIRST_DAY
    by_year: dict[int, list[dict]] = {}
    reasons: Counter[str] = Counter()
    with open(args.games, encoding="utf-8") as src:
        for line in src:
            row = json.loads(line)
            reason = eligible(row, last_day)
            if reason is None and dt.date.fromisoformat(row["date"]) < since:
                reason = f"before {since}"
            reasons[reason or "eligible"] += 1
            if reason is None:
                by_year.setdefault(int(row["date"][:4]), []).append(row)
    sizes = {year: len(rows) for year, rows in sorted(by_year.items())}
    if args.all:
        quota = sizes
        picked = [row for year in sorted(by_year) for row in by_year[year]]
    else:
        quota = allocate(sizes, args.count, args.per_year_floor)
        picked = []
        for year in sorted(by_year):
            picked.extend(rng.sample(by_year[year], quota[year]))
    control = {
        row["appid"] for row in rng.sample(picked, min(args.control, len(picked)))
    }
    sample = []
    for row in picked:
        shots = row["screenshots"]
        pick = rng.randrange(len(shots))
        if row["appid"] in control:
            indices = range(len(shots))
        elif args.shots > 1:
            # The first screenshot (the developer's choice) plus random others.
            others = rng.sample(
                range(1, len(shots)), min(args.shots - 1, len(shots) - 1)
            )
            indices = sorted([0, *others])
            pick = rng.choice(indices)
        else:
            indices = [pick]
        sample.append(
            {
                "appid": row["appid"],
                "name": row["name"],
                "date": row["date"],
                "genres": row["genres"],
                "adult": row["adult"],
                "pick": pick,
                "gallery_size": len(shots),
                "control": row["appid"] in control,
                "screenshots": [
                    {"index": i, "url": thumbnail(shots[i])} for i in indices
                ],
            }
        )
    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(sample, ensure_ascii=False, indent=1), encoding="utf-8")
    print("games by reason:", dict(reasons.most_common()))
    print("year: eligible -> sampled")
    for year, size in sizes.items():
        print(f"  {year}: {size} -> {quota[year]}")
    n_shots = sum(len(row["screenshots"]) for row in sample)
    print(
        f"sampled {len(sample)} games, {len(control)} with full galleries, {n_shots} screenshots -> {out}"
    )


class Stats:
    def __init__(self, totals: dict[str, int], failures: Path) -> None:
        self.totals = totals
        self.counts: dict[str, Counter[str]] = {name: Counter() for name in totals}
        self.failures = failures
        self.lock = threading.Lock()

    def add(self, worker: str, outcome: str, n: int = 1) -> None:
        with self.lock:
            self.counts[worker][outcome] += n

    def fail(self, worker: str, key: str, error: str) -> None:
        with self.lock:
            self.counts[worker]["failed"] += 1
            with open(self.failures, "a", encoding="utf-8") as f:
                record = {"worker": worker, "key": key, "error": error}
                f.write(json.dumps(record) + "\n")

    def line(self) -> str:
        with self.lock:
            parts = []
            for name, total in self.totals.items():
                c = self.counts[name]
                done = c["done"] + c["skipped"]
                parts.append(f"{name} {done}/{total} failed {c['failed']}")
            return time.strftime("%H:%M:%S ") + "; ".join(parts)


def http_get(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=30) as response:
        return response.read()


class KeepAlive:
    """GETs over one persistent HTTPS connection per thread and host (fetch --keep-alive).

    A new connection costs a DNS lookup and TCP and TLS handshakes, several times the
    transfer of a 50 KB screenshot. Only a plain 200 is answered here. Errors from 400
    up raise HTTPError as urllib does; redirects and anything else unusual go through
    http_get. A connection that fails is dropped and the request tried once more on a
    new one, then through http_get, which raises what urllib would.
    """

    def __init__(self) -> None:
        self.local = threading.local()
        self.context = ssl.create_default_context()

    def drop(self, host: str) -> None:
        conn = self.local.__dict__.setdefault("conns", {}).pop(host, None)
        if conn is not None:
            conn.close()

    def get(self, url: str) -> bytes:
        parts = urllib.parse.urlsplit(url)
        host = parts.hostname
        if parts.scheme != "https" or not host or parts.port or parts.username:
            return http_get(url)
        target = parts.path + (f"?{parts.query}" if parts.query else "")
        conns = self.local.__dict__.setdefault("conns", {})
        for _attempt in range(2):
            conn = conns.get(host)
            if conn is None:
                conn = conns[host] = http.client.HTTPSConnection(
                    host, timeout=30, context=self.context
                )
            try:
                conn.request("GET", target, headers={"User-Agent": USER_AGENT})
                response = conn.getresponse()
                body = response.read()
            except (OSError, http.client.HTTPException):
                self.drop(host)
                continue
            if response.will_close:
                self.drop(host)
            if response.status == 200:
                return body
            if response.status >= 400:
                raise urllib.error.HTTPError(
                    url, response.status, response.reason, response.headers, None
                )
            break
        return http_get(url)


def write_atomic(path: Path, body: bytes) -> None:
    tmp = path.with_suffix(path.suffix + ".part")
    tmp.write_bytes(body)
    tmp.replace(path)


def paced_json(
    worker: str,
    appids: list[int],
    url: str,
    out_dir: Path,
    delay: float,
    valid,
    stats: Stats,
    stop: threading.Event,
) -> None:
    """Fetch one JSON per app, one request per ``delay`` seconds, with backoff."""
    out_dir.mkdir(parents=True, exist_ok=True)
    for appid in appids:
        path = out_dir / f"{appid}.json"
        if path.exists():
            stats.add(worker, "skipped")
            continue
        backoff = 60.0
        for _attempt in range(6):
            if stop.is_set():
                return
            started = time.monotonic()
            try:
                body = http_get(url.format(appid=appid))
                if not valid(json.loads(body)):
                    raise ValueError("unexpected JSON")
            except urllib.error.HTTPError as err:
                if err.code == 404:
                    stats.fail(worker, str(appid), "HTTP 404")
                    break
                error = f"HTTP {err.code}"
                if err.code in (403, 429) and delay < MAX_DELAY:
                    # Steam asks us to slow down: keep the slower pace from now on.
                    delay = min(max(delay * 1.5, 1.5), MAX_DELAY)
                    print(
                        f"{worker}: refused, now one request per {delay:.2f} s",
                        flush=True,
                    )
            except (OSError, ValueError) as err:
                error = f"{type(err).__name__}: {err}"
            else:
                write_atomic(path, body)
                stats.add(worker, "done")
                stop.wait(max(0.0, delay - (time.monotonic() - started)))
                break
            print(f"{worker} {appid}: {error}; waiting {backoff:.0f} s", flush=True)
            stop.wait(backoff)
            backoff = min(backoff * 2, 900.0)
        else:
            stats.fail(worker, str(appid), error)


def fetch_shot(
    job: tuple[int, int, str], out_dir: Path, stats: Stats, stop, get=http_get
) -> None:
    appid, index, url = job
    path = out_dir / f"{appid}_{index:02d}.jpg"
    if path.exists():
        stats.add("shots", "skipped")
        return
    error = "not tried"
    for attempt in range(4):
        if stop.is_set():
            return
        try:
            body = get(url)
        except urllib.error.HTTPError as err:
            error = f"HTTP {err.code}"
            if err.code == 404:
                break
        except OSError as err:
            error = f"{type(err).__name__}: {err}"
        else:
            if body.startswith(b"\xff\xd8"):
                write_atomic(path, body)
                stats.add("shots", "done")
                return
            error = "not a JPEG"
            break
        stop.wait(10.0 * (attempt + 1))
    stats.fail("shots", f"{appid}_{index}", error)


def needs_store_item(path: Path) -> bool:
    """Missing, or saved before review summaries were requested (2026-11-20)."""
    try:
        item = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return True
    return item.get("success") == 1 and "reviews" not in item


def store_items(
    appids: list[int], out_dir: Path, delay: float, stats: Stats, stop: threading.Event
) -> None:
    """Tags, release date and review summary from Steam's store service.

    STORE_BATCH apps per request. The review summary is the store's lifetime
    count and % positive (``reviews.summary_filtered``).
    """
    out_dir.mkdir(parents=True, exist_ok=True)
    todo = [a for a in appids if needs_store_item(out_dir / f"{a}.json")]
    stats.add("storetags", "skipped", len(appids) - len(todo))
    context = {"language": "english", "country_code": "US", "steam_realm": 1}
    wanted = {
        "include_tag_count": 20,
        "include_basic_info": True,
        "include_release": True,
        "include_reviews": True,
    }
    for start in range(0, len(todo), STORE_BATCH):
        batch = todo[start : start + STORE_BATCH]
        query = {
            "ids": [{"appid": a} for a in batch],
            "context": context,
            "data_request": wanted,
        }
        url = STORE_ITEMS_URL.format(
            query=urllib.parse.quote(json.dumps(query, separators=(",", ":")))
        )
        backoff = 60.0
        error = "not tried"
        for _attempt in range(6):
            if stop.is_set():
                return
            started = time.monotonic()
            try:
                items = json.loads(http_get(url))["response"]["store_items"]
            except urllib.error.HTTPError as err:
                error = f"HTTP {err.code}"
            except (OSError, ValueError, KeyError, TypeError) as err:
                error = f"{type(err).__name__}: {err}"
            else:
                by_id = {item.get("appid") or item.get("id"): item for item in items}
                for appid in batch:
                    item = by_id.get(appid, {"appid": appid, "success": 0})
                    write_atomic(out_dir / f"{appid}.json", json.dumps(item).encode())
                stats.add("storetags", "done", len(batch))
                stop.wait(max(0.0, delay - (time.monotonic() - started)))
                break
            print(
                f"storetags batch from {batch[0]}: {error}; waiting {backoff:.0f} s",
                flush=True,
            )
            stop.wait(backoff)
            backoff = min(backoff * 2, 900.0)
        else:
            for appid in batch:
                stats.fail("storetags", str(appid), error)


def cmd_fetch(args: argparse.Namespace) -> None:
    sample = json.loads(Path(args.sample).read_text(encoding="utf-8"))
    if args.limit:
        sample = sample[: args.limit]
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    appids = [row["appid"] for row in sample]
    jobs = [
        (row["appid"], s["index"], s["url"])
        for row in sample
        for s in row["screenshots"]
    ]
    del sample  # the full list holds ~1 GB of Python objects
    # The sample runs by release year; a random order lets training start on a fair
    # part of the games while the rest still downloads.
    if args.shuffle is not None:
        random.Random(args.shuffle).shuffle(jobs)
    if args.first:
        first = {int(a) for a in Path(args.first).read_text(encoding="utf-8").split()}
        jobs.sort(key=lambda job: job[0] not in first)  # stable: keeps the order within
    totals = {}
    if args.histogram:
        totals["histogram"] = len(appids)
    if args.steamspy:
        totals["steamspy"] = len(appids)
    if args.store_tags:
        totals["storetags"] = len(appids)
        if not (out / "taglist.json").exists():
            body = http_get(TAG_LIST_URL)
            if not json.loads(body)["response"]["tags"]:
                raise SystemExit("Steam returned an empty tag list")
            write_atomic(out / "taglist.json", body)
    totals["shots"] = len(jobs)
    stats = Stats(totals, out / "failures.jsonl")
    stop = threading.Event()
    shots_dir = out / "shots"
    shots_dir.mkdir(exist_ok=True)
    get = KeepAlive().get if args.keep_alive else http_get

    def run_shots(part: list[tuple[int, int, str]]) -> None:
        # Plain threads over slices: a pool would hold a future per screenshot.
        for job in part:
            if stop.is_set():
                return
            try:
                fetch_shot(job, shots_dir, stats, stop, get)
            except Exception as err:  # noqa: BLE001 - one bad file must not stop a worker
                stats.fail("shots", f"{job[0]}_{job[1]}", repr(err))

    workers = [
        threading.Thread(target=run_shots, args=(jobs[i :: args.shot_workers],))
        for i in range(args.shot_workers)
    ]
    if args.histogram:
        workers.append(
            threading.Thread(
                target=paced_json,
                args=(
                    "histogram",
                    appids,
                    HISTOGRAM_URL,
                    out / "histogram",
                    args.histogram_delay,
                    lambda d: isinstance(d, dict) and "success" in d,
                    stats,
                    stop,
                ),
            )
        )
    if args.steamspy:
        workers.append(
            threading.Thread(
                target=paced_json,
                args=(
                    "steamspy",
                    appids,
                    STEAMSPY_URL,
                    out / "steamspy",
                    args.steamspy_delay,
                    lambda d: isinstance(d, dict) and "appid" in d,
                    stats,
                    stop,
                ),
            )
        )
    if args.store_tags:
        workers.append(
            threading.Thread(
                target=store_items,
                args=(appids, out / "storetags", args.store_delay, stats, stop),
            )
        )
    for worker in workers:
        worker.start()
    try:
        while any(worker.is_alive() for worker in workers):
            for worker in workers:
                worker.join(timeout=args.report_every / len(workers))
            print(stats.line(), flush=True)
    except KeyboardInterrupt:
        stop.set()
        print("stopping...", flush=True)
        for worker in workers:
            worker.join()
    print("finished:", stats.line(), flush=True)


def bucket_end(start: dt.date, rollup_type: str) -> dt.date:
    if rollup_type == "week":
        return start + dt.timedelta(days=7)
    if rollup_type == "day":
        return start + dt.timedelta(days=1)
    return (start.replace(day=28) + dt.timedelta(days=4)).replace(day=1)


def window_counts(histogram: dict, release: dt.date, days: int) -> dict:
    """Reviews in the first ``days`` after the game became available.

    The window starts at the store release date or at the day of the first
    review if that is earlier (Early Access; ``start_date`` of the history).
    A bucket only partly inside the window is counted in proportion to the
    days the game was on sale in it.
    """
    results = histogram.get("results") or {}
    rollup_type = results.get("rollup_type") or "month"
    buckets = []
    for rollup in results.get("rollups") or []:
        start = dt.datetime.fromtimestamp(rollup["date"], dt.UTC).date()
        up, down = rollup["recommendations_up"], rollup["recommendations_down"]
        buckets.append((start, bucket_end(start, rollup_type), up, down))
    lifetime = sum(b[2] + b[3] for b in buckets)
    first_review = None
    if lifetime and results.get("start_date"):
        first_review = dt.datetime.fromtimestamp(results["start_date"], dt.UTC).date()
    anchor = min(release, first_review or release)
    end = anchor + dt.timedelta(days=days)
    up_sum = down_sum = 0.0
    for start, stop, up, down in buckets:
        on_sale_from = max(start, anchor)
        available = (stop - on_sale_from).days
        overlap = (min(stop, end) - on_sale_from).days
        if available <= 0 or overlap <= 0:
            continue
        share = overlap / available
        up_sum += up * share
        down_sum += down * share
    total = round(up_sum + down_sum)
    return {
        "anchor": anchor.isoformat(),
        "first_review": first_review.isoformat() if first_review else None,
        "old_system": anchor < REVIEWS_SINCE,
        "rollup_type": rollup_type,
        "window_reviews": total,
        "window_up": round(up_sum, 1),
        "window_down": round(down_sum, 1),
        "window_positive": round(100 * up_sum / (up_sum + down_sum), 1)
        if total >= MIN_SCORED
        else None,
        "lifetime_reviews": lifetime,
    }


def input_tags(votes: dict) -> list[str]:
    """SteamSpy tags in vote order without looks, opinions and meta tags."""
    kept = []
    for name in sorted(votes, key=votes.get, reverse=True):
        name = RENAMED_TAGS.get(name.strip(), name.strip())
        if name not in DROPPED_TAGS and name not in kept:
            kept.append(name)
    return kept


def window_label(
    row: dict, data: Path, listed: int, days: int, dropped: Counter
) -> dict | None:
    """Reviews in the first ``days`` from the review history (the pilot)."""
    hist_path = data / "histogram" / f"{row['appid']}.json"
    if not hist_path.exists():
        dropped["no histogram"] += 1
        return None
    histogram = json.loads(hist_path.read_text(encoding="utf-8"))
    if histogram.get("success") != 1:
        dropped[f"histogram success={histogram.get('success')}"] += 1
        return None
    counts = window_counts(histogram, dt.date.fromisoformat(row["date"]), days)
    lifetime = counts["lifetime_reviews"]
    # Some store entries share one review pool with another app (Portal 2
    # 659 vs 620): the history is empty or tiny while the dataset copies
    # the other app's totals.
    if listed >= 10 and lifetime == 0:
        dropped["reviews belong to another app"] += 1
        return None
    if listed >= 100 and lifetime < 0.05 * listed:
        dropped["reviews mostly on another app"] += 1
        return None
    return {
        "reviews": counts["window_reviews"],
        "positive": counts["window_positive"],
        **counts,
    }


def store_label(store: dict, dropped: Counter) -> dict | None:
    """The total count and % the store page shows (Steam purchases, no off-topic)."""
    if store.get("success") != 1:
        dropped["no store item"] += 1
        return None
    reviews = store.get("reviews") or {}
    summary = reviews.get("summary_filtered")
    if summary is None:
        dropped["no review summary"] += 1
        return None
    count = summary.get("review_count", 0)
    positive = summary.get("percent_positive") if count >= MIN_SCORED else None
    if count >= MIN_SCORED and positive is None:
        dropped["no % positive"] += 1
        return None
    return {
        "reviews": count,
        "positive": positive,
        "store_label": summary.get("review_score_label"),
        "all_purchases": bool(reviews.get("counts_all_purchases")),
    }


def split_of(row: dict, test_since: str, val_share: float) -> str:
    """Releases from ``test_since`` on are the test; a stable random share is val."""
    if row["date"] >= test_since:
        return "test"
    return (
        "val"
        if random.Random(f"split:{row['appid']}").random() < val_share
        else "train"
    )


def cmd_labels(args: argparse.Namespace) -> None:
    sample = json.loads(Path(args.sample).read_text(encoding="utf-8"))
    data = Path(args.data)
    wanted = {row["appid"] for row in sample}
    dataset_reviews = {}
    with Path(args.games).open(encoding="utf-8") as src:
        for line in src:
            game = json.loads(line)
            if game["appid"] in wanted:
                dataset_reviews[game["appid"]] = game["positive"] + game["negative"]
    tag_names = {}
    if (data / "taglist.json").exists():
        tag_list = json.loads((data / "taglist.json").read_text(encoding="utf-8"))
        tag_names = {t["tagid"]: t["name"] for t in tag_list["response"]["tags"]}
    out_rows, dropped = [], Counter()
    for row in sample:
        spy_path = data / "steamspy" / f"{row['appid']}.json"
        store_path = data / "storetags" / f"{row['appid']}.json"
        store = {}
        if store_path.exists():
            store = json.loads(store_path.read_text(encoding="utf-8"))
        listed = dataset_reviews.get(row["appid"], 0)
        if args.reviews == "window":
            label = window_label(row, data, listed, args.days, dropped)
        else:
            label = store_label(store, dropped)
        if label is None:
            continue
        tags, tag_source = {}, None
        if store.get("tags"):
            tags = {
                tag_names.get(t["tagid"], str(t["tagid"])): t.get("weight", 0)
                for t in store.get("tags") or []
            }
            tag_source = "store"
        if not tags and spy_path.exists():
            spy = json.loads(spy_path.read_text(encoding="utf-8"))
            if isinstance(spy.get("tags"), dict) and spy["tags"]:
                tags, tag_source = spy["tags"], "steamspy"
        released = (store.get("release") or {}).get("steam_release_date")
        shots = [
            s["index"]
            for s in row["screenshots"]
            if args.planned_shots
            or (data / "shots" / f"{row['appid']}_{s['index']:02d}.jpg").exists()
        ]
        out_rows.append(
            {
                "appid": row["appid"],
                "name": row["name"],
                "date": row["date"],
                "year": int(row["date"][:4]),
                "genres": row["genres"],
                "tags": input_tags(tags),
                "all_tags": len(tags),
                "tag_source": tag_source,
                "store_date": dt.datetime.fromtimestamp(released, dt.UTC)
                .date()
                .isoformat()
                if released
                else None,
                "content_descriptors": store.get("content_descriptorids") or [],
                "adult": row["adult"],
                "control": row["control"],
                "pick": row["pick"],
                "shots": shots,
                "dataset_reviews": listed,
                "split": split_of(row, args.test_since, args.val_share),
                **label,
            }
        )
    out = Path(args.output)
    out.write_text(
        "".join(json.dumps(r, ensure_ascii=False) + "\n" for r in out_rows),
        encoding="utf-8",
    )
    print(f"labelled {len(out_rows)} games -> {out}; dropped {dict(dropped)}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(required=True)

    p = sub.add_parser("index", help="games.json -> compact JSONL")
    p.add_argument("games_json")
    p.add_argument("output")
    p.set_defaults(func=cmd_index)

    p = sub.add_parser("sample", help="draw the pilot sample")
    p.add_argument("games", help="JSONL from the index step")
    p.add_argument("output")
    p.add_argument("--count", type=int, default=10_000)
    p.add_argument("--per-year-floor", type=int, default=300)
    p.add_argument(
        "--control", type=int, default=1_000, help="games with full galleries"
    )
    p.add_argument(
        "--last-day", default="2026-08-15", help="last release day with 3 months"
    )
    p.add_argument(
        "--since", default=None, help="first release day (default 2011-01-01)"
    )
    p.add_argument("--all", action="store_true", help="every eligible game, no quotas")
    p.add_argument(
        "--shots",
        type=int,
        default=1,
        help="screenshots per game: the first + random others",
    )
    p.add_argument("--seed", type=int, default=20261119)
    p.set_defaults(func=cmd_sample)

    p = sub.add_parser("fetch", help="download histograms, tags and screenshots")
    p.add_argument("sample")
    p.add_argument("out")
    p.add_argument("--limit", type=int, default=0, help="only the first N games")
    p.add_argument("--histogram-delay", type=float, default=1.5)
    p.add_argument("--steamspy-delay", type=float, default=1.0)
    p.add_argument("--histogram", action=argparse.BooleanOptionalAction, default=True)
    p.add_argument("--steamspy", action=argparse.BooleanOptionalAction, default=True)
    p.add_argument(
        "--store-tags", action="store_true", help="tags from Steam's store service"
    )
    p.add_argument("--store-delay", type=float, default=2.0, help="seconds per batch")
    p.add_argument("--shot-workers", type=int, default=4)
    p.add_argument(
        "--keep-alive",
        action="store_true",
        help="screenshots over one reused HTTPS connection per worker and host",
    )
    p.add_argument(
        "--shuffle",
        type=int,
        default=None,
        metavar="SEED",
        help="download screenshots in a random order instead of by release year",
    )
    p.add_argument("--first", help="file of appids whose screenshots come first")
    p.add_argument("--report-every", type=float, default=60.0)
    p.set_defaults(func=cmd_fetch)

    p = sub.add_parser("labels", help="review count, % positive and the split")
    p.add_argument("sample")
    p.add_argument("data", help="output directory of the fetch step")
    p.add_argument("output")
    p.add_argument("--games", required=True, help="games.jsonl of the index step")
    p.add_argument(
        "--reviews",
        choices=("store", "window"),
        default="store",
        help="store: totals the store shows; window: first --days from histories",
    )
    p.add_argument("--days", type=int, default=91, help="window length")
    p.add_argument(
        "--test-since", default="2026-01-01", help="releases from this day are the test"
    )
    p.add_argument("--val-share", type=float, default=0.05)
    p.add_argument(
        "--planned-shots",
        action="store_true",
        help="list every sampled screenshot, also those not downloaded yet",
    )
    p.set_defaults(func=cmd_labels)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    sys.exit(main())
