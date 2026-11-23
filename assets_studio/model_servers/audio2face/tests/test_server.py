"""Declarations, scene reader, audio, runner output and glTF; no GPU, weights or SDK."""

import io
import json
import struct
import typing
from pathlib import Path

import numpy as np
import pytest
import soundfile
from audio2face_model_server import glb
from audio2face_model_server.engine import shapes
from audio2face_model_server.heads import (
    Eye,
    Head,
    Part,
    euler_xyz,
    head_from_scene,
    read_head,
    read_scene,
)
from audio2face_model_server.server import (
    AUDIO_TO_MOTION,
    EMOTIONS,
    MODELS,
    SAMPLE_RATE,
    TASK,
    Audio2FaceServer,
    ModelName,
    Params,
    read_solved,
    read_speech,
)
from pydantic import ValidationError

from model_server_sdk import InvalidInput, create_app


def test_server_declarations_pass_the_sdk_validation() -> None:
    create_app(Audio2FaceServer())
    assert TASK.task == AUDIO_TO_MOTION == "audio-to-motion"
    assert [spec.role for spec in TASK.inputs] == ["audio"]
    assert TASK.output_mime == ("model/gltf-binary",)
    assert TASK.prompt == "none"
    assert TASK.max_count == 1


def test_every_model_name_is_offered_and_emotions_default_to_none() -> None:
    assert set(typing.get_args(ModelName)) == set(MODELS)
    params = Params()
    assert params.model == "v3.0-claire"
    assert [getattr(params, name) for name in EMOTIONS] == [0.0] * 10
    with pytest.raises(ValidationError):
        Params(joy=1.5)
    with pytest.raises(ValidationError):
        Params(model="v4-claire")  # type: ignore[arg-type]


def test_diffusion_actors_share_one_network_and_regression_ones_have_their_own() -> None:
    diffusion = [model for model in MODELS.values() if model.kind == "diffusion"]
    assert len({model.repository for model in diffusion}) == 1
    assert sorted(model.identity for model in diffusion) == [0, 1, 2]  # type: ignore[type-var]
    assert MODELS["v3.0-james"].blendshapes("skin").name == "bs_skin_James.npz"
    assert MODELS["v2.3-mark"].blendshapes("tongue").name == "bs_tongue.npz"


# A quad lifted by its transform, one vertex tweaked, the second face given
# with a reversed edge; a jaw group whose saved animation (opm) is ignored.
SCENE = """//Maya ASCII 2024 scene
requires maya "2024";
currentUnit -l centimeter -a degree -t film;
createNode transform -n "root";
\trename -uid "1";
\tsetAttr ".t" -type "double3" 0 10 0 ;
createNode transform -n "c_bottomDenture_grp_hi" -p "root";
\tsetAttr ".opm" -type "matrix" 1 0 0 0 0 1 0 0 0 0 1 0 5 5 5 1;
createNode transform -n "quad" -p "c_bottomDenture_grp_hi";
createNode mesh -n "quadShape" -p "quad";
\trename -uid "2";
\tsetAttr -k off ".v";
\tsetAttr ".uvst[0].uvsn" -type "string" "map1";
\tsetAttr -s 4 ".vt";
\tsetAttr ".vt[0:3]"  0 0 0 1 0 0
\t\t 1 1 0 0 1 0;
\tsetAttr ".pt[2]" -type "float3"  0 0 0.5 ;
\tsetAttr -s 5 ".ed";
\tsetAttr ".ed[0:4]"  0 1 0 1 2 0 2 0 0 2 3 0 3 0 0;
\tsetAttr -s 2 ".fc[0:1]" -type "polyFaces"
\t\tf 3 0 1 2
\t\tmu 0 3 0 1 2
\t\tf 3 -3 3 4;
select -ne :time1;
\tsetAttr ".o" 1;
"""


def test_a_maya_ascii_mesh_is_read_with_tweaks_transforms_and_reversed_edges(
    tmp_path: Path,
) -> None:
    path = tmp_path / "scene.ma"
    path.write_text(SCENE)
    nodes = read_scene(path)
    assert nodes["quadShape"].parent == "quad"
    from audio2face_model_server.heads import _part  # pyright: ignore[reportPrivateUsage]

    part = _part(nodes, "quadShape")
    np.testing.assert_allclose(part.positions, [[0, 10, 0], [1, 10, 0], [1, 11, 0.5], [0, 11, 0]])
    assert part.triangles.tolist() == [[0, 1, 2], [0, 2, 3]]


def test_a_scene_in_other_units_is_refused(tmp_path: Path) -> None:
    path = tmp_path / "scene.ma"
    path.write_text(SCENE.replace("-l centimeter", "-l meter"))
    with pytest.raises(ValueError, match="units"):
        read_scene(path)


def test_maya_rotations_turn_x_then_y_then_z() -> None:
    np.testing.assert_allclose(euler_xyz([0, 90, 0]) @ [0, 0, 1], [1, 0, 0], atol=1e-12)
    np.testing.assert_allclose(euler_xyz([90, 0, 0]) @ [0, 0, 1], [0, -1, 0], atol=1e-12)
    # X first: the Y turn then swings the result about the world's Y.
    np.testing.assert_allclose(
        euler_xyz([90, 90, 0]), euler_xyz([0, 90, 0]) @ euler_xyz([90, 0, 0])
    )


def _wav(samples: np.ndarray, rate: int) -> bytes:
    buffer = io.BytesIO()
    soundfile.write(buffer, samples, rate, format="WAV", subtype="PCM_16")
    return buffer.getvalue()


def test_speech_is_made_mono_at_16_khz_and_bad_audio_refused() -> None:
    seconds = 1.5
    t = np.arange(int(44_100 * seconds)) / 44_100
    tone = 0.3 * np.sin(2 * np.pi * 220 * t)
    samples = read_speech(_wav(np.stack([tone, tone], axis=1), 44_100))
    assert samples.dtype == np.dtype("<f4")
    assert abs(len(samples) - SAMPLE_RATE * seconds) <= 2
    assert 0.25 < float(np.abs(samples).max()) < 0.35
    with pytest.raises(InvalidInput):
        read_speech(b"RIFF....not audio")
    with pytest.raises(InvalidInput):
        read_speech(_wav(np.zeros(800), SAMPLE_RATE))  # 0.05 s


def test_the_runners_output_is_read_frame_by_frame(tmp_path: Path) -> None:
    prefix = tmp_path / "result"
    jaw = np.eye(4)
    jaw[:3, 3] = (0.1, -0.2, 0.3)  # a translation: column major puts it at 12..14
    rows = []
    for frame in range(3):
        rows.append([frame * 0.1, 0.5, 0.25, *jaw.T.reshape(-1), 1, 2, 0, 3, 4, 0])
    np.asarray(rows, dtype="<f4").tofile(prefix.with_suffix(".f32"))
    prefix.with_suffix(".json").write_text(
        json.dumps(
            {
                "frames": 3,
                "frame_rate": [60, 1],
                "jaw_size": 16,
                "eyes_size": 6,
                "skin_poses": ["jawOpen", "mouthClose"],
                "tongue_poses": ["tongueIn"],
                "timestamps": [0, 267, 533],
            }
        )
    )
    solved = read_solved(prefix)
    np.testing.assert_allclose(solved.times, [0, 267 / 16000, 533 / 16000])
    np.testing.assert_allclose(solved.skin[:, 0], [0, 0.1, 0.2], atol=1e-7)
    np.testing.assert_allclose(solved.tongue[:, 0], 0.25)
    np.testing.assert_allclose(solved.jaw[1], jaw, atol=1e-7)
    np.testing.assert_allclose(solved.eyes[0], [[1, 2, 0], [3, 4, 0]])


def test_engine_shapes_come_from_trt_info(tmp_path: Path) -> None:
    (tmp_path / "trt_info.json").write_text(
        json.dumps(
            {
                "trt_build_param": {
                    "batch": [
                        "--minShapes=input:1x1x8320,emotion:1x1x26",
                        "--maxShapes=input:{MAX_BATCH_SIZE}x1x8320,emotion:{MAX_BATCH_SIZE}x1x26",
                        "--optShapes=input:{OPT_BATCH_SIZE}x1x8320,emotion:{OPT_BATCH_SIZE}x1x26",
                    ]
                }
            }
        )
    )
    assert shapes(tmp_path, 128, 8) == {
        "input": ((1, 1, 8320), (8, 1, 8320), (128, 1, 8320)),
        "emotion": ((1, 1, 26), (8, 1, 26), (128, 1, 26)),
    }


def test_quaternions_are_unit_and_keep_to_one_side() -> None:
    turns = np.stack([euler_xyz([0, angle, 0]) for angle in (0, 90, 179, 181)])
    q = glb.quaternions(turns)
    np.testing.assert_allclose(np.linalg.norm(q, axis=1), 1, atol=1e-6)
    np.testing.assert_allclose(q[0], [0, 0, 0, 1], atol=1e-7)
    np.testing.assert_allclose(q[1], [0, np.sqrt(0.5), 0, np.sqrt(0.5)], atol=1e-6)
    assert np.all(np.sum(q[1:] * q[:-1], axis=1) > 0)


def _box(offset: float = 0.0) -> Part:
    positions = np.array(
        [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]],
        dtype=np.float32,
    )
    triangles = np.array(
        [
            [0, 2, 1],
            [0, 3, 2],
            [4, 5, 6],
            [4, 6, 7],
            [0, 1, 5],
            [0, 5, 4],
            [1, 2, 6],
            [1, 6, 5],
            [2, 3, 7],
            [2, 7, 6],
            [3, 0, 4],
            [3, 4, 7],
        ],
        dtype=np.uint32,
    )
    return Part(positions + offset, triangles)


def _chunks(data: bytes) -> tuple[dict, bytes]:
    magic, version, length = struct.unpack_from("<4sII", data)
    assert (magic, version, length) == (b"glTF", 2, len(data))
    json_length, json_type = struct.unpack_from("<I4s", data, 12)
    assert json_type == b"JSON"
    document = json.loads(data[20 : 20 + json_length])
    bin_length, bin_type = struct.unpack_from("<I4s", data, 20 + json_length)
    assert bin_type == b"BIN\0"
    return document, data[28 + json_length : 28 + json_length + bin_length]


def test_the_glb_carries_morphs_hierarchy_and_animation() -> None:
    eye = Eye(np.array([0.5, 0.5, 0.5], np.float32), _box(), _box())
    head = Head(skin=_box(), upper_teeth=_box(), lower_teeth=_box(), tongue=_box(), eyes=(eye, eye))
    deltas = np.zeros((2, 8, 3), np.float32)
    deltas[0, 6] = (0, 0, 1)  # one corner of the skin moves, 1 cm
    frames = 4
    jaw = np.repeat(np.eye(4)[None], frames, axis=0)
    jaw[:, 1, 3] = -1.0  # the jaw drops 1 cm
    data = glb.encode(
        head,
        head.skin.positions,
        glb.Morphs(["jawOpen", "mouthClose"], deltas),
        head.tongue.positions,
        glb.Morphs(["tongueIn"], np.zeros((1, 8, 3), np.float32)),
        glb.Motion(
            np.arange(frames) / 60,
            np.linspace(0, 1, frames * 2, dtype=np.float32).reshape(frames, 2),
            np.zeros((frames, 1), np.float32),
            jaw,
            np.zeros((frames, 2, 3)),
        ),
        generator="test",
    )
    document, binary = _chunks(data)
    assert document["buffers"][0]["byteLength"] == len(binary)
    names = [node["name"] for node in document["nodes"]]
    jaw_node = document["nodes"][names.index("jaw")]
    assert [document["nodes"][child]["name"] for child in jaw_node["children"]] == [
        "lower_teeth",
        "tongue",
    ]
    skin = document["meshes"][document["nodes"][names.index("skin")]["mesh"]]
    assert skin["extras"]["targetNames"] == ["jawOpen", "mouthClose"]
    target = document["accessors"][skin["primitives"][0]["targets"][0]["POSITION"]]
    # The moved corner, and its neighbours, whose normals turn with it.
    assert 1 < target["sparse"]["count"] < 8
    assert target["max"] == [0.0, 0.0, pytest.approx(0.01)]
    animation = document["animations"][0]
    paths = sorted(
        (document["nodes"][c["target"]["node"]]["name"], c["target"]["path"])
        for c in animation["channels"]
    )
    assert paths == [
        ("jaw", "rotation"),
        ("jaw", "translation"),
        ("left_eye", "rotation"),
        ("right_eye", "rotation"),
        ("skin", "weights"),
        ("tongue", "weights"),
    ]
    times = document["accessors"][animation["samplers"][0]["input"]]
    assert times["count"] == frames
    assert times["max"] == [pytest.approx(3 / 60)]
    translation = next(c for c in animation["channels"] if c["target"]["path"] == "translation")
    output = document["accessors"][animation["samplers"][translation["sampler"]]["output"]]
    view = document["bufferViews"][output["bufferView"]]
    values = np.frombuffer(binary, "<f4", output["count"] * 3, view["byteOffset"]).reshape(-1, 3)
    np.testing.assert_allclose(values[0], [0, -0.01, 0], atol=1e-7)  # metres


QUAD = """createNode transform -n "{transform}" -p "{parent}";
createNode mesh -n "{shape}" -p "{transform}";
\tsetAttr ".vt[0:3]"  0 0 0 1 0 0 1 1 0 0 1 0;
\tsetAttr ".ed[0:4]"  0 1 0 1 2 0 2 0 0 2 3 0 3 0 0;
\tsetAttr ".fc[0:1]" -type "polyFaces" f 3 0 1 2 f 3 -3 3 4;
"""


def test_a_head_read_twice_comes_from_the_cache(tmp_path: Path) -> None:
    text = (
        "currentUnit -l centimeter -a degree;\n"
        'createNode transform -n "root";\n'
        '\tsetAttr ".t" -type "double3" 0 10 0 ;\n'
    )
    for side in "rl":
        text += f'createNode transform -n "{side}_eye_grp_hi" -p "root";\n'
        text += '\tsetAttr ".r" -type "double3" 10 20 0 ;\n'
        text += '\tsetAttr ".rp" -type "double3" 0.5 0.5 0 ;\n'
    shapes = [
        "c_headWatertight_hiShape",
        "c_topDenture_hiShape",
        "c_bottomDenture_hiShape",
        "c_tongue_hiShape",
    ]
    shapes += [f"{side}_{name}_hiShape" for side in "rl" for name in ("choroid", "lens")]
    for index, shape in enumerate(shapes):
        parent = f"{shape[0]}_eye_grp_hi" if shape[:2] in ("r_", "l_") else "root"
        text += QUAD.format(transform=f"t{index}", shape=shape, parent=parent)
    scene, cache = tmp_path / "head.ma", tmp_path / "head.npz"
    scene.write_text(text)
    first = read_head(scene, cache)
    assert cache.is_file()
    second = read_head(scene, cache)
    np.testing.assert_array_equal(first.skin.positions, second.skin.positions)
    np.testing.assert_allclose(first.skin.positions[2], [1, 11, 0])
    # The root's lift applies; the eye's saved turn does not.
    np.testing.assert_allclose(first.eyes[0].pivot, [0.5, 10.5, 0])
    np.testing.assert_allclose(first.eyes[1].ball.positions[2], [1, 11, 0], atol=1e-6)
    assert head_from_scene(read_scene(scene)).tongue.triangles.tolist() == [[0, 1, 2], [0, 2, 3]]
