import importlib.util
from pathlib import Path
from types import SimpleNamespace

import pytest
from gvhmr_model_server.server import WORKER


@pytest.fixture
def adapter():
    spec = importlib.util.spec_from_file_location(
        "gvhmr_camera_reference", WORKER.with_name("camera_reference.py")
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


@pytest.mark.parametrize("reference", [None, Path("original.mp4")])
def test_only_camera_estimation_uses_reference_and_factory_is_restored(adapter, reference):
    calls = []
    cfg = SimpleNamespace(video_path="segmented.mp4")

    def factory(video, **kwargs):
        calls.append((video, kwargs))
        return "camera-estimator"

    def process(config):
        assert config is cfg
        assert config.video_path == "segmented.mp4"
        assert demo.SimpleVO(config.video_path, step=8) == "camera-estimator"

    demo = SimpleNamespace(SimpleVO=factory, run_preprocess=process)
    adapter.preprocess(cfg, demo, reference)
    assert calls == [(str(reference) if reference else "segmented.mp4", {"step": 8})]
    assert demo.SimpleVO is factory


@pytest.mark.parametrize("missing_pose", [False, True])
def test_camera_failure_is_actionable_without_changing_other_errors(adapter, missing_pose):
    original_error = (
        AttributeError("'NoneType' object has no attribute 'matrix'")
        if missing_pose
        else AttributeError("unrelated")
    )

    def process(config):
        raise original_error

    factory = object()
    demo = SimpleNamespace(SimpleVO=factory, run_preprocess=process)
    with pytest.raises(RuntimeError if missing_pose else AttributeError) as caught:
        adapter.preprocess(object(), demo, Path("original.mp4"))
    assert demo.SimpleVO is factory
    if missing_pose:
        assert "Cannot estimate camera motion" in str(caught.value)
        assert "camera_video" in str(caught.value)
        assert caught.value.__cause__ is original_error
    else:
        assert caught.value is original_error
