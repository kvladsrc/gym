"""Projects (ADR-010): every asset and job belongs to one; "default" holds what came before."""

from pathlib import Path

import pytest
from fake_model_server import FakeModelServer
from studio.domain import JobStatus
from studio.services.generation import JobRequestError
from studio.services.projects import ProjectConflict, ProjectError
from support import live_server
from test_api import call, fake_and_studio, wait_ready
from test_studio import model_server, png, running_studio, wait_for

from model_server_sdk import create_app, media


def test_without_a_project_everything_goes_to_default(tmp_path: Path) -> None:
    with running_studio(tmp_path) as studio:
        (default,) = studio.projects.all()
        assert (default.id, default.title, default.asset_count) == ("default", "Default", 0)
        asset = studio.library.import_bytes(png())
        assert asset.project == "default"
        assert studio.projects.get("default").asset_count == 1


def test_an_id_is_made_from_the_title(tmp_path: Path) -> None:
    with running_studio(tmp_path) as studio:
        assert studio.projects.create("Выучка").id == "vyuchka"
        assert studio.projects.create("  Выучка  ").id == "vyuchka-2"  # taken: numbered
        assert studio.projects.create("!!!").id == "project"
        named = studio.projects.create("Soulslike: boss", "souls")
        assert (named.id, named.title) == ("souls", "Soulslike: boss")
        with pytest.raises(ProjectConflict):
            studio.projects.create("Again", "souls")
        with pytest.raises(ProjectError):
            studio.projects.create("Bad id", "Souls Like")
        with pytest.raises(ProjectError):
            studio.projects.create("   ")
        assert [project.id for project in studio.projects.all()] == [
            "default",
            "project",
            "souls",
            "vyuchka",
            "vyuchka-2",
        ]
        assert studio.projects.rename("souls", "Души").title == "Души"


def test_a_job_and_its_results_belong_to_its_project(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        studio.projects.create("Forest")
        sketch = studio.library.import_bytes(png())  # default: inputs come from any project
        job = studio.generation.submit(
            "image", "image-to-image", inputs={"image": sketch.id}, count=2, project="forest"
        )
        done = wait_for(studio, job.id, JobStatus.SUCCEEDED)
        assert done.project == "forest"
        outputs = [studio.library.asset(asset_id) for asset_id in done.outputs]
        assert {asset.project for asset in outputs if asset} == {"forest"}
        assert {asset.id for asset in studio.library.assets(project="forest")} == set(done.outputs)
        assert [asset.id for asset in studio.library.assets(project="default")] == [sketch.id]
        assert len(studio.library.assets()) == 3  # every project's
        assert [job.id for job in studio.generation.jobs(project="forest")] == [job.id]
        assert studio.generation.jobs(project="default") == []
        with pytest.raises(JobRequestError, match="project"):
            studio.generation.submit("image", "text-to-image", project="missing")


def test_a_retry_stays_in_the_project(tmp_path: Path) -> None:
    with running_studio(tmp_path, model_server("image", "http://127.0.0.1:9")) as studio:
        studio.projects.create("Forest")
        job = studio.generation.submit("image", "text-to-image", project="forest")
        studio.generation.cancel(job.id)
        assert studio.generation.retry(job.id).project == "forest"


def test_an_asset_moves_alone(tmp_path: Path) -> None:
    with running_studio(tmp_path) as studio:
        studio.projects.create("Forest")
        asset = studio.library.import_bytes(png())
        moved = studio.library.update(asset.id, project="forest")
        assert moved is not None
        assert moved.project == "forest"
        assert studio.projects.get("forest").asset_count == 1
        assert studio.projects.get("default").asset_count == 0
        with pytest.raises(ProjectError):
            studio.library.update(asset.id, project="missing")
        with pytest.raises(ProjectError):
            studio.library.import_bytes(png(), project="missing")


def test_only_an_empty_project_is_deleted(tmp_path: Path) -> None:
    with running_studio(tmp_path, model_server("image", "http://127.0.0.1:9")) as studio:
        with pytest.raises(ProjectConflict, match="default"):
            studio.projects.delete("default")
        studio.projects.create("Forest")
        asset = studio.library.import_bytes(png(), project="forest")
        with pytest.raises(ProjectConflict, match="assets"):
            studio.projects.delete("forest")
        studio.library.delete(asset.id)
        waiting = studio.generation.submit("image", "text-to-image", project="forest")
        with pytest.raises(ProjectConflict, match="unfinished"):
            studio.projects.delete("forest")
        studio.generation.cancel(waiting.id)

        deleted = studio.projects.delete("forest")
        assert deleted.deleted_at is not None
        assert [project.id for project in studio.projects.all()] == ["default"]
        # Its history stays; nothing new goes in, and the id is not given out again.
        assert [job.id for job in studio.generation.jobs(project="forest")] == [waiting.id]
        with pytest.raises(JobRequestError, match="project"):
            studio.generation.submit("image", "text-to-image", project="forest")
        with pytest.raises(ProjectError):
            studio.library.import_bytes(png(), project="forest")
        with pytest.raises(ProjectError):
            studio.projects.rename("forest", "Again")
        assert studio.projects.create("Forest").id == "forest-2"


def test_projects_over_http(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (_, http):
        created = http.post("/api/projects", json={"title": "Выучка"})
        assert created.status_code == 201, created.text
        assert created.json()["id"] == "vyuchka"
        assert http.post("/api/projects", json={"title": "x", "id": "vyuchka"}).status_code == 409
        assert http.post("/api/projects", json={"title": "x", "id": "Bad Id"}).status_code == 422

        uploaded = http.post(
            "/api/assets",
            content=png(),
            params={"project": "vyuchka"},
            headers={"Content-Type": "application/octet-stream"},
        ).json()
        assert uploaded["project"] == "vyuchka"
        listed = http.get("/api/assets", params={"project": "vyuchka"}).json()
        assert [asset["id"] for asset in listed] == [uploaded["id"]]
        assert http.get("/api/assets", params={"project": "default"}).json() == []
        assert http.get("/api/assets", params={"project": "missing"}).status_code == 404
        assert http.get("/api/jobs", params={"project": "missing"}).status_code == 404
        unknown = http.post(
            "/api/assets",
            content=png(),
            params={"project": "missing"},
            headers={"Content-Type": "application/octet-stream"},
        )
        assert unknown.status_code == 422

        wait_ready(http)
        job = http.post(
            "/api/jobs",
            json={
                "server": "image",
                "task": "text-to-image",
                "prompt": "мох",
                "project": "vyuchka",
            },
        ).json()
        done = http.post(f"/api/jobs/{job['id']}/wait", params={"timeout_s": 10}).json()
        assert (done["status"], done["project"]) == ("succeeded", "vyuchka")
        assert http.get(f"/api/assets/{done['outputs'][0]}").json()["project"] == "vyuchka"
        assert [item["id"] for item in http.get("/api/jobs?project=vyuchka").json()] == [job["id"]]
        assert http.get("/api/jobs?project=default").json() == []

        moved = http.patch(f"/api/assets/{uploaded['id']}", json={"project": "default"})
        assert moved.json()["project"] == "default"
        bad_move = http.patch(f"/api/assets/{uploaded['id']}", json={"project": "missing"})
        assert bad_move.status_code == 422

        renamed = http.patch("/api/projects/vyuchka", json={"title": "Выучка 2"})
        assert renamed.json()["title"] == "Выучка 2"
        assert http.patch("/api/projects/missing", json={"title": "x"}).status_code == 404
        counts = {item["id"]: item["asset_count"] for item in http.get("/api/projects").json()}
        assert counts == {"default": 1, "vyuchka": 1}
        assert http.delete("/api/projects/vyuchka").status_code == 409  # its result is there
        http.delete(f"/api/assets/{done['outputs'][0]}")
        assert http.delete("/api/projects/vyuchka").json()["deleted_at"] is not None
        assert http.delete("/api/projects/vyuchka").status_code == 404
        assert [item["id"] for item in http.get("/api/projects").json()] == ["default"]


def test_projects_over_mcp(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (url, http):
        wait_ready(http)
        project = call(url, "create_project", {"title": "Forest"})
        assert project["id"] == "forest"
        assert "error" in call(url, "create_project", {"title": "x", "id": "forest"})
        listed = call(url, "list_projects")["result"]
        assert [item["id"] for item in listed] == ["default", "forest"]

        job = call(
            url,
            "generate",
            {"server": "image", "task": "text-to-image", "prompt": "мох", "project": "forest"},
        )
        done = call(url, "wait_for_job", {"job_id": job["id"], "timeout_s": 10})
        assert done["project"] == "forest"
        found = call(url, "list_assets", {"project": "forest"})["result"]
        assert [asset["id"] for asset in found] == done["outputs"]
        assert call(url, "list_assets", {"project": "default"})["result"] == []
        assert "error" in call(url, "list_assets", {"project": "missing"})

        file = tmp_path / "sketch.png"
        file.write_bytes(media.encode_png(4, 4, lambda x, y: (0, 0, 0)))
        imported = call(url, "import_asset", {"path": str(file), "project": "forest"})
        assert imported["project"] == "forest"
        moved = call(url, "move_asset", {"asset_id": imported["id"], "project": "default"})
        assert moved["project"] == "default"
        assert "error" in call(url, "move_asset", {"asset_id": imported["id"], "project": "x"})
