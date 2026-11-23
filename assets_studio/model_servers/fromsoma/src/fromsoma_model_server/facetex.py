"""The pictures' own colour on the person's atlas: the front and the left profile projected onto
the head through their cameras (cameras.py) and blended into the pigment atlas.

Picture side, for each picture
- Warp: the head's landmarks as the fitted camera projects them are moved onto the picture's
  by a thin-plate spline displacement field (the smoothing chosen by 5-fold held-out error),
  with zero anchors on a ring round the face so that it fades outside. Front: MediaPipe's
  points (the face oval and the inner lips smoothed tenfold; pairs the camera misses by over 30
  px are dropped). Profile: the visible eye, iris, nose, lips and brow, and the outline.
- Masks: the head (the plain backdrop out, a grey shirt out by its lack of chroma), the eye
  openings (MediaPipe's eye contour grown, with the sclera, iris, lashes and the waterline near
  it), the lips' meeting line, the brows, the lips and the nostrils.
- The eye openings and the lips' meeting line are filled from the skin round them (normalised
  Gaussians, coarse to fine): the lid margins and the lips' inner edges take skin and lip
  colour, never the eye's.
- Light: the head's own shading in the picture (its normals carried through the inverse warp,
  second-order spherical harmonics fitted to the picture's skin), blurred by a masked 20 mm
  Gaussian and normalised to 1 on the skin, divides the colour. Broad light gradients go; form
  shading, redness, brows and lips stay.

Atlas side, the head's UV tile
- Every texel takes its point on the head into the front, the profile, and the profile mirrored
  onto the right side (SOMA's mirror vertices). The render's depth decides visibility, the angle
  to the camera weights it, the warp moves it onto the picture. The views blend with sharpened
  weights (w^4), the front preferred; one gain per channel brings the profile's colour to the
  front's where both hold weight.
- The mouth bag and the eye bags keep the atlas (the head's own mouth and eyes stay).
- Holes in the coverage inside the face are filled from the covered skin round them.
- Ears: no landmark places them and SOMA's folds differ from the pictures', so they keep the
  atlas's own ear, scaled to the pictures' tone round it. The zone is the auricle (what sticks
  out of the scalp's ellipsoid at the sides) grown 15 mm over the surface and faded over 12
  mm, so the pictures' ear, never where the model's is, does not show as a second ear.
- The pictures' colour fades into the atlas over 35 mm from what no picture covers (the back of
  the head, the neck down to the body's tile), so the tone does not jump.
- Tone: the face's skin tone (a masked 20 mm low-pass, brows, lips, nostrils and the eyes'
  surroundings left out) is pulled halfway to the atlas's skin tone there; everything finer,
  brows, lips and lids, rides on top unchanged.
"""

import logging
import time
from collections.abc import Callable, Mapping
from dataclasses import dataclass, replace
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage, sparse
from scipy.interpolate import RBFInterpolator
from scipy.optimize import least_squares
from scipy.sparse.csgraph import connected_components, dijkstra
from scipy.spatial import cKDTree

from fromsoma_model_server.cameras import (
    BROW_LEFT,
    BROW_RIGHT,
    EYE_LEFT,
    EYE_RIGHT,
    FACE_OVAL,
    LIPS_INNER,
    LIPS_OUTER,
    Eye,
    Head,
    Scene,
    View,
    bilinear,
    fit_views,
    head_scene,
    linear_to_srgb,
    luminance,
    rasterize,
    srgb_to_linear,
    to_u8,
    vertex_normals,
)
from fromsoma_model_server.person import TEXTURE, Person

logger = logging.getLogger(__name__)

TILE = TEXTURE // 2  # the head's UV tile: the atlas's top left quarter
MAX_PICTURE = 1600  # larger pictures are scaled down to this many pixels across
SIGMA_D1_MM = 20.0  # the light estimate's blur
FADE_MM = 35.0  # the fade into the atlas from the uncovered region and the neck's cut
TONE_MM = 20.0  # the skin tone's low-pass
EAR_MM = 20.0  # the tone's low-pass round the ears
EAR_GROW_MM = 15.0  # the ear zone: the auricle grown this far over the surface
EAR_FADE_MM = 12.0  # from the ear zone out to the pictures' colour
BAG_GATE_MM = (0.2, 1.0)  # no picture within 0.2 mm of the mouth bag and eye bags, all 1 mm on
FACING = (0.2, 0.8)  # cosine to the camera: no weight below, full above
DEPTH_TOL = 0.0015  # metres
SHARPEN = 4
VIEW_PREFERENCE = {"front": 1.0, "left": 0.85, "mirror": 0.75}
GUTTER = 8  # texels the change grows into the gutter round the islands
HEAD_JOINTS = ("Head", "Jaw", "LeftEye", "RightEye")
SETTINGS = {  # in each report
    "light_blur_mm": SIGMA_D1_MM,
    "fade_mm": FADE_MM,
    "tone_mm": TONE_MM,
    "ear_zone_mm": [EAR_GROW_MM, EAR_FADE_MM],
    "bag_gate_mm": list(BAG_GATE_MM),
    "max_picture_px": MAX_PICTURE,
}


class PicturesError(ValueError):
    """The pictures cannot give the texture; the message says why."""


def smoothstep(x: Any) -> np.ndarray:
    x = np.clip(x, 0, 1)
    return x * x * (3 - 2 * x)


def square(picture: np.ndarray, limit: int = MAX_PICTURE) -> np.ndarray:
    """The picture (H, W, 3) uint8 made square, padded right and below with its border's median
    colour, and scaled down to `limit` pixels across if larger."""
    h, w = picture.shape[:2]
    if h != w:
        border = np.concatenate([picture[0], picture[-1], picture[:, 0], picture[:, -1]])
        padded = np.empty((max(h, w), max(h, w), 3), np.uint8)
        padded[:] = np.median(border, 0).round().astype(np.uint8)
        padded[:h, :w] = picture
        picture = padded
    if picture.shape[0] > limit:
        picture = np.asarray(Image.fromarray(picture).resize((limit, limit), Image.LANCZOS))
    return np.ascontiguousarray(picture)


# ----------------------------------------------------------------------------------------------
# picture masks and blurs


def polygon_mask(shape: tuple[int, int], points: np.ndarray, grow: float = 0) -> np.ndarray:
    img = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(img).polygon([tuple(p) for p in points], fill=255)
    m = np.asarray(img) > 0
    if grow > 0:
        m = ndimage.binary_dilation(m, iterations=round(grow))
    return m


def foreground(picture: np.ndarray, tolerance: float = 14) -> np.ndarray:
    border = np.concatenate([picture[0], picture[-1], picture[:, 0], picture[:, -1]])
    diff = np.abs(picture.astype(np.float32) - np.median(border.astype(np.float32), 0)).max(2)
    lab, _ = ndimage.label(diff < tolerance)
    edge = np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))
    return ~np.isin(lab, edge[edge > 0])


def blur_norm(
    values: np.ndarray, weights: np.ndarray, sigma: float
) -> tuple[np.ndarray, np.ndarray]:
    """Normalised (masked) Gaussian blur; values (H, W[, C]), weights (H, W)."""
    w = ndimage.gaussian_filter(weights.astype(np.float32), sigma)
    if values.ndim == 3:
        channels = [values[..., c] * weights for c in range(values.shape[2])]
        out = np.stack([ndimage.gaussian_filter(c, sigma) for c in channels], -1)
        return out / np.maximum(w, 1e-6)[..., None], w
    return ndimage.gaussian_filter(values * weights, sigma) / np.maximum(w, 1e-6), w


def soft(mask: np.ndarray, sigma: float) -> np.ndarray:
    """A mask feathered inwards: 1 deep inside, 0 at and outside its edge."""
    return smoothstep(ndimage.distance_transform_edt(mask) / max(sigma, 1e-6))


def picture_masks(picture: np.ndarray, pts: np.ndarray, view: str, px_mm: float) -> dict[str, Any]:
    """The picture's masks (H, W): the head's skin usable for colour, the eye openings, the
    lips' meeting line, the brows, lips and nostrils; and the skin's red-blue difference."""
    H, W = picture.shape[:2]
    srgb = picture.astype(np.float32)
    rb = srgb[..., 0] - srgb[..., 2]
    skin_like = foreground(picture) & (rb > 20)
    lab, n = ndimage.label(skin_like)
    if n == 0:
        raise PicturesError(f"no skin-coloured head on the {view} picture (a grey picture?)")
    sizes = ndimage.sum(skin_like, lab, range(1, n + 1))
    head = lab == (1 + int(np.argmax(sizes)))
    head = ndimage.binary_fill_holes(ndimage.binary_closing(head, iterations=3))
    skin_rb = float(np.median(rb[head & (rb > 20)]))
    polygons = [EYE_LEFT] if view == "left" else [EYE_LEFT, EYE_RIGHT]
    eyes = np.zeros((H, W), bool)
    near_all = np.zeros((H, W), bool)
    for poly in polygons:
        p = pts[poly]
        width = np.linalg.norm(p[0] - p[8])
        eyes |= polygon_mask((H, W), p, grow=max(2, 0.06 * width))
        near = polygon_mask((H, W), p, grow=0.18 * width)
        eyes |= near & (rb < 0.5 * skin_rb)  # sclera, iris, lashes: not skin-coloured
        close = polygon_mask((H, W), p, grow=0.12 * width)
        eyes |= close & (rb > 1.35 * skin_rb)  # the waterline and reddish lid rims
        near_all |= polygon_mask((H, W), p, grow=0.24 * width)
    # lashes: thin dark strokes beyond the lid margin, far darker than the skin round them
    lum = luminance(srgb_to_linear(srgb / 255))
    local, _ = blur_norm(lum, (head & ~eyes).astype(np.float32), 2.0 * px_mm)
    lashes = near_all & head & (lum < 0.7 * local)
    eyes |= ndimage.binary_dilation(lashes, iterations=2)
    eyes = ndimage.binary_closing(eyes, iterations=max(2, round(px_mm)))  # ~1 mm between lashes
    inner = polygon_mask((H, W), pts[LIPS_INNER], grow=max(2, 0.6 * px_mm))
    brows = np.zeros((H, W), bool)
    for poly in [BROW_LEFT] if view == "left" else [BROW_LEFT, BROW_RIGHT]:
        brows |= polygon_mask((H, W), pts[poly], grow=2.5 * px_mm)
    lips = polygon_mask((H, W), pts[LIPS_OUTER], grow=1.0 * px_mm)
    # nostrils: much darker than the skin round the nose
    nose = pts[[1, 2, 98, 327, 64, 294, 129, 358, 49, 279]]
    lo, hi = nose.min(0) - 4 * px_mm, nose.max(0) + 4 * px_mm
    box = np.zeros((H, W), bool)
    box[int(max(lo[1], 0)) : int(min(hi[1], H)), int(max(lo[0], 0)) : int(min(hi[0], W))] = True
    ref = np.median(lum[box & head & ~eyes])
    nostrils = ndimage.binary_dilation(
        box & head & (lum < 0.45 * ref), iterations=round(0.8 * px_mm)
    )
    valid = ndimage.binary_erosion(head & ~eyes & ~inner, iterations=2, border_value=0)
    return {
        "head": head,
        "valid": valid,
        "eyes": eyes,
        "inner_lips": inner,
        "brows": brows,
        "lips": lips,
        "nostrils": nostrils,
        "skin_rb": skin_rb,
    }


# ----------------------------------------------------------------------------------------------
# warp and light


def _grid_sampler(grid: np.ndarray, step: int) -> Callable[[np.ndarray], np.ndarray]:
    n = grid.shape[0]

    def sample(p: np.ndarray) -> np.ndarray:
        x = np.clip(p[:, 0] / step, 0, n - 1.001)
        y = np.clip(p[:, 1] / step, 0, n - 1.001)
        x0, y0 = x.astype(np.int64), y.astype(np.int64)
        fx, fy = (x - x0)[:, None], (y - y0)[:, None]
        a = grid[y0, x0] * (1 - fx) + grid[y0, x0 + 1] * fx
        b = grid[y0 + 1, x0] * (1 - fx) + grid[y0 + 1, x0 + 1] * fx
        return a * (1 - fy) + b * fy

    return sample


def warp_field(
    src: np.ndarray,
    dst: np.ndarray,
    smooth: np.ndarray,
    size: int,
    ring: float = 1.4,
    step: int = 4,
    folds: int = 5,
) -> tuple[Callable[[np.ndarray], np.ndarray], dict[str, Any]]:
    """The displacement dst - src as a thin-plate spline of src, zero on a ring of anchors round
    the points; smooth: per-point relative smoothing. Returns the field's sampler (on a grid of
    `step` pixels) and the fit's and the held-out errors (pixels)."""
    centre = src.mean(0)
    radius = np.linalg.norm(src - centre, axis=1).max() * ring
    angles = np.linspace(0, 2 * np.pi, 36, endpoint=False)
    anchors = centre + radius * np.stack([np.cos(angles), np.sin(angles)], 1)
    disp = dst - src
    best: tuple[float, float, float] | None = None
    order = np.random.default_rng(0).permutation(len(src))
    for lam in (0.0003, 0.001, 0.003, 0.01, 0.03, 0.1):
        errors = []
        for k in range(folds):
            test = order[k::folds]
            train = np.setdiff1d(np.arange(len(src)), test)
            rbf = RBFInterpolator(
                np.concatenate([src[train], anchors]) / 100,
                np.concatenate([disp[train], np.zeros_like(anchors)]),
                kernel="thin_plate_spline",
                smoothing=np.concatenate([lam * smooth[train], np.full(len(anchors), lam * 30)]),
                degree=1,
            )
            errors.append(np.linalg.norm(src[test] + rbf(src[test] / 100) - dst[test], axis=1))
        e = np.concatenate(errors)
        score = float(np.median(e))
        if best is None or score < best[1]:
            best = (lam, score, float(np.percentile(e, 90)))
    assert best is not None
    lam = best[0]
    rbf = RBFInterpolator(
        np.concatenate([src, anchors]) / 100,
        np.concatenate([disp, np.zeros_like(anchors)]),
        kernel="thin_plate_spline",
        smoothing=np.concatenate([lam * smooth, np.full(len(anchors), lam * 30)]),
        degree=1,
    )
    fit = np.linalg.norm(src + rbf(src / 100) - dst, axis=1)
    moved = np.linalg.norm(disp, axis=1)
    g = np.arange(0, size + step, step, dtype=np.float64)
    gx, gy = np.meshgrid(g, g)
    grid = rbf(np.stack([gx.ravel(), gy.ravel()], 1) / 100).reshape(len(g), len(g), 2)
    stats = {
        "pairs": len(src),
        "smoothing": lam,
        "fit_median_px": round(float(np.median(fit)), 2),
        "fit_p90_px": round(float(np.percentile(fit, 90)), 2),
        "held_out_median_px": round(best[1], 2),
        "held_out_p90_px": round(best[2], 2),
        "displacement_median_px": round(float(np.median(moved)), 2),
        "displacement_max_px": round(float(moved.max()), 2),
    }
    return _grid_sampler(grid.astype(np.float32), step), stats


def inverse_warp(sample: Callable[[np.ndarray], np.ndarray], size: int) -> np.ndarray:
    """For every picture pixel q, the render pixel p with p + d(p) = q (fixed point)."""
    yy, xx = np.mgrid[:size, :size]
    q = np.stack([xx.ravel() + 0.5, yy.ravel() + 0.5], 1).astype(np.float64)
    p = q.copy()
    for _ in range(8):
        p = q - sample(p)
    return p.reshape(size, size, 2)


def sh_basis(n: np.ndarray) -> np.ndarray:
    x, y, z = n[..., 0], n[..., 1], n[..., 2]
    return np.stack(
        [np.ones_like(x), x, y, z, x * y, x * z, y * z, x * x - y * y, 3 * z * z - 1], -1
    )


def shading_fit(Y: np.ndarray, normals: np.ndarray, fitmask: np.ndarray) -> np.ndarray:
    """Second-order spherical harmonics of the normals (H, W, 3, camera frame, NaN off the
    head) fitted to the luminance Y on fitmask, robustly; the shading (H, W), NaN off the head."""
    sel = fitmask & np.isfinite(normals[..., 0])
    B = sh_basis(normals[sel])
    y = Y[sel]
    w = np.ones(len(y))
    coef = np.zeros(B.shape[1])
    for _ in range(5):
        coef, *_ = np.linalg.lstsq(B * w[:, None], y * w, rcond=None)
        rel = np.abs(y / np.maximum(B @ coef, 1e-6) - 1)
        w = np.where(rel < 0.12, 1.0, 0.12 / np.maximum(rel, 1e-9))
    S = np.full(Y.shape, np.nan, np.float32)
    ok = np.isfinite(normals[..., 0])
    S[ok] = sh_basis(normals[ok]) @ coef
    return S


@dataclass
class Domain:
    """One picture's side: its camera, warp, masks and light."""

    view: View
    sample: Callable[[np.ndarray], np.ndarray]
    warp: dict[str, Any]
    masks: dict[str, Any]
    px_mm: float
    shading: np.ndarray  # the head's shading in the picture, filled out to the edges
    features: np.ndarray  # brows + lips + nostrils + the eyes' surroundings, soft
    depth: np.ndarray  # the render's depth (inf off the head)
    edge_soft: np.ndarray  # 0 at the render's depth edges, 1 two millimetres away


def picture_domain(view: View, picture: np.ndarray, scene: Scene) -> Domain:
    cam, pts, X = view.camera, view.points, view.head_points
    size = picture.shape[0]
    px_mm = view.px_per_mm
    if view.outline is None:  # the front: all of MediaPipe's points on the head
        ids = np.array([i for i in range(468) if view.lifted[i] >= 0])
        src, dst = cam.project(X[ids])[0], pts[ids]
        keep = np.linalg.norm(src - dst, axis=1) < 30  # a mislifted point lands far away
        smooth = np.where(np.isin(ids[keep], FACE_OVAL + LIPS_INNER), 10.0, 1.0)
        src, dst = src[keep], dst[keep]
    else:
        ids = view.pairs
        src, dst = cam.project(X[ids])[0], pts[ids]
        keep = np.linalg.norm(src - dst, axis=1) < 30
        smooth = np.where(np.isin(ids[keep], BROW_LEFT), 5.0, 1.0)
        sil_src = cam.project(view.outline[0])[0][::4]
        sil_dst = view.outline[1][::4]
        ok = np.linalg.norm(sil_src - sil_dst, axis=1) < 30
        src = np.concatenate([src[keep], sil_src[ok]])
        dst = np.concatenate([dst[keep], sil_dst[ok]])
        smooth = np.concatenate([smooth, np.full(int(ok.sum()), 3.0)])
    sample, warp = warp_field(src, dst, smooth, size)
    warp["dropped_pairs"] = int((~keep).sum())
    masks = picture_masks(picture, pts, view.name, px_mm)

    # the head's normals in the picture: the render's, carried through the inverse warp
    tri_map, bary_map, depth = view.maps
    pinv = inverse_warp(sample, size)
    col = np.clip(np.floor(pinv[..., 0]).astype(np.int64), 0, size - 1)
    row = np.clip(np.floor(pinv[..., 1]).astype(np.int64), 0, size - 1)
    del pinv
    t = tri_map[row, col]
    normals = np.full((size, size, 3), np.nan, np.float32)
    ok = (t >= 0) & (t < scene.skin)
    nn = np.einsum(
        "nk,nkc->nc",
        bary_map[row[ok], col[ok]],
        scene.normals[scene.triangles[t[ok]]].astype(np.float32),
    )
    nn /= np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-9)
    normals[ok] = nn @ cam.R.T.astype(np.float32)
    del t, row, col, nn

    Y = luminance(srgb_to_linear(picture.astype(np.float32) / 255))
    features = masks["brows"] | masks["lips"] | masks["eyes"] | masks["nostrils"]
    fitmask = masks["valid"] & ~features & (-normals[..., 2] > 0.3)
    Sh = shading_fit(Y, normals, fitmask)
    del normals
    missing = ~np.isfinite(Sh)  # off the head's render (its outline differs): the nearest
    near = ndimage.distance_transform_edt(missing, return_distances=False, return_indices=True)
    Sh = Sh[near[0], near[1]]
    Sh = np.maximum(Sh, 0.15 * np.median(Sh[fitmask]))

    soft_features = sum(
        ndimage.gaussian_filter(masks[k].astype(np.float32), 0.8 * px_mm)
        for k in ("brows", "lips", "nostrils")
    )
    eye_near = ndimage.gaussian_filter(
        ndimage.binary_dilation(masks["eyes"], iterations=int(1.5 * px_mm)).astype(np.float32),
        px_mm,
    )
    # the render's depth edges (occlusion boundaries): visibility feathered near them
    m = np.isfinite(depth)
    big = np.where(m, depth, 1e3)
    dz = np.zeros_like(big)
    for ax in (0, 1):
        dz = np.maximum(dz, np.abs(np.diff(big, axis=ax, append=big.take([-1], axis=ax))))
        dz = np.maximum(dz, np.abs(np.diff(big, axis=ax, prepend=big.take([0], axis=ax))))
    edge_soft = smoothstep(ndimage.distance_transform_edt(~(dz > 0.004)) / (2.0 * px_mm))
    return Domain(
        view,
        sample,
        warp,
        masks,
        px_mm,
        Sh.astype(np.float32),
        (soft_features + eye_near).astype(np.float32),
        np.where(m, depth, np.inf),
        edge_soft.astype(np.float32),
    )


def eye_and_lip_holes(domain: Domain, picture: np.ndarray) -> np.ndarray:
    """The picture's eye openings and the lips' meeting line (H, W)."""
    m, pts = domain.masks, domain.view.points
    H, W = picture.shape[:2]
    srgb = picture.astype(np.float32)
    rb = srgb[..., 0] - srgb[..., 2]
    lum = luminance(srgb_to_linear(srgb / 255))
    skin_lum = float(np.median(lum[m["valid"]]))
    hole = m["inner_lips"].copy()
    for poly in [EYE_LEFT] if domain.view.name == "left" else [EYE_LEFT, EYE_RIGHT]:
        p = pts[poly]
        width = np.linalg.norm(p[0] - p[8])
        hole |= polygon_mask((H, W), p, grow=max(2, 0.05 * width))
        near = polygon_mask((H, W), p, grow=0.10 * width)
        hole |= near & (rb < 0.5 * m["skin_rb"]) & (lum > 0.6 * skin_lum)  # sclera by the contour
    return ndimage.binary_closing(hole, iterations=2) & m["head"]


def inpaint(img: np.ndarray, hole: np.ndarray, known: np.ndarray, px_mm: float) -> np.ndarray:
    """img (H, W, C) with the hole filled from the known pixels round it, coarse to fine."""
    w0 = (known & ~hole).astype(np.float32)
    est = None
    for s in (8.0, 4.0, 2.0, 1.0, 0.5):
        v, w = blur_norm(img, w0, s * px_mm)
        a = smoothstep(w / 0.3)[..., None]
        est = v if est is None else est * (1 - a) + v * a
    assert est is not None
    out = img.copy()
    out[hole] = est[hole]
    return out


def delit(domain: Domain, picture: np.ndarray) -> tuple[np.ndarray, np.ndarray, dict[str, Any]]:
    """The picture's colour (linear), its eyes and lips' line filled, divided by the blurred
    shading; the soft validity and stats."""
    m, px_mm = domain.masks, domain.px_mm
    lin = srgb_to_linear(picture.astype(np.float32) / 255)
    hole = eye_and_lip_holes(domain, picture)
    filled = inpaint(lin, hole, m["head"], px_mm)
    light, _ = blur_norm(domain.shading, m["head"].astype(np.float32), SIGMA_D1_MM * px_mm)
    light = np.clip(light / float(np.median(light[m["valid"]])), 0.25, 4.0)
    colour = filled / light[..., None]
    valid = ndimage.binary_erosion(m["head"], iterations=2, border_value=0)
    stats = {
        "filled_px": int(hole.sum()),
        "light_p5_p50_p95": [
            round(float(x), 3) for x in np.percentile(light[m["valid"]], [5, 50, 95])
        ],
    }
    return colour.astype(np.float32), soft(valid, 1.2 * px_mm).astype(np.float32), stats


def project(
    domain: Domain, colour: np.ndarray, valid: np.ndarray, X: np.ndarray, n: np.ndarray
) -> dict[str, np.ndarray]:
    """Per texel (points X, normals n): the weight, the colour and the feature share."""
    cam = domain.view.camera
    size = cam.size
    uv, z = cam.project(X)
    inside = (uv[:, 0] >= 1) & (uv[:, 0] < size - 1) & (uv[:, 1] >= 1) & (uv[:, 1] < size - 1)
    col = np.clip(np.floor(uv[:, 0]).astype(np.int64), 0, size - 1)
    row = np.clip(np.floor(uv[:, 1]).astype(np.int64), 0, size - 1)
    visible = inside & (z <= domain.depth[row, col] + DEPTH_TOL)
    cos = (n * cam.view_dir(X)).sum(1)
    facing = smoothstep((cos - FACING[0]) / (FACING[1] - FACING[0]))
    q = uv + domain.sample(uv)
    inside_q = (q[:, 0] >= 1) & (q[:, 0] < size - 1) & (q[:, 1] >= 1) & (q[:, 1] < size - 1)
    w = (
        (visible & inside_q)
        * facing
        * bilinear(valid, q[:, 0], q[:, 1])
        * bilinear(domain.edge_soft, uv[:, 0], uv[:, 1])
    )
    return {
        "w": w.astype(np.float32),
        "colour": bilinear(colour, q[:, 0], q[:, 1]).astype(np.float32),
        "feat": np.clip(bilinear(domain.features, q[:, 0], q[:, 1]), 0, 1).astype(np.float32),
    }


# ----------------------------------------------------------------------------------------------
# the atlas's head tile


def to_tile(idx: np.ndarray, values: np.ndarray) -> np.ndarray:
    v = np.asarray(values, np.float32).reshape(len(idx), -1)
    img = np.zeros((TILE * TILE, v.shape[1]), np.float32)
    img[idx] = v
    return img.reshape(TILE, TILE, v.shape[1])


def tile_blur(
    values: np.ndarray, weights: np.ndarray, sigma: float
) -> tuple[np.ndarray, np.ndarray]:
    """Normalised Gaussian over the head tile: values (T, T, C), weights (T, T); large sigmas
    on a reduced grid. Returns (values, the blurred weights) at full size."""
    f = 1
    while sigma / (2 * f) >= 3 and f < 16:
        f *= 2
    vw, ww = values * weights[..., None], weights
    if f > 1:
        n = TILE // f
        vw = vw.reshape(n, f, n, f, -1).mean((1, 3))
        ww = weights.reshape(n, f, n, f).mean((1, 3))
    num = np.stack(
        [ndimage.gaussian_filter(vw[..., c], sigma / f) for c in range(vw.shape[-1])], -1
    )
    den = ndimage.gaussian_filter(ww, sigma / f)
    if f > 1:
        num = np.stack([ndimage.zoom(num[..., c], f, order=1) for c in range(num.shape[-1])], -1)
        den = ndimage.zoom(den, f, order=1)
    return num / np.maximum(den, 1e-6)[..., None], den


def fill_holes(
    values: np.ndarray, weights: np.ndarray, idx: np.ndarray, mm_per_texel: float
) -> np.ndarray:
    est = None
    for s in (16.0, 8.0, 4.0, 2.0, 1.0):
        v, w = tile_blur(values, weights, s / mm_per_texel)
        a = smoothstep(w / 0.3)[..., None]
        est = v if est is None else est * (1 - a) + v * a
    assert est is not None
    return est.reshape(-1, est.shape[-1])[idx]


def surface_distance(
    V: np.ndarray, tris: np.ndarray, sources: np.ndarray, limit: float
) -> np.ndarray:
    """Shortest path along the mesh's edges from the nearest source vertex, metres; inf past
    limit."""
    if not len(sources):
        return np.full(len(V), np.inf)
    e = np.concatenate([tris[:, [0, 1]], tris[:, [1, 2]], tris[:, [2, 0]]])
    e = np.unique(np.sort(e, axis=1), axis=0)
    length = np.linalg.norm(V[e[:, 0]] - V[e[:, 1]], axis=1)
    graph = sparse.coo_matrix((length, (e[:, 0], e[:, 1])), shape=(len(V), len(V))).tocsr()
    return dijkstra(graph, directed=False, indices=sources, min_only=True, limit=limit)


def tile_cut(tris: np.ndarray, tris_uv: np.ndarray, uv: np.ndarray) -> np.ndarray:
    """The vertices of the mesh edges where the head tile meets another tile (UV seams)."""
    in_tile = (uv[tris_uv] < 0.5).all(axis=(1, 2))
    ev, eu, et = [], [], []
    for k in range(3):
        a, b = tris[:, k], tris[:, (k + 1) % 3]
        ua, ub = tris_uv[:, k], tris_uv[:, (k + 1) % 3]
        swap = a > b
        ev.append(np.stack([np.where(swap, b, a), np.where(swap, a, b)], 1))
        eu.append(np.stack([np.where(swap, ub, ua), np.where(swap, ua, ub)], 1))
        et.append(np.arange(len(tris)))
    v, u, t = np.concatenate(ev), np.concatenate(eu), np.concatenate(et)
    order = np.lexsort((v[:, 1], v[:, 0]))
    v, u, t = v[order], u[order], t[order]
    i = np.flatnonzero((v[1:] == v[:-1]).all(1))
    j = i + 1
    seam = (u[i] != u[j]).any(1)
    i, j = i[seam], j[seam]
    ti, tj = in_tile[t[i]], in_tile[t[j]]
    cross = ti ^ tj
    return np.unique(v[np.where(ti[cross], i[cross], j[cross])])


def vertex_regions(head: Head, tris: np.ndarray, haircap: np.ndarray, front: View) -> np.ndarray:
    """Per vertex: 0 face (inside the head's own face oval from the front, facing it), 1 ears
    (out of the scalp's ellipsoid at the sides), 2 scalp, 3 neck and the rest."""
    V = head.vertices
    scalp = V[haircap]

    def residual(x: np.ndarray) -> np.ndarray:
        c, a = x[:3], x[3:]
        r = np.linalg.norm((scalp - c) / a, axis=1)
        return (r - 1) * np.linalg.norm(scalp - c, axis=1) / np.maximum(r, 1e-9)

    sol = least_squares(residual, np.concatenate([scalp.mean(0), np.ptp(scalp, axis=0) / 2])).x
    q = (V - sol[:3]) / np.abs(sol[3:])
    r = np.linalg.norm(q, axis=1)
    ears = (
        head.in_head
        & (r > 1.05)
        & (np.abs(q[:, 0]) > 0.75)
        & (q[:, 2] > -0.55)
        & (q[:, 2] < 0.05)
        & (q[:, 1] > -0.75)
        & (q[:, 1] < 0.15)
    )
    cam = front.camera
    oval = polygon_mask((cam.size, cam.size), front.render_points[FACE_OVAL])
    uv, _ = cam.project(V)
    col = np.clip(uv[:, 0].astype(int), 0, cam.size - 1)
    row = np.clip(uv[:, 1].astype(int), 0, cam.size - 1)
    N = vertex_normals(V, tris)
    face = oval[row, col] & ((N * cam.view_dir(V)).sum(1) > 0.2) & ~ears
    on_scalp = np.zeros(len(V), bool)
    on_scalp[haircap] = True
    region = np.full(len(V), 3, np.int8)
    region[on_scalp & ~face & ~ears] = 2
    region[ears] = 1
    region[face] = 0
    return region


@dataclass(frozen=True)
class _Tile:
    """The head tile's texels: the SOMA triangle under each (-1: gutter) and the barycentrics;
    the gutter texels within GUTTER of an island and the island texel nearest each."""

    tri: np.ndarray  # (TILE, TILE) int32
    bary: np.ndarray  # (TILE, TILE, 3) float32
    gutter: np.ndarray  # (G,) flat indices
    nearest: np.ndarray  # (G,) flat indices


class FaceTexture:
    """The pictures' colour on a person's atlas; SOMA's topology loaded once, the head tile's
    raster made at the first person."""

    def __init__(
        self,
        tris: np.ndarray,
        tris_uv: np.ndarray,
        uv_vertex: np.ndarray,
        segments: Mapping[str, np.ndarray],
        soma_root: Path,
        landmarker: Path,
    ) -> None:
        npz = np.load(Path(soma_root) / "SOMA_neutral.npz")
        self.tris, self.tris_uv, self.uv_vertex = tris, tris_uv, uv_vertex
        self.mirror = npz["mirror_vert_indices"].astype(np.int64)
        self.haircap = npz["segment_haircap"].astype(np.int64)
        self.bags = np.concatenate([segments["mouth_bag"], segments["eye_bags"]])
        self.landmarker = Path(landmarker)
        self._tile: _Tile | None = None
        self._cut: np.ndarray | None = None

    def head(self, person: Person) -> Head:
        """The person's SOMA vertices, joints and eyeballs, from the GLB's surfaces."""
        surfaces = {s.name: s for s in person.surfaces}
        body = surfaces["body"]
        count = len(self.mirror)
        vertices = np.zeros((count, 3))
        vertices[self.uv_vertex] = body.positions
        strongest = np.zeros(count, np.int64)
        largest = body.weights.argmax(1)
        strongest[self.uv_vertex] = body.joints[np.arange(len(largest)), largest]
        names, world = person.skeleton.names, person.skeleton.world
        joints = {n: world[i, :3, 3].astype(np.float64) for i, n in enumerate(names)}
        in_head = np.isin(np.asarray(names)[strongest], HEAD_JOINTS)
        eyes = tuple(
            Eye(surfaces[f"eye_{side}"].positions, surfaces[f"eye_{side}"].triangles,
                surfaces[f"eye_{side}"].uv)
            for side in ("left", "right")
        )  # fmt: skip
        assert len(eyes) == 2
        return Head(vertices, joints, in_head, body.uv.astype(np.float64), (eyes[0], eyes[1]))

    def tile(self, uv: np.ndarray) -> _Tile:
        if self._tile is None:
            corners = uv[self.tris_uv]
            ids = np.flatnonzero((corners < 0.5).all(axis=(1, 2)))
            local, bary, _ = rasterize(
                uv * TEXTURE, np.zeros(len(uv)), self.tris_uv[ids], TILE, TILE
            )
            tri = np.where(local >= 0, ids[np.maximum(local, 0)], -1).astype(np.int32)
            covered = tri >= 0
            dist, src = ndimage.distance_transform_edt(~covered, return_indices=True)
            gutter = (~covered) & (dist <= GUTTER)
            self._tile = _Tile(
                tri,
                bary.astype(np.float32),
                np.flatnonzero(gutter),
                (src[0] * TILE + src[1])[gutter],
            )
            self._cut = tile_cut(self.tris, self.tris_uv, uv)
        return self._tile

    def paint(
        self, person: Person, front: np.ndarray, left: np.ndarray, mirrored: bool = False
    ) -> tuple[Person, dict[str, Any]]:
        """The person with the pictures' colour on its atlas; the pictures (H, W, 3) uint8, the
        profile looking to the picture's left (mirrored: looking right, flipped here as the
        reader flipped it). Returns the person and a report."""
        started = time.monotonic()
        head = self.head(person)
        if mirrored:
            left = left[:, ::-1]
        pictures = {"front": square(front), "left": square(left)}
        scene = head_scene(head, self.tris, self.tris_uv)
        views = fit_views(
            self.landmarker, head, scene, person.texture, pictures["front"], pictures["left"]
        )
        fitted = time.monotonic()
        texture, report = self.bake(head, scene, person.texture, views, pictures)
        report["cameras"] = {v.name: v.report for v in views}
        report["picture_px"] = pictures["front"].shape[0]
        report["profile_mirrored"] = mirrored
        report["settings"] = SETTINGS
        report["seconds"] = {
            "cameras": round(fitted - started, 1),
            "texture": round(time.monotonic() - fitted, 1),
        }
        logger.info("pictures' colour on the head: %s s", report["seconds"])
        return replace(person, texture=texture), report

    def bake(
        self,
        head: Head,
        scene: Scene,
        atlas: np.ndarray,
        views: tuple[View, View],
        pictures: Mapping[str, np.ndarray],
    ) -> tuple[np.ndarray, dict[str, Any]]:
        """The atlas (TEXTURE, TEXTURE, 3) uint8 with the pictures' colour on the head tile."""
        tile = self.tile(head.uv)
        tris, V = self.tris, head.vertices
        covered = tile.tri >= 0
        idx = np.flatnonzero(covered.ravel())
        t = tile.tri.ravel()[idx].astype(np.int64)
        b = tile.bary.reshape(-1, 3)[idx].astype(np.float64)
        corners = tris[t]
        N = vertex_normals(V, tris)
        Xt = np.einsum("nk,nkc->nc", b, V[corners])
        nt = np.einsum("nk,nkc->nc", b, N[corners])
        nt /= np.linalg.norm(nt, axis=1, keepdims=True)
        mirror = self.mirror[corners]
        Xm = np.einsum("nk,nkc->nc", b, V[mirror])
        nm = np.einsum("nk,nkc->nc", b, N[mirror])
        nm /= np.linalg.norm(nm, axis=1, keepdims=True)
        del mirror

        # the bags' gate (tight: the lips and lids keep the pictures' colour up to the openings)
        excluded = np.zeros(len(V), bool)
        excluded[self.bags] = True
        bags = cKDTree(V[excluded])
        d_bags, _ = bags.query(V)
        gate_v = smoothstep((d_bags - BAG_GATE_MM[0] / 1000) / (BAG_GATE_MM[1] / 1000))
        gate = (b * gate_v[corners]).sum(1)
        assert self._cut is not None
        cut = self._cut
        d_cut, _ = bags.query(V[cut])
        eye_y = (head.joints["LeftEye"][1] + head.joints["RightEye"][1]) / 2
        neck = cut[(d_cut > 0.005) & (V[cut, 1] < eye_y - 0.08)]  # the neck's cut, not the eyes'

        samples: dict[str, dict[str, np.ndarray]] = {}
        report: dict[str, Any] = {"source": "pictures", "warp": {}, "pictures": {}}
        for view in views:
            domain = picture_domain(view, pictures[view.name], scene)
            colour, valid, stats = delit(domain, pictures[view.name])
            samples[view.name] = project(domain, colour, valid, Xt, nt)
            if view.name == "left":
                samples["mirror"] = project(domain, colour, valid, Xm, nm)
            report["warp"][view.name] = domain.warp
            report["pictures"][view.name] = stats
            del domain, colour, valid
        del Xm, nm
        cx = (head.joints["LeftEye"][0] + head.joints["RightEye"][0]) / 2
        side = smoothstep((Xt[:, 0] - cx) / 0.01 + 0.5)  # 0 on the right, 1 on the left
        samples["left"]["w"] *= side
        samples["mirror"]["w"] *= 1 - side
        for k in samples:
            samples[k]["w"] *= VIEW_PREFERENCE[k] * gate
        ws = np.stack([samples[k]["w"] for k in samples])
        sharp = ws**SHARPEN
        share = sharp / np.maximum(sharp.sum(0), 1e-12)
        coverage = smoothstep(ws.max(0) / 0.6)
        del sharp

        # one gain per channel brings the profile's colour to the front's where both hold
        wf = samples["front"]["w"]
        num, den, overlap = np.zeros(3), np.zeros(3), 0
        for other in ("left", "mirror"):
            m = (wf > 0.3) & (samples[other]["w"] > 0.3)
            ww = np.minimum(wf[m], samples[other]["w"][m])[:, None]
            num += (ww * samples["front"]["colour"][m]).sum(0)
            den += (ww * samples[other]["colour"][m]).sum(0)
            overlap += int(m.sum())
        gain = np.clip(num / np.maximum(den, 1e-9), 0.5, 2.0)
        samples["left"]["colour"] *= gain.astype(np.float32)
        samples["mirror"]["colour"] *= gain.astype(np.float32)
        report["profile_gain"] = [round(float(x), 3) for x in gain]
        report["overlap_texels"] = overlap
        Dp = sum(share[i][:, None] * s["colour"] for i, s in enumerate(samples.values()))
        feat = sum(share[i] * s["feat"] for i, s in enumerate(samples.values()))
        del samples, share, ws
        assert isinstance(Dp, np.ndarray)
        assert isinstance(feat, np.ndarray)

        head_tile = atlas[:TILE, :TILE].astype(np.float32)
        clean = srgb_to_linear(head_tile / 255).reshape(-1, 3)[idx]

        # regions (face, ears, scalp, neck) and the face's texel size
        region_v = vertex_regions(head, tris, self.haircap, views[0])
        nearest = corners[np.arange(len(t)), b.argmax(1)]
        region = region_v[nearest]
        pos = np.full(TILE * TILE, -1, np.int64)
        pos[idx] = np.arange(len(idx))
        right = pos[np.minimum(idx + 1, len(pos) - 1)]
        ok = (right >= 0) & (idx % TILE < TILE - 1) & (region == 0)
        mmpt = float(np.median(np.linalg.norm(Xt[ok] - Xt[right[ok]], axis=1))) * 1000
        del pos, right, ok
        d_ear = surface_distance(
            V, tris, np.flatnonzero(region_v == 1), (EAR_GROW_MM + EAR_FADE_MM) / 1000
        )
        ear_v = smoothstep(1 - (d_ear - EAR_GROW_MM / 1000) / (EAR_FADE_MM / 1000))
        ear_w = (b * ear_v[corners]).sum(1).astype(np.float32)

        # holes inside the face filled; the ears: the atlas's ear at the pictures' tone
        cov_t = to_tile(idx, coverage)[..., 0]
        atlas_t = to_tile(idx, clean)
        round_t = to_tile(idx, coverage * (1 - ear_w))[..., 0]  # the skin round the ears
        Dt = to_tile(idx, Dp)
        fill = fill_holes(Dt, cov_t, idx, mmpt)
        Df = Dp * coverage[:, None] + fill * (1 - coverage[:, None])
        ear_num = tile_blur(Dt, round_t, EAR_MM / mmpt)[0].reshape(-1, 3)[idx]
        ear_den = tile_blur(atlas_t, round_t, EAR_MM / mmpt)[0].reshape(-1, 3)[idx]
        ratio = np.clip(ear_num / np.maximum(ear_den, 1e-5), 0.25, 4.0)
        face = (Df * (1 - ear_w[:, None]) + clean * ratio * ear_w[:, None]).astype(np.float32)
        del Dp, Dt, fill, Df, ear_num, ear_den, ratio, cov_t, round_t

        # the wide fade: the 3D distance to the uncovered region that reaches the neck's cut
        count = np.bincount(nearest, minlength=len(V))
        vertex_cover = np.bincount(nearest, weights=coverage, minlength=len(V))
        in_tile = np.zeros(len(V), bool)
        in_tile[np.unique(corners)] = True
        uncovered = ~in_tile | (vertex_cover / np.maximum(count, 1) < 0.5)
        uncovered[neck] = True
        uncovered[excluded] = False  # the bags never start a fade
        E = np.concatenate([tris[:, [0, 1]], tris[:, [1, 2]], tris[:, [2, 0]]])
        E = E[uncovered[E[:, 0]] & uncovered[E[:, 1]]]
        graph = sparse.coo_matrix((np.ones(len(E)), (E[:, 0], E[:, 1])), shape=(len(V), len(V)))
        _, lab = connected_components(graph, directed=False)
        U = uncovered & np.isin(lab, np.unique(lab[neck]))
        if not U.any():
            U = uncovered
        d_u, _ = cKDTree(V[U]).query(V)
        fade = (b * smoothstep(d_u / (FADE_MM / 1000))[corners]).sum(1).astype(np.float32)
        blend = fade * gate

        # the tone: the face's skin tone pulled halfway to the atlas's
        wt_t = to_tile(idx, coverage * (1 - np.clip(feat, 0, 1)))[..., 0]
        a_low = tile_blur(atlas_t, wt_t, TONE_MM / mmpt)[0].reshape(-1, 3)[idx]
        d_low = tile_blur(to_tile(idx, face), wt_t, TONE_MM / mmpt)[0].reshape(-1, 3)[idx]
        tone = face * (0.5 + 0.5 * a_low / np.maximum(d_low, 1e-5))
        final = clean + blend[:, None] * (tone - clean)
        del atlas_t, wt_t, a_low, d_low

        # on the atlas, the change grown into the gutter round the islands
        delta = np.zeros((TILE * TILE, 3), np.float32)
        delta[idx] = linear_to_srgb(np.clip(final, 0, 1)) * 255 - head_tile.reshape(-1, 3)[idx]
        delta[tile.gutter] = delta[tile.nearest]
        texture = atlas.copy()
        texture[:TILE, :TILE] = np.clip(
            np.clip(head_tile + delta.reshape(TILE, TILE, 3), 0, 255) + 0.5, 0, 255
        ).astype(np.uint8)

        on_face = (region == 0) & (fade > 0.99) & (feat < 0.05)
        report["coverage"] = {
            name: {
                "covered": round(float((coverage[region == r] > 0.5).mean()), 3),
                "full_colour": round(float((blend[region == r] > 0.99).mean()), 3),
            }
            for r, name in enumerate(("face", "ears", "scalp", "neck"))
            if (region == r).any()
        }
        report["face_skin_srgb"] = {
            "pictures": [round(float(x), 1) for x in to_u8(linear_to_srgb(face[on_face])).mean(0)],
            "toned": [round(float(x), 1) for x in to_u8(linear_to_srgb(tone[on_face])).mean(0)],
            "pigments": [round(float(x), 1) for x in to_u8(linear_to_srgb(clean[on_face])).mean(0)],
        }
        report["texel_mm"] = round(mmpt, 4)
        return texture, report
