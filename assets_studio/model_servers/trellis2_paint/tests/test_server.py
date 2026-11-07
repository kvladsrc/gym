import io

import numpy as np
import pytest
import trimesh
from PIL import Image
from trellis2_paint_model_server.server import (
    MAX_SPREAD_SCALE,
    TASK,
    TrellisPaintServer,
    match_colours,
    oklab_to_srgb,
    prepare_mesh,
    read_image,
    read_mesh,
    srgb_to_oklab,
    surface_colours,
    verify_surface,
)

from model_server_sdk import GenerationError, InvalidInput, create_app


def test_sdk_contract():
    create_app(TrellisPaintServer())
    assert TASK.task == "3d-paint"
    assert [s.role for s in TASK.inputs] == ["mesh", "image"]


def test_invalid_inputs_are_explained():
    with pytest.raises(InvalidInput, match="mesh"):
        read_mesh(b"not glb")
    buffer = io.BytesIO()
    Image.new("RGB", (4, 4), "white").save(buffer, format="PNG")
    with pytest.raises(InvalidInput, match="BiRefNet"):
        read_image(buffer.getvalue())


def test_placement_uv_and_input_are_preserved():
    mesh = trimesh.creation.box(extents=(2, 4, 1))
    mesh.apply_translation((10, -3, 7))
    mesh.visual = trimesh.visual.TextureVisuals(uv=np.zeros((len(mesh.vertices), 2)))
    original = mesh.vertices.copy()
    prepared, center, scale = prepare_mesh(mesh)
    positions = prepared.vertices.copy()
    positions[:, 1], positions[:, 2] = positions[:, 2].copy(), -positions[:, 1].copy()
    assert np.allclose(positions / scale + center, original)
    assert np.array_equal(mesh.vertices, original)
    assert np.array_equal(prepared.visual.uv, mesh.visual.uv)


def test_uv_seam_duplicates_and_face_reordering_keep_the_surface():
    mesh = trimesh.creation.box()
    vertices = mesh.vertices[mesh.faces[::-1]].reshape(-1, 3)
    output = trimesh.Trimesh(vertices, np.arange(len(vertices)).reshape(-1, 3), process=False)
    assert verify_surface(mesh, output) < 1e-12
    output.vertices += (0.1, 0, 0)
    with pytest.raises(GenerationError, match="surface"):
        verify_surface(mesh, output)


def test_changed_triangle_count_is_rejected():
    mesh = trimesh.creation.box()
    with pytest.raises(GenerationError, match="triangle count"):
        verify_surface(mesh, trimesh.creation.icosphere(subdivisions=1))


def textured_quad(texture, uv_top=1.0):
    vertices = np.array([[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]], float)
    uv = vertices[:, :2] * (1, uv_top)
    material = trimesh.visual.material.PBRMaterial(baseColorTexture=Image.fromarray(texture))
    visual = trimesh.visual.TextureVisuals(uv=uv, material=material)
    return trimesh.Trimesh(vertices, [[0, 1, 2], [0, 2, 3]], visual=visual, process=False)


def test_surface_colours_read_texture_rows_top_down():
    texture = np.zeros((8, 8, 4), np.uint8)
    texture[:4], texture[4:] = (255, 0, 0, 255), (0, 255, 0, 255)
    colours = surface_colours(textured_quad(texture, uv_top=0.5), texture, count=1000)
    assert (colours == (0, 255, 0, 255)).all()


def test_colour_match_moves_surface_statistics_to_the_reference_foreground():
    rng = np.random.default_rng(1)
    texture = np.full((64, 64, 4), 255, np.uint8)
    texture[..., :3] = rng.normal((150, 60, 30), 20, (64, 64, 3)).clip(0, 255)
    reference = np.zeros((32, 32, 4), np.uint8)
    reference[..., 2] = 255  # transparent background colour, ignored
    foreground = rng.normal((180, 155, 130), 8, (16, 16, 3)).clip(0, 255)
    reference[8:24, 8:24] = np.dstack([foreground, np.full((16, 16), 255)])
    mesh = textured_quad(texture)
    report = match_colours(mesh, Image.fromarray(reference))
    painted = np.asarray(mesh.visual.material.baseColorTexture)[..., :3].reshape(-1, 3) / 255
    result, target = srgb_to_oklab(painted), srgb_to_oklab(foreground.reshape(-1, 3) / 255)
    assert np.allclose(result.mean(axis=0), target.mean(axis=0), atol=0.005)
    assert np.allclose(result.std(axis=0), target.std(axis=0), atol=0.005)
    assert np.allclose(oklab_to_srgb(srgb_to_oklab(painted)), painted, atol=1e-4)
    assert all(scale < 1 for scale in report["spread_scale"])


def test_colour_match_does_not_stretch_a_near_flat_paint():
    rng = np.random.default_rng(2)
    texture = np.full((64, 64, 4), 255, np.uint8)
    texture[..., :3] = (120, 70, 40)
    texture[::2, ::2, 0] = 122  # quantisation-level noise
    reference = np.full((16, 16, 4), 255, np.uint8)
    reference[..., :3] = rng.normal((180, 100, 40), 25, (16, 16, 3)).clip(0, 255)
    mesh = textured_quad(texture)
    report = match_colours(mesh, Image.fromarray(reference))
    painted = np.asarray(mesh.visual.material.baseColorTexture)[..., :3].reshape(-1, 3) / 255
    lab, target = srgb_to_oklab(painted), srgb_to_oklab(reference[..., :3].reshape(-1, 3) / 255)
    assert max(report["spread_scale"]) <= MAX_SPREAD_SCALE
    assert np.allclose(lab.mean(axis=0), target.mean(axis=0), atol=0.01)
    assert (lab.std(axis=0) < target.std(axis=0) / 3).all()
