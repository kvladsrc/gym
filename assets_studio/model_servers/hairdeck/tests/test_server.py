"""Declarations, the GLB and the per-seed stages with a fake pipeline; no GPU,
weights or source."""

import io
from contextlib import contextmanager
from pathlib import Path
from typing import Any

import numpy as np
import pytest
import trimesh
from hairdeck_model_server.server import (
    TASK,
    WEIGHTS_FILES,
    HairDeckServer,
    Params,
    Upstream,
    build_glb,
    describe,
    missing_files,
)
from PIL import Image
from pydantic import ValidationError

from model_server_sdk import GenerationError, InvalidInput, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(HairDeckServer())
    assert TASK.task == "image-to-3d"
    assert [spec.role for spec in TASK.inputs] == ["image"]
    assert TASK.output_mime == ("model/gltf-binary",)


def test_parameters_stay_in_upstream_ranges() -> None:
    assert Params().model_dump() == {
        "steps": 50,
        "guidance": 2.0,
        "colors": True,
        "head_proxy": False,
    }
    with pytest.raises(ValidationError):
        Params(guidance=5.0)
    with pytest.raises(ValidationError):
        Params(steps=5)


def test_missing_weights_are_listed(tmp_path: Path) -> None:
    assert WEIGHTS_FILES[0] in missing_files(tmp_path)
    for name in missing_files(tmp_path):
        (tmp_path / name).parent.mkdir(parents=True, exist_ok=True)
        (tmp_path / name).touch()
    assert missing_files(tmp_path) == []


def test_the_glb_has_the_cards_and_optionally_the_head() -> None:
    hair, head = trimesh.creation.box(), trimesh.creation.icosphere()
    alone = trimesh.load(io.BytesIO(build_glb(hair)), file_type="glb")
    both = trimesh.load(io.BytesIO(build_glb(hair, head)), file_type="glb")
    assert isinstance(alone, trimesh.Scene)
    assert isinstance(both, trimesh.Scene)
    assert set(alone.geometry) == {"hair_cards"}
    assert set(both.geometry) == {"hair_cards", "head_proxy"}


def test_metadata_counts_cards_and_names_the_frame() -> None:
    cards = [(np.zeros((4, 3)), 0), (np.zeros((2, 3)), 1)]
    meta = describe(trimesh.creation.box(), cards)
    assert (meta["cards"], meta["meshed_cards"], meta["triangles"]) == (2, 1, 12)
    assert (meta["up_axis"], meta["forward_axis"], meta["units"]) == ("Y", "+Z", "m")


class _Residency:
    enabled, reason = True, "test"

    @contextmanager
    def request(self) -> Any:
        yield


class _Pipeline:
    """The stages of HairDeck's Pipeline the server calls; a card per seed,
    none for seed 13."""

    def __init__(self, *, head_found: bool = True) -> None:
        self.residency = _Residency()
        self.head_found = head_found
        self.sampled: list[tuple[int, int, float]] = []

    def prep(self, rgb: Any, alpha: Any) -> tuple[dict[str, Any], Any]:
        box = (1.0, 2.0, 30.0, 40.0) if self.head_found else None
        prep = {"rgb": rgb, "hair": None, "status": "ok", "notes": [], "box": box}
        return prep, np.zeros((8, 8, 3), np.uint8) if self.head_found else None

    def matte_crop(self, prep: Any) -> Any:
        return np.ones((8, 8), bool)

    def color_context(self, crop: Any, matte: Any) -> str:
        return "context"

    def noise(self, seed: int) -> int:
        return seed

    def sample(self, crop: Any, x0: int, steps: int, cfg: float) -> tuple[list[Any], int]:
        self.sampled.append((x0, steps, cfg))
        return ([] if x0 == 13 else [(np.full((4, 3), float(x0)), 0)]), x0

    def card_colors(self, context: Any, tokens: int, n_cards: int) -> Any:
        return np.full((n_cards, 16, 3), 0.5)


def _hair_trimesh(cards: list[Any], colour: Any, card_srgb: Any = None) -> Any:
    box = trimesh.creation.box()
    box.apply_translation((float(cards[0][0][0, 0]), 0, 0))  # tells the seeds apart
    return box


def _server(pipeline: _Pipeline) -> HairDeckServer:
    server = HairDeckServer()
    server._pipeline = pipeline
    server._upstream = Upstream(
        load_image=lambda image: (np.asarray(image.convert("RGB")), None, None),
        hair_color=lambda rgb, hair: [0.4, 0.3, 0.25],
        hair_trimesh=_hair_trimesh,
        head_trimesh=trimesh.creation.icosphere,
    )
    return server


def _png() -> bytes:
    buffer = io.BytesIO()
    Image.new("RGB", (16, 16), "white").save(buffer, format="PNG")
    return buffer.getvalue()


def test_each_output_comes_from_its_own_seed() -> None:
    pipeline = _Pipeline()
    params = Params(steps=10, guidance=3.0)
    outputs = _server(pipeline)._generate(_png(), params, (7, 3))
    assert pipeline.sampled == [(7, 10, 3.0), (3, 10, 3.0)]
    centres = [
        trimesh.load(io.BytesIO(output.data), file_type="glb", force="mesh").bounds.mean(axis=0)[0]
        for output in outputs
    ]
    assert centres == pytest.approx([7.0, 3.0])
    assert outputs[0].meta["colors"] == "per card"
    assert outputs[0].meta["head_box"] == [1.0, 2.0, 30.0, 40.0]


def test_one_hair_colour_when_colours_are_off() -> None:
    (output,) = _server(_Pipeline())._generate(_png(), Params(colors=False), (1,))
    assert output.meta["colors"] == "hair colour"


def test_an_image_without_a_head_is_refused() -> None:
    with pytest.raises(InvalidInput, match="no head"):
        _server(_Pipeline(head_found=False))._generate(_png(), Params(), (1,))


def test_a_seed_without_cards_fails_the_job() -> None:
    with pytest.raises(GenerationError, match="seed 13"):
        _server(_Pipeline())._generate(_png(), Params(), (1, 13))
