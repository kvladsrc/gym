"""Already matted videos must reach DINO without a second segmentation."""

from pathlib import Path

import numpy as np
import pytest
import yaml
from mocapanything_model_server.inference_adapter import prepare_white_image
from mocapanything_model_server.pipeline import Job, Models
from PIL import Image


def test_white_preparation_preserves_foreground_pixels_and_padding(tmp_path):
    pixels = np.full((100, 120, 3), 254, dtype=np.uint8)
    pixels[20:60, 30:50] = [120, 40, 25]
    # Bright detail inside the body must survive without a second matte.
    pixels[35, 40] = [255, 255, 255]
    path = tmp_path / "frame.png"
    Image.fromarray(pixels).save(path)
    result = prepare_white_image(str(path), np.ones(3), rmbg_net=object())
    assert result.size == (48, 48)
    assert result.getpixel((0, 0)) == (255, 255, 255)
    assert result.getpixel((14, 4)) == (120, 40, 25)
    assert result.getpixel((24, 19)) == (255, 255, 255)


def test_empty_white_frame_is_not_a_valid_foreground(tmp_path):
    path = tmp_path / "empty.png"
    Image.new("RGB", (50, 50), "white").save(path)
    with pytest.raises(ValueError, match="no foreground"):
        prepare_white_image(str(path), np.ones(3))


@pytest.mark.parametrize("removed", [False, True])
def test_input_mode_selects_inference_entry_without_modifying_upstream(
    tmp_path, monkeypatch, removed
):
    source, work = tmp_path / "source", tmp_path / "job"
    (source / "examples/custom_rig").mkdir(parents=True)
    work.mkdir()
    template = source / "examples/custom_rig/inference.yaml"
    template.write_text(yaml.safe_dump({"weights": {}, "data": {}, "output": {}}))
    model = Models(Path("/w"), Path("/r"), Path("/d"), Path("/t"), "model.pt")
    job = Job(work, source, "blender", model, 60)
    commands = []

    def execute(stage, command):
        commands.append((stage, command))
        output = work / "out/clip_rot6d_pred.bvh"
        output.parent.mkdir()
        output.write_bytes(b"motion")

    monkeypatch.setattr(job, "run", execute)
    assert job.infer(background_removed=removed).read_bytes() == b"motion"
    command = commands[0][1]
    assert command[-2:] == ["--config", str(work / "inference.yaml")]
    assert any("inference_adapter.py" in arg for arg in command) == removed
    assert ("-m" in command) != removed
    assert (
        yaml.safe_load((work / "inference.yaml").read_text())["data"]["background_removed"]
        == removed
    )
    assert yaml.safe_load(template.read_text()) == {"weights": {}, "data": {}, "output": {}}
