"""Wire models of the model-server contract, version 1.

This module is the reference definition of the JSON exchanged between the
studio and model servers (see docs/adr/0001-model-server-contract.md). It
depends only on pydantic so that the studio can import it without pulling
in the server runtime.

Requests forbid unknown fields so that typos fail loudly. Responses ignore
unknown fields so that a client keeps working when a server adds optional
fields.
"""

from enum import StrEnum
from typing import Annotated, Any, Literal, cast

from pydantic import BaseModel, ConfigDict, Field

CONTRACT_VERSION = 1
SEED_MODULUS = 2**32

Slug = Annotated[str, Field(pattern=r"^[a-z0-9]+(?:[-_][a-z0-9]+)*$", max_length=64)]
MimeType = Annotated[str, Field(pattern=r"^[a-z]+/[a-z0-9.+-]+$", max_length=127)]
MimePattern = Annotated[str, Field(pattern=r"^[a-z]+/(?:\*|[a-z0-9.+-]+)$", max_length=127)]
Seed = Annotated[int, Field(ge=0, lt=SEED_MODULUS)]

ServerStatus = Literal["loading", "ready", "busy", "error"]
PromptUsage = Literal["required", "optional", "none"]
ErrorCode = Literal[
    "busy", "not_ready", "unsupported_task", "invalid_request", "generation_failed", "internal"
]


class KnownTask(StrEnum):
    """Tasks the studio has UI templates for. Servers may declare others."""

    TEXT_TO_IMAGE = "text-to-image"
    IMAGE_TO_IMAGE = "image-to-image"
    IMAGE_TO_3D = "image-to-3d"
    TEXT_TO_SPEECH = "text-to-speech"
    TEXT_TO_AUDIO = "text-to-audio"
    AUDIO_TO_AUDIO = "audio-to-audio"
    TEXT_TO_TEXT = "text-to-text"
    IMAGE_TO_VIDEO = "image-to-video"
    TEXT_TO_3D = "text-to-3d"
    # The prompt is the lyrics; the style is a parameter.
    TEXT_TO_SONG = "text-to-song"
    # A character mesh (GLB) to a rigged one: skeleton and skin weights (FBX).
    RIG_3D = "3d-to-rig"
    # A mesh and the image it was made from to the mesh with a texture.
    PAINT_3D = "3d-paint"


class _Request(BaseModel):
    model_config = ConfigDict(extra="forbid")


class _Response(BaseModel):
    model_config = ConfigDict(extra="ignore")


class ModelInfo(_Response):
    id: Slug
    name: str
    revision: str | None = None
    license: str | None = None
    source: str | None = None


class InputSpec(_Response):
    role: Slug
    mime: list[MimePattern] = Field(min_length=1)
    required: bool = True
    description: str | None = None
    # The description in other languages, for the studio's interface (ADR-005).
    labels: dict[str, str] | None = None


class TaskInfo(_Response):
    task: Slug
    prompt: PromptUsage
    inputs: list[InputSpec]
    params_schema: dict[str, Any]
    max_count: int = Field(ge=1)
    output_mime: list[MimePattern] = Field(min_length=1)


class Info(_Response):
    contract: int
    status: ServerStatus
    status_message: str | None = None
    model: ModelInfo
    tasks: list[TaskInfo]


class InputFile(_Request):
    role: Slug
    mime: MimeType
    data_b64: str


class GenerateRequest(_Request):
    task: Slug
    prompt: str | None = None
    inputs: list[InputFile] = Field(default_factory=list[InputFile])
    params: dict[str, Any] = Field(default_factory=dict[str, Any])
    count: int = Field(default=1, ge=1)
    seed: Seed | None = None


class OutputFile(_Response):
    mime: MimeType
    data_b64: str
    meta: dict[str, Any]


class Timing(_Response):
    generate_s: float = Field(ge=0)


class GenerateResponse(_Response):
    model: ModelInfo
    task: Slug
    seed: Seed
    # Effective parameters: the request's values with defaults filled in.
    params: dict[str, Any]
    outputs: list[OutputFile]
    timing: Timing


class ErrorDetail(_Response):
    code: ErrorCode
    message: str
    retryable: bool


class ErrorResponse(_Response):
    error: ErrorDetail


# Every input that accepts a kind of media must accept its canonical format;
# the studio converts other formats (e.g. an uploaded JPEG) before sending.
CANONICAL_MIME = {
    "image": "image/png",
    "audio": "audio/wav",
    "model": "model/gltf-binary",
    "text": "text/plain",
    "video": "video/mp4",
}

# Marks a parameter the studio shows up front rather than under "advanced"
# (ADR-003): ``Field(..., json_schema_extra=PRIMARY)``. See also ui().
PRIMARY: dict[str, Any] = {"x-primary": True}


def ui(*, primary: bool = False, **labels: str) -> dict[str, Any]:
    """``json_schema_extra`` for a parameter: its label in other languages
    for the studio's interface (ADR-005), the ``description`` being the
    English one, and whether it is shown up front (ADR-003).

    ``Field(default=40, description="Steps", json_schema_extra=ui(ru="Шаги"))``
    """
    extra: dict[str, Any] = {"x-labels": dict(labels)} if labels else {}
    if primary:
        extra.update(PRIMARY)
    return extra


_SCALAR_TYPES = {"string", "number", "integer", "boolean"}
_COMPOSITE_KEYWORDS = ("$ref", "anyOf", "allOf", "oneOf", "items", "properties")


def _is_labels(value: object) -> bool:
    """Whether ``value`` maps language codes to texts (ADR-005)."""
    if not isinstance(value, dict):
        return False
    items = cast(dict[object, object], value).items()
    return all(isinstance(key, str) and isinstance(text, str) for key, text in items)


def params_schema_problems(schema: dict[str, Any]) -> list[str]:
    """Violations of the parameter rules; empty when the schema is valid.

    Parameters form a flat object of scalars or enumerations, each with a
    default, so that the studio can render a form and send an empty object.
    """
    problems: list[str] = []
    if "$defs" in schema:
        problems.append("nested definitions ($defs) are not allowed; use Literal for choices")
    properties: dict[str, dict[str, Any]] = schema.get("properties", {})
    for name, prop in properties.items():
        if "default" not in prop:
            problems.append(f"{name}: no default")
        composite = [keyword for keyword in _COMPOSITE_KEYWORDS if keyword in prop]
        if composite:
            problems.append(f"{name}: not a flat scalar ({', '.join(composite)})")
        elif prop.get("type") not in _SCALAR_TYPES and "enum" not in prop:
            problems.append(f"{name}: type {prop.get('type')!r} is not a scalar")
        if "x-labels" in prop and not _is_labels(prop["x-labels"]):
            problems.append(f"{name}: x-labels must map language codes to strings")
    return problems


def input_spec_problems(spec: InputSpec) -> list[str]:
    kinds = {pattern.partition("/")[0] for pattern in spec.mime}
    return [
        f"input {spec.role}: must accept {CANONICAL_MIME[kind]}"
        for kind in sorted(kinds & CANONICAL_MIME.keys())
        if not any(mime_matches(CANONICAL_MIME[kind], pattern) for pattern in spec.mime)
    ]


def mime_matches(mime: str, pattern: str) -> bool:
    """Match a concrete MIME type against a pattern such as ``image/*``."""
    kind, _, subtype = pattern.partition("/")
    mime_kind, _, mime_subtype = mime.partition("/")
    return kind == mime_kind and subtype in ("*", mime_subtype)


def output_seed(seed: int, index: int) -> int:
    """Seed of the ``index``-th output of a request made with ``seed``."""
    return (seed + index) % SEED_MODULUS
