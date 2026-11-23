"""frommhr's declarations, the stubble rule, its reader network without known inputs, the body's
least squares, the neck's blend and the feature masks' lookup; no weights, data or GPU."""

from pathlib import Path

import numpy as np
import pytest
import torch
from fromsoma_model_server import mhr
from fromsoma_model_server.frommhr import FromMhrServer, missing_files, stubble_for
from fromsoma_model_server.glb import Skeleton
from fromsoma_model_server.mhr import MhrBodies, head_vertices, neck_blend, rigid_residual
from fromsoma_model_server.reader import Network, condition_size, output_size
from fromsoma_model_server.server import TASK, Params

from model_server_sdk import create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(FromMhrServer())
    (task,) = FromMhrServer.tasks
    assert task.task == "image-to-3d"
    assert [spec.role for spec in task.inputs] == ["image", "profile"]
    assert task.output_mime == ("model/gltf-binary",)
    assert FromMhrServer.model.id == "frommhr"


def test_the_parameters_are_fromsomas_children_included() -> None:
    (task,) = FromMhrServer.tasks
    assert task is TASK
    assert task.params is Params
    assert Params(sex="female", age=12.0).age == 12.0


def test_stubble_only_for_men_from_eighteen() -> None:
    read = {"stubble": 0.6, "lips": 0.3}
    assert stubble_for(read, female=False, years=40.0) == read
    assert stubble_for(read, female=False, years=18.0)["stubble"] == 0.6
    assert stubble_for(read, female=False, years=15.0) == {"stubble": 0.0, "lips": 0.3}
    assert stubble_for(read, female=True, years=40.0)["stubble"] == 0.0
    assert read["stubble"] == 0.6  # the reading itself is left alone


def test_missing_files_name_the_reader(tmp_path: Path) -> None:
    missing = missing_files(tmp_path)
    assert missing[0] == "frommhr.pt"
    assert "face_landmarker.task" in missing


def test_a_reader_without_known_inputs() -> None:
    config = {
        "backbone": "vit_small_patch14_dinov2.lvd142m",
        "size": 70,
        "known": False,
        "phenotype": [],
        "origin": [],
        "free": [],
        "age_bins": [],
        "coefficients": 20,
        "eye_strip": [0.3, 0.5, 0.2, 0.8],
        "eye_width": 8,
        "head": [16, 16],
        "sliders": [["s", 0.0, 1.0, "lin"]] * 14,
    }
    assert condition_size(config) == 0
    assert output_size(config) == 34
    network = Network(config).eval()
    with torch.no_grad():
        out = network(torch.rand(2, 2, 3, 70, 70), torch.zeros(2, 0))
    assert out.shape == (2, 34)


def _bodies(nb: int = 2, ns: int = 1, vertices: int = 40) -> MhrBodies:
    bodies = object.__new__(MhrBodies)
    bodies.nb, bodies.ns = nb, ns
    g = torch.Generator().manual_seed(0)
    use = torch.ones(vertices, dtype=torch.bool)
    use[:5] = False  # say, the head
    bodies._prepare_fit(
        {
            "use": use,
            "mean_bind": torch.randn(vertices, 3, generator=g),
            "body_bind": torch.randn(nb, vertices, 3, generator=g),
            "scale_bind": torch.randn(ns, vertices, 3, generator=g),
            "prior_body": 0.0,
            "prior_scales": 0.0,
        }
    )
    return bodies


def test_the_body_fit_recovers_coefficients_scales_and_shift() -> None:
    bodies = _bodies()
    b, s, shift = torch.tensor([0.7, -1.2]), torch.tensor([0.4]), torch.tensor([0.01, -0.02, 0.03])
    directions = bodies.fit_matrix[:, :3].T.reshape(3, -1, 3)  # the used vertices only
    target = bodies.mean_bind.clone()
    target[bodies.use] += torch.einsum("j,jvc->vc", torch.cat([b, s]).double(), directions)
    target[bodies.use] += shift.double()
    target[~bodies.use] += 5.0  # left out of the fit
    got_b, got_s, report = bodies.solve(target)
    assert torch.allclose(got_b, b, atol=1e-5)
    assert torch.allclose(got_s, s, atol=1e-5)
    assert report["residual_mm"]["rms"] < 1e-3


def test_the_head_is_head_and_the_joints_below_it() -> None:
    names = ["Root", "Hips", "Neck", "Head", "Jaw", "JawEnd", "LeftEye", "Arm"]
    parents = [-1, 0, 1, 2, 3, 4, 3, 1]
    skeleton = Skeleton(names, parents, np.tile(np.eye(4), (len(names), 1, 1)))
    weights = np.zeros((5, len(names)), dtype=np.float32)
    weights[0, 3] = 1.0  # the skull
    weights[1, [4, 2]] = 0.6, 0.4  # the chin
    weights[2, [2, 3]] = 0.6, 0.4  # the neck
    weights[3, 7] = 1.0  # an arm
    weights[4, [5, 6, 2]] = 0.3, 0.3, 0.4  # the jaw's child and an eye together
    assert head_vertices(weights, skeleton).tolist() == [True, True, False, False, True]


def test_the_neck_blend_fades_in_from_the_head() -> None:
    heights = np.array([0.02, 0.0, -0.01, -0.03, -0.06, -0.1])
    vertices = np.stack([np.zeros_like(heights), heights, np.zeros_like(heights)], 1)
    head = heights >= 0.0
    blend = neck_blend(vertices, head, fade=0.06)
    assert blend[:2].tolist() == [0.0, 0.0]
    assert blend[2] == pytest.approx((1 / 6) ** 2 * (3 - 2 / 6))
    assert blend[3] == pytest.approx(0.5)
    assert blend[4:].tolist() == [1.0, 1.0]
    assert np.all(np.diff(blend) >= 0)


def test_the_head_shape_ignores_a_rigid_move_but_not_a_change() -> None:
    points = np.random.default_rng(0).normal(size=(50, 3)) * 0.1
    angle = np.radians(1.5)
    turn = np.array(
        [[1, 0, 0], [0, np.cos(angle), -np.sin(angle)], [0, np.sin(angle), np.cos(angle)]]
    )
    moved = points @ turn.T + [0.0, 0.01, -0.002]
    assert rigid_residual(moved, points).max() < 1e-9
    moved[7] += [0.0, 0.0, 0.01]  # one point out of shape
    residual = rigid_residual(moved, points)
    assert residual.argmax() == 7
    assert residual[7] > 0.009


def test_masks_come_from_the_first_quarter_texel_for_texel(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(mhr, "TEXTURE", 8)
    bodies = object.__new__(MhrBodies)
    bodies.neutral = {"brows": "brows_thin"}
    names = ["lips", "eyelids", "ears", "brows_thin", "brows_medium", "brows_thick"]
    bodies.mask_index = {name: i for i, name in enumerate(names)}
    masks = torch.zeros(6, 4, 4, dtype=torch.uint8)
    masks[0, 1, 2] = 255  # lips at row 1, column 2
    masks[3, 3, 0] = 51  # thin brows at row 3, column 0
    bodies.masks = masks
    bodies.segments = {"mouth_bag": torch.tensor([0]), "eye_bags": torch.tensor([1])}
    drawn = torch.zeros(8, 8, dtype=torch.bool)
    for row, col in ((1, 2), (3, 0), (1, 6), (6, 2)):  # two in the first quarter, two outside
        drawn[row, col] = True
    colours = {"skin": torch.full((3, 3), 0.5)}
    albedo, got = bodies._texels(
        colours, torch.zeros(4, dtype=torch.long), lambda v: v[:1].expand(4, 3), drawn
    )
    assert albedo.shape == (4, 3)
    # drawn texels in row-major order: (1, 2), (1, 6), (3, 0), (6, 2)
    assert got[:, 0].tolist() == [1.0, 0.0, 0.0, 0.0]
    assert got[:, 3].tolist() == pytest.approx([0.0, 0.0, 0.2, 0.0])
    assert got[:, 1:3].abs().sum() == 0
