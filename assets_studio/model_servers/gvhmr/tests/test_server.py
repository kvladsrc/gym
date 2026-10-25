import subprocess

import pytest
from gvhmr_model_server import server
from pydantic import ValidationError

from model_server_sdk import GenerationError


def test_params_reject_unknown_and_invalid_modes():
    for params in ({"camera": "auto"}, {"root_motion": "other"}, {"unknown": 1}):
        with pytest.raises(ValidationError):
            server.Params(**params)


@pytest.mark.parametrize("region", ["0,0,1.1,1", ".8,0,.2,1", "0,0,1", "0,nan,1,1"])
def test_person_exclusions_require_valid_normalized_rectangles(region):
    with pytest.raises(ValidationError):
        server.NativeParams(ignored_person_region=region)


def test_missing_checkpoints_prevent_ready(tmp_path, monkeypatch):
    monkeypatch.setenv("GVHMR_SOURCE", str(tmp_path))
    monkeypatch.setenv("GVHMR_PYTHON", str(tmp_path / "python"))
    (tmp_path / "tools/demo").mkdir(parents=True)
    (tmp_path / "tools/demo/demo.py").touch()
    (tmp_path / "python").touch()
    with pytest.raises(FileNotFoundError, match="SMPLX_NEUTRAL"):
        server.GVHMRServer().load()


def test_child_timeout_is_generation_error(monkeypatch):
    def timeout(*args, **kwargs):
        raise subprocess.TimeoutExpired(args[0], 1)

    monkeypatch.setattr(server.subprocess, "run", timeout)
    with pytest.raises(GenerationError, match="exceeded"):
        server.run(["worker"])


def test_child_oom_is_retryable(monkeypatch):
    monkeypatch.setattr(
        server.subprocess,
        "run",
        lambda *a, **kw: subprocess.CompletedProcess(a[0], 1, "", "CUDA out of memory"),
    )
    with pytest.raises(GenerationError) as caught:
        server.run(["worker"])
    assert caught.value.retryable


@pytest.mark.parametrize("native_parameters", [False, True])
@pytest.mark.parametrize("region", ["", "0,0,0.35,0.55"])
def test_native_motion_needs_no_rig_or_blender_transfer(monkeypatch, native_parameters, region):
    import json
    from pathlib import Path

    import imageio_ffmpeg
    from asset_processing.native_motion import read_native_motion, smpl_bvh

    from model_server_sdk import InputData, Job

    bvh = smpl_bvh(
        [[0.0, i * 0.1, 0.0] for i in range(22)],
        [[[0.0, 0.0, 0.0] for _ in range(22)] for _ in range(2)],
        [[0.0, 1.0, 0.0], [0.1, 1.0, 0.0]],
    )
    commands = []
    raw = {"format": "studio.smplx-parameters.v1", "body_pose": [[0.0] * 63] * 2}

    def execute(command, **kwargs):
        commands.append(command)
        if str(server.WORKER) in command:
            selection = json.loads((Path(command[3]) / "person_selection.json").read_text())
            assert selection["ignored_person_regions"] == ([[0, 0, 0.35, 0.55]] if region else [])
            (Path(command[3]) / "motion.bvh").write_text(bvh)
            if native_parameters:
                (Path(command[3]) / "smplx.json").write_text(json.dumps(raw))
        return ""

    monkeypatch.setattr(server, "run", execute)
    monkeypatch.setattr(
        imageio_ffmpeg,
        "read_frames",
        lambda _: (frame for frame in [{"size": (640, 480)}, b"a", b"b"]),
    )
    model = server.GVHMRServer()
    model._blender = "blender"
    result = model.generate(
        Job(
            "video-to-native-motion",
            None,
            {"video": InputData("video/mp4", b"video")},
            server.NativeParams(ignored_person_region=region),
            (0,),
        )
    )
    assert len(commands) == 2
    assert result[0].mime == "application/vnd.studio.smplx-motion+json"
    assert read_native_motion(result[0].data).frames == 2
    assert json.loads(result[0].data)["retarget_bvh"] == bvh
    if native_parameters:
        assert json.loads(result[0].data)["smplx"] == raw
        assert result[0].meta["has_smplx_parameters"] is True
    else:
        assert "smplx" not in json.loads(result[0].data)


@pytest.mark.parametrize("mime", ["model/x-fbx", "model/gltf-binary"])
@pytest.mark.parametrize("camera_reference", [False, True])
def test_generate_routes_video_and_rig(monkeypatch, mime, camera_reference):
    import imageio_ffmpeg

    from model_server_sdk import InputData, Job

    commands = []

    def execute(command, **kwargs):
        commands.append((command, kwargs))
        if str(server.RETARGET_SCRIPT) in command:
            from pathlib import Path

            Path(command[command.index("--") + 3]).write_bytes(b"animated-fbx")
        elif any("convert_rig.py" in arg for arg in command):
            from pathlib import Path

            Path(command[command.index("--") + 2]).write_bytes(b"converted-rig")
        return ""

    monkeypatch.setattr(server, "run", execute)
    monkeypatch.setattr(
        imageio_ffmpeg,
        "read_frames",
        lambda _: (frame for frame in [{"size": (640, 480)}, b"frame", b"frame"]),
    )
    model = server.GVHMRServer()
    model._blender = "blender"
    inputs = {"video": InputData("video/mp4", b"video"), "rig": InputData(mime, b"rig")}
    if camera_reference:
        inputs["camera_video"] = InputData("video/mp4", b"original-scene")
    job = Job(
        "video-to-motion",
        None,
        inputs,
        server.Params(camera="moving", root_motion="world"),
        (42,),
    )
    result = model.generate(job)
    assert result[0].data == b"animated-fbx"
    assert result[0].meta == {"frames": 2, "fps": 30, "camera": "moving", "root_motion": "world"}
    worker, options = next((cmd, kw) for cmd, kw in commands if str(server.WORKER) in cmd)
    if camera_reference:
        assert worker[-1].endswith("camera.mp4")
        assert worker[-2] == "moving"
    else:
        assert worker[-1] == "moving"
    assert options["cwd"] == server.source_directory()
    assert commands[-1][0][-2:] == ["world", "smpl"]
    assert len(commands) == (4 if mime == "model/gltf-binary" else 3) + int(camera_reference)


@pytest.mark.parametrize("mismatch", ["frames", "size"])
def test_camera_reference_requires_matching_frames_and_dimensions(monkeypatch, mismatch):
    import imageio_ffmpeg

    from model_server_sdk import InputData, InvalidInput, Job

    monkeypatch.setattr(server, "run", lambda *a, **kw: "")

    def frames(path):
        is_reference = path.endswith("camera.mp4")
        size = (320, 240) if is_reference and mismatch == "size" else (640, 480)
        count = 3 if is_reference and mismatch == "frames" else 2
        yield {"size": size}
        yield from [b"frame"] * count

    monkeypatch.setattr(imageio_ffmpeg, "read_frames", frames)
    model = server.GVHMRServer()
    model._blender = "blender"
    job = Job(
        "video-to-motion",
        None,
        {
            "video": InputData("video/mp4", b"pose"),
            "camera_video": InputData("video/mp4", b"scene"),
        },
        server.Params(camera="moving"),
        (42,),
    )
    with pytest.raises(InvalidInput, match="same frame count and dimensions"):
        model.generate(job)


def test_long_video_is_rejected_before_inference(monkeypatch):
    import imageio_ffmpeg

    from model_server_sdk import InputData, InvalidInput, Job

    commands = []
    monkeypatch.setattr(server, "run", lambda command: commands.append(command))
    monkeypatch.setattr(
        imageio_ffmpeg, "read_frames", lambda _: (frame for frame in [{}, *([b"frame"] * 301)])
    )
    model = server.GVHMRServer()
    model._blender = "blender"
    job = Job(
        "video-to-motion", None, {"video": InputData("video/mp4", b"video")}, server.Params(), (42,)
    )
    with pytest.raises(InvalidInput, match="300 frames"):
        model.generate(job)
    assert len(commands) == 1
    assert commands[0][commands[0].index("-frames:v") + 1] == "301"


@pytest.mark.parametrize("region", ["0,0,1.1,1", ".8,0,.2,1", "0,0,1", "0,nan,1,1"])
def test_observation_task_checks_person_exclusions_alike(region):
    with pytest.raises(ValidationError):
        server.ObservationParams(ignored_person_region=region)


def test_observation_task_is_declared_with_video_only():
    from model_server_sdk import create_app

    create_app(server.GVHMRServer())
    task = server.OBSERVATIONS_TASK
    assert task.task == "video-to-pose-observations"
    assert task.output_mime == ("text/plain",)
    assert [spec.role for spec in task.inputs] == ["video"]
    assert (
        server.ObservationParams(ignored_person_region=" 0,0,.35,.55 ").ignored_person_region
        == "0,0,.35,.55"
    )
    with pytest.raises(ValidationError):
        server.ObservationParams(camera="static")
