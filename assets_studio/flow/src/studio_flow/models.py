"""Versioned graph format; references carry a studio input role and output index."""

from typing import Annotated, Any, Literal, TypeAlias

from pydantic import BaseModel, ConfigDict, Field, model_validator

NodeId: TypeAlias = Annotated[str, Field(pattern=r"^[a-zA-Z0-9_-]{1,80}$")]


class StrictModel(BaseModel):
    model_config = ConfigDict(extra="forbid", allow_inf_nan=False)


class Binding(StrictModel):
    node: NodeId
    output_index: int = Field(default=0, ge=0)


class NodeBase(StrictModel):
    id: NodeId
    title: str = Field(default="", max_length=200)
    x: float = 80
    y: float = 80


class AssetNode(NodeBase):
    kind: Literal["asset"]
    asset_id: str = Field(min_length=1, max_length=100)


class TaskNode(NodeBase):
    kind: Literal["task"]
    server: str = Field(min_length=1, max_length=64)
    task: str = Field(min_length=1, max_length=64)
    prompt: str | None = None
    params: dict[str, Any] = Field(default_factory=dict)
    count: int = Field(default=1, ge=1, le=64)
    seed: int | None = Field(default=None, ge=0, lt=2**32)
    inputs: dict[str, Binding] = Field(default_factory=dict)


Node: TypeAlias = Annotated[AssetNode | TaskNode, Field(discriminator="kind")]


class Graph(StrictModel):
    schema_version: Literal[1] = 1
    title: str = Field(min_length=1, max_length=200)
    nodes: list[Node] = Field(default_factory=lambda: list[Node](), max_length=200)

    @model_validator(mode="after")
    def valid_edges(self) -> "Graph":
        nodes = {node.id: node for node in self.nodes}
        if len(nodes) != len(self.nodes):
            raise ValueError("node ids must be unique")
        for node in self.nodes:
            if isinstance(node, AssetNode):
                continue
            for role, binding in node.inputs.items():
                if binding.node not in nodes:
                    raise ValueError(f"{node.id}/{role}: source node {binding.node} not found")
                source = nodes[binding.node]
                count = source.count if isinstance(source, TaskNode) else 1
                if binding.output_index >= count:
                    raise ValueError(f"{node.id}/{role}: output index must be less than {count}")
        self.ordered()
        return self

    def ordered(self) -> list[Node]:
        pending = list(self.nodes)
        result: list[Node] = []
        done: set[str] = set()
        while pending:
            ready = [
                node
                for node in pending
                if isinstance(node, AssetNode) or all(b.node in done for b in node.inputs.values())
            ]
            if not ready:
                raise ValueError("the workflow contains a cycle")
            for node in ready:
                result.append(node)
                done.add(node.id)
                pending.remove(node)
        return result


class FlowUpdate(StrictModel):
    revision: int = Field(ge=1)
    graph: Graph


class StartRun(StrictModel):
    request_id: str = Field(min_length=1, max_length=100)
