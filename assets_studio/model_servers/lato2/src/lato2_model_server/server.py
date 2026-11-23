"""LATO.2 low-poly mesh generation server (contract v1).

LATO.2 (Meshy AI, July 2026) rebuilds an existing surface as a game-style
triangle mesh with two flow models instead of a token sequence: a vertex flow
places about as many vertices as asked on the 64³ voxels the surface occupies,
guided by a rendered view of it, and a topology flow connects them. Geometry
only: no UVs, no colour; bake those from the detailed mesh afterwards.

Upstream reports about 8 GB with every part on the GPU. Here the three groups
(the image encoder, the vertex models, the topology models) take turns on it
and wait in RAM otherwise; the weights stay as published.

The model gives vertices and edges: its triangles are the triples of connected
vertices and face no particular way. The server winds each one like the input
surface next to it, and puts the mesh back at the input's place and size (the
model works in a unit box).

Upstream declares Python 3.10 and torch 2.6.0+cu124; the server imports the
pinned source on Python 3.11 and torch 2.8.0+cu129 (see pyproject.toml).

Licence: MIT, the code and the weights.
"""

import io
import logging
import os
import sys
import tempfile
import time
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path
from typing import Any

import numpy as np
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
    ui,
)

logger = logging.getLogger("lato2_model_server")

WEIGHTS_REPO = "0x4c48/LATO.2"
# Git revisions, not secrets.
WEIGHTS_REVISION = "a91090e8077b9318ab87ac08fd9eb905903d4515"  # pragma: allowlist secret
SOURCE_REVISION = "fbb1f5a5755e6db8700cf6922fd506830b7cdccd"  # pragma: allowlist secret
DINOV2_REVISION = "7764ea0f912e53c92e82eb78a2a1631e92725fc8"  # pragma: allowlist secret
CHECKPOINTS = ("vflow.pt", "vvae.pt", "offset_head.pt", "tflow.pt", "tvae.pt", "voxel_encoder.pt")

RETOPO_3D = "3d-retopo"

# Upstream's inference defaults that are not parameters here.
INFERENCE_THRESHOLD = 0.5
EDGE_THRESHOLD = 0.0
CHUNK_SIZE = 20_000

# Voxelising and rendering a mesh of a million triangles takes well under a minute.
PREPARATION_TIMEOUT_S = 600

# Points of the input surface that say which way it faces.
ORIENTATION_SAMPLES = 200_000


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    vertices: int = Field(
        default=2000,
        ge=200,
        le=5000,
        description="Target vertex count; about 2.5 times as many triangles come out",
        json_schema_extra=ui(
            ru="Цель по вершинам; треугольников выходит примерно в 2,5 раза больше", primary=True
        ),
    )
    guidance: float = Field(
        default=3.0,
        ge=0.0,
        le=10.0,
        description="How strictly the vertices follow the rendered view of the input",
        json_schema_extra=ui(ru="Насколько строго вершины следуют виду исходной сетки"),
    )
    vertex_steps: int = Field(
        default=24,
        ge=4,
        le=100,
        description="Steps of the vertex flow",
        json_schema_extra=ui(ru="Шаги потока вершин"),
    )
    topology_steps: int = Field(
        default=50,
        ge=10,
        le=200,
        description="Steps of the topology flow",
        json_schema_extra=ui(ru="Шаги потока топологии"),
    )
    fill_quad_rings: bool = Field(
        default=True,
        description=(
            "Close rings of four edges that came without a diagonal, with two triangles:"
            " fewer holes, more overlapping triangles"
        ),
        json_schema_extra=ui(
            ru=(
                "Закрывать двумя треугольниками кольца из четырёх рёбер без диагонали:"
                " меньше дыр, больше наложенных треугольников"
            )
        ),
    )


TASK = TaskSpec(
    RETOPO_3D,
    Params,
    ("model/gltf-binary",),
    prompt="none",
    inputs=(
        InputSpec(
            role="mesh",
            mime=["model/gltf-binary"],
            description="The detailed mesh to rebuild with few triangles; its colour is not kept",
            labels={"ru": "Детальная сетка, которую нужно пересобрать; цвет не сохраняется"},
        ),
    ),
    max_count=4,
)


def source_directory() -> Path:
    return Path(os.environ.get("LATO2_SOURCE", "~/.cache/assets-studio/lato2-source")).expanduser()


def weights_directory() -> Path:
    """The checkpoints, and under ``dinov2`` the image encoder's source and weights."""
    return Path(os.environ.get("LATO2_WEIGHTS", "~/.cache/assets-studio/lato2")).expanduser()


def dinov2_directory() -> Path:
    """The torch.hub directory upstream's encoder loads DINOv2 from."""
    return weights_directory() / "dinov2"


def download() -> None:
    """The checkpoints at the pinned revision (~3.3 GB). Network only; DINOv2's
    weights come on the first load."""
    from huggingface_hub import snapshot_download

    snapshot_download(
        WEIGHTS_REPO,
        revision=WEIGHTS_REVISION,
        local_dir=weights_directory(),
        allow_patterns=list(CHECKPOINTS),
    )


def read_mesh(data: bytes) -> Any:
    import trimesh

    try:
        mesh = trimesh.load(io.BytesIO(data), file_type="glb", force="mesh", process=False)
    except Exception as error:  # trimesh raises many kinds on a bad file
        raise InvalidInput(f"cannot read the mesh: {error}") from error
    if not isinstance(mesh, trimesh.Trimesh) or not len(mesh.faces):
        raise InvalidInput("the mesh has no triangles")
    return mesh


def frame_of(mesh: Any) -> tuple[np.ndarray, float]:
    """The centre and the largest side of the bounding box: the unit box
    LATO.2 works in, as its quantisation of the input takes it."""
    lo, hi = np.asarray(mesh.bounds, dtype=np.float64)
    return (lo + hi) / 2, max(float((hi - lo).max()), 1e-7)


def vertices_from_grid(
    grid: np.ndarray, offsets: np.ndarray, resolution: int, centre: np.ndarray, size: float
) -> np.ndarray:
    """Generated vertices in the input's coordinates. Each is a voxel of the
    ``resolution``³ grid over the unit box and an offset from the voxel's
    centre, in half voxels. Upstream's export leaves the half voxel of the
    centre out; it is kept here so that the mesh lies on the input."""
    unit = (grid.astype(np.float64) + 0.5) / resolution
    unit += offsets.astype(np.float64) / (2 * resolution)
    return (unit - 0.5) * size + centre


def wound_like(vertices: np.ndarray, faces: np.ndarray, reference: Any) -> tuple[np.ndarray, int]:
    """``faces`` each wound to face the way ``reference`` does at the nearest
    sampled point of its surface, and how many were turned. LATO.2's triangles
    are triples of connected vertices in index order: about half face inwards,
    and a game engine culls those."""
    import trimesh
    from scipy.spatial import cKDTree

    points, index = trimesh.sample.sample_surface(reference, ORIENTATION_SAMPLES, seed=0)
    normals = np.asarray(reference.face_normals)[index]
    corners = vertices[faces]
    own = np.cross(corners[:, 1] - corners[:, 0], corners[:, 2] - corners[:, 0])
    _, nearest = cKDTree(points).query(corners.mean(axis=1))
    turn = np.einsum("ij,ij->i", own, normals[nearest]) < 0
    faces = faces.copy()
    faces[turn] = faces[turn][:, ::-1]
    return faces, int(turn.sum())


def edge_faults(faces: np.ndarray) -> tuple[int, int]:
    """How many edges belong to one triangle only (the rim of a hole) and to
    more than two (a fin or a doubled wall)."""
    edges = np.sort(faces[:, [0, 1, 1, 2, 2, 0]].reshape(-1, 2), axis=1).astype(np.int64)
    _, uses = np.unique(edges[:, 0] * (int(edges.max()) + 1) + edges[:, 1], return_counts=True)
    return int((uses == 1).sum()), int((uses > 2).sum())


def describe(mesh: Any) -> dict[str, Any]:
    """Metadata of a trimesh in glTF axes (Y up)."""
    open_edges, shared_edges = edge_faults(np.asarray(mesh.faces))
    return {
        "vertices": len(mesh.vertices),
        "triangles": len(mesh.faces),
        "watertight": bool(mesh.is_watertight),
        "open_edges": open_edges,
        "non_manifold_edges": shared_edges,
        "bounds": np.asarray(mesh.bounds, dtype=float).round(4).tolist(),
        "up_axis": "Y",
        "color": "none",
    }


@contextmanager
def measured(phase: str) -> Iterator[None]:
    """Logs how long ``phase`` took and its peak GPU memory."""
    import torch

    torch.cuda.reset_peak_memory_stats()
    started = time.monotonic()
    yield
    logger.info(
        "%s: %.1f s, peak %.2f GiB",
        phase,
        time.monotonic() - started,
        torch.cuda.max_memory_allocated() / 2**30,
    )


class Lato2Server(ModelServer):
    model = ModelInfo(
        id="lato2",
        name="LATO.2",
        revision=WEIGHTS_REVISION[:12],
        license="MIT",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
        details={
            "kind": "two flow models: vertices, then their connections; not autoregressive",
            "precision": "as published, nothing quantised",
            "offloading": "image encoder, vertex models and topology models take turns on the GPU",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._groups: dict[str, tuple[Any, ...]] = {}
        self._models: Any = None

    def load(self) -> None:
        import torch

        source = source_directory()
        if not (source / "scripts/e2e_inference.py").is_file():
            raise FileNotFoundError(f"LATO.2 source not found in {source}; run `just setup`")
        if not (dinov2_directory() / "facebookresearch_dinov2_main/hubconf.py").is_file():
            raise FileNotFoundError(
                f"DINOv2 source not found in {dinov2_directory()}; run `just setup`"
            )
        sys.path.insert(0, str(source))
        # As upstream's script: Open3D's headless renderer can hang on the default platform.
        os.environ.setdefault("EGL_PLATFORM", "surfaceless")
        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("LATO.2 needs a CUDA GPU")
        download()
        self._models = _Models(weights_directory())
        self._groups = {
            "image": (self._models.dino,),
            "vertices": (self._models.vflow, self._models.vvae, self._models.offset_head),
            "topology": (self._models.voxel_encoder, self._models.tflow, self._models.tvae),
        }
        logger.info("LATO.2 ready (three groups take turns on the GPU)")

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, Params)
        data = job.inputs["mesh"].data
        reference = read_mesh(data)
        batch = self._prepare(data)
        try:
            self._only("image")
            with measured("view encoding"):
                condition = self._models.dino(np.stack(batch["image"])).float()
            return [self._one(batch, condition, reference, seed, job.params) for seed in job.seeds]
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            raise GenerationError(
                "out of GPU memory; free GPU memory or ask for fewer vertices"
            ) from error
        finally:
            self._only()

    def _only(self, group: str | None = None) -> None:
        """``group`` on the GPU, everything else in RAM."""
        import torch

        for name, parts in self._groups.items():
            for part in parts:
                part.to("cuda" if name == group else "cpu")
        torch.cuda.empty_cache()

    def _prepare(self, data: bytes) -> dict[str, Any]:
        """Upstream's preparation of one input: its vertices on the 1024³ grid,
        the 64³ voxels its surface occupies and the rendered view. As in
        upstream's script it runs in a process started for this input. The
        renderer's frames differ from one to the next, so only a new renderer
        draws the same view of the same mesh again; and its EGL context stays
        out of the process that holds the models."""
        from functools import partial

        from dataset.voxel_dataset import (  # pyright: ignore[reportMissingImports]
            VoxelVertexDataset,
            collate_fn,
        )
        from torch.utils.data import DataLoader
        from utils.inference import worker_init  # pyright: ignore[reportMissingImports]

        models = self._models
        sizes = {"resolution": models.resolution, "min_resolution": models.min_resolution}
        with tempfile.TemporaryDirectory(prefix="lato2-") as directory:
            (Path(directory) / "mesh.glb").write_bytes(data)
            dataset = VoxelVertexDataset(
                root_dir=directory, need_encoder_inputs=False, render=True, **sizes
            )
            loader = DataLoader(
                dataset,
                collate_fn=partial(collate_fn, **sizes),
                num_workers=1,
                multiprocessing_context="spawn",
                worker_init_fn=worker_init,
                timeout=PREPARATION_TIMEOUT_S,
            )
            batch = next(iter(loader))
        for failure in batch["errors"]:
            raise InvalidInput(f"cannot prepare the mesh: {failure['error'].splitlines()[0]}")
        return batch

    def _one(
        self, batch: dict[str, Any], condition: Any, reference: Any, seed: int, params: Params
    ) -> Output:
        import torch
        import trimesh
        from modules.sparse import SparseTensor  # pyright: ignore[reportMissingImports]
        from utils.inference import (  # pyright: ignore[reportMissingImports]
            build_voxel_fields,
            decode_vertices,
            edges_to_faces,
            pad_verts,
        )

        models = self._models
        gpu = torch.device("cuda")
        torch.manual_seed(seed)
        occupied = batch[f"active_voxels_{models.min_resolution}"]

        self._only("vertices")
        with measured("vertex flow"), torch.autocast("cuda", dtype=torch.bfloat16):
            coords = occupied.to(gpu)
            noise = SparseTensor(
                coords=coords.int(),
                feats=torch.randn(coords.shape[0], models.latent_dim, device=gpu),
            )
            latent = models.vertex_sampler.sample(
                model=models.vflow,
                noise=noise,
                cond=condition,
                neg_cond=torch.zeros_like(condition),
                steps=params.vertex_steps,
                cfg_strength=params.guidance,
                rescale_t=1.0,
                # As upstream's compute_density.
                density=torch.tensor([float(params.vertices)], device=gpu)
                / models.density_max
                * 1000.0,
            )
            placed, offsets = decode_vertices(
                models.vvae, models.offset_head, latent, INFERENCE_THRESHOLD
            )
        grid = placed[:, 1:].long()  # a batch of one
        count = int(grid.shape[0])
        if count < 3:
            raise GenerationError(f"only {count} vertices came out; try another seed")
        if count > models.max_vertices:
            raise GenerationError(
                f"{count} vertices came out, more than the topology model's"
                f" {models.max_vertices}; ask for fewer"
            )

        self._only("topology")
        with measured("topology flow"):
            verts, mask, lengths = pad_verts([grid], gpu)
            field = build_voxel_fields([occupied[:, 1:].long()], models.voxel_resolution, gpu)
            latent = models.topology_sampler.sample(
                model=models.tflow,
                noise=torch.randn(1, count, models.z_dim, device=gpu),
                verts=verts,
                mask=mask,
                cond=models.voxel_encoder(field),
                steps=params.topology_steps,
            )
            latent = latent.float() / models.latent_scale
            with torch.autocast("cuda", dtype=torch.bfloat16):
                edges = models.tvae.decode(
                    latent,
                    verts=verts,
                    verts_mask=mask,
                    chunk_size=CHUNK_SIZE,
                    threshold=EDGE_THRESHOLD,
                )[0]
        faces = edges_to_faces(edges, lengths[0], params.fill_quad_rings)
        if not len(faces):
            raise GenerationError("the vertices were not connected into triangles; try a new seed")

        vertices = vertices_from_grid(
            grid.numpy(), offsets.numpy(), models.resolution, *frame_of(reference)
        )
        faces, _ = wound_like(vertices, faces, reference)
        mesh = trimesh.Trimesh(vertices=vertices, faces=faces, process=False)
        mesh.remove_unreferenced_vertices()
        meta = {
            **describe(mesh),
            "target_vertices": params.vertices,
            "unconnected_vertices": count - len(mesh.vertices),
        }
        return Output("model/gltf-binary", mesh.export(file_type="glb"), meta)


class _Models:
    """The six checkpoints and the image encoder, in RAM, with the settings
    their configurations carry."""

    def __init__(self, weights: Path) -> None:
        import torch
        from models import (  # pyright: ignore[reportMissingImports]
            DinoV2Encoder,
            OffsetHead,
            TopoFlowEulerSampler,
            TopologySiTFlow,
            TopologyVAE,
            VertexSLatFlowModel,
            VertexVAE,
            VertFlowEulerCfgSampler,
            VoxelFieldConditioner,
        )
        from utils.load import load_latov2_model  # pyright: ignore[reportMissingImports]

        def loaded(kind: Any, name: str) -> tuple[Any, dict[str, Any]]:
            return load_latov2_model(kind, str(weights / name), torch.device("cpu"))

        self.vflow, vflow = loaded(VertexSLatFlowModel, "vflow.pt")
        self.vvae, vvae = loaded(VertexVAE, "vvae.pt")
        self.offset_head, _ = loaded(OffsetHead, "offset_head.pt")
        self.tflow, tflow = loaded(TopologySiTFlow, "tflow.pt")
        self.tvae, _ = loaded(TopologyVAE, "tvae.pt")
        self.voxel_encoder, voxels = loaded(VoxelFieldConditioner, "voxel_encoder.pt")
        self.dino = DinoV2Encoder(
            model_name=vflow["dino_version"],
            hub_dir=str(weights / "dinov2"),
            img_res=vflow["image_resolution"],
        ).eval()
        self.vertex_sampler = VertFlowEulerCfgSampler()
        self.topology_sampler = TopoFlowEulerSampler()

        self.resolution = int(vvae["resolution"])
        self.min_resolution = int(vvae["min_resolution"])
        self.latent_dim = int(vflow["latent_dim"])
        self.density_max = float(vflow["max_vertex_num"])
        self.z_dim = int(tflow["args"]["z_dim"])
        self.max_vertices = int(tflow["args"]["max_vertices"])
        self.latent_scale = float(tflow["latent_scale"])
        self.voxel_resolution = int(voxels["resolution"])
