"""The pictures' colour on the head: the depth rasteriser, the camera fit, the picture's outline,
the landmark warp, the pictures' squaring, the head tile's cut and the servers' error mapping;
no weights, data or GPU."""

import numpy as np
import pytest
from fromsoma_model_server.cameras import (
    R_VIEW,
    Camera,
    CameraError,
    fit_camera,
    picture_silhouette,
    rasterize,
    rodrigues,
)
from fromsoma_model_server.facetex import (
    MAX_PICTURE,
    PicturesError,
    picture_masks,
    square,
    tile_cut,
    warp_field,
)
from fromsoma_model_server.server import colour_head

from model_server_sdk import GenerationError, InvalidInput


def test_the_depth_rasteriser_keeps_the_nearest_surface() -> None:
    corners = [[0.0, 0.0], [8.0, 0.0], [0.0, 8.0], [8.0, 8.0]]
    xy = np.array(corners * 2)
    z = np.array([2.0] * 4 + [1.0] * 4)
    tris = np.array([[0, 1, 2], [1, 3, 2], [4, 5, 6], [5, 7, 6]])  # the far square, the near one
    tri, bary, depth = rasterize(xy, z, tris, 8, 8)
    assert set(np.unique(tri).tolist()) == {2, 3}
    assert np.allclose(depth, 1.0)
    assert np.allclose(bary.sum(-1), 1.0)
    centres = (bary[..., None] * xy[tris[tri]]).sum(-2)
    ys, xs = np.mgrid[:8, :8]
    assert np.allclose(centres, np.stack([xs + 0.5, ys + 0.5], -1), atol=1e-5)


def test_a_camera_is_recovered_from_its_projections() -> None:
    rng = np.random.default_rng(0)
    X = rng.normal(scale=0.08, size=(60, 3))
    R = rodrigues([0.05, -0.1, 0.02]) @ R_VIEW["front"]
    start = Camera(R_VIEW["front"], np.zeros(3), 1000, None, 2500.0, (500.0, 500.0))
    truth = Camera(R, np.zeros(3), 1000, None, 3000.0, (480.0, 530.0))
    cam, errors = fit_camera(X, truth.project(X)[0], start)
    assert errors.max() < 1e-3
    assert np.allclose(cam.R, truth.R, atol=1e-5)
    assert cam.s == pytest.approx(3000.0, rel=1e-5)
    near = Camera(R, (0.01, -0.02, 0.6), 1000, f=1400.0)
    cam, errors = fit_camera(X, near.project(X)[0], start, f=1400.0)
    assert errors.max() < 1e-3
    assert np.allclose(cam.t, near.t, atol=1e-5)


def test_the_outline_is_a_signed_distance_to_the_figure() -> None:
    picture = np.full((200, 200, 3), 160, np.uint8)
    yy, xx = np.mgrid[:200, :200]
    disc = (xx - 100) ** 2 + (yy - 90) ** 2 <= 50**2
    picture[disc] = (200, 150, 130)
    picture[85:95, 95:105] = 160  # the backdrop's colour inside the figure stays figure
    sdf = picture_silhouette(picture)
    assert sdf[90, 100] == pytest.approx(-51, abs=1.5)
    assert sdf[90, 170] == pytest.approx(20, abs=1.0)
    assert ((sdf < 0) == disc).mean() > 0.999


def test_the_warp_moves_the_head_points_onto_the_pictures() -> None:
    rng = np.random.default_rng(1)
    src = rng.uniform(300, 700, size=(80, 2))
    shift = np.array([3.0, -2.0])
    sample, stats = warp_field(src, src + shift, np.ones(len(src)), 1000)
    assert np.abs(sample(src) - shift).max() < 0.5
    assert stats["pairs"] == 80
    assert stats["held_out_median_px"] < 0.5


def test_pictures_are_squared_with_their_backdrop_and_scaled_down() -> None:
    picture = np.full((300, 200, 3), (90, 100, 110), np.uint8)
    picture[100:200, 50:150] = (200, 150, 130)
    out = square(picture)
    assert out.shape == (300, 300, 3)
    assert (out[:, :200] == picture).all()
    assert (out[:, 200:] == (90, 100, 110)).all()
    assert square(np.zeros((2000, 2000, 3), np.uint8)).shape == (MAX_PICTURE, MAX_PICTURE, 3)


def test_a_grey_picture_has_no_skin_to_take_colour_from() -> None:
    picture = np.full((200, 200, 3), 160, np.uint8)
    picture[50:150, 50:150] = 120
    with pytest.raises(PicturesError, match="grey"):
        picture_masks(picture, np.zeros((478, 2)), "front", 3.0)


def test_the_head_tiles_cut_to_another_tile_is_found() -> None:
    tris = np.array([[0, 1, 2], [1, 3, 2]])  # the edge 1-2 is shared
    tris_uv = np.array([[0, 1, 2], [3, 4, 5]])  # the first in the head tile, the second not
    uv = np.array([[0.1, 0.1], [0.3, 0.1], [0.1, 0.3], [0.7, 0.1], [0.9, 0.1], [0.7, 0.3]])
    assert tile_cut(tris, tris_uv, uv).tolist() == [1, 2]
    assert tile_cut(tris, np.array([[0, 1, 2], [1, 3, 2]]), uv[[0, 1, 2, 2]]).tolist() == []


class _Texture:
    def __init__(self, error: Exception | None = None) -> None:
        self.error = error

    def paint(self, person, front, left, mirrored=False):
        if self.error is not None:
            raise self.error
        return person, {"mirrored": mirrored}


def test_the_servers_pass_the_readers_mirroring_and_name_the_pigments_option() -> None:
    crops = {"left": {"mirrored": True}}
    assert colour_head(_Texture(), "person", None, None, crops) == ("person", {"mirrored": True})
    with pytest.raises(InvalidInput, match="texture=pigments"):
        colour_head(_Texture(PicturesError("no skin-coloured head")), None, None, None, crops)
    with pytest.raises(GenerationError, match="texture=pigments"):
        colour_head(_Texture(CameraError("no face on the head's render")), None, None, None, crops)
