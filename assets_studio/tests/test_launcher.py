"""The launcher's decisions, with stand-in processes."""

from pathlib import Path
from typing import Any

import pytest

import launcher
from launcher import BACKOFF_S, IDLE_S, SWITCH_S, Demand, Launcher, demand_of


def job(id: str, server: str, status: str = "queued", after: str | None = None) -> dict[str, Any]:
    deps = {"image": {"job_id": after, "output_index": 0}} if after else {}
    return {"id": id, "server": server, "status": status, "created_at": id, "dependencies": deps}


class Fake:
    def __init__(self, recipe: str) -> None:
        self.recipe = recipe
        self.pid = 4242
        self.code: int | None = None
        self.stopped = False

    def poll(self) -> int | None:
        return self.code

    def stop(self) -> None:
        self.stopped = True


def make() -> tuple[Launcher, list[Fake]]:
    started: list[Fake] = []

    def start(recipe: str) -> Fake:
        started.append(Fake(recipe))
        return started[-1]

    return Launcher(start=start), started


def test_jobs_waiting_on_others_are_not_ready() -> None:
    demand = demand_of(
        [job("1", "flux", "running"), job("2", "hunyuan3d", after="1"), job("3", "sound")]
    )
    assert demand == Demand(frozenset({"flux"}), ("sound",))


def test_a_finished_dependency_makes_the_job_ready() -> None:
    assert demand_of([job("2", "hunyuan3d", after="gone")]).ready == ("hunyuan3d",)


def test_the_oldest_ready_job_picks_the_server() -> None:
    demand = demand_of([job("2", "sound"), job("1", "hunyuan3d"), job("3", "hunyuan3d")])
    assert demand.ready == ("hunyuan3d", "sound")


def test_starts_one_server_and_keeps_it_while_it_has_jobs() -> None:
    run, started = make()
    run.step(0, Demand(frozenset(), ("hunyuan3d", "sound")), {})
    run.step(5, Demand(frozenset({"hunyuan3d"}), ("sound",)), {"hunyuan3d": "busy"})
    assert [p.recipe for p in started] == ["hunyuan3d"]
    assert not started[0].stopped


def test_interleaved_jobs_run_by_server_not_by_queue() -> None:
    # A queue of hunyuan3d, sound, hunyuan3d, sound, hunyuan3d, sound: the loaded server
    # runs all its jobs, also those queued after another server's; two
    # starts, not six.
    run, started = make()
    queue = [job(f"{i}", "hunyuan3d" if i % 2 == 0 else "sound") for i in range(6)]
    now = 0.0
    while queue:
        current = run.current.server if run.current else None
        run.step(now, demand_of(queue), {current: "ready"} if current else {})
        current = run.current.server if run.current else None
        mine = [j for j in queue if j["server"] == current]
        if mine:
            queue.remove(mine[0])
        now += 5
    assert [p.recipe for p in started] == ["hunyuan3d", "stable_audio"]


def test_an_idle_server_stops_after_the_grace() -> None:
    run, started = make()
    run.step(0, Demand(frozenset(), ("hunyuan3d",)), {})
    run.step(1, Demand(frozenset(), ()), {"hunyuan3d": "ready"})
    run.step(1 + IDLE_S - 1, Demand(frozenset(), ()), {"hunyuan3d": "ready"})
    assert not started[0].stopped
    run.step(1 + IDLE_S, Demand(frozenset(), ()), {"hunyuan3d": "ready"})
    assert started[0].stopped
    assert run.current is None


def test_switches_soon_when_another_server_has_jobs() -> None:
    run, started = make()
    run.step(0, Demand(frozenset(), ("hunyuan3d",)), {})
    run.step(1, Demand(frozenset(), ("sound",)), {"hunyuan3d": "ready"})
    run.step(1 + SWITCH_S, Demand(frozenset(), ("sound",)), {"hunyuan3d": "ready"})
    assert started[0].stopped
    # The studio still reports the stopped server; that must not block the next.
    run.step(2 + SWITCH_S, Demand(frozenset(), ("sound",)), {"hunyuan3d": "ready"})
    assert [p.recipe for p in started] == ["hunyuan3d", "stable_audio"]


def test_waits_for_a_server_started_by_someone_else() -> None:
    run, started = make()
    run.step(0, Demand(frozenset(), ("hunyuan3d",)), {"flux": "ready"})
    assert started == []
    run.step(3, Demand(frozenset(), ("hunyuan3d",)), {"flux": "unavailable"})
    assert [p.recipe for p in started] == ["hunyuan3d"]


def test_a_crashed_server_is_not_restarted_at_once() -> None:
    run, started = make()
    run.step(0, Demand(frozenset(), ("hunyuan3d",)), {})
    started[0].code = 1
    run.step(3, Demand(frozenset(), ("hunyuan3d", "sound")), {})
    assert [p.recipe for p in started] == ["hunyuan3d", "stable_audio"]
    started[1].code = 0
    run.step(4, Demand(frozenset(), ("hunyuan3d",)), {})
    assert len(started) == 2
    run.step(3 + BACKOFF_S, Demand(frozenset(), ("hunyuan3d",)), {})
    assert [p.recipe for p in started] == ["hunyuan3d", "stable_audio", "hunyuan3d"]


def test_unknown_servers_are_ignored() -> None:
    run, started = make()
    run.step(0, Demand(frozenset(), ("fake",)), {})
    assert started == []


def test_every_recipe_exists_in_the_model_servers_justfile() -> None:
    justfile = (launcher.MODEL_SERVERS / "justfile").read_text()
    for recipe in launcher.RECIPES.values():
        assert f"\n{recipe} *args:" in justfile, recipe


def test_a_busy_server_is_left_running_for_the_next_launcher(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(launcher, "LOGS", tmp_path)
    run, started = make()
    run.step(0, Demand(frozenset(), ("hunyuan3d",)), {})
    run.step(1, Demand(frozenset({"hunyuan3d"}), ()), {"hunyuan3d": "busy"})
    run.shutdown()
    assert not started[0].stopped
    assert (tmp_path / "hunyuan3d.pid").read_text() == "4242"


def test_an_idle_server_is_stopped_on_exit(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(launcher, "LOGS", tmp_path)
    run, started = make()
    run.step(0, Demand(frozenset(), ("hunyuan3d",)), {})
    run.step(1, Demand(frozenset(), ()), {"hunyuan3d": "ready"})
    run.shutdown()
    assert started[0].stopped
    assert not list(tmp_path.glob("*.pid"))


def test_a_live_server_left_behind_is_taken_over(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import os

    monkeypatch.setattr(launcher, "LOGS", tmp_path)
    (tmp_path / "hunyuan3d.pid").write_text(str(os.getpgrp()))  # a live process group
    (tmp_path / "sound.pid").write_text("999999999")  # gone
    run, started = make()
    run.adopt()
    assert run.current is not None
    assert run.current.server == "hunyuan3d"
    assert not list(tmp_path.glob("*.pid"))
    # It is managed as usual: kept while it has jobs, not started again.
    run.step(0, Demand(frozenset({"hunyuan3d"}), ()), {"hunyuan3d": "busy"})
    assert started == []
