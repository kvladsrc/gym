"""The safetensors package: what goes in comes out, with no pickle."""

from pathlib import Path

import numpy as np
import pytest
import torch
from fromsoma_model_server.package import load, metadata, save


def test_a_package_comes_back_as_saved(tmp_path: Path) -> None:
    package = {
        "format": "test.v1",
        "weights": {"head.0.weight": torch.randn(3, 4), "head.0.bias": torch.zeros(3)},
        "config": {"size": 518, "sliders": [["melanin", 0.002, 0.45, "log"]], "known": False},
        "masks": torch.randint(0, 255, (2, 4, 4), dtype=torch.uint8),
        "use": torch.tensor([True, False]),
        "rim": [torch.arange(3), torch.arange(2)],
        "prior": np.float32(0.01),
        "table": np.ones((2, 2), dtype=np.float32),
        "init": None,
    }
    path = tmp_path / "p.safetensors"
    save(path, package)
    back = load(path)
    assert back["format"] == "test.v1"
    assert torch.equal(back["weights"]["head.0.weight"], package["weights"]["head.0.weight"])
    assert back["masks"].dtype == torch.uint8
    assert torch.equal(back["masks"], package["masks"])
    assert back["use"].dtype == torch.bool
    assert [r.tolist() for r in back["rim"]] == [[0, 1, 2], [0, 1]]
    assert back["config"] == package["config"]
    assert back["prior"] == pytest.approx(0.01)
    assert torch.equal(back["table"], torch.ones(2, 2))
    assert back["init"] is None
    assert metadata(path)["weights"]["head.0.bias"] == {"$tensor": "weights.head.0.bias"}


def test_unsupported_values_are_refused(tmp_path: Path) -> None:
    with pytest.raises(TypeError):
        save(tmp_path / "p.safetensors", {"format": "x", "keys": {1: torch.zeros(1)}})
    with pytest.raises(TypeError):
        save(tmp_path / "p.safetensors", {"format": "x", "thing": object()})
