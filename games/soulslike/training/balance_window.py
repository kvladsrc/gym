"""The stretch of a run the stats scripts look at (weapons.py, actions.py):
the last this many steps, but not from before the balance last changed
(DuelEpisode's Balance/version, the hash of training/balance.json in use)."""

TAG = "Balance/version"


def window(values: dict[str, list[tuple[int, float]]], last: int, steps: int, across: bool) -> tuple[int, str]:
    """The step the stretch starts after, and a line telling the balance."""
    since = max(0, last - steps)
    points = sorted(values.get(TAG, []))
    if not points:
        return since, "balance: not written (the player predates balance.json)"
    changed, version = points[0]
    for step, value in points[1:]:
        if value != version:
            changed, version = step, value
    line = f"balance {version:06.0f} from step {changed}"
    if not across and changed > since:
        since = changed
        line += " (the window starts at the balance change; --across for all)"
    return since, line
