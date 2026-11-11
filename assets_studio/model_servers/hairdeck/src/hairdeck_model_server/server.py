"""HairDeck hair-card server (contract v1).

HairDeck makes the hair of a character in one image as hair cards: flat strips
with UVs and colours along them, as games draw hair. SAM 3 finds the person,
the hair and the face, BiRefNet-matting cuts the person out, and a 448 px crop
of the head goes through DINOv3 ViT-L/16 into a set diffusion transformer that
samples up to 192 card tokens; a card VAE decodes them into strips and a colour
model paints 16 colours along each.

The result is in HairDeck's head frame: Y up, the face toward +Z, metres, the
cards one double-sided mesh with per-card UVs and vertex colours (COLOR_0). A
grey head proxy of the same frame can be added as a separate node, to see
where the hair sits.

Upstream's spring-bone chains (a third DiT) and its VRM export (Blender) are
left out: the chains are only drawn as tubes, a rig needs skinning.

On a GPU under 12 GB the stages take turns on it (upstream's residency
groups: SAM 3, the matting, the generation models), the rest waits in RAM.

Licences: HairDeck Apache-2.0; SAM 3 under the SAM License, DINOv3 under the
DINOv3 License (both gated on Hugging Face), BiRefNet MIT.
"""

import logging
import os
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Literal

import numpy as np
from model_server_sdk.images import open_image
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    KnownTask,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
    ui,
)

logger = logging.getLogger("hairdeck_model_server")

SOURCE_URL = "https://github.com/hyz317/HairDeck"
WEIGHTS_REPO = "hyz317/HairDeck"
# Git revisions, not secrets.
SOURCE_REVISION = "cb20f7073419a33c9ab88f22f686fcb9060a99f0"  # pragma: allowlist secret
WEIGHTS_REVISION = "7d2e07c5b4b22fbffa2df58d20b242db035ed69c"  # pragma: allowlist secret

# The files of the release that inference reads (~1.5 GB); the repository also
# holds the chain and point-cloud models, test data, the demo's avatar and media.
WEIGHTS_FILES = (
    "ckpt/image_model/model.safetensors",
    "ckpt/color_model/model.safetensors",
    "ckpt/card_vae/model.safetensors",
    "ckpt/card_vae/stats.json",
    "ckpt/card_vae/latent_stats_head.json",
    "data/token_stats_head.json",
    "data/head_frame_train.npz",
)
# Items of upstream's downloader (pinned revisions, SHA-256 checked, ~5.5 GB):
# SAM 3 and DINOv3 weights from gated repositories and their pinned sources.
THIRD_PARTY = ("birefnet", "sam3", "sam3_src", "dinov3_src", "dinov3")
THIRD_PARTY_FILES = (
    "third_party/weights/birefnet_matting/model.safetensors",
    "third_party/weights/sam3/sam3.pt",
    "third_party/weights/dinov3/model.safetensors",
    "third_party/sam3_src/.hairdeck_commit",
    "third_party/dinov3_src/.hairdeck_commit",
)

INPUT_MIME = ["image/png", "image/jpeg", "image/webp"]


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    steps: int = Field(
        default=50,
        ge=10,
        le=100,
        description="Sampling steps (upstream offers 50 and the faster 10)",
        json_schema_extra=ui(ru="Шаги семплирования (у авторов 50 и быстрые 10)", primary=True),
    )
    guidance: float = Field(
        default=2.0,
        ge=1.0,
        le=4.0,
        description="Guidance scale: how closely the cards follow the image",
        json_schema_extra=ui(ru="Сила следования картинке (guidance)"),
    )
    colors: bool = Field(
        default=True,
        description="Colours along every card from the colour model; off: one hair colour",
        json_schema_extra=ui(ru="Цвета вдоль каждой карты (модель цвета); иначе один цвет волос"),
    )
    head_proxy: bool = Field(
        default=False,
        description="Add the grey head proxy as a separate node, to see where the hair sits",
        json_schema_extra=ui(ru="Добавить серую голову-заглушку отдельным узлом — видно посадку"),
    )


TASK = TaskSpec(
    KnownTask.IMAGE_TO_3D,
    Params,
    ("model/gltf-binary",),
    prompt="none",
    inputs=(
        InputSpec(
            role="image",
            mime=INPUT_MIME,
            description="A character with the head and hair visible",
            labels={"ru": "Персонаж, голова и волосы видны"},
        ),
    ),
    max_count=4,
)


def source_directory() -> Path:
    return Path(
        os.environ.get("HAIRDECK_SOURCE", "~/.cache/assets-studio/hairdeck-source")
    ).expanduser()


def root_directory() -> Path:
    """HairDeck's workspace root (HAIRDECK_ROOT): ckpt/, data/, third_party/."""
    return Path(os.environ.get("HAIRDECK_ROOT", "~/.cache/assets-studio/hairdeck")).expanduser()


def missing_files(root: Path) -> list[str]:
    return [name for name in (*WEIGHTS_FILES, *THIRD_PARTY_FILES) if not (root / name).is_file()]


def download(gated_source: Literal["huggingface", "modelscope"] = "huggingface") -> None:
    """The release files inference needs at the pinned revision, then SAM 3,
    BiRefNet-matting and DINOv3 through upstream's downloader (pinned and
    checked). Network only. SAM 3 and DINOv3 are gated on Hugging Face: accept
    their licences and set HF_TOKEN; ``modelscope`` takes the same files (same
    checksums) from ModelScope's mirrors instead."""
    from huggingface_hub import hf_hub_download

    root = root_directory()
    for name in WEIGHTS_FILES:
        hf_hub_download(WEIGHTS_REPO, name, revision=WEIGHTS_REVISION, local_dir=root)
    script = source_directory() / "scripts/setup/download_weights.py"
    if not script.is_file():
        raise FileNotFoundError(
            f"HairDeck source not found in {source_directory()}; run `just setup`"
        )
    command = [sys.executable, str(script), "--root", str(root), "--only", *THIRD_PARTY]
    subprocess.run(  # noqa: S603 -- fixed argv, no shell
        [*command, "--gated-source", gated_source], check=True
    )


@dataclass(frozen=True)
class Upstream:
    """The functions of HairDeck's source the server calls besides the pipeline."""

    load_image: Any
    hair_color: Any
    hair_trimesh: Any
    head_trimesh: Any


def build_glb(hair: Any, head: Any = None) -> bytes:
    """One scene as upstream writes it: the cards node, optionally the head."""
    import trimesh

    scene = trimesh.Scene()
    scene.add_geometry(hair, node_name="hair_cards", geom_name="hair_cards")
    if head is not None:
        scene.add_geometry(head, node_name="head_proxy", geom_name="head_proxy")
    return scene.export(file_type="glb")


def describe(hair: Any, cards: list[Any]) -> dict[str, Any]:
    """Metadata of the cards mesh (head frame: Y up, face toward +Z, metres)."""
    return {
        "cards": len(cards),
        "meshed_cards": int(sum(len(points) >= 3 for points, _ in cards)),
        "vertices": len(hair.vertices),
        "triangles": len(hair.faces),
        "bounds": np.asarray(hair.bounds, dtype=float).round(4).tolist(),
        "up_axis": "Y",
        "forward_axis": "+Z",
        "units": "m",
    }


class HairDeckServer(ModelServer):
    model = ModelInfo(
        id="hairdeck",
        name="HairDeck",
        revision=WEIGHTS_REVISION[:12],
        license="Apache-2.0; SAM 3: SAM License; DINOv3: DINOv3 License; BiRefNet: MIT",
        source=SOURCE_URL,
        details={
            "parameters": "182 M card DiT, 183 M colour model, card VAE;"
            " SAM 3, BiRefNet-matting, DINOv3 ViT-L/16",
            "precision": "as published, autocast on the GPU",
            "offloading": "under 12 GB the stages take turns on the GPU"
            " (SAM 3, matting, generation)",
            "output": "hair cards in the head frame: Y up, face toward +Z, metres",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._pipeline: Any = None
        self._upstream: Upstream | None = None

    def load(self) -> None:
        import torch

        source = source_directory()
        if not (source / "hairdeck/infer/image.py").is_file():
            raise FileNotFoundError(f"HairDeck source not found in {source}; run `just setup`")
        root = root_directory()
        if missing := missing_files(root):
            raise FileNotFoundError(
                f"HairDeck weights missing in {root}: {', '.join(missing)}; run `just download`"
            )
        os.environ["HAIRDECK_ROOT"] = str(root)  # where upstream's paths module looks
        sys.path.insert(0, str(source))
        from hairdeck.infer import glb, image  # pyright: ignore[reportMissingImports]

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("HairDeck needs a CUDA GPU")
        self._pipeline = image.Pipeline(
            device="cuda",
            chains=False,
            offload=os.environ.get("HAIRDECK_OFFLOAD", "auto"),
        )
        if self._pipeline.color is None:
            raise RuntimeError(
                f"HairDeck's colour model did not load: {self._pipeline.color_error}"
            )
        self._upstream = Upstream(
            load_image=image.load_image,
            hair_color=glb.hair_color,
            hair_trimesh=glb.hair_trimesh,
            head_trimesh=glb.head_trimesh,
        )
        residency = self._pipeline.residency
        logger.info(
            "HairDeck ready (offload %s: %s)",
            "on" if residency.enabled else "off",
            residency.reason,
        )

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, Params)
        try:
            with self._pipeline.residency.request():
                return self._generate(job.inputs["image"].data, job.params, job.seeds)
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            torch.cuda.empty_cache()
            raise GenerationError(
                "out of GPU memory; free GPU memory or set HAIRDECK_OFFLOAD=on"
            ) from error

    def _generate(self, data: bytes, params: Params, seeds: tuple[int, ...]) -> list[Output]:
        """The upstream stages of ``Pipeline.run`` with any seeds and no files:
        the preprocessing and the colour context once, then per seed its noise,
        sampling, card colours and the GLB."""
        assert self._upstream is not None
        pipeline, upstream = self._pipeline, self._upstream
        rgb, alpha, size_note = upstream.load_image(open_image(data, keep_alpha=True))
        prep, crop = pipeline.prep(rgb, alpha)
        notes = [*prep["notes"], *([size_note] if size_note else [])]
        if crop is None:
            raise InvalidInput(
                f"no head found in the image ({prep['status']}); use one where the hair or"
                " the face is clearly visible"
            )
        hair_colour = upstream.hair_color(prep["rgb"], prep["hair"])
        context = pipeline.color_context(crop, pipeline.matte_crop(prep)) if params.colors else None
        head = upstream.head_trimesh() if params.head_proxy else None
        residency = pipeline.residency
        common = {
            "preprocessing": prep["status"],
            "head_box": [round(float(v), 1) for v in prep["box"]],  # x0, y0, x1, y1 in pixels
            "hair_color": [round(float(c), 4) for c in hair_colour],
            "head_proxy": params.head_proxy,
            "offload": bool(residency.enabled),
        }
        outputs = []
        for seed in seeds:
            cards, tokens = pipeline.sample(
                crop, pipeline.noise(seed), params.steps, params.guidance
            )
            card_colours, seed_notes = None, list(notes)
            if context is not None and cards:
                try:
                    card_colours = pipeline.card_colors(context, tokens, len(cards))
                except Exception as error:  # upstream keeps one hair colour then too
                    logger.warning("seed %d: card colours failed: %s", seed, error)
                    seed_notes.append(f"one hair colour: card colours failed ({error})")
            hair = (
                upstream.hair_trimesh(cards, hair_colour, card_srgb=card_colours) if cards else None
            )
            if hair is None:
                raise GenerationError(
                    f"no hair cards for seed {seed}; try another seed or image", retryable=False
                )
            meta = {
                **describe(hair, cards),
                **common,
                "colors": "per card" if card_colours is not None else "hair colour",
                "steps": params.steps,
                "guidance": params.guidance,
            }
            if seed_notes:
                meta["notes"] = seed_notes
            outputs.append(Output("model/gltf-binary", build_glb(hair, head), meta))
        return outputs
