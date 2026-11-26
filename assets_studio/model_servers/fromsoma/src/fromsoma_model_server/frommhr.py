"""Model server for frommhr: a realistic person from Anny's parameters and two pictures of a bald
head, in MHR's face space.

It works like fromsoma, with two differences. The reader was trained on renders of random MHR
people (Meta's Momentum Human Rig, built from adults only) and has no known inputs: it reads the
head's 20 MHR coefficients and the 14 pigment sliders from the pictures alone. The body is the
one fromsoma makes: MHR's body coefficients and bone scales are fitted to Anny's body of the given
parameters, and what MHR's body space cannot reach is added back below the head (mhr.py), on one
mesh, nothing stitched. The parameters are fromsoma's, children included, but MHR's identity
space was built from adult scans, so a child gets the closest adult head. Stubble
is kept only for men from BEARD_FROM years on, since the reader looked for it on everyone. The
head takes the pictures' own colour as in fromsoma (texture=pictures, facetex.py).

The server shares fromsoma's root directory (FROMSOMA_ROOT) with its SOMA-X data and Face
Landmarker; its reader is frommhr.safetensors there, published on Hugging Face (HF_REPO, `just
download`) with its training code (frommhr/ in the repository).
"""

import logging
import os
from pathlib import Path
from typing import Any

import numpy as np
from model_server_sdk.images import open_image

from fromsoma_model_server.server import (
    BUILD,
    LANDMARKER_FILE,
    SOMA_DIR,
    SOMA_FILES,
    TASK,
    Params,
    colour_head,
    describe,
    face_texture,
    root_directory,
)
from model_server_sdk import (
    GenerationError,
    InvalidInput,
    Job,
    ModelInfo,
    ModelServer,
    Output,
    serve,
)

logger = logging.getLogger(__name__)

MODEL_FILE = "frommhr.safetensors"
FORMAT = "frommhr.reader.v1"
HF_REPO = "zjgkkn/frommhr"
# The published package's commit.
HF_REVISION = "e7747532d766b71c2fd7baf3c40b4d125da35cd6"  # pragma: allowlist secret
DEFAULT_PORT = 9143
BEARD_FROM = 18.0  # years


def stubble_for(pigments: dict[str, float], female: bool, years: float) -> dict[str, float]:
    """The reader looks for stubble on everyone (it is not told the sex or the age); only men
    from BEARD_FROM years on keep it."""
    if female or years < BEARD_FROM:
        return {**pigments, "stubble": 0.0}
    return pigments


def required_files() -> list[str]:
    return [MODEL_FILE, LANDMARKER_FILE] + [f"{SOMA_DIR}/{name}" for name in SOMA_FILES]


def missing_files(root: Path) -> list[str]:
    return [name for name in required_files() if not (root / name).is_file()]


def download() -> None:
    """The reader's package from Hugging Face (the SOMA-X data and landmarker: server.download)."""
    from huggingface_hub import hf_hub_download

    root = root_directory()
    root.mkdir(parents=True, exist_ok=True)
    hf_hub_download(HF_REPO, MODEL_FILE, revision=HF_REVISION, local_dir=root)
    missing = missing_files(root)
    print("missing:", ", ".join(missing) if missing else "nothing")


class FromMhrServer(ModelServer):
    model = ModelInfo(
        id="frommhr",
        name="frommhr face reader",
        license=(
            "reader weights and package: Apache-2.0; SOMA-X, Anny, MHR, DINOv2 and MediaPipe"
            " Face Landmarker: Apache-2.0; MPFB2 assets: CC0"
        ),
        source=f"https://huggingface.co/{HF_REPO}",
        details={
            "network": "DINOv2 ViT-S/14 on front and profile at 518 px, an eye-strip CNN",
            "reads": "the head's 20 MHR coefficients and 14 pigment sliders, nothing given",
            "body": (
                "Anny's body of the parameters (MHR's body fit, its remainder added back below"
                " the head, faded in over 6 cm)"
            ),
            "children": "Anny's child body; the head is MHR's closest adult head",
            "texture": (
                "the pigments painted; on the head, the pictures' own colour projected through"
                " cameras fitted to the head (texture=pictures)"
            ),
            "output": (
                "SOMA mesh, root + 77 joints, 4 weights per vertex, eyeballs, one 4096 texture;"
                " metres, Y up, face toward +Z"
            ),
            "device": "CPU",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._reader: Any = None
        self._bodies: Any = None
        self._texture: Any = None
        self._package: dict[str, Any] = {}

    def load(self) -> None:
        import torch

        from fromsoma_model_server.mhr import MhrBodies
        from fromsoma_model_server.package import load
        from fromsoma_model_server.reader import MhrReader

        torch.set_grad_enabled(False)
        torch.set_num_threads(max(1, min(8, (os.cpu_count() or 2) // 2)))
        root = root_directory()
        missing = missing_files(root)
        if missing:
            raise RuntimeError(
                f"missing in {root}: {', '.join(missing)}"
                f" (`just download` fetches {MODEL_FILE} from {HF_REPO})"
            )
        package = load(root / MODEL_FILE)
        if package.get("format") != FORMAT:
            raise RuntimeError(f"{root / MODEL_FILE} is not a frommhr reader package")
        self._bodies = MhrBodies(package, root / SOMA_DIR)
        self._reader = MhrReader(package, root / LANDMARKER_FILE)
        self._texture = face_texture(self._bodies, root)
        self._package = {k: package[k] for k in ("config", "training")}
        logger.info("frommhr reader %s loaded", describe(self._package))

    def generate(self, job: Job) -> list[Output]:
        import torch

        from fromsoma_model_server.glb import build_glb
        from fromsoma_model_server.person import png
        from fromsoma_model_server.reader import PictureError, slider_values

        params = job.params
        assert isinstance(params, Params)
        if self._reader is None or self._bodies is None or self._texture is None:
            raise GenerationError("the model is not loaded", retryable=True)
        front = np.asarray(open_image(job.inputs["image"].data))
        left = np.asarray(open_image(job.inputs["profile"].data))
        try:
            reading = self._reader.read_face(front, left)
        except PictureError as error:
            raise InvalidInput(str(error)) from error
        female = params.sex == "female"
        pigments = stubble_for(
            slider_values(reading.sliders, self._package["config"]["sliders"]), female, params.age
        )
        phenotype = {
            "gender": 1.0 if female else 0.0,
            "age": float(self._bodies.to_anny_age(torch.tensor([params.age]))[0]),
            **{name: getattr(params, name) for name in BUILD},
            **params.origin(),
            "cupsize": params.cupsize,
            "firmness": params.firmness,
        }
        person, fit = self._bodies.build_mhr(phenotype, reading.coefficients, pigments)
        texture: dict[str, Any] = {"source": "pigments"}
        if params.texture == "pictures":
            person, texture = colour_head(self._texture, person, front, left, reading.crops)
        data = build_glb(person.surfaces, person.skeleton, png(person.texture))
        meta = {
            "seed": job.seeds[0],
            "sex": params.sex,
            "age_years": params.age,
            "anny_phenotype": {k: round(v, 4) for k, v in phenotype.items()},
            "mhr_head": [round(v, 4) for v in reading.coefficients],
            "mhr_body": fit["mhr_body"],
            "mhr_scales": fit["mhr_scales"],
            "body_fit": {
                k: fit[k]
                for k in (
                    "residual_mm",
                    "mhr_fit_mm",
                    "head_shape_mm",
                    "neck_fade_m",
                    "anny_height_m",
                    "placed_mm",
                )
            },
            "pigments": {k: round(v, 5) for k, v in pigments.items()},
            "crops_px": reading.crops,
            "reader": describe(self._package),
            "joints": len(person.skeleton.names),
            "texture": texture,
            **person.meta,
        }
        return [Output("model/gltf-binary", data, meta)]


def main() -> None:
    serve(
        lambda _: FromMhrServer(),
        default_port=DEFAULT_PORT,
        description="frommhr: a person on SOMA's mesh and skeleton from two bald-head pictures",
    )


if __name__ == "__main__":
    main()
