"""BPT artist-like mesh generation server (contract v1).

BPT (Tencent Hunyuan, CVPR 2025) is an autoregressive transformer: it writes
a triangle mesh as a sequence of tokens, looking at a point cloud of the
surface to rebuild. Its tokenisation (Blocked and Patchified) names a vertex
by its block of the grid and its place in the block, and writes a fan of
triangles around a vertex as one patch, about a quarter of the tokens a plain
list of coordinates takes. The released "lite" model is used, the only one
published; it reads a point cloud and nothing else. Geometry only: no UVs, no
colour; bake those from the detailed mesh afterwards.

The model decides how many triangles the shape needs: there is no target
count. A mesh is at most 10,000 tokens long and its vertices lie on a 128³
grid over the shape's bounding box.

The weights stay as published (bfloat16) and run in float16, as upstream runs
them. Sampling is upstream's ``MeshTransformer.generate`` with the settings of
its ``main.py``.

Upstream tests on Python 3.9 with torch 2.1.2; the server imports the pinned
source on Python 3.11 with torch 2.5.1 (see pyproject.toml).

Licence: the code's repository carries the Tencent Hunyuan 3D 2.0 Community
License, which is not granted in the European Union, the United Kingdom and
South Korea; the weights' page says MIT.
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

logger = logging.getLogger("bpt_model_server")

WEIGHTS_REPO = "whaohan/bpt"
WEIGHTS_FILE = "bpt-8-16-500m.pt"
# Git revisions, not secrets.
WEIGHTS_REVISION = "4a7c93a2691b38682fc33ba8a414f2bf849700e3"  # pragma: allowlist secret
SOURCE_REVISION = "4e6510e7ebaac6d21f558252ad7eb573aed0234a"  # pragma: allowlist secret

RETOPO_3D = "3d-retopo"

# Upstream's main.py and utils.py.
MODEL_CONFIG = "config/BPT-open-8k-8-16.yaml"
MAX_TOKENS = 10_000  # the configuration's max_seq_len: the model has no position after it
POINTS = 4096
TOP_K, TOP_P = 50, 0.95

# The frame the model looks at the surface in: the bounding box centred, its
# longest side 1.9.
EXTENT = 1.9

# The checkpoint is a whole training state. Besides the weights it names
# these classes of the training run, which nothing here needs.
TRAINING_STATE = (
    ("deepspeed.runtime.fp16.loss_scaler", "LossScaler"),
    ("deepspeed.runtime.zero.config", "ZeroStageEnum"),
    ("deepspeed.utils.tensor_fragment", "fragment_address"),
)


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    temperature: float = Field(
        default=0.5,
        ge=0.1,
        le=1.5,
        description="Randomness of each token, among the 50 likeliest ones",
        json_schema_extra=ui(ru="Случайность каждого токена, среди 50 вероятнейших", primary=True),
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
    return Path(os.environ.get("BPT_SOURCE", "~/.cache/assets-studio/bpt-source")).expanduser()


def weights_directory() -> Path:
    return Path(os.environ.get("BPT_WEIGHTS", "~/.cache/assets-studio/bpt")).expanduser()


def download() -> None:
    """The checkpoint at the pinned revision (~1.6 GB). Network only."""
    from huggingface_hub import snapshot_download

    snapshot_download(
        WEIGHTS_REPO,
        revision=WEIGHTS_REVISION,
        local_dir=weights_directory(),
        allow_patterns=[WEIGHTS_FILE],
    )


def read_weights(path: Path) -> dict[str, Any]:
    """The model's tensors out of the checkpoint, read with PyTorch's
    weights-only loader. Upstream unpickles the whole file, which runs what
    the pickle says and needs DeepSpeed installed for the training state in
    it; here empty stand-ins take the place of those classes."""
    import torch

    def stand_in(module: str, name: str) -> type:
        return type(
            name,
            (),
            {
                "__module__": module,
                "__new__": lambda cls, *args, **kwargs: object.__new__(cls),
                "__init__": lambda self, *args, **kwargs: None,
                "__setstate__": lambda self, state: None,
            },
        )

    with torch.serialization.safe_globals([stand_in(*path_) for path_ in TRAINING_STATE]):
        return torch.load(path, map_location="cpu", weights_only=True)["model"]


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
    return np.concatenate([points, normals], axis=1).astype(np.float16)  # as upstream


def placed(vertices: np.ndarray, centre: np.ndarray, size: float) -> np.ndarray:
    """Decoded vertices at the input's place and size."""
    return np.asarray(vertices, dtype=np.float64) * (size / EXTENT) + centre


def mesh_from_tokens(tokens: np.ndarray, **layout: Any) -> Any:
    """Upstream's decoding of a finished sequence: three corners per
    triangle, then equal corners merged, repeated triangles dropped and the
    winding made consistent. ``layout`` is the model's block size, offset
    size and whether a patch starts with a special block token."""
    from model.data_utils import to_mesh  # pyright: ignore[reportMissingImports]
    from model.serializaiton import BPT_deserialize  # pyright: ignore[reportMissingImports]

    try:
        # A copy: the decoder rewrites the first token of each patch in place.
        corners = BPT_deserialize(tokens.astype(np.int64), **layout)
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


class BptServer(ModelServer):
    model = ModelInfo(
        id="bpt",
        name="BPT 0.5B",
        revision=WEIGHTS_REVISION[:12],
        license=(
            "Tencent Hunyuan 3D 2.0 Community (code; not granted in the EU, UK and"
            " South Korea), MIT (weights, per their page)"
        ),
        source=f"https://huggingface.co/{WEIGHTS_REPO}",
        details={
            "kind": "autoregressive transformer: the mesh as block and offset tokens, in patches",
            "precision": "bfloat16 weights as published, run in float16",
            "limits": "no target triangle count; 10,000 tokens; vertices on a 128³ grid",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._model: Any = None

    def load(self) -> None:
        import torch
        import yaml

        source = source_directory()
        if not (source / "model/model.py").is_file():
            raise FileNotFoundError(f"BPT source not found in {source}; run `just setup`")
        sys.path.insert(0, str(source))
        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("BPT needs a CUDA GPU")
        download()

        from model.model import MeshTransformer  # pyright: ignore[reportMissingImports]

        config = yaml.safe_load((source / MODEL_CONFIG).read_text())
        if config["max_seq_len"] != MAX_TOKENS or config["pc_num"] != POINTS:
            raise RuntimeError(f"{MODEL_CONFIG} is not the configuration this server was made for")
        # Upstream names the point-cloud encoder's configuration by a path
        # relative to its source.
        with contextlib.chdir(source):
            model = MeshTransformer(  # as upstream's main.py
                dim=config["dim"],
                attn_depth=config["depth"],
                max_seq_len=config["max_seq_len"],
                dropout=config["dropout"],
                mode=config["mode"],
                num_discrete_coors=2 ** int(config["quant_bit"]),
                block_size=config["block_size"],
                offset_size=config["offset_size"],
                conditioned_on_pc=config["conditioned_on_pc"],
                use_special_block=config["use_special_block"],
                encoder_name=config["encoder_name"],
                encoder_freeze=config["encoder_freeze"],
            )
        model.load_state_dict(read_weights(weights_directory() / WEIGHTS_FILE))  # strict
        self._model = model.eval().half().to("cuda")
        logger.info("BPT ready, %.2f GiB on the GPU", torch.cuda.memory_allocated() / 2**30)

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
        mesh = mesh_from_tokens(
            tokens,
            block_size=self._model.block_size,
            offset_size=self._model.offset_size,
            use_special_block=self._model.use_special_block,
        )
        mesh.vertices = placed(mesh.vertices, *frame_of(reference))
        meta = {**describe(mesh), "tokens": len(tokens)}
        return Output("model/gltf-binary", mesh.export(file_type="glb"), meta)

    def _tokens(self, cloud: Any, seed: int, params: Params) -> np.ndarray:
        """The tokens of a finished mesh, without the end one."""
        import torch
        from x_transformers.autoregressive_wrapper import top_k, top_p

        def likeliest(logits: Any) -> Any:  # upstream's joint_filter
            return top_p(top_k(logits, k=TOP_K), thres=TOP_P)

        torch.manual_seed(seed)
        codes = self._model.generate(
            batch_size=1,
            temperature=params.temperature,
            pc=cloud,
            filter_logits_fn=likeliest,
            filter_kwargs={},
            return_codes=True,
        )[0]
        # From the end token on, upstream returns padding.
        ended = codes == self._model.pad_id
        if not bool(ended.any()):
            raise GenerationError(
                f"the mesh was not finished in the model's {MAX_TOKENS} tokens;"
                " the shape is too detailed for it, or try another seed"
            )
        return codes[~ended].cpu().numpy()
