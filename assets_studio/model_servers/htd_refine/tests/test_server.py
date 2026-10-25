import copy
import hashlib
import json
import subprocess
from pathlib import Path

import pytest
from asset_processing.native_motion import FORMAT, smpl_bvh
from htd_refine_model_server import server

from model_server_sdk import GenerationError, InputData, InvalidInput, Job


def package():
    n = 4
    params = {
        "global_orient": [[0.0] * 3] * n,
        "body_pose": [[0.0] * 63] * n,
        "betas": [[0.0] * 10] * n,
        "transl": [[0.0] * 3] * n,
    }
    return dict(
        format=FORMAT,
        coordinate_space="model-world-y-up",
        frames=n,
        fps=30,
        retarget_bvh=smpl_bvh(
            [[0.0, i * 0.1, 0.0] for i in range(22)], [[[0.0] * 3] * 22] * n, [[0.0, 1.0, 0.0]] * n
        ),
        source={"camera": "static"},
        smplx=dict(
            format="studio.smplx-parameters.v1",
            fps=30,
            smpl_params_global=copy.deepcopy(params),
            smpl_params_incam=params,
            K_fullimg=[[[500.0, 0.0, 320.0], [0.0, 500.0, 240.0], [0.0, 0.0, 1.0]]] * n,
            bbx_xyxy=[[0.0, 0.0, 100.0, 200.0]] * n,
        ),
    )


@pytest.mark.parametrize(
    "mutation",
    [
        lambda p: p.pop("smplx"),
        lambda p: p["source"].update(camera="moving"),
        lambda p: p["smplx"]["smpl_params_global"].update(body_pose=[[0.0] * 63]),
        lambda p: p["smplx"]["K_fullimg"][0][0].__setitem__(0, float("nan")),
        lambda p: p["smplx"]["bbx_xyxy"][0].__setitem__(2, -1.0),
    ],
)
def test_rejects_incompatible_raw_capture(mutation):
    p = package()
    mutation(p)
    with pytest.raises(InvalidInput):
        server.validate_package(json.dumps(p).encode())


def test_video_fingerprint_rejects_wrong_source_before_gpu(monkeypatch):
    p = package()
    p["source"]["video_sha256"] = hashlib.sha256(b"original").hexdigest()
    with pytest.raises(InvalidInput, match="Video differs"):
        server.HTDRefineServer().generate(
            Job(
                "refine-native-motion",
                None,
                {
                    "motion": InputData(
                        "application/vnd.studio.smplx-motion+json", json.dumps(p).encode()
                    ),
                    "video": InputData("video/mp4", b"wrong"),
                },
                server.Params(),
                (0,),
            )
        )


def test_refinement_routes_exact_inputs_and_preserves_clock(monkeypatch):
    p = package()
    commands = []

    def execute(command, **kwargs):
        commands.append(command)
        if "export" in command:
            work = Path(command[-1])
            out = json.loads((work / "input.json").read_text())
            out.pop("smplx")
            (work / "output.json").write_text(json.dumps(out))
            (work / "report.json").write_text("{}")

    monkeypatch.setattr(server, "run", execute)
    monkeypatch.setattr(
        server.imageio_ffmpeg,
        "read_frames",
        lambda _: (v for v in [{"fps": 30}, b"x", b"x", b"x", b"x"]),
    )
    out = server.HTDRefineServer().generate(
        Job(
            "refine-native-motion",
            None,
            {
                "motion": InputData(
                    "application/vnd.studio.smplx-motion+json", json.dumps(p).encode()
                ),
                "video": InputData("video/mp4", b"video"),
            },
            server.Params(),
            (0,),
        )
    )[0]
    assert [c[2] for c in commands if str(server.WORKER) in c] == ["prepare", "pva", "export"]
    assert len(commands) == 4
    assert out.mime == "application/vnd.studio.smplx-motion+json"
    assert out.meta["frames"] == 4
    assert out.meta["video_identity_verified"] is False


def test_child_oom_is_retryable(monkeypatch, tmp_path):
    def fail(*args, **kwargs):
        raise subprocess.CalledProcessError(1, args[0], stderr="RuntimeError: CUDA out of memory")

    monkeypatch.setattr(server.subprocess, "run", fail)
    with pytest.raises(GenerationError) as caught:
        server.run(["worker"], cwd=tmp_path, env={})
    assert caught.value.retryable
