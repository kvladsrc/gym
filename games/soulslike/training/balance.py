"""The balance's panel: training/balance.json edited in the browser, checked
before it is saved, beside the run's numbers.

    .venv/bin/python balance.py [run-id] [--port 8765]   (balance.sh)

Serves 127.0.0.1 only. A save is checked against the duellists' actions
(actions.json, written by the editor's Build Arena): every base, rule key,
weapon and animation named must exist, every factor be a positive number.
The file is replaced at once (a player reading it never sees half of it),
the one it replaces kept in balance_history/. The players of a running
training take it in as their next fights begin (Balance.cs); their logs say
so ("Soulslike: balance <hash>"), and the panel shows those lines.
"""

import argparse
import glob
import json
import os
import re
import subprocess
import sys
import time
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
BALANCE = os.path.join(HERE, "balance.json")
ACTIONS = os.path.join(HERE, "actions.json")
HISTORY = os.path.join(HERE, "balance_history")
PAGE = os.path.join(HERE, "balance.html")
MOVES_PAGE = os.path.join(HERE, "actions.html")

HELD = ["sword", "hammer", "hands"]
PARTS = ["sword", "hammer", "limb"]
FACTORS = ["damage", "speed", "stagger"]
# Factors past these are allowed, but told: likely a slip.
SANE = (0.5, 2.0)


def actions() -> list[dict]:
    with open(ACTIONS) as f:
        return json.load(f)["actions"]


def check(balance: object, known: list[dict]) -> tuple[list[str], list[str]]:
    """The errors (it is not saved) and the warnings (it is) of a balance."""
    errors: list[str] = []
    warnings: list[str] = []
    if not isinstance(balance, dict):
        return ["not a JSON object"], []
    for key in balance:
        if key not in ("base", "evade", "rules"):
            errors.append(f"unknown key '{key}'")
    base = balance.get("base")
    if not isinstance(base, dict):
        errors.append("no base")
        base = {}
    for part in PARTS:
        if part not in base:
            errors.append(f"base: no '{part}' (its blows would do no damage)")
    for part, value in base.items():
        where = f"base.{part}"
        if part not in PARTS:
            errors.append(
                f"{where}: no such striking part (there are {', '.join(PARTS)})"
            )
            continue
        if not isinstance(value, dict):
            errors.append(f"{where}: not an object")
            continue
        for key in value:
            if key not in ("damage", "stagger", "note"):
                errors.append(f"{where}: unknown key '{key}'")
        for key in ("damage", "stagger"):
            v = value.get(key)
            if not isinstance(v, (int, float)) or isinstance(v, bool) or v < 0:
                errors.append(f"{where}.{key}: needs a number ≥ 0")
    evade = balance.get("evade", {})
    if not isinstance(evade, dict):
        errors.append("evade: not an object")
        evade = {}
    for key in evade:
        if key not in ("from", "to", "cancel", "note"):
            errors.append(f"evade: unknown key '{key}'")
    window = [evade.get(k, d) for k, d in (("from", 0.2), ("to", 0.6))]
    if not all(
        isinstance(v, (int, float)) and not isinstance(v, bool) and 0 <= v <= 1
        for v in window
    ):
        errors.append("evade: from and to need numbers 0–1 (shares of the action)")
    elif window[0] >= window[1]:
        errors.append("evade: from must come before to")
    elif not (
        isinstance(evade.get("cancel", 0), (int, float))
        and 0 <= evade.get("cancel", 0) <= 1
    ):
        errors.append("evade.cancel: needs a number 0–1 (0: blows are never dropped)")
    elif evade.get("cancel", 0) > 0.2:
        warnings.append(
            "evade.cancel past 0.2: a blow may be dropped after it can strike"
        )
    elif window[1] - window[0] > 0.6:
        warnings.append(f"evade: untouchable for {window[1] - window[0]:.0%} of a roll")
    rules = balance.get("rules", [])
    if not isinstance(rules, list):
        errors.append("rules: not a list")
        rules = []
    names = {a["name"]: a for a in known}
    seen: dict[tuple, int] = {}
    for i, rule in enumerate(rules, 1):
        where = f"rule {i}"
        if not isinstance(rule, dict):
            errors.append(f"{where}: not an object")
            continue
        for key in rule:
            if key not in ("held", "blow", "note", *FACTORS):
                errors.append(f"{where}: unknown key '{key}'")
        held, blow = rule.get("held") or None, rule.get("blow") or None
        if held is not None and held not in HELD:
            errors.append(f"{where}: no weapon '{held}' (there are {', '.join(HELD)})")
        if blow is not None:
            action = names.get(blow)
            if action is None:
                errors.append(f"{where}: no animation '{blow}'")
            elif held in HELD and action["needs"] not in ("any", held):
                errors.append(
                    f"{where}: '{blow}' is not open to '{held}' (only {action['needs']})"
                )
        given = [k for k in FACTORS if k in rule]
        if not given:
            warnings.append(f"{where}: no factor, it changes nothing")
        for key in given:
            v = rule[key]
            if not isinstance(v, (int, float)) or isinstance(v, bool) or v <= 0:
                errors.append(f"{where}.{key}: needs a number > 0")
            elif not SANE[0] <= v <= SANE[1]:
                warnings.append(f"{where}.{key} = {v}: more than twofold")
        filters = (held, blow)
        if filters in seen:
            warnings.append(
                f"{where}: the same address as rule {seen[filters]} (the factors multiply)"
            )
        seen.setdefault(filters, i)
    return errors, warnings


def save(balance: dict) -> None:
    os.makedirs(HISTORY, exist_ok=True)
    if os.path.exists(BALANCE):
        stamp = time.strftime("%Y-%m-%d_%H-%M-%S")
        with (
            open(BALANCE) as f,
            open(os.path.join(HISTORY, f"{stamp}.json"), "w") as out,
        ):
            out.write(f.read())
    temp = BALANCE + ".tmp"
    with open(temp, "w") as f:
        json.dump(balance, f, ensure_ascii=False, indent=2)
        f.write("\n")
    os.replace(temp, BALANCE)


def players(run: str) -> list[dict]:
    """Each player log's balance lines: the last hash taken, and any complaint."""
    shown = []
    for path in sorted(
        glob.glob(os.path.join(HERE, "results", run, "run_logs", "Player-*.log"))
    ):
        last, count, complaints = None, 0, []
        try:
            with open(path, errors="replace") as f:
                for line in f:
                    if line.startswith("Soulslike: balance "):
                        last = line.split()[2]
                        count += 1
                    elif (
                        "Soulslike: balance not" in line
                        or "names no such animation" in line
                    ):
                        complaints.append(line.strip())
        except OSError:
            continue
        shown.append(
            {
                "log": os.path.basename(path),
                "hash": last,
                "taken": count,
                "complaints": complaints[-3:],
                "age": int(time.time() - os.path.getmtime(path)),
            }
        )
    return shown


def stats(run: str, steps: int) -> str:
    out = []
    for script in ("weapons.py", "actions.py"):
        done = subprocess.run(
            [sys.executable, os.path.join(HERE, script), run, "--steps", str(steps)],
            capture_output=True,
            text=True,
            timeout=120,
            check=False,
        )
        out.append((done.stdout or "") + (done.stderr or ""))
    return "\n\n".join(out)


def plain(name: str) -> str:
    """A clip's name as the Mixamo package's rename made it (stats keep the old)."""
    s = re.sub(r"\s*\((\d+)\)", r" \1", name.lower())
    return re.sub(r"[^a-z0-9]+", "_", s).strip("_")


def view(run: str, steps: int, stats: bool = True) -> dict:
    """The actions as the dashboard shows them: each with, per
    weapon that may take it, its share of that weapon's actions and of its
    blows landed (DuelEpisode's stats, by the action's place; none where the
    place held another clip then). Without stats, at once: reading the
    run's logs takes seconds."""
    import actions as stats_actions

    table, line, since, last = (
        stats_actions.usage(run, steps, False) if stats else ({}, "", None, None)
    )
    known = actions()
    for i, a in enumerate(known):
        a["index"] = i
        a["usage"] = {}
        for held, rows in table.items():
            for tag, (share, hit) in rows.items():
                number, _, name = tag.partition(" ")
                if number.isdigit() and int(number) == i:
                    same = (
                        plain(name) == a["name"]
                        or name == "roll"
                        and a["name"] == "roll"
                    )
                    a["usage"][held.lower()] = {
                        "share": share,
                        "hit": hit,
                        "was": None if same else name,
                    }
    with open(BALANCE) as f:
        balance = json.load(f)
    return {
        "actions": known,
        "balance": balance,
        "line": line,
        "since": since,
        "last": last,
    }


def handler(run: str):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def send(self, code: int, body: object, kind: str = "application/json") -> None:
            data = (
                body
                if isinstance(body, bytes)
                else json.dumps(body, ensure_ascii=False).encode()
            )
            self.send_response(code)
            self.send_header(
                "Content-Type",
                kind
                + (
                    "; charset=utf-8"
                    if kind.startswith(("text/", "application/json"))
                    else ""
                ),
            )
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            try:
                self.wfile.write(data)
            except (BrokenPipeError, ConnectionResetError):
                pass  # the page went (reloaded, closed) before its answer

        def do_GET(self) -> None:
            path = self.path.split("?")[0]
            if path == "/":
                with open(PAGE, "rb") as f:
                    self.send(200, f.read(), "text/html")
            elif path == "/actions":
                with open(MOVES_PAGE, "rb") as f:
                    self.send(200, f.read(), "text/html")
            elif path == "/api/view":
                query = dict(
                    p.split("=", 1)
                    for p in self.path.partition("?")[2].split("&")
                    if "=" in p
                )
                self.send(
                    200,
                    view(
                        run, int(query.get("steps", 500000)), query.get("stats") != "0"
                    ),
                )
            elif path == "/api/state":
                with open(BALANCE) as f:
                    balance = json.load(f)
                history = (
                    sorted(os.listdir(HISTORY), reverse=True)[:30]
                    if os.path.isdir(HISTORY)
                    else []
                )
                self.send(
                    200,
                    {
                        "balance": balance,
                        "actions": actions(),
                        "run": run,
                        "players": players(run),
                        "history": history,
                    },
                )
            elif path == "/api/players":
                self.send(200, players(run))
            elif path.startswith("/api/history/"):
                name = os.path.basename(path[len("/api/history/") :])
                try:
                    with open(os.path.join(HISTORY, name)) as f:
                        self.send(200, json.load(f))
                except (OSError, json.JSONDecodeError):
                    self.send(404, {"error": "no such version"})
            elif path == "/api/stats":
                query = dict(
                    p.split("=", 1)
                    for p in self.path.partition("?")[2].split("&")
                    if "=" in p
                )
                self.send(200, {"text": stats(run, int(query.get("steps", 500000)))})
            else:
                self.send(404, {"error": "not found"})

        def do_POST(self) -> None:
            length = int(self.headers.get("Content-Length", 0))
            try:
                balance = json.loads(self.rfile.read(length))
            except json.JSONDecodeError as e:
                self.send(400, {"errors": [f"not JSON: {e}"], "warnings": []})
                return
            errors, warnings = check(balance, actions())
            if self.path == "/api/save" and not errors:
                save(balance)
                self.send(200, {"saved": True, "errors": [], "warnings": warnings})
            else:
                self.send(200, {"saved": False, "errors": errors, "warnings": warnings})

    return Handler


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("run", nargs="?", default="duel")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--no-browser", action="store_true")
    parser.add_argument(
        "--check", action="store_true", help="check balance.json and exit"
    )
    args = parser.parse_args()
    if not os.path.exists(ACTIONS):
        raise SystemExit("no actions.json: build the arena (Soulslike → Build Arena)")
    with open(BALANCE) as f:
        errors, warnings = check(json.load(f), actions())
    for e in errors:
        print("in balance.json:", e, file=sys.stderr)
    if args.check:
        for w in warnings:
            print("in balance.json (warning):", w, file=sys.stderr)
        raise SystemExit(1 if errors else 0)
    server = ThreadingHTTPServer(("127.0.0.1", args.port), handler(args.run))
    url = f"http://127.0.0.1:{args.port}/"
    print(f"balance panel: {url}  (Ctrl+C to stop)")
    if not args.no_browser:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
