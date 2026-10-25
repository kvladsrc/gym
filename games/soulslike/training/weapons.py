"""The weapons' table of a run: how often the row's weapon beats the column's
in the fights decided (DuelEpisode's Weapons/<A> vs <B>, from the run's
TensorBoard logs), over its last stretch of training; and how often it beats
the other two overall (the mean of its row: each weapon is drawn as often, so
each pair comes as often). Below it, tables of the fights between two weapons:
how often they cross in their blows' open phases (DuelEpisode's Clash/), how
many blows of its weapon the row's fighter makes a fight, and the share of
them a blow taken cut short; and of a sword against a hammer, the share of
the sword's blows the hammer blocked (Guard/), and the sword's hits a fight.
The stretch starts no earlier than the balance's last change (balance_window).

    .venv/bin/python weapons.py [run-id] [--steps 500000] [--across]
"""

import argparse
import glob
import os

from balance_window import TAG, window
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

WEAPONS = ["Sword", "Hammer", "Hands"]
NAMES = {"Sword": "sword", "Hammer": "hammer", "Hands": "hands"}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("run", nargs="?", default="duel")
    parser.add_argument(
        "--steps", type=int, default=500_000, help="the last this many steps"
    )
    parser.add_argument(
        "--across", action="store_true", help="also before the balance's last change"
    )
    args = parser.parse_args()
    here = os.path.dirname(os.path.abspath(__file__))
    files = sorted(
        glob.glob(
            os.path.join(here, "results", args.run, "Fighter", "events.out.tfevents.*")
        ),
        key=os.path.getmtime,
    )
    values: dict[str, list[tuple[int, float]]] = {}
    for f in files:
        acc = EventAccumulator(f, size_guidance={"scalars": 0})
        acc.Reload()
        for tag in acc.Tags()["scalars"]:
            if tag.startswith(("Weapons/", "Clash/", "Guard/")) or tag == TAG:
                values.setdefault(tag, []).extend(
                    (e.step, e.value) for e in acc.Scalars(tag)
                )
    if not values:
        raise SystemExit("no Weapons/ statistics in this run yet")
    last = max(step for points in values.values() for step, _ in points)
    since, balance = window(values, last, args.steps, args.across)

    def share(a: str, b: str) -> float | None:
        """How often a beats b, of the summaries in the stretch (each already a mean)."""
        low, high = sorted([a, b], key=["Hands", "Sword", "Hammer"].index)
        points = [v for s, v in values.get(f"Weapons/{low} vs {high}", []) if s > since]
        if not points:
            return None
        mean = sum(points) / len(points)
        return mean if a == low else 1 - mean

    width = 8
    print(balance)
    print(f"the row's wins over the column, steps {since}–{last}")
    print(
        " " * width
        + "".join(NAMES[w].rjust(width) for w in WEAPONS)
        + "overall".rjust(width + 2)
    )
    for a in WEAPONS:
        row = NAMES[a].ljust(width)
        known = []
        for b in WEAPONS:
            if a == b:
                row += "—".rjust(width)
            else:
                v = share(a, b)
                if v is not None:
                    known.append(v)
                row += ("?" if v is None else f"{v * 100:.0f} %").rjust(width)
        overall = sum(known) / len(known) if len(known) == len(WEAPONS) - 1 else None
        row += ("?" if overall is None else f"{overall * 100:.0f} %").rjust(width + 2)
        print(row)

    def mean(tag: str) -> float | None:
        points = [v for s, v in values.get(tag, []) if s > since]
        return sum(points) / len(points) if points else None

    armed = ["Sword", "Hammer"]

    def clashes(a: str, b: str) -> float | None:
        low, high = sorted([a, b], key=WEAPONS.index)
        return mean(f"Clash/{low} vs {high}")

    tables = [
        ("weapons crossed a fight", clashes, lambda v: f"{v:.2f}"),
        (
            "the row's weapon blows a fight",
            lambda a, b: mean(f"Clash/{a} vs {b}: blows"),
            lambda v: f"{v:.1f}",
        ),
        (
            "the row's blows cut short",
            lambda a, b: mean(f"Clash/{a} vs {b}: blows cut"),
            lambda v: f"{v * 100:.0f} %",
        ),
    ]
    if all(clashes(a, b) is None for a in armed for b in armed):
        return
    for title, value, shown in tables:
        print()
        print(title)
        print(" " * width + "".join(NAMES[w].rjust(width) for w in armed))
        for a in armed:
            row = NAMES[a].ljust(width)
            for b in armed:
                v = value(a, b)
                row += ("?" if v is None else shown(v)).rjust(width)
            print(row)

    blocked = mean("Guard/Sword blows blocked")
    hits = mean("Guard/Sword hits")
    if hits is not None:
        print()
        print("the hammer as a shield (sword against hammer)")
        for label, v, shown in [
            ("sword blows the hammer blocked", blocked, lambda v: f"{v * 100:.0f} %"),
            ("sword hits a fight", hits, lambda v: f"{v:.1f}"),
        ]:
            print(
                f"  {label}".ljust(32) + ("?" if v is None else shown(v)).rjust(width)
            )


if __name__ == "__main__":
    main()
