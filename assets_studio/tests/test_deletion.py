"""Deleting assets (ADR-004): gone from the library, still in jobs and lineage;
the file goes when no other asset shares it."""

from pathlib import Path

import httpx2 as httpx
import pytest
from fake_model_server import FakeModelServer
from studio.config import load_config
from studio.domain import Dependency, Job, JobStatus, new_id
from studio.services.generation import JobRequestError
from studio.services.library import AssetInUse
from studio.storage.repository import InputDeleted, Repository
from support import live_server
from test_api import fake_and_studio, wait_ready
from test_studio import model_server, png, running_studio, wait_for

from model_server_sdk import create_app


def test_a_deleted_asset_leaves_the_library_and_its_file_goes(tmp_path: Path) -> None:
    with running_studio(tmp_path) as studio:
        asset = studio.library.import_bytes(png())
        path = studio.library.file(asset)
        deleted = studio.library.delete(asset.id)
        assert deleted is not None
        assert deleted.deleted_at is not None
        assert asset.id not in [item.id for item in studio.library.assets()]
        assert not path.exists()
        # Still there for jobs and lineage, marked deleted.
        found = studio.library.asset(asset.id)
        assert found is not None
        assert found.deleted_at is not None
        assert studio.library.delete(asset.id) == found  # twice is harmless
        assert studio.library.delete("missing") is None


def test_a_shared_file_stays_while_another_asset_uses_it(tmp_path: Path) -> None:
    with running_studio(tmp_path) as studio:
        first = studio.library.import_bytes(png())
        second = studio.library.import_bytes(png())  # same content, same file
        studio.library.delete(first.id)
        assert studio.library.file(second).exists()
        studio.library.delete(second.id)
        assert not studio.library.file(second).exists()


def test_an_input_of_an_unfinished_job_cannot_be_deleted(tmp_path: Path) -> None:
    # No model server is running: the job waits, holding its input.
    with running_studio(tmp_path, model_server("image", "http://127.0.0.1:9")) as studio:
        source = studio.library.import_bytes(png())
        job = studio.generation.submit("image", "image-to-image", inputs={"image": source.id})
        with pytest.raises(AssetInUse, match=job.id):
            studio.library.delete(source.id)
        studio.generation.cancel(job.id)
        assert studio.library.delete(source.id) is not None


def test_a_deleted_asset_cannot_be_an_input(tmp_path: Path) -> None:
    with running_studio(tmp_path, model_server("image", "http://127.0.0.1:9")) as studio:
        source = studio.library.import_bytes(png())
        studio.library.delete(source.id)
        with pytest.raises(JobRequestError, match="was deleted"):
            studio.generation.submit("image", "image-to-image", inputs={"image": source.id})


def test_deleting_a_result_keeps_its_job(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        job = studio.generation.submit("image", "text-to-image", prompt="мох", count=2)
        done = wait_for(studio, job.id, JobStatus.SUCCEEDED)
        studio.library.delete(done.outputs[0])
        again = studio.generation.job(job.id)
        assert again is not None
        assert again.outputs == done.outputs
        assert [asset.id for asset in studio.library.assets()] == [done.outputs[1]]


def test_delete_over_http(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (_, http):
        wait_ready(http)
        asset = http.post("/api/assets", content=png()).json()
        response = http.delete(f"/api/assets/{asset['id']}")
        assert response.status_code == 200
        assert response.json()["deleted_at"] is not None
        assert http.get(f"/api/assets/{asset['id']}").json()["deleted_at"] is not None
        assert http.get(f"/api/assets/{asset['id']}/file").status_code == 410
        assert asset["id"] not in [item["id"] for item in http.get("/api/assets").json()]
        assert http.delete("/api/assets/missing").status_code == 404


def test_delete_of_an_input_in_use_is_a_conflict(tmp_path: Path) -> None:
    with fake_and_studio(tmp_path) as (url, http):
        source = http.post("/api/assets", content=png()).json()
        job = http.post(
            "/api/jobs",
            json={
                "server": "image",
                "task": "image-to-image",
                "inputs": {"image": source["id"]},
                "params": {"delay_s": 5},
            },
        ).json()
        response = http.delete(f"/api/assets/{source['id']}")
        assert response.status_code == 409
        assert job["id"] in response.json()["detail"]
        httpx.post(f"{url}/api/jobs/{job['id']}/cancel", timeout=30)


def test_an_output_another_job_waits_for_cannot_be_deleted(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        upstream = studio.generation.submit("image", "text-to-image", prompt="мох")
        done = wait_for(studio, upstream.id, JobStatus.SUCCEEDED)
        # The downstream job waits long enough for the attempt to delete.
        downstream = studio.generation.submit(
            "image",
            "image-to-image",
            params={"delay_s": 5},
            dependencies={"image": Dependency(upstream.id, 0)},
        )
        wait_for(studio, downstream.id, JobStatus.RUNNING)
        with pytest.raises(AssetInUse, match=downstream.id):
            studio.library.delete(done.outputs[0])
        wait_for(studio, downstream.id, JobStatus.SUCCEEDED, timeout_s=20)
        assert studio.library.delete(done.outputs[0]) is not None


def test_a_deleted_input_is_caught_when_the_job_is_stored(tmp_path: Path) -> None:
    """The service checks inputs before storing the job; a deletion committed
    in between is caught inside the storing transaction."""
    with running_studio(tmp_path, model_server("image", "http://127.0.0.1:9")) as studio:
        source = studio.library.import_bytes(png())
        studio.database.connection.execute(
            "UPDATE assets SET deleted_at = '2026-01-01' WHERE id = ?", (source.id,)
        )
        job = Job(
            id=new_id(),
            server="image",
            task="image-to-image",
            status=JobStatus.QUEUED,
            count=1,
            created_at="2026-01-01",
            inputs={"image": source.id},
        )
        with pytest.raises(InputDeleted):
            Repository(studio.database).add_job(job)


def test_a_config_with_tabs_explains_the_change(tmp_path: Path) -> None:
    path = tmp_path / "studio.toml"
    path.write_text('[[tabs]]\nid = "image"\ntitle = "SDXL"\nurl = "http://127.0.0.1:9101"\n')
    with pytest.raises(ValueError, match=r"\[\[servers\]\]"):
        load_config(path)


def test_a_job_cannot_depend_on_a_deleted_output(tmp_path: Path) -> None:
    with (
        live_server(create_app(FakeModelServer())) as url,
        running_studio(tmp_path, model_server("image", url)) as studio,
    ):
        upstream = studio.generation.submit("image", "text-to-image", prompt="мох")
        done = wait_for(studio, upstream.id, JobStatus.SUCCEEDED)
        studio.library.delete(done.outputs[0])
        with pytest.raises(JobRequestError, match="was deleted"):
            studio.generation.submit(
                "image", "image-to-image", dependencies={"image": Dependency(upstream.id, 0)}
            )
