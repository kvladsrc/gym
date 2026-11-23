"""DeepMesh artist-like mesh generation server (contract v1).

DeepMesh (Tsinghua and ShengShu, ICCV 2025) is an autoregressive transformer:
it writes a triangle mesh as a sequence of tokens, face after face, the way an
artist would lay them out, looking at a point cloud of the surface to rebuild.
The released 0.5B model is used. Geometry only: no UVs, no colour; bake those
from the detailed mesh afterwards.

The model decides how many triangles the shape needs: there is no target
count. Vertices lie on a 512³ grid over the shape's bounding box.

The weights stay as published (float32, with bfloat16 autocast as upstream).
The sampling loop is upstream's ``sample.py`` for one mesh at a time: the
9000-token window that rereads its last 4500 tokens, the Gumbel-max sampling
and the mesh clean-up are the same.

Upstream declares torch 2.5.1 on Python 3.12; the server imports the pinned
source on Python 3.11 (see pyproject.toml).

Licence: Apache-2.0 the code, MIT the weights.
"""

import contextlib
import io
import logging
import os
import sys
import time
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

logger = logging.getLogger("deepmesh_model_server")

WEIGHTS_REPO = "zzzrw/DeepMesh"
WEIGHTS_FILE = "pytorch_model.bin"
# Git revisions, not secrets.
WEIGHTS_REVISION = "a19a1bff1018eed1cf3506298f8da8d27ae3f678"  # pragma: allowlist secret
SOURCE_REVISION = "940d23ee2b5e4e94c565c800b49d6563c575c26d"  # pragma: allowlist secret

RETOPO_3D = "3d-retopo"

# Upstream's sample.py.
MODEL_CONFIG = "Diff_LLaMA_551M"
VOCABULARY = 2 * 4**3 + 8**3 + 16**3 + 2  # patch, block and offset tokens, start, end
START, END = VOCABULARY - 2, VOCABULARY - 1
BLOCK_SIZE = 270_000
WINDOW = 9000  # tokens the model attends to
STRIDE = 4500  # the window moves by this much, rereading the rest
POINTS = 16_384

# The frame the model looks at the surface in: the bounding box centred, its
# longest side 1.9, the axes turned so that the third one comes first. It
# writes the vertices with the axes in the input's order.
EXTENT = 1.9
MODEL_AXES = (2, 0, 1)


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    temperature: float = Field(
        default=0.5,
        ge=0.0,
        le=1.5,
        description="Randomness of each token; 0 always takes the likeliest one",
        json_schema_extra=ui(
            ru="Случайность каждого токена; 0 всегда берёт самый вероятный", primary=True
        ),
    )
    max_tokens: int = Field(
        default=90_000,
        ge=1000,
        le=90_000,
        description="Give up if the mesh is not finished after this many tokens",
        json_schema_extra=ui(ru="Сдаться, если сетка не закончена за столько токенов"),
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
    return Path(
        os.environ.get("DEEPMESH_SOURCE", "~/.cache/assets-studio/deepmesh-source")
    ).expanduser()


def weights_directory() -> Path:
    return Path(os.environ.get("DEEPMESH_WEIGHTS", "~/.cache/assets-studio/deepmesh")).expanduser()


def download() -> None:
    """The checkpoint at the pinned revision (~3.4 GB). Network only."""
    from huggingface_hub import snapshot_download

    snapshot_download(
        WEIGHTS_REPO,
        revision=WEIGHTS_REVISION,
        local_dir=weights_directory(),
        allow_patterns=[WEIGHTS_FILE],
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
    """The centre and the largest side of the bounding box."""
    lo, hi = np.asarray(mesh.bounds, dtype=np.float64)
    return (lo + hi) / 2, max(float((hi - lo).max()), 1e-7)


def point_cloud(mesh: Any) -> np.ndarray:
    """What the model looks at: ``POINTS`` points spread evenly over the
    surface, each with the normal of its triangle, in the model's frame. The
    same points for every seed."""
    import trimesh

    centre, size = frame_of(mesh)
    points, index = trimesh.sample.sample_surface(mesh, POINTS, seed=0)
    points = (np.asarray(points) - centre) * (EXTENT / size)
    normals = np.asarray(mesh.face_normals)[index]
    cloud = np.concatenate([points[:, MODEL_AXES], normals[:, MODEL_AXES]], axis=1)
    return cloud.astype(np.float16)  # as upstream stores it


def window_start(position: int) -> int:
    """The first token the model is given when it writes token ``position``.
    Usually the previous one alone, the earlier ones being cached. Once the
    window is full, every ``STRIDE`` tokens it moves on and the model rereads
    the ``STRIDE`` tokens it keeps."""
    if position > WINDOW and (position - WINDOW - 1) % STRIDE == 0:
        return position - STRIDE - 1
    return position - 1


def placed(vertices: np.ndarray, centre: np.ndarray, size: float) -> np.ndarray:
    """Decoded vertices at the input's place and size."""
    return np.asarray(vertices, dtype=np.float64) * (size / EXTENT) + centre


def mesh_from_tokens(tokens: np.ndarray) -> Any:
    """Upstream's decoding of a finished sequence: three corners per
    triangle, then equal corners merged, repeated triangles dropped and the
    winding made consistent. Upstream's sample.py also exchanges the first and
    third coordinates, which mirrors the mesh against its input; the decoded
    coordinates are in the input's axes as they are."""
    from sft.datasets.data_utils import to_mesh  # pyright: ignore[reportMissingImports]
    from sft.datasets.serializaiton import deserialize  # pyright: ignore[reportMissingImports]

    try:
        corners = deserialize(tokens.astype(np.int64))
    except (ValueError, IndexError) as error:  # no complete patch among the tokens
        raise GenerationError("the tokens do not describe a mesh; try another seed") from error
    if len(corners) < 3:
        raise GenerationError("the tokens do not describe a mesh; try another seed")
    faces = np.arange(len(corners)).reshape(-1, 3)
    return to_mesh(corners, faces, transpose=False, post_process=True)


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


class DeepMeshServer(ModelServer):
    model = ModelInfo(
        id="deepmesh",
        name="DeepMesh 0.5B",
        revision=WEIGHTS_REVISION[:12],
        license="Apache-2.0 (code), MIT (weights)",
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
        details={
            "kind": "autoregressive transformer: the mesh as a sequence of tokens",
            "precision": "float32 weights as published, bfloat16 autocast",
            "limits": "no target triangle count; vertices on a 512³ grid",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._gpt: Any = None

    def load(self) -> None:
        import torch

        source = source_directory()
        if not (source / "lit_gpt/model_cache.py").is_file():
            raise FileNotFoundError(f"DeepMesh source not found in {source}; run `just setup`")
        sys.path.insert(0, str(source))
        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("DeepMesh needs a CUDA GPU")
        download()

        from lit_gpt.model_cache import Config, GPTCache  # pyright: ignore[reportMissingImports]

        config = Config.from_name(MODEL_CONFIG)
        config.padded_vocab_size = VOCABULARY
        config.block_size = BLOCK_SIZE
        # Upstream names the point-cloud encoder's configuration by a path
        # relative to its source.
        with contextlib.chdir(source):
            gpt = GPTCache(config)
        state = torch.load(
            weights_directory() / WEIGHTS_FILE, map_location="cpu", weights_only=True
        )
        missing, unexpected = gpt.load_state_dict(state, strict=False)  # as upstream
        del state
        if missing or unexpected:
            logger.warning(
                "checkpoint: %d missing keys (%s), %d unexpected (%s)",
                len(missing),
                ", ".join(missing[:5]),
                len(unexpected),
                ", ".join(unexpected[:5]),
            )
        self._gpt = gpt.eval().to("cuda")
        logger.info("DeepMesh ready, %.2f GiB on the GPU", torch.cuda.memory_allocated() / 2**30)

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, Params)
        reference = read_mesh(job.inputs["mesh"].data)
        cloud = torch.from_numpy(point_cloud(reference))[None].to("cuda")
        try:
            return [self._one(cloud, reference, seed, job.params) for seed in job.seeds]
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            raise GenerationError("out of GPU memory; free GPU memory and try again") from error
        finally:
            self._forget()

    def _forget(self) -> None:
        """Drops what the model keeps between tokens: the keys and values of
        its window at each level and the point cloud's embedding."""
        import torch

        level = self._gpt.transformer.h
        while level is not None:
            level.pre_kv_caches = []
            if level.hourglass is not None:
                level.post_kv_caches = []
            level = level.hourglass
        self._gpt.cond_embeds = None
        torch.cuda.empty_cache()

    def _one(self, cloud: Any, reference: Any, seed: int, params: Params) -> Output:
        import torch

        torch.cuda.reset_peak_memory_stats()
        started = time.monotonic()
        tokens = self._tokens(cloud, seed, params)
        seconds = time.monotonic() - started
        logger.info(
            "%d tokens in %.0f s (%.1f per second), peak %.2f GiB",
            len(tokens),
            seconds,
            len(tokens) / seconds,
            torch.cuda.max_memory_allocated() / 2**30,
        )
        mesh = mesh_from_tokens(tokens)
        mesh.vertices = placed(mesh.vertices, *frame_of(reference))
        meta = {**describe(mesh), "tokens": len(tokens)}
        return Output("model/gltf-binary", mesh.export(file_type="glb"), meta)

    def _tokens(self, cloud: Any, seed: int, params: Params) -> np.ndarray:
        """The mesh's tokens, without the start and the end ones."""
        import torch

        gpu = torch.device("cuda")
        self._forget()  # nothing of the previous mesh
        torch.manual_seed(seed)
        tokens = torch.full((1, params.max_tokens), END, dtype=torch.long, device=gpu)
        tokens[0, 0] = START
        condition = cloud  # read with the first token, cached by the model afterwards
        for position in range(1, params.max_tokens):
            start = window_start(position)
            with torch.autocast("cuda", dtype=torch.bfloat16):
                logits = self._gpt(
                    tokens[:, start:position],
                    pc=condition,
                    start=start,
                    window_size=WINDOW,
                    input_pos=torch.arange(position, device=gpu),
                )[:, -1]
            condition = None
            # Gumbel-max in float64, as upstream (arXiv 2409.02908).
            logits = logits.to(torch.float64)
            noise = -torch.log(torch.rand_like(logits))
            token = int(torch.argmax(logits.exp() / noise**params.temperature, dim=-1))
            if token == END:
                return tokens[0, 1:position].cpu().numpy()
            tokens[0, position] = token
            if position % 5000 == 0:
                logger.info("%d tokens", position)
        raise GenerationError(
            f"the mesh was not finished in {params.max_tokens} tokens;"
            " the shape may be too detailed for the model, or try another seed"
        )
