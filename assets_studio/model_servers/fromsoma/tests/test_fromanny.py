"""fromanny's declarations, its reader's output layout and the face modifiers' way into Anny; no
weights, data or GPU."""

from pathlib import Path
from typing import ClassVar

import pytest
import torch
from fromsoma_model_server.fromanny import FromAnnyServer, missing_files
from fromsoma_model_server.person import Bodies
from fromsoma_model_server.reader import Network, condition_size, output_size
from fromsoma_model_server.server import BUILD, ORIGIN, TASK, FromSomaServer

from model_server_sdk import create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(FromAnnyServer())
    (task,) = FromAnnyServer.tasks
    assert task is TASK
    assert FromAnnyServer.model.id == "fromanny"
    assert (FromAnnyServer.model_file, FromAnnyServer.model_format) == (
        "fromanny.pt",
        "fromanny.reader.v1",
    )


def test_missing_files_name_the_reader(tmp_path: Path) -> None:
    missing = missing_files(tmp_path)
    assert missing[0] == "fromanny.pt"
    assert "face_landmarker.task" in missing


def test_the_reader_reads_the_modifiers_after_the_phenotype() -> None:
    config = {
        "backbone": "vit_small_patch14_dinov2.lvd142m",
        "size": 70,
        "phenotype": ["gender", "age", *BUILD],
        "origin": list(ORIGIN),
        "free": list(BUILD),
        "age_bins": [5.0, 10.0, 15.0, 20.0, 30.0, 45.0, 60.0, 75.0],
        "coefficients": 95,
        "eye_strip": [0.3, 0.5, 0.2, 0.8],
        "eye_width": 8,
        "head": [16, 16],
        "sliders": [["s", 0.0, 1.0, "lin"]] * 14,
    }
    assert condition_size(config) == 19
    assert output_size(config) == 6 + 3 + 95 + 14
    network = Network(config).eval()
    with torch.no_grad():
        out = network(torch.rand(2, 2, 3, 70, 70), torch.zeros(2, 19))
    assert out.shape == (2, 118)


class _Anny:
    """Records the local changes Anny is given."""

    local_change_labels: ClassVar[list[str]] = ["l-eye-a", "r-eye-a", "nose-b", "neck-c"]

    def parse_phenotype_kwargs(self, phenotype: dict) -> dict:
        return dict(phenotype)

    def get_phenotype_blendshape_coefficients(
        self, local_changes: dict, **_: object
    ) -> torch.Tensor:
        self.local = {k: float(v[0]) for k, v in local_changes.items()}
        return torch.zeros(1, 2)

    def get_rest_vertices(self, coefficients: torch.Tensor) -> torch.Tensor:
        return torch.ones(1, 5, 3)


def _bodies() -> Bodies:
    bodies = object.__new__(Bodies)
    bodies.anny = _Anny()
    bodies.fields, bodies.modifiers = None, ["eye-a", "nose-b"]
    bodies.members = [["l-eye-a", "r-eye-a"], ["nose-b"]]
    return bodies


def test_a_modifier_drives_both_sides_within_annys_range() -> None:
    bodies = _bodies()
    rest = bodies.rest({"gender": 1.0, "age": 0.5}, [0.25, -1.7])
    assert rest.shape == (1, 5, 3)  # Anny's own shape, no fields added
    assert bodies.anny.local == {"l-eye-a": 0.25, "r-eye-a": 0.25, "nose-b": -1.0}
    with pytest.raises(ValueError, match=r"zip\(\) argument"):
        bodies.rest({"gender": 1.0}, [0.1])  # one value per modifier


def test_the_metadata_names_the_modifiers() -> None:
    reader = FromAnnyServer()
    reader._bodies = _bodies()
    assert reader.face_meta([0.123456, 1.3]) == {"anny_face": {"eye-a": 0.1235, "nose-b": 1.0}}
    assert FromSomaServer().face_meta([0.5, -0.25]) == {"mhr_coefficients": [0.5, -0.25]}
