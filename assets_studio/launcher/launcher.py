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
  One that exits with EXIT_GPU_BROKEN (a generation broke its CUDA state, the
  model is fine) is started again at once.
- The pid of the server it runs is in ~/.cache/assets-studio/launcher/<server>.pid
  until it stops that server. On exit (Ctrl+C, SIGTERM, SIGHUP) a busy or
  loading server is left running, and so is any server when the launcher dies
  without exiting; the next launcher takes it over. A restart must not lose a
  generation in progress, and a server nobody manages blocks every other one.
- One server at a time holds across launchers too: a launcher starts or takes
  over a server only while it holds the GPU lock, an exclusive flock on
  ~/.cache/assets-studio/launcher/gpu.lock whose text names the holder. Two
  launchers started by mistake would otherwise both start a server before the
  studio reports either. The kernel drops the lock when its holder's process
  ends, so a crash leaves no stale lock behind.
- A shared flock on ~/.cache/assets-studio/launcher/pause.lock pauses the
  launcher for as long as it is held: the running server finishes the jobs
  queued for it and then stops at once, and no server starts. Other programs
  take the GPU this way (`just gpu-hold <command>` holds the pause and the GPU
  lock while the command runs). The studio sends a server its next job as soon
  as the last one ends, so the launcher cannot stop one between two jobs
  without breaking the second. A pause ends with the process that holds it.

    uv run python launcher/launcher.py [--studio http://127.0.0.1:9000]
"""

import argparse
import fcntl
import json
import logging
import os
import signal
import subprocess
import sys
import time
import urllib.error
import urllib.request
from collections.abc import Callable, Iterable, Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Protocol, TextIO

from model_server_sdk import EXIT_GPU_BROKEN

logger = logging.getLogger("launcher")

# Studio server ids (as in studio.example.toml) and the model_servers/justfile
# recipes that start them. The launcher leaves servers missing here alone: start
# them yourself, or add yours here when your config names them differently.
RECIPES = {
    "qwen_image": "qwen_image",
    "flux3": "flux3",
    "birefnet": "birefnet",
    "pbrfusion4": "pbrfusion4",
    "hunyuan3d21": "hunyuan3d21",
    "hi3d": "hi3d",
    "lato2": "lato2",
    "deepmesh": "deepmesh",
    "bpt": "bpt",
    "trellis2_paint": "trellis2_paint",
    "hairdeck": "hairdeck",
    "fromsoma": "fromsoma",
    "frommhr": "frommhr",
    "rig": "mia",
    "skintokens": "skintokens",
    "unirig": "unirig",
    "motion": "kimodo",
    "motion_smplx": "kimodo_smplx",
    "mocapanything": "mocapanything",
    "gvhmr": "gvhmr",
    "gemx": "gemx",
    "htd_refine": "htd_refine",
    "prompthmr": "prompthmr",
    "vitpose": "vitpose",
    "audio2face": "audio2face",
    "animation_tools": "animation_tools",
    "sam2": "sam2",
    "sam3": "sam3",
    "paint21": "hunyuan_paint21",
    "chatterbox": "chatterbox",
    "sound": "stable_audio",
    "song": "yue2",
    "qwen3_tts_design": "qwen3_tts_design",
    "qwen3_tts_voice": "qwen3_tts_voice",
    "qwen3_tts_clone": "qwen3_tts_clone",
    "gemma": "gemma",
    "qwen": "qwen",
    "unlimited_ocr": "unlimited_ocr",
    "qwen_review": "qwen_review",
}

POLL_S = 3.0
IDLE_S = 120.0
SWITCH_S = 10.0
BACKOFF_S = 300.0
STOP_GRACE_S = 30.0
STALE_S = 30.0

ACTIVE = ("queued", "waiting_model", "running")
MODEL_SERVERS = Path(__file__).resolve().parent.parent / "model_servers"
LOGS = Path(os.environ.get("XDG_CACHE_HOME") or Path.home() / ".cache") / "assets-studio/launcher"


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


class GpuLock:
    """An exclusive flock on a file, held while a launcher runs a server.

    The lock belongs to the open file, not to a process: a second GpuLock on
    the same path, even in the same process, does not get it. It is not
    passed on to the servers, so it ends with the launcher's process.
    """

    def __init__(self, path: Path) -> None:
        self.path = path
        self._file: TextIO | None = None

    def acquire(self, holder: str) -> bool:
        """Take the lock, or keep it, for ``holder``; False while someone else has it."""
        if self._file is None:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            # Append mode: opening must not wipe the note of the one who holds it.
            file = self.path.open("a+")
            try:
                fcntl.flock(file, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                file.close()
                return False
            self._file = file
        self._file.truncate(0)
        self._file.write(
            f"{holder} (launcher pid {os.getpid()}, since {time.strftime('%Y-%m-%d %H:%M:%S')})\n"
        )
        self._file.flush()
        return True

    def release(self) -> None:
        if self._file is None:
            return
        self._file.truncate(0)
        fcntl.flock(self._file, fcntl.LOCK_UN)
        self._file.close()
        self._file = None

    def holder(self) -> str:
        """Who holds it, as the holder wrote when it took the lock. Ask only
        when the lock is taken: a crashed holder's note stays behind."""
        try:
            return self.path.read_text().strip()
        except FileNotFoundError:
            return ""


def paused_by(path: Path) -> list[str] | None:
    """Who holds a lock on ``path`` (a pause), or None when nobody does.

    The list may be empty: /proc/locks does not always say who holds it.
    """
    try:
        file = path.open()
    except FileNotFoundError:
        return None
    with file:
        try:
            fcntl.flock(file, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            return _lock_holders(path)
        fcntl.flock(file, fcntl.LOCK_UN)
    return None


def _lock_holders(path: Path) -> list[str]:
    """The processes /proc/locks names for ``path``, with their command lines."""
    try:
        inode = str(path.stat().st_ino)
        lines = Path("/proc/locks").read_text().splitlines()
    except OSError:
        return []
    holders: list[str] = []
    for line in lines:
        # "3: FLOCK  ADVISORY  READ  1234 00:1b:5678 0 EOF"; a waiter's line has "->".
        fields = line.split()
        if len(fields) < 6 or "->" in fields or fields[5].rpartition(":")[2] != inode:
            continue
        pid = fields[4]
        try:
            command = Path(f"/proc/{pid}/cmdline").read_bytes().replace(b"\0", b" ")
        except OSError:
            continue
        # The end says what runs: `flock … pause.lock <script> <command>`.
        text = command.decode(errors="replace").strip()
        holders.append(f"pid {pid}: {text if len(text) <= 160 else '…' + text[-159:]}")
    return holders


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
    lock: GpuLock = field(default_factory=lambda: GpuLock(LOGS / "gpu.lock"))
    failed_at: dict[str, float] = field(default_factory=dict[str, float])
    # The studio sees a stopped server go down only at its next check.
    stopped_at: dict[str, float] = field(default_factory=dict[str, float])
    # Why the last wanted server was not started, logged once.
    _waiting: str | None = None
    # The last poll's view, for the decision on exit.
    _demand: Demand = field(default_factory=lambda: Demand(frozenset(), ()))
    _states: Mapping[str, str] = field(default_factory=dict[str, str])

    def step(
        self,
        now: float,
        demand: Demand,
        states: Mapping[str, str],
        paused_by: Sequence[str] | None = None,
    ) -> None:
        """One poll: ``states`` maps studio server ids to their state;
        ``paused_by`` names who holds a pause, None when nobody does."""
        self._demand, self._states = demand, states
        pause = None if paused_by is None else ("; ".join(paused_by) or "holder unknown")
        current = self.current
        if current is not None and (code := current.process.poll()) is not None:
            if code == EXIT_GPU_BROKEN:
                logger.warning("%s broke its GPU state; restarting it", current.server)
            else:
                logger.warning(
                    "%s exited with code %s; not restarted for %.0f s",
                    current.server,
                    code,
                    BACKOFF_S,
                )
                self.failed_at[current.server] = now
            _forget(current.server)
            self.lock.release()
            self.current = current = None
        if current is not None:
            if demand.wants(current.server):
                current.idle_since = None
                if pause is not None:
                    self._wait(f"paused ({pause}): {current.server} finishes its jobs first")
                return
            if current.idle_since is None:
                current.idle_since = now
            others = any(server != current.server for server in self._startable(now, demand))
            grace = 0.0 if pause is not None else SWITCH_S if others else IDLE_S
            if now - current.idle_since >= grace:
                why = "the launcher is paused" if pause is not None else "no jobs left"
                logger.info("stopping %s: %s", current.server, why)
                current.process.stop()
                _forget(current.server)
                self.lock.release()
                self.stopped_at[current.server] = now
                self.current = None
            return
        if pause is not None:
            self._wait(f"paused ({pause}): no server starts")
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
            self._wait(f"{wanted} has jobs, but {busy[0]} is already running (not started here)")
            return
        if not self.lock.acquire(wanted):
            holder = self.lock.holder() or "no note"
            self._wait(f"{wanted} has jobs, but the GPU lock is taken ({holder})")
            return
        self._waiting = None
        logger.info("starting %s (just %s)", wanted, self.recipes[wanted])
        self.current = Current(wanted, self.start(self.recipes[wanted]))
        _remember(wanted, self.current.process.pid)

    def _wait(self, why: str) -> None:
        if self._waiting != why:
            logger.info("%s", why)
            self._waiting = why

    def _startable(self, now: float, demand: Demand) -> list[str]:
        return [
            server
            for server in demand.ready
            if server in self.recipes and now - self.failed_at.get(server, -BACKOFF_S) >= BACKOFF_S
        ]

    def shutdown(self) -> None:
        """Stop the server, unless it is busy or loading: then leave it running
        and its pid file for the next launcher, which takes it over (a
        restart of the launcher must not lose a generation in progress)."""
        current = self.current
        if current is None:
            return
        busy = current.server in self._demand.running or self._states.get(current.server) in (
            "busy",
            "loading",
        )
        if busy:
            logger.info(
                "leaving %s running (busy); the next launcher takes it over", current.server
            )
        else:
            logger.info("stopping %s", current.server)
            current.process.stop()
            _forget(current.server)
        # A server left running is the next launcher's: it takes the lock back.
        self.lock.release()
        self.current = None

    def adopt(self) -> None:
        """Take over a server a previous launcher left running; its pid file
        stays. One whose launcher still runs (it holds the GPU lock) is left to
        that launcher, pid file and all."""
        for path in sorted(LOGS.glob("*.pid")):
            server = path.stem
            try:
                pid = int(path.read_text())
            except ValueError:
                pid = 0
            process = AdoptedProcess(pid)
            alive = server in self.recipes and process.poll() is None
            if alive and self.current is None and self.lock.acquire(server):
                logger.info("taking over %s (pid %d) from the previous launcher", server, pid)
                self.current = Current(server, process)
            elif alive and self.current is None:
                holder = self.lock.holder() or "no note"
                logger.info("%s (pid %d) is another launcher's (%s)", server, pid, holder)
            else:
                path.unlink(missing_ok=True)


def _remember(server: str, pid: int) -> None:
    """Record a server the launcher runs, for a launcher after this one."""
    LOGS.mkdir(parents=True, exist_ok=True)
    (LOGS / f"{server}.pid").write_text(str(pid))


def _forget(server: str) -> None:
    (LOGS / f"{server}.pid").unlink(missing_ok=True)


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
    # A closed terminal or desktop session.
    signal.signal(signal.SIGHUP, _exit)
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
                pause = paused_by(LOGS / "pause.lock")
                launcher.step(time.monotonic(), demand_of(jobs), states, pause)
            time.sleep(POLL_S)
    except KeyboardInterrupt:
        pass
    finally:
        launcher.shutdown()


if __name__ == "__main__":
    main()
