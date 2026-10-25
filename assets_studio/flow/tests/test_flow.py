import asyncio
import json

import httpx2
import pytest
from fastapi.testclient import TestClient
from pydantic import ValidationError
from studio_flow.app import create_app
from studio_flow.engine import Engine, GraphError, preflight
from studio_flow.models import Graph
from studio_flow.store import Conflict, Store


def graph():
    return Graph.model_validate(
        {
            "title": "Rig and animate",
            "nodes": [
                {"id": "asset", "kind": "asset", "asset_id": "mesh"},
                {
                    "id": "prepare",
                    "kind": "task",
                    "server": "tools",
                    "task": "prepare",
                    "inputs": {"rig": {"node": "asset"}},
                },
                {
                    "id": "animate",
                    "kind": "task",
                    "server": "tools",
                    "task": "prepare",
                    "inputs": {"rig": {"node": "prepare"}},
                },
            ],
        }
    )


class StudioStub:
    def __init__(self):
        self.jobs = {}
        self.bodies = {}
        self.lose_response = False
        self.fail = False
        self.cancel_offline = False

    def handle(self, request):
        path = request.url.path
        if path == "/api/servers":
            task = {
                "task": "prepare",
                "prompt": "none",
                "inputs": [{"role": "rig", "required": True, "mime": ["model/x-fbx"]}],
                "params_schema": {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
                "max_count": 2,
                "output_mime": ["model/x-fbx"],
            }
            return httpx2.Response(200, json=[{"id": "tools", "tasks": [task]}])
        if path == "/api/assets/mesh":
            return httpx2.Response(200, json={"id": "mesh", "mime": "model/x-fbx"})
        if path == "/api/jobs" and request.method == "POST":
            if self.fail:
                return httpx2.Response(422, json={"detail": "input deleted"})
            body = json.loads(request.content)
            key = body["idempotency_key"]
            if key not in self.jobs:
                self.jobs[key] = {
                    "id": "job" + str(len(self.jobs)),
                    "status": "queued",
                    "outputs": [],
                }
                self.bodies[key] = body
            if self.lose_response and len(self.jobs) == 2:
                self.lose_response = False
                raise httpx2.ReadError("connection lost after studio committed", request=request)
            return httpx2.Response(201, json=self.jobs[key])
        for job in self.jobs.values():
            if path == "/api/jobs/" + job["id"]:
                return httpx2.Response(200, json=job)
            if path == "/api/jobs/" + job["id"] + "/cancel":
                if self.cancel_offline:
                    raise httpx2.ConnectError("offline", request=request)
                job["status"] = "cancelled"
                return httpx2.Response(200, json=job)
        return httpx2.Response(404)


def test_graph_rejects_cycles_dangling_edges_and_invalid_output_indices():
    for mutate in [
        lambda data: data["nodes"][1]["inputs"]["rig"].update(node="animate"),
        lambda data: data["nodes"][1]["inputs"]["rig"].update(node="missing"),
        lambda data: data["nodes"][2]["inputs"]["rig"].update(output_index=1),
        lambda data: data["nodes"][2].update(id="asset"),
    ]:
        data = graph().model_dump()
        mutate(data)
        with pytest.raises(ValidationError):
            Graph.model_validate(data)


def test_versions_and_run_snapshots_survive_edits_and_restart(tmp_path):
    store = Store(tmp_path / "flow.sqlite3")
    saved = store.save(graph())
    run = store.new_run(saved, "request")
    changed = graph().model_copy(update={"title": "Changed"})
    second = store.save(changed, saved["id"], 1)
    assert second["revision"] == 2
    with pytest.raises(Conflict):
        store.save(graph(), saved["id"], 1)
    reopened = Store(tmp_path / "flow.sqlite3")
    assert reopened.run(run["id"])["graph"]["title"] == "Rig and animate"
    assert reopened.new_run(saved, "request")["id"] == run["id"]
    with pytest.raises(Conflict):
        reopened.new_run(second, "request")


def test_resume_after_lost_post_response_creates_no_duplicate_jobs(tmp_path):
    async def scenario():
        stub = StudioStub()
        store = Store(tmp_path / "flow.sqlite3")
        saved = store.save(graph())
        run = store.new_run(saved, "request")
        async with httpx2.AsyncClient(
            base_url="http://studio", transport=httpx2.MockTransport(stub.handle)
        ) as client:
            await preflight(graph(), client)
            stub.lose_response = True
            await Engine(store, client).submit(run["id"])
            partial = store.run(run["id"])
            assert len(partial["jobs"]) == 1
            assert len(stub.jobs) == 2  # the second POST committed but its receipt was lost
            engine = Engine(Store(tmp_path / "flow.sqlite3"), client)
            await engine.submit(run["id"])
            assert len(stub.jobs) == 2
            assert store.run(run["id"])["state"] == "submitted"
            bodies = list(stub.bodies.values())
            assert bodies[0]["inputs"] == {"rig": "mesh"}
            assert bodies[1]["dependencies"] == {"rig": {"job_id": "job0", "output_index": 0}}
            assert bodies[1]["inputs"] == {}
            assert len({body["idempotency_key"] for body in bodies}) == 2
            for job in stub.jobs.values():
                job["status"] = "succeeded"
            assert (await engine.view(run["id"]))["status"] == "succeeded"

    asyncio.run(scenario())


def test_invalid_input_stops_submission_without_hiding_existing_jobs(tmp_path):
    async def scenario():
        stub = StudioStub()
        stub.fail = True
        store = Store(tmp_path / "flow.sqlite3")
        run = store.new_run(store.save(graph()), "request")
        async with httpx2.AsyncClient(
            base_url="http://studio", transport=httpx2.MockTransport(stub.handle)
        ) as client:
            await Engine(store, client).submit(run["id"])
            assert store.run(run["id"])["state"] == "submission_failed"
            assert "deleted" in store.run(run["id"])["error"]

    asyncio.run(scenario())


def test_preflight_rejects_missing_inputs_types_and_unknown_models():
    async def scenario():
        stub = StudioStub()
        async with httpx2.AsyncClient(
            base_url="http://studio", transport=httpx2.MockTransport(stub.handle)
        ) as client:
            for change in [
                lambda data: data["nodes"][1].update(inputs={}),
                lambda data: data["nodes"][1].update(server="missing"),
                lambda data: data["nodes"][1].update(params={"unknown": 1}),
                lambda data: data["nodes"][1].update(count=3),
            ]:
                data = graph().model_dump()
                change(data)
                with pytest.raises(GraphError):
                    await preflight(Graph.model_validate(data), client)

    asyncio.run(scenario())


def test_cancellation_survives_studio_outage(tmp_path):
    async def scenario():
        stub = StudioStub()
        store = Store(tmp_path / "flow.sqlite3")
        run = store.new_run(store.save(graph()), "request")
        async with httpx2.AsyncClient(
            base_url="http://studio", transport=httpx2.MockTransport(stub.handle)
        ) as client:
            engine = Engine(store, client)
            await engine.submit(run["id"])
            run = store.run(run["id"])
            store.receipt(run["id"], run["jobs"], state="cancelling")
            stub.cancel_offline = True
            await engine.cancel(run["id"])
            assert store.run(run["id"])["state"] == "cancelling"
            stub.cancel_offline = False
            await Engine(Store(tmp_path / "flow.sqlite3"), client).cancel(run["id"])
            assert store.run(run["id"])["state"] == "cancelled"
            assert all(job["status"] == "cancelled" for job in stub.jobs.values())

    asyncio.run(scenario())


def test_http_save_reload_conflicts_and_deleted_flow_history(tmp_path):
    stub = StudioStub()
    app = create_app(
        database=tmp_path / "flow.sqlite3", transport=httpx2.MockTransport(stub.handle)
    )
    with TestClient(app) as client:
        saved = client.post("/flow-api/flows", json=graph().model_dump()).json()
        assert client.get("/flow-api/flows").json()[0]["id"] == saved["id"]
        path = "/flow-api/flows/" + saved["id"]
        assert (
            client.put(path, json={"revision": 1, "graph": graph().model_dump()}).status_code == 200
        )
        assert (
            client.put(path, json={"revision": 1, "graph": graph().model_dump()}).status_code == 409
        )
        started = client.post(path + "/runs", json={"request_id": "run1"})
        assert started.status_code == 202
        identity = started.json()["id"]
        assert client.post(path + "/runs", json={"request_id": "run1"}).json()["id"] == identity
        assert client.delete(path).status_code == 204
        assert client.get(path).status_code == 404
        assert client.get("/flow-api/runs/" + identity).status_code == 200


def test_branch_merge_and_variant_index_are_preserved(tmp_path):
    async def scenario():
        stub = StudioStub()
        data = graph().model_dump()
        data["nodes"][1]["count"] = 2
        data["nodes"].append(
            {
                "id": "branch",
                "kind": "task",
                "server": "tools",
                "task": "prepare",
                "inputs": {"rig": {"node": "asset"}},
            }
        )
        data["nodes"][2]["inputs"] = {
            "rig": {"node": "prepare", "output_index": 1},
            "motion": {"node": "branch"},
        }
        definition = Graph.model_validate(data)
        store = Store(tmp_path / "flow.sqlite3")
        run = store.new_run(store.save(definition), "request")
        async with httpx2.AsyncClient(
            base_url="http://studio", transport=httpx2.MockTransport(stub.handle)
        ) as client:
            await Engine(store, client).submit(run["id"])
        receipt = store.run(run["id"])
        merged = stub.bodies[f"flow-{run['id']}-animate"]
        assert merged["dependencies"] == {
            "rig": {"job_id": receipt["jobs"]["prepare"], "output_index": 1},
            "motion": {"job_id": receipt["jobs"]["branch"], "output_index": 0},
        }
        assert len(stub.jobs) == 3

    asyncio.run(scenario())


def test_mime_mismatch_is_rejected_before_any_job_is_created():
    async def scenario():
        stub = StudioStub()

        def handle(request):
            if request.url.path == "/api/assets/mesh":
                return httpx2.Response(200, json={"id": "mesh", "mime": "image/png"})
            return stub.handle(request)

        async with httpx2.AsyncClient(
            base_url="http://studio", transport=httpx2.MockTransport(handle)
        ) as client:
            with pytest.raises(GraphError, match="incompatible"):
                await preflight(graph(), client)
            assert not stub.jobs

    asyncio.run(scenario())


def test_output_download_keeps_filename_and_forces_attachment(tmp_path):
    def handler(request):
        assert request.url.path == "/api/assets/output/file"
        return httpx2.Response(
            200,
            content=b"fake FBX",
            headers={
                "Content-Type": "model/x-fbx",
                "Content-Disposition": 'inline; filename="character.fbx"',
            },
        )

    app = create_app(database=tmp_path / "flow.sqlite3", transport=httpx2.MockTransport(handler))
    with TestClient(app) as client:
        response = client.get("/flow-api/studio/assets/output/file")
        assert response.content == b"fake FBX"
        assert response.headers["content-disposition"] == 'attachment; filename="character.fbx"'
        assert response.headers["content-type"] == "model/x-fbx"
