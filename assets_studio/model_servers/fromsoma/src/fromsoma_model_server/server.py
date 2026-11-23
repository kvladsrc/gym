"""Model server for fromsoma: a realistic person from Anny's parameters and two pictures of a
bald head.

The character's Anny parameters (sex, age in years, build, origin mix, ...) make the body. The
front and the left profile of the bald head on a plain backdrop give the face: the reader
(reader.py), told the parameters as in its training, reads the face's MHR variation and the
pigment sliders. The person goes onto SOMA's mesh and skeleton (person.py) and is written as
glTF binary (glb.py): the body skinned to a root and SOMA's 77 public joints in SOMA's bind
pose, eyeballs at the eye joints, one texture painted from the pigments. The head's tile then
takes the pictures' own colour (facetex.py: both pictures projected onto the head through
cameras fitted to it, cameras.py), unless texture=pigments. Everything runs on the CPU: about
fifteen seconds a person with the pigments alone, a minute and a half with the pictures' colour.

The root directory (FROMSOMA_ROOT, default ~/.cache/assets-studio/fromsoma) holds model.pt (the
trained reader with the data it was trained with: MHR's head fields, the colour tables, Anny's
feature masks), SOMA-X's data (soma-x/, from Hugging Face) and MediaPipe's Face Landmarker.
"""

import logging
import os
import urllib.request
from pathlib import Path
from typing import Any, Literal

import numpy as np
from model_server_sdk.images import open_image
from pydantic import BaseModel, ConfigDict, Field, model_validator

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
    ui,
)

logger = logging.getLogger(__name__)

INPUT_MIME = ["image/png", "image/jpeg", "image/webp"]
MODEL_FILE = "model.pt"
SOMA_DIR = "soma-x"
SOMA_REPO, SOMA_REVISION = "nvidia/soma-x", "v0.3.3"  # the release py-soma-x 0.3.3 reads
SOMA_FILES = (
    "Anny/SOMA_wrap.obj",
    "Anny/base_body.obj",
    "LICENSE",
    "README.md",
    "SOMA_neutral.npz",
    "SOMA_procedural_transforms.json",
    "SOMA_template_rig.usda",
    "correctives_model.pt",
)
LANDMARKER_FILE = "face_landmarker.task"
LANDMARKER_URL = "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task"
BUILD = ("muscle", "weight", "height", "proportions")  # the reader's "free" phenotype values
ORIGIN = ("african", "asian", "caucasian")


def _anny(description: str, ru: str) -> Any:
    """One of Anny's phenotype values, 0..1, at Anny's own default."""
    return Field(default=0.5, ge=0.0, le=1.0, description=description, json_schema_extra=ui(ru=ru))


class Params(BaseModel):
    """The character's Anny parameters; the age in years (Anny's own age mapping)."""

    model_config = ConfigDict(extra="forbid")

    sex: Literal["female", "male"] = Field(
        default="female",
        description="The character's sex",
        json_schema_extra=ui(ru="Пол", primary=True),
    )
    age: float = Field(
        default=30.0,
        ge=3.0,
        le=90.0,
        description="The character's age, years",
        json_schema_extra=ui(ru="Возраст, лет", primary=True),
    )
    muscle: float = _anny("Anny's muscle", "Мышцы")
    weight: float = _anny("Anny's weight", "Вес")
    height: float = _anny("Anny's height", "Рост")
    proportions: float = _anny(
        "Anny's proportions: 0 ideal, 1 uncommon", "Пропорции: 0 идеальные, 1 необычные"
    )
    african: float = _anny(
        "Anny's origin mix, African (the three shares are normalised)",
        "Происхождение: африканское",
    )
    asian: float = _anny(
        "Anny's origin mix, Asian (the three shares are normalised)", "Происхождение: азиатское"
    )
    caucasian: float = _anny(
        "Anny's origin mix, Caucasian (the three shares are normalised)",
        "Происхождение: европеоидное",
    )
    cupsize: float = _anny("Anny's cup size", "Размер груди")
    firmness: float = _anny("Anny's breast firmness", "Упругость груди")
    texture: Literal["pictures", "pigments"] = Field(
        default="pictures",
        description=(
            "The head's colour: pictures, the pictures' own colour baked onto the face and blended"
            " into the pigment texture round it (about a minute more on the CPU); pigments, the"
            " pigment texture alone, no bake: for quick checks"
        ),
        json_schema_extra=ui(
            ru=(
                "Цвет головы: pictures — запечь из картинок (+1–1,5 мин),"
                " pigments — без запекания, для быстрых проверок"
            ),
            primary=True,
        ),
    )

    @model_validator(mode="after")
    def _some_origin(self) -> "Params":
        if self.african + self.asian + self.caucasian <= 0:
            raise ValueError("the origin mix needs a share above zero")
        return self

    def origin(self) -> dict[str, float]:
        total = self.african + self.asian + self.caucasian
        return {name: getattr(self, name) / total for name in ORIGIN}


TASK = TaskSpec(
    KnownTask.IMAGE_TO_3D,
    Params,
    ("model/gltf-binary",),
    prompt="none",
    inputs=(
        InputSpec(
            role="image",
            mime=INPUT_MIME,
            description="Front view of the character's bald head, on a plain backdrop",
            labels={"ru": "Лысая голова в фас на ровном фоне"},
        ),
        InputSpec(
            role="profile",
            mime=INPUT_MIME,
            description="Profile of the same bald head on a plain backdrop, the face turned to the"
            " picture's left (the other way is mirrored)",
            labels={"ru": "Та же голова в профиль, лицом влево (другой профиль отражается)"},
        ),
    ),
    max_count=1,
)


def root_directory() -> Path:
    return Path(os.environ.get("FROMSOMA_ROOT", "~/.cache/assets-studio/fromsoma")).expanduser()


def required_files(model_file: str = MODEL_FILE) -> list[str]:
    return [model_file, LANDMARKER_FILE] + [f"{SOMA_DIR}/{name}" for name in SOMA_FILES]


def missing_files(root: Path, model_file: str = MODEL_FILE) -> list[str]:
    return [name for name in required_files(model_file) if not (root / name).is_file()]


def download() -> None:
    """SOMA-X's data and MediaPipe's Face Landmarker; model.pt comes from the reader's training."""
    from huggingface_hub import snapshot_download

    root = root_directory()
    root.mkdir(parents=True, exist_ok=True)
    snapshot_download(
        repo_id=SOMA_REPO,
        revision=SOMA_REVISION,
        local_dir=root / SOMA_DIR,
        allow_patterns=list(SOMA_FILES),
    )
    target = root / LANDMARKER_FILE
    if not target.is_file():
        partial = target.with_suffix(".part")
        urllib.request.urlretrieve(LANDMARKER_URL, partial)
        partial.replace(target)
    missing = missing_files(root)
    print("missing:", ", ".join(missing) if missing else "nothing")


def describe(package: dict[str, Any]) -> dict[str, Any]:
    training = package.get("training", {})
    return {
        "run": training.get("run"),
        "step": training.get("step"),
        "exported": training.get("exported"),
    }


def face_texture(bodies: Any, root: Path) -> Any:
    """The pictures' colour for people of these bodies (facetex.FaceTexture)."""
    from fromsoma_model_server.facetex import FaceTexture

    return FaceTexture(
        bodies.tris,
        bodies.tris_uv,
        bodies.uv_vertex,
        bodies.segments,
        root / SOMA_DIR,
        root / LANDMARKER_FILE,
    )


def colour_head(
    texture: Any, person: Any, front: np.ndarray, left: np.ndarray, crops: dict[str, Any]
) -> tuple[Any, dict[str, Any]]:
    """The person with the pictures' own colour on the head and the texture's report; the
    profile flipped as the reader flipped it."""
    from fromsoma_model_server.cameras import CameraError
    from fromsoma_model_server.facetex import PicturesError

    hint = "; texture=pigments keeps the pigment texture"
    try:
        return texture.paint(person, front, left, mirrored=bool(crops["left"]["mirrored"]))
    except PicturesError as error:
        raise InvalidInput(f"{error}{hint}") from error
    except CameraError as error:
        raise GenerationError(f"{error}{hint}") from error


class FromSomaServer(ModelServer):
    model = ModelInfo(
        id="fromsoma",
        name="fromsoma face reader",
        license=(
            "reader weights: trained locally (README); SOMA-X, Anny, MHR, DINOv2 and MediaPipe"
            " Face Landmarker: Apache-2.0; MPFB2 assets: CC0"
        ),
        source="https://github.com/NVlabs/SOMA-X",
        details={
            "network": "DINOv2 ViT-S/14 on front and profile, an eye-strip CNN, known sex and age",
            "reads": "the face's 20 MHR coefficients and 14 pigment sliders, given the Anny values",
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
    model_file = MODEL_FILE  # the reader's package in the root directory, and its format
    model_format = "fromsoma.reader.v1"

    def __init__(self) -> None:
        self._reader: Any = None
        self._bodies: Any = None
        self._texture: Any = None
        self._package: dict[str, Any] = {}

    def face_meta(self, coefficients: list[float]) -> dict[str, Any]:
        """The face the reader read, for the output's metadata."""
        return {"mhr_coefficients": [round(v, 4) for v in coefficients]}

    def load(self) -> None:
        import torch

        from fromsoma_model_server.person import Bodies
        from fromsoma_model_server.reader import Reader

        torch.set_grad_enabled(False)
        torch.set_num_threads(max(1, min(8, (os.cpu_count() or 2) // 2)))
        root = root_directory()
        missing = missing_files(root, self.model_file)
        if missing:
            raise RuntimeError(
                f"missing in {root}: {', '.join(missing)}"
                f" (`just download`; {self.model_file} comes from the reader's training)"
            )
        package = torch.load(root / self.model_file, map_location="cpu", weights_only=False)
        if package.get("format") != self.model_format:
            raise RuntimeError(f"{root / self.model_file} is not a {self.model.id} reader package")
        config = package["config"]
        if tuple(config["free"]) != BUILD or tuple(config["origin"]) != ORIGIN:
            raise RuntimeError("the reader was trained with another build or origin layout")
        self._bodies = Bodies(package, root / SOMA_DIR)
        self._reader = Reader(package, root / LANDMARKER_FILE, self._bodies.to_anny_age)
        self._texture = face_texture(self._bodies, root)
        self._package = {k: package[k] for k in ("config", "training")}
        logger.info("%s reader %s loaded", self.model.id, describe(self._package))

    def generate(self, job: Job) -> list[Output]:
        from fromsoma_model_server.glb import build_glb
        from fromsoma_model_server.person import png
        from fromsoma_model_server.reader import PictureError, slider_values

        params = job.params
        assert isinstance(params, Params)
        if self._reader is None or self._bodies is None or self._texture is None:
            raise GenerationError("the model is not loaded", retryable=True)
        front = np.asarray(open_image(job.inputs["image"].data))
        left = np.asarray(open_image(job.inputs["profile"].data))
        build = {name: getattr(params, name) for name in BUILD}
        try:
            reading = self._reader.read(
                front, left, params.sex == "female", params.age, build, params.origin()
            )
        except PictureError as error:
            raise InvalidInput(str(error)) from error
        phenotype = {**reading.phenotype, "cupsize": params.cupsize, "firmness": params.firmness}
        pigments = slider_values(reading.sliders, self._package["config"]["sliders"])
        person = self._bodies.build(phenotype, reading.coefficients, pigments)
        texture: dict[str, Any] = {"source": "pigments"}
        if params.texture == "pictures":
            person, texture = colour_head(self._texture, person, front, left, reading.crops)
        data = build_glb(person.surfaces, person.skeleton, png(person.texture))
        meta = {
            "seed": job.seeds[0],
            "sex": params.sex,
            "age_years": params.age,
            "anny_phenotype": {k: round(v, 4) for k, v in phenotype.items()},
            **self.face_meta(reading.coefficients),
            "pigments": {k: round(v, 5) for k, v in pigments.items()},
            "crops_px": reading.crops,
            "reader": describe(self._package),
            "joints": len(person.skeleton.names),
            "texture": texture,
            **person.meta,
        }
        return [Output("model/gltf-binary", data, meta)]
