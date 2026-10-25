"""Validate graphs and submit dependencies to studio with durable idempotency keys."""

import asyncio
import logging
from typing import Any

import httpx2
from jsonschema import ValidationError, validate

from studio_flow.models import AssetNode, Graph
from studio_flow.store import Store


class GraphError(ValueError):
    pass


async def preflight(graph: Graph, client: httpx2.AsyncClient) -> None:
    servers_response = await client.get("/api/servers")
    servers_response.raise_for_status()
    servers = {server["id"]: server for server in servers_response.json()}
    outputs: dict[str, list[str]] = {}
    tasks = 0
    for node in graph.ordered():
        if isinstance(node, AssetNode):
            response = await client.get(f"/api/assets/{node.asset_id}")
            if response.status_code in (404, 410):
                raise GraphError(f"{node.title or node.id}: input asset not found")
            response.raise_for_status()
            asset = response.json()
            if asset.get("deleted_at"):
                raise GraphError(f"{node.title or node.id}: input asset was deleted")
            outputs[node.id] = [asset["mime"]]
            continue
        tasks += 1
        server = servers.get(node.server)
        spec = (
            next((t for t in server["tasks"] if t["task"] == node.task), None) if server else None
        )
        if spec is None:
            raise GraphError(
                f"{node.title or node.id}: task {node.server}/{node.task} is unknown; "
                "start its server once so studio can discover it"
            )
        if node.count > spec["max_count"]:
            raise GraphError(f"{node.id}: at most {spec['max_count']} outputs are supported")
        if spec["prompt"] == "required" and not (node.prompt or "").strip():
            raise GraphError(f"{node.id}: a prompt is required")
        if spec["prompt"] == "none" and node.prompt:
            raise GraphError(f"{node.id}: this task does not accept a prompt")
        params = dict(node.params)
        for name, schema in spec["params_schema"].get("properties", {}).items():
            if name not in params and "default" in schema:
                params[name] = schema["default"]
        try:
            validate(params, spec["params_schema"])
        except ValidationError as error:
            raise GraphError(f"{node.id}: {error.message}") from error
        roles = {item["role"]: item for item in spec["inputs"]}
        unknown = node.inputs.keys() - roles.keys()
        missing = [
            role for role, item in roles.items() if item["required"] and role not in node.inputs
        ]
        if unknown or missing:
            raise GraphError(
                f"{node.id}: missing inputs {missing}; unknown roles {sorted(unknown)}"
            )
        for role, binding in node.inputs.items():
            accepted = roles[role]["mime"]
            if not any(
                any(pattern == mime or pattern == mime.split("/")[0] + "/*" for pattern in accepted)
                for mime in outputs[binding.node]
            ):
                raise GraphError(
                    f"{node.id}/{role}: incompatible file types "
                    f"{outputs[binding.node]} → {accepted}"
                )
        outputs[node.id] = spec["output_mime"]
    if not tasks:
        raise GraphError("Add at least one task to run the workflow")


class Engine:
    def __init__(self, store: Store, client: httpx2.AsyncClient) -> None:
        self.store = store
        self.client = client
        self.lock = asyncio.Lock()

    async def submit(self, identity: str) -> None:
        async with self.lock:
            run = self.store.run(identity)
            if run is None or run["state"] != "submitting":
                return
            graph = Graph.model_validate(run["graph"])
            nodes = {node.id: node for node in graph.nodes}
            jobs = dict(run["jobs"])
            try:
                for node in graph.ordered():
                    if isinstance(node, AssetNode) or node.id in jobs:
                        continue
                    inputs: dict[str, str] = {}
                    dependencies: dict[str, dict[str, Any]] = {}
                    for role, binding in node.inputs.items():
                        source = nodes[binding.node]
                        if isinstance(source, AssetNode):
                            inputs[role] = source.asset_id
                        else:
                            dependencies[role] = {
                                "job_id": jobs[source.id],
                                "output_index": binding.output_index,
                            }
                    response = await self.client.post(
                        "/api/jobs",
                        json={
                            "server": node.server,
                            "task": node.task,
                            "prompt": node.prompt,
                            "params": node.params,
                            "count": node.count,
                            "seed": node.seed,
                            "inputs": inputs,
                            "dependencies": dependencies,
                            "idempotency_key": f"flow-{identity}-{node.id}",
                        },
                    )
                    response.raise_for_status()
                    jobs[node.id] = response.json()["id"]
                    self.store.receipt(identity, jobs)
            except httpx2.HTTPStatusError as error:
                transient = error.response.status_code >= 500 or error.response.status_code == 429
                self.store.receipt(
                    identity,
                    jobs,
                    state="submitting" if transient else "submission_failed",
                    error=error.response.text[:2000],
                )
                return
            except httpx2.TransportError as error:
                # The POST may have succeeded before the connection failed; retry with
                # the same node key, including after a flow service restart.
                self.store.receipt(identity, jobs, error=str(error))
                return
            self.store.receipt(identity, jobs, state="submitted")

    async def cancel(self, identity: str) -> None:
        async with self.lock:
            run = self.store.run(identity)
            if run is None or run["state"] != "cancelling":
                return
            try:
                for job_id in run["jobs"].values():
                    response = await self.client.post(f"/api/jobs/{job_id}/cancel")
                    if response.status_code not in (404, 409):
                        response.raise_for_status()
            except httpx2.HTTPError as error:
                self.store.receipt(identity, run["jobs"], state="cancelling", error=str(error))
                return
            self.store.receipt(identity, run["jobs"], state="cancelled")

    async def pump(self) -> None:
        while True:
            for run in self.store.runs(pending=True):
                try:
                    if run["state"] == "cancelling":
                        await self.cancel(run["id"])
                    else:
                        await self.submit(run["id"])
                except Exception:
                    logging.getLogger(__name__).exception(
                        "workflow submission failed: %s", run["id"]
                    )
            await asyncio.sleep(2)

    async def view(self, identity: str) -> dict[str, Any] | None:
        run = self.store.run(identity)
        if run is None:
            return None
        jobs: dict[str, Any] = {}
        semaphore = asyncio.Semaphore(8)

        async def read(node: str, job_id: str) -> None:
            async with semaphore:
                try:
                    response = await self.client.get(f"/api/jobs/{job_id}")
                    response.raise_for_status()
                    jobs[node] = response.json()
                except httpx2.HTTPError as error:
                    jobs[node] = {"id": job_id, "status": "unknown", "error_message": str(error)}

        await asyncio.gather(*(read(node, job) for node, job in run["jobs"].items()))
        statuses = {job["status"] for job in jobs.values()}
        status = run["state"]
        if status == "submitted":
            if "failed" in statuses:
                status = "failed"
            elif "cancelled" in statuses:
                status = "cancelled"
            elif statuses == {"succeeded"}:
                status = "succeeded"
            else:
                status = "running"
        return {**run, "status": status, "node_jobs": jobs}
