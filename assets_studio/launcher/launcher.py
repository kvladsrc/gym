"""Starts model servers when the studio has jobs for them and stops them after.

A stand-in outside the studio's design, where the user starts the servers
(ADR-002): it lets work happen in the studio alone. It polls the studio's
REST API, runs one server at a time (the GPU holds one heavy model), and
leaves servers it did not start alone.

- A job is ready when the jobs it depends on are no longer pending or running;
  the server of the oldest ready job is started with `just <recipe>` from
  model_servers/.
- A server stops when it has neither running nor ready jobs: after IDLE_S, or
  after SWITCH_S when another server has ready jobs.
- A server that exits on its own is not restarted for BACKOFF_S; its jobs keep
  waiting in the studio. Its output is in ~/.cache/assets-studio/launcher/.
- On exit a busy or loading server is left running, its pid in
  ~/.cache/assets-studio/launcher/<server>.pid; the next launcher takes it
  over (a restart must not lose a generation in progress).

    uv run python launcher/launcher.py [--studio http://127.0.0.1:9000]
"""

import argparse
import json
import logging
import os
import signal
import subprocess
import sys
import time
import urllib.error
import urllib.request
from collections.abc import Callable, Iterable, Mapping
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Protocol

logger = logging.getLogger("launcher")

# Studio server ids (studio.example.toml) and their model_servers/justfile recipes.
RECIPES = {
    "flux": "flux",
    "qwen_image": "qwen_image",
    "hunyuan3d": "hunyuan3d",
    "rig": "mia",
    "paint": "hunyuan_paint",
    "voice": "xtts",
    "sound": "stable_audio",
    "song": "yue2",
    "gemma": "gemma",
    "qwen": "qwen",
    "video": "wan",
}

POLL_S = 3.0
IDLE_S = 120.0
SWITCH_S = 10.0
BACKOFF_S = 300.0
STOP_GRACE_S = 30.0
STALE_S = 30.0

ACTIVE = ("queued", "waiting_model", "running")
MODEL_SERVERS = Path(__file__).resolve().parent.parent / "model_servers"
LOGS = Path(os.environ.get("XDG_CACHE_HOME", Path.home() / ".cache")) / "assets-studio" / "launcher"


@dataclass(frozen=True)
class Demand:
    """What the studio's queue needs right now."""

    running: frozenset[str]
    # Servers with jobs ready to be sent, by their oldest such job.
    ready: tuple[str, ...]

    def wants(self, server: str) -> bool:
        return server in self.running or server in self.ready


def demand_of(jobs: Iterable[Mapping[str, Any]]) -> Demand:
    """The demand of the active (queued, waiting or running) jobs."""
    jobs = list(jobs)
    active = {job["id"] for job in jobs}
    running = frozenset(job["server"] for job in jobs if job["status"] == "running")
    ready: list[str] = []
    for job in sorted(jobs, key=lambda job: job["created_at"]):
        if job["status"] == "running" or job["server"] in ready:
            continue
        # A dependency that is no longer active has finished; if it failed,
        # the studio fails this job too.
        if all(dep["job_id"] not in active for dep in job.get("dependencies", {}).values()):
            ready.append(job["server"])
    return Demand(running, tuple(ready))


class Process(Protocol):
    pid: int

    def poll(self) -> int | None: ...

    def stop(self) -> None: ...


@dataclass
class Current:
    server: str
    process: Process
    idle_since: float | None = None


@dataclass
class Launcher:
    start: Callable[[str], Process]
    recipes: Mapping[str, str] = field(default_factory=lambda: dict(RECIPES))
    current: Current | None = None
    failed_at: dict[str, float] = field(default_factory=dict[str, float])
    # The studio sees a stopped server go down only at its next check.
    stopped_at: dict[str, float] = field(default_factory=dict[str, float])
    _waiting_for: str | None = None
    # The last poll's view, for the decision on exit.
    _demand: Demand = field(default_factory=lambda: Demand(frozenset(), ()))
    _states: Mapping[str, str] = field(default_factory=dict[str, str])

    def step(self, now: float, demand: Demand, states: Mapping[str, str]) -> None:
        """One poll: ``states`` maps studio server ids to their state."""
        self._demand, self._states = demand, states
        current = self.current
        if current is not None and (code := current.process.poll()) is not None:
            logger.warning(
                "%s exited with code %s; not restarted for %.0f s", current.server, code, BACKOFF_S
            )
            self.failed_at[current.server] = now
            self.current = current = None
        if current is not None:
            if demand.wants(current.server):
                current.idle_since = None
                return
            if current.idle_since is None:
                current.idle_since = now
            others = any(server != current.server for server in self._startable(now, demand))
            if now - current.idle_since >= (SWITCH_S if others else IDLE_S):
                logger.info("stopping %s: no jobs left", current.server)
                current.process.stop()
                self.stopped_at[current.server] = now
                self.current = None
            return
        wanted = next(iter(self._startable(now, demand)), None)
        if wanted is None:
            return
        # A managed server someone else started holds the GPU: wait for it.
        busy = [
            server
            for server in self.recipes
            if states.get(server, "unavailable") != "unavailable"
            and now - self.stopped_at.get(server, -STALE_S) >= STALE_S
        ]
        if busy:
            if self._waiting_for != wanted:
                logger.info(
                    "%s has jobs, but %s is already running (not started here)", wanted, busy[0]
                )
                self._waiting_for = wanted
            return
        self._waiting_for = None
        logger.info("starting %s (just %s)", wanted, self.recipes[wanted])
        self.current = Current(wanted, self.start(self.recipes[wanted]))

    def _startable(self, now: float, demand: Demand) -> list[str]:
        return [
            server
            for server in demand.ready
            if server in self.recipes and now - self.failed_at.get(server, -BACKOFF_S) >= BACKOFF_S
        ]

    def shutdown(self) -> None:
        """Stop the server, unless it is busy or loading: then leave it running
        and leave its pid for the next launcher, which takes it over (a
        restart of the launcher must not lose a generation in progress)."""
        current = self.current
        if current is None:
            return
        busy = current.server in self._demand.running or self._states.get(current.server) in (
            "busy",
            "loading",
        )
        if busy:
            LOGS.mkdir(parents=True, exist_ok=True)
            (LOGS / f"{current.server}.pid").write_text(str(current.process.pid))
            logger.info(
                "leaving %s running (busy); the next launcher takes it over", current.server
            )
        else:
            logger.info("stopping %s", current.server)
            current.process.stop()
        self.current = None

    def adopt(self) -> None:
        """Take over a server a previous launcher left running."""
        for path in sorted(LOGS.glob("*.pid")):
            server = path.stem
            try:
                pid = int(path.read_text())
            except ValueError:
                pid = 0
            path.unlink(missing_ok=True)
            process = AdoptedProcess(pid)
            if self.current is None and server in self.recipes and process.poll() is None:
                logger.info("taking over %s (pid %d) from the previous launcher", server, pid)
                self.current = Current(server, process)


def _stop_group(pid: int, wait: Callable[[float], bool]) -> None:
    """Ctrl+C first (the servers unload the model and free the GPU cleanly),
    then SIGTERM, then SIGKILL; ``wait`` returns True once the process ended."""
    for sig, seconds in (
        (signal.SIGINT, STOP_GRACE_S),
        (signal.SIGTERM, 10.0),
        (signal.SIGKILL, 5.0),
    ):
        try:
            os.killpg(pid, sig)
        except ProcessLookupError:
            return
        if wait(seconds):
            return


class AdoptedProcess:
    """A server another launcher started (not our child: no exit code)."""

    def __init__(self, pid: int) -> None:
        self.pid = pid

    def poll(self) -> int | None:
        if self.pid <= 0:
            return 0
        try:
            os.killpg(self.pid, 0)
        except (ProcessLookupError, PermissionError):
            return 0
        return None

    def stop(self) -> None:
        def wait(seconds: float) -> bool:
            deadline = time.monotonic() + seconds
            while time.monotonic() < deadline:
                if self.poll() is not None:
                    return True
                time.sleep(0.5)
            return False

        _stop_group(self.pid, wait)


class ServerProcess:
    """`just <recipe>` in its own process group, so that a stop reaches the
    whole chain (just → nix develop → uv → the server)."""

    def __init__(self, recipe: str) -> None:
        LOGS.mkdir(parents=True, exist_ok=True)
        log = (LOGS / f"{recipe}.log").open("a")
        log.write(f"\n=== {time.strftime('%Y-%m-%d %H:%M:%S')} just {recipe}\n")
        log.flush()
        # The launcher runs in the studio's environment; each server has its own.
        env = {key: value for key, value in os.environ.items() if key != "VIRTUAL_ENV"}
        # The recipe comes from RECIPES, not from the studio.
        self._popen = subprocess.Popen(  # noqa: S603
            ["just", "--justfile", str(MODEL_SERVERS / "justfile"), recipe],  # noqa: S607
            cwd=MODEL_SERVERS,
            stdin=subprocess.DEVNULL,
            stdout=log,
            stderr=subprocess.STDOUT,
            start_new_session=True,
            env=env,
        )
        log.close()
        self.pid = self._popen.pid

    def poll(self) -> int | None:
        return self._popen.poll()

    def stop(self) -> None:
        def wait(seconds: float) -> bool:
            try:
                self._popen.wait(seconds)
                return True
            except subprocess.TimeoutExpired:
                return False

        _stop_group(self._popen.pid, wait)


def fetch(studio: str, path: str) -> Any:
    with urllib.request.urlopen(f"{studio}{path}", timeout=10) as response:  # noqa: S310
        return json.load(response)


def _exit(signum: int, frame: object) -> None:
    sys.exit(0)  # through the finally below: the started server stops too


def main() -> None:
    parser = argparse.ArgumentParser(description="Start model servers for the studio's jobs.")
    parser.add_argument("--studio", default="http://127.0.0.1:9000")
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s", datefmt="%H:%M:%S")
    launcher = Launcher(start=ServerProcess)
    launcher.adopt()
    signal.signal(signal.SIGTERM, _exit)
    query = "&".join(f"status={status}" for status in ACTIVE)
    reachable = True
    try:
        while True:
            try:
                jobs = fetch(args.studio, f"/api/jobs?{query}&limit=1000")
                states = {
                    server["id"]: server["state"] for server in fetch(args.studio, "/api/servers")
                }
            except (urllib.error.URLError, OSError, ValueError) as error:
                if reachable:
                    logger.warning("studio unreachable (%s); keeping the current server", error)
                reachable = False
            else:
                reachable = True
                launcher.step(time.monotonic(), demand_of(jobs), states)
            time.sleep(POLL_S)
    except KeyboardInterrupt:
        pass
    finally:
        launcher.shutdown()


if __name__ == "__main__":
    main()
