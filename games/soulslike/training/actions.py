"""Which actions a run's fighters take, by what they hold: each action's share
of that weapon's actions over the run's last stretch of training, and of its
blows the share that landed (DuelEpisode's Actions/ and Actions landed/, from
the run's TensorBoard logs). One under half a percent is all but forgotten.
The stretch starts no earlier than the balance's last change (balance_window).

    .venv/bin/python actions.py [run-id] [--steps 500000] [--weapon sword] [--across]
"""

import argparse
import glob
import os

from balance_window import TAG, window
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

WEAPONS = {"sword": "Sword", "hammer": "Hammer", "hands": "Hands"}
NAMES = {"Sword": "sword", "Hammer": "hammer", "Hands": "hands"}
RARE = 0.005


def usage(
    run: str, steps: int, across: bool
) -> tuple[dict[str, dict[str, tuple[float, float | None]]], str, int, int]:
    """Per weapon held, each action's (share of that weapon's actions, share
    of its blows landed or None), and the balance's line and the stretch."""
    here = os.path.dirname(os.path.abspath(__file__))
    files = sorted(
        glob.glob(
            os.path.join(here, "results", run, "Fighter", "events.out.tfevents.*")
        ),
        key=os.path.getmtime,
    )
    values: dict[str, list[tuple[int, float]]] = {}
    for f in files:
        acc = EventAccumulator(f, size_guidance={"scalars": 0})
        acc.Reload()
        for tag in acc.Tags()["scalars"]:
            if tag.startswith("Actions") or tag == TAG:
                values.setdefault(tag, []).extend(
                    (e.step, e.value) for e in acc.Scalars(tag)
                )
    if not values:
        return {}, "no Actions/ stats in this run", 0, 0
    last = max(step for points in values.values() for step, _ in points)
    since, balance = window(values, last, steps, across)

    def total(tag: str) -> float:
        return sum(v for s, v in values.get(tag, []) if s > since)

    table: dict[str, dict[str, tuple[float, float | None]]] = {}
    for held in WEAPONS.values():
        prefix = f"Actions/{held}/"
        rows = {}
        for tag in values:
            if tag.startswith(prefix):
                name = tag[len(prefix) :]
                landed_tag = f"Actions landed/{held}/{name}"
                rows[name] = (
                    total(tag),
                    total(landed_tag) if landed_tag in values else None,
                )
        count = sum(taken for taken, _ in rows.values())
        if count:
            table[held] = {
                name: (
                    taken / count,
                    None if landed is None or taken == 0 else landed / taken,
                )
                for name, (taken, landed) in rows.items()
            }
            table[held]["__count__"] = (count, None)
    return table, balance, since, last


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("run", nargs="?", default="duel")
    parser.add_argument(
        "--steps", type=int, default=500_000, help="the last this many steps"
    )
    parser.add_argument("--weapon", choices=list(WEAPONS), help="only this weapon")
    parser.add_argument(
        "--across", action="store_true", help="also before the balance's last change"
    )
    args = parser.parse_args()
    table, balance, since, last = usage(args.run, args.steps, args.across)
    if not table:
        raise SystemExit(balance)
    print(balance)
    print(f"actions by the weapon in hand, steps {since}–{last}")
    for held in [WEAPONS[args.weapon]] if args.weapon else WEAPONS.values():
        rows = dict(table.get(held, {}))
        count = rows.pop("__count__", (0, None))[0]
        if not count:
            continue
        ordered = sorted(rows.items(), key=lambda r: -r[1][0])
        used = sum(1 for _, (share, _) in ordered if share >= RARE)
        print()
        print(
            f"{NAMES[held]}: {count:.0f} actions, {used} of {len(ordered)} in use (from {RARE * 100:.1f} %)"
        )
        print("   share landed  action")
        for name, (share, hit) in ordered:
            shown = "—" if hit is None else f"{hit * 100:.0f} %"
            mark = "  ·" if share < RARE else ""
            print(f"{share * 100:6.1f} %{shown:>7}  {name}{mark}")


if __name__ == "__main__":
    main()
