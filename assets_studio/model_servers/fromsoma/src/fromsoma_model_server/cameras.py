"""Where each picture's camera stood: the front's and the left profile's camera against the
person's head, for projecting the pictures onto the atlas (facetex.py).

MediaPipe's Face Landmarker finds the face on the picture and on a render of the head (its own
atlas, both eyeballs, one Lambert light) from the picture's view at the picture's size; the
render's points are lifted onto the head through the render's triangle and barycentric maps.

- Front: a camera is fitted to (lifted point, picture pixel) pairs, robustly (Huber, 3 px):
  scaled orthographic, and perspective over a series of focal lengths. The head is rendered
  again through the best camera and the fit repeated (three rounds), so MediaPipe sees the head
  as it sees the picture. The face oval, the inner lips and the irises stay out of the pairs.
- Profile: MediaPipe flattens a profile (its cheek and oval points spread as on a 45° view, so a
  fit to all of them turns the camera to a three-quarter view), and it finds no face on a pure
  profile render. So the camera is fitted to the visible eye, iris, nose and lips (the front
  render's points, lifted) and to the outline: the head's silhouette above the chin against the
  picture's on its plain backdrop. The yaw is searched on a grid of 60-97.5°, with pitch, roll,
  scale and offset fitted at each; the scale is held near the front's.

Frames: SOMA's metres, Y up, the face towards +Z, the person's left at +X. Cameras follow
OpenCV (x right, y down, z forward; pixel i spans [i, i + 1)). The pictures are square.
"""

from collections.abc import Callable, Iterator, Sequence
from contextlib import contextmanager
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
from scipy import ndimage

# Face Landmarker's 478 points: groups, the polygons in drawing order
FACE_OVAL = [10, 338, 297, 332, 284, 251, 389, 356, 454, 323, 361, 288, 397, 365, 379, 378, 400,
             377, 152, 148, 176, 149, 150, 136, 172, 58, 132, 93, 234, 127, 162, 21, 54, 103, 67,
             109]  # fmt: skip
LIPS_OUTER = [61, 146, 91, 181, 84, 17, 314, 405, 321, 375, 291, 409, 270, 269, 267, 0, 37, 39, 40,
              185]  # fmt: skip
LIPS_INNER = [78, 95, 88, 178, 87, 14, 317, 402, 318, 324, 308, 415, 310, 311, 312, 13, 82, 81, 80,
              191]  # fmt: skip
EYE_LEFT = [263, 249, 390, 373, 374, 380, 381, 382, 362, 398, 384, 385, 386, 387, 388, 466]
EYE_RIGHT = [33, 7, 163, 144, 145, 153, 154, 155, 133, 173, 157, 158, 159, 160, 161, 246]
BROW_LEFT = [276, 283, 282, 295, 285, 336, 296, 334, 293, 300]  # the person's left
BROW_RIGHT = [46, 53, 52, 65, 55, 107, 66, 105, 63, 70]
IRIS_LEFT = list(range(473, 478))
LEFT_IRIS, CHIN, FOREHEAD = 473, 152, 10

R_VIEW = {  # world -> camera rotation of the canonical views
    "front": np.diag([1.0, -1.0, -1.0]),  # camera on +Z; the person's left on the picture's right
    "left": np.array([[0.0, 0.0, -1.0], [0.0, -1.0, 0.0], [-1.0, 0.0, 0.0]]),  # on +X, nose left
}
FOCALS_MM = (35, 50, 85, 135, 200, 300, 500, 1000, None)  # 36 mm across; None: orthographic
FRONT_SKIP = set(FACE_OVAL) | set(LIPS_INNER) | set(range(468, 478))
PROFILE_FEATURES = sorted(
    set(EYE_LEFT + IRIS_LEFT)
    | {1, 4, 5, 195, 197, 6, 168, 2, 94, 19, 279, 358, 294, 327, 278, 439, 455}  # nose
    | {291, 375, 321, 405, 314, 17, 0, 267, 269, 270, 409}  # lips
)
PROFILE_WARP_EXTRA = BROW_LEFT  # brows move with the expression: warped, not used for the camera
PROFILE_YAWS = np.arange(60.0, 97.6, 2.5)
SCALE_PRIOR = 0.04  # the profile's scale against the front's (both framed alike)
TURNS = (0, 5, 10, 15, 20, 25, 30, 35)  # degrees towards the camera when no face is found
# OpenCV's 7x7 ellipse: the backdrop mask's opening and closing
ELLIPSE = np.array([[0, 0, 0, 1, 0, 0, 0]] + [[0, 1, 1, 1, 1, 1, 0]] + [[1] * 7] * 3
                   + [[0, 1, 1, 1, 1, 1, 0]] + [[0, 0, 0, 1, 0, 0, 0]], dtype=bool)  # fmt: skip

Detect = Callable[[np.ndarray], np.ndarray | None]
Maps = tuple[np.ndarray, np.ndarray, np.ndarray]  # triangle (H, W), barycentrics, depth


class CameraError(RuntimeError):
    """A picture's camera could not be found; the message says why."""


@dataclass(frozen=True)
class Eye:
    vertices: np.ndarray  # (N, 3)
    triangles: np.ndarray  # (T, 3)
    uv: np.ndarray  # (N, 2) in the atlas


@dataclass(frozen=True)
class Head:
    """A person for the cameras and the texture, in SOMA's bind pose."""

    vertices: np.ndarray  # (Vs, 3) SOMA's vertices
    joints: dict[str, np.ndarray]  # bind-pose positions by name
    in_head: np.ndarray  # (Vs,) weighted most to the head, the jaw or an eye
    uv: np.ndarray  # (U, 2) each UV point's atlas UV (glTF's: v from the top)
    eyes: tuple[Eye, Eye]  # left, right


@dataclass(frozen=True)
class Scene:
    """The head's skin above the neck and both eyeballs as one triangle soup."""

    vertices: np.ndarray
    triangles: np.ndarray
    corner_uv: np.ndarray  # (T, 3, 2)
    normals: np.ndarray
    skin: int  # the first `skin` triangles are the head's skin


# ----------------------------------------------------------------------------------------------
# colour and sampling


def srgb_to_linear(x: Any) -> np.ndarray:
    x = np.asarray(x, np.float32)
    return np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4).astype(np.float32)


def linear_to_srgb(x: Any) -> np.ndarray:
    x = np.clip(np.asarray(x, np.float32), 0, 1)
    return np.where(x <= 0.0031308, 12.92 * x, 1.055 * np.power(x, 1 / 2.4) - 0.055).astype(
        np.float32
    )


def to_u8(srgb: Any) -> np.ndarray:
    return np.clip(np.asarray(srgb) * 255 + 0.5, 0, 255).astype(np.uint8)


def luminance(rgb: np.ndarray) -> np.ndarray:
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def bilinear(img: np.ndarray, x: Any, y: Any) -> np.ndarray:
    """img (H, W[, C]) at pixel coordinates x, y (pixel centres at i + 0.5), clamped."""
    H, W = img.shape[:2]
    x = np.clip(np.asarray(x, np.float64) - 0.5, 0, W - 1.000001)
    y = np.clip(np.asarray(y, np.float64) - 0.5, 0, H - 1.000001)
    x0, y0 = x.astype(np.int64), y.astype(np.int64)
    x1, y1 = np.minimum(x0 + 1, W - 1), np.minimum(y0 + 1, H - 1)
    fx, fy = x - x0, y - y0
    if img.ndim == 3:
        fx, fy = fx[..., None], fy[..., None]
    a = img[y0, x0] * (1 - fx) + img[y0, x1] * fx
    b = img[y1, x0] * (1 - fx) + img[y1, x1] * fx
    return a * (1 - fy) + b * fy


def vertex_normals(vertices: np.ndarray, tris: np.ndarray) -> np.ndarray:
    c = vertices[tris]
    face = np.cross(c[:, 1] - c[:, 0], c[:, 2] - c[:, 0])
    n = np.zeros_like(vertices)
    for k in range(3):
        np.add.at(n, tris[:, k], face)
    return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)


# ----------------------------------------------------------------------------------------------
# rasteriser


def rasterize(
    xy: np.ndarray,
    z: np.ndarray,
    tris: np.ndarray,
    width: int,
    height: int,
    perspective: bool = False,
    chunk: int = 3_000_000,
) -> Maps:
    """Nearest-surface rasterisation: xy (N, 2) pixel coordinates, z (N,) depth (smaller is
    nearer), tris (T, 3); pixel centres at i + 0.5. Returns the triangle per pixel (H, W) (-1:
    none), the barycentrics (H, W, 3) (perspective-correct when asked) and the depth (inf:
    none); ties go to the triangle met first."""
    P = xy[tris]  # (T, 3, 2)
    Z = z[tris]
    x0 = np.maximum(np.ceil(P[:, :, 0].min(1) - 0.5).astype(np.int64), 0)
    x1 = np.minimum(np.floor(P[:, :, 0].max(1) - 0.5).astype(np.int64), width - 1)
    y0 = np.maximum(np.ceil(P[:, :, 1].min(1) - 0.5).astype(np.int64), 0)
    y1 = np.minimum(np.floor(P[:, :, 1].max(1) - 0.5).astype(np.int64), height - 1)
    area = (P[:, 1, 0] - P[:, 0, 0]) * (P[:, 2, 1] - P[:, 0, 1]) - (P[:, 2, 0] - P[:, 0, 0]) * (
        P[:, 1, 1] - P[:, 0, 1]
    )
    ok = (x1 >= x0) & (y1 >= y0) & (np.abs(area) > 1e-12)
    span = np.maximum(x1 - x0 + 1, y1 - y0 + 1)
    frags = []
    sizes = [1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 4096]
    for lo, hi in zip([0, *sizes[:-1]], sizes, strict=True):  # triangles by their box's size
        sel = np.flatnonzero(ok & (span > lo) & (span <= hi))
        g = np.arange(hi)
        per = max(1, chunk // (hi * hi))
        for start in range(0, len(sel), per):
            t = sel[start : start + per]
            gx = x0[t, None, None] + g[None, None, :]
            gy = y0[t, None, None] + g[None, :, None]
            inb = (gx <= x1[t, None, None]) & (gy <= y1[t, None, None])
            px, py = gx + 0.5, gy + 0.5
            p = P[t]
            a = area[t][:, None, None]
            w0 = (
                (p[:, 1, 0, None, None] - px) * (p[:, 2, 1, None, None] - py)
                - (p[:, 2, 0, None, None] - px) * (p[:, 1, 1, None, None] - py)
            ) / a
            w1 = (
                (p[:, 2, 0, None, None] - px) * (p[:, 0, 1, None, None] - py)
                - (p[:, 0, 0, None, None] - px) * (p[:, 2, 1, None, None] - py)
            ) / a
            w2 = 1 - w0 - w1
            inside = inb & (w0 >= -1e-7) & (w1 >= -1e-7) & (w2 >= -1e-7)
            k, iy, ix = np.nonzero(inside)
            if not len(k):
                continue
            b = np.stack([w0[k, iy, ix], w1[k, iy, ix], w2[k, iy, ix]], 1)
            zt = Z[t[k]]
            if perspective:
                inv = b / zt
                s = inv.sum(1, keepdims=True)
                depth = 1.0 / s[:, 0]
                b = inv / s
            else:
                depth = (b * zt).sum(1)
            pix = gy[k, iy, 0] * width + gx[k, 0, ix]
            frags.append((pix, depth, t[k], b.astype(np.float32)))
    tri = np.full(width * height, -1, np.int64)
    bary = np.zeros((width * height, 3), np.float32)
    dep = np.full(width * height, np.inf, np.float64)
    if frags:
        pix = np.concatenate([f[0] for f in frags])
        depth = np.concatenate([f[1] for f in frags])
        tid = np.concatenate([f[2] for f in frags])
        bb = np.concatenate([f[3] for f in frags])
        order = np.lexsort((depth, pix))
        pix, depth, tid, bb = pix[order], depth[order], tid[order], bb[order]
        first = np.ones(len(pix), bool)
        first[1:] = pix[1:] != pix[:-1]
        tri[pix[first]] = tid[first]
        bary[pix[first]] = bb[first]
        dep[pix[first]] = depth[first]
    return tri.reshape(height, width), bary.reshape(height, width, 3), dep.reshape(height, width)


# ----------------------------------------------------------------------------------------------
# cameras


def rodrigues(w: Sequence[float] | np.ndarray) -> np.ndarray:
    w = np.asarray(w, np.float64)
    th = np.linalg.norm(w)
    if th < 1e-12:
        return np.eye(3)
    k = w / th
    K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + np.sin(th) * K + (1 - np.cos(th)) * K @ K


class Camera:
    """World -> pixels: perspective (f in pixels, the principal point at the picture's centre)
    or scaled orthographic (f None: s pixels per metre, the picture offset c)."""

    def __init__(
        self,
        R: Any,
        t: Any,
        size: int,
        f: float | None = None,
        s: float | None = None,
        c: Any = None,
    ) -> None:
        self.R, self.t, self.size = np.asarray(R, np.float64), np.asarray(t, np.float64), int(size)
        self.f, self.s = f, s
        self.c = np.asarray(c if c is not None else (size / 2, size / 2), np.float64)

    @property
    def ortho(self) -> bool:
        return self.f is None

    def to_camera(self, X: np.ndarray) -> np.ndarray:
        return np.asarray(X, np.float64) @ self.R.T + self.t

    def project(self, X: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
        Xc = self.to_camera(X)
        if self.f is None:
            return self.s * Xc[:, :2] + self.c, Xc[:, 2]
        return self.f * Xc[:, :2] / Xc[:, 2:3] + self.c, Xc[:, 2]

    def view_dir(self, X: np.ndarray) -> np.ndarray:
        """Unit vectors from the points towards the camera (world frame)."""
        if self.ortho:
            return np.broadcast_to(-self.R[2], np.shape(X)).copy()
        v = -self.R.T @ self.t - np.asarray(X)
        return v / np.linalg.norm(v, axis=-1, keepdims=True)

    def px_per_m(self, X: np.ndarray) -> np.ndarray:
        if self.f is None:
            return np.full(len(X), self.s)
        return self.f / self.to_camera(X)[:, 2]

    def describe(self) -> dict[str, Any]:
        return {
            "rotation": np.round(self.R, 5).tolist(),
            "translation": np.round(self.t, 5).tolist(),
            "focal_px": None if self.f is None else round(self.f, 2),
            "px_per_m": None if self.s is None else round(self.s, 2),
            "offset_px": np.round(self.c, 2).tolist(),
        }


def levenberg(
    residual: Callable[[np.ndarray], np.ndarray],
    params: np.ndarray,
    iterations: int = 60,
    huber: float | None = None,
    groups: int = 1,
    weights: np.ndarray | None = None,
) -> tuple[np.ndarray, np.ndarray]:
    """Robust Levenberg-Marquardt: residual(params) -> (M,), numeric Jacobian. huber (None:
    plain least squares) applies per group of `groups` consecutive values (2 for pixel pairs);
    weights per group multiply it."""
    lam = 1e-3
    r = residual(params)
    for _ in range(iterations):
        norm = np.linalg.norm(r.reshape(-1, groups), axis=1)
        w = (
            np.ones(len(norm))
            if huber is None
            else np.where(norm <= huber, 1.0, huber / np.maximum(norm, 1e-12))
        )
        wr = np.repeat(w if weights is None else weights * w, groups)
        J = np.zeros((len(r), len(params)))
        for j in range(len(params)):
            step = 1e-6 * max(1.0, abs(params[j]))
            d = np.zeros_like(params)
            d[j] = step
            J[:, j] = (residual(params + d) - residual(params - d)) / (2 * step)
        A = J.T @ (J * wr[:, None])
        g = J.T @ (wr * r)
        cost = (wr * r * r).sum()
        improved = False
        delta = np.zeros_like(params)
        for _ in range(10):
            delta = np.linalg.solve(A + lam * np.diag(np.diag(A) + 1e-9), -g)
            trial = params + delta
            rt = residual(trial)
            if (wr * rt * rt).sum() < cost:
                params, r, lam, improved = trial, rt, max(lam / 3, 1e-9), True
                break
            lam *= 4
        if not improved or np.abs(delta).max() < 1e-10:
            break
    return params, r


def _posed(start: Camera, f: float | None) -> Callable[[np.ndarray], Camera]:
    """A camera from (rotation vector after start's rotation, log scale and offset) or
    (rotation vector, translation)."""

    def make(p: np.ndarray) -> Camera:
        R = rodrigues(p[:3]) @ start.R
        if f is None:
            return Camera(R, np.zeros(3), start.size, None, float(np.exp(p[3])), p[4:6])
        return Camera(R, p[3:6], start.size, f)

    return make


def _reprojection(
    make: Callable[[np.ndarray], Camera], X: np.ndarray, uv: np.ndarray
) -> Callable[[np.ndarray], np.ndarray]:
    def residual(p: np.ndarray) -> np.ndarray:
        return (make(p).project(X)[0] - uv).ravel()

    return residual


def fit_camera(
    X: np.ndarray,
    uv: np.ndarray,
    start: Camera,
    f: float | None = None,
    weights: np.ndarray | None = None,
) -> tuple[Camera, np.ndarray]:
    """A camera fitted to 3D points X (M, 3) and their pixels uv (M, 2), robustly (Huber 3 px),
    from start's rotation; f the focal length in pixels (None: scaled orthographic). Returns
    the camera and each point's error (pixels)."""
    Xr = X @ start.R.T
    spread = np.sqrt(((uv - uv.mean(0)) ** 2).sum(1).mean())
    s0 = spread / np.sqrt(((Xr[:, :2] - Xr[:, :2].mean(0)) ** 2).sum(1).mean())
    if f is None:
        if start.ortho and start.s and abs(np.log(start.s / s0)) < 0.5:
            s0 = start.s
        params = np.concatenate([[0, 0, 0], [np.log(s0)], uv.mean(0) - s0 * Xr[:, :2].mean(0)])
    else:
        centre = np.array([start.size / 2, start.size / 2])
        tz = f / s0 - Xr[:, 2].mean()
        txy = (uv.mean(0) - centre) / f * (Xr[:, 2].mean() + tz) - Xr[:, :2].mean(0)
        params = np.concatenate([[0, 0, 0], txy, [tz]])
    make = _posed(start, f)
    prior = np.ones(len(X)) if weights is None else np.asarray(weights, np.float64)
    params, r = levenberg(
        _reprojection(make, X, uv), params, iterations=60, huber=3.0, groups=2, weights=prior
    )
    return make(params), np.linalg.norm(r.reshape(-1, 2), axis=1)


# ----------------------------------------------------------------------------------------------
# the head and its renders


def head_scene(head: Head, tris: np.ndarray, tris_uv: np.ndarray, below: float = 0.05) -> Scene:
    """The skin triangles with every corner above the Neck1 joint less `below`, and the eyes."""
    keep = head.vertices[:, 1] > head.joints["Neck1"][1] - below
    ids = np.flatnonzero(keep[tris].all(1))
    verts, faces = [head.vertices], [tris[ids]]
    uvs, norms = [head.uv[tris_uv[ids]]], [vertex_normals(head.vertices, tris)]
    offset = len(head.vertices)
    for eye in head.eyes:
        ev, et = eye.vertices.astype(np.float64), eye.triangles.astype(np.int64)
        verts.append(ev)
        faces.append(et + offset)
        uvs.append(eye.uv.astype(np.float64)[et])
        norms.append(vertex_normals(ev, et))
        offset += len(ev)
    return Scene(
        np.concatenate(verts),
        np.concatenate(faces),
        np.concatenate(uvs),
        np.concatenate(norms),
        len(ids),
    )


def raster(cam: Camera, scene: Scene) -> Maps:
    xy, z = cam.project(scene.vertices)
    return rasterize(xy, z, scene.triangles, cam.size, cam.size, perspective=not cam.ortho)


def render(cam: Camera, scene: Scene, atlas: np.ndarray) -> tuple[np.ndarray, Maps]:
    """A Lambert render of the textured head through cam (light from above the camera, grey
    backdrop); atlas: (S, S, 3) uint8 sRGB. Returns the sRGB picture and the maps."""
    maps = raster(cam, scene)
    tri, bary, _ = maps
    img = np.empty((cam.size, cam.size, 3), np.float32)
    img[:] = srgb_to_linear(np.full(3, 180, np.float32) / 255)
    m = tri >= 0
    t = tri[m]
    b = bary[m].astype(np.float64)
    uv = np.einsum("nk,nkc->nc", b, scene.corner_uv[t])
    colour = srgb_to_linear(
        bilinear(atlas, uv[:, 0] * atlas.shape[1], uv[:, 1] * atlas.shape[0]) / 255.0
    )
    n = np.einsum("nk,nkc->nc", b, scene.normals[scene.triangles[t]])
    n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
    light = np.array([0.0, -0.55, -1.0])
    shade = 0.35 + 0.8 * np.clip((n @ cam.R.T) @ (light / np.linalg.norm(light)), 0, 1)
    img[m] = colour * shade[:, None]
    return to_u8(linear_to_srgb(img)), maps


def lift(points: np.ndarray, maps: Maps, scene: Scene) -> tuple[np.ndarray, np.ndarray]:
    """Pixels (M, 2) -> the surface point under each (M, 3) and its triangle (-1: none)."""
    tri, bary, _ = maps
    H, W = tri.shape
    col = np.clip(np.floor(points[:, 0]).astype(np.int64), 0, W - 1)
    row = np.clip(np.floor(points[:, 1]).astype(np.int64), 0, H - 1)
    t = tri[row, col]
    out = np.zeros((len(points), 3))
    ok = t >= 0
    out[ok] = np.einsum(
        "nk,nkc->nc",
        bary[row[ok], col[ok]].astype(np.float64),
        scene.vertices[scene.triangles[t[ok]]],
    )
    return out, t


@contextmanager
def landmarker(model: Path) -> Iterator[Detect]:
    """MediaPipe's Face Landmarker for one person's pictures and renders, closed after."""
    import mediapipe as mp
    from mediapipe.tasks.python import BaseOptions
    from mediapipe.tasks.python.vision import FaceLandmarker, FaceLandmarkerOptions

    options = FaceLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=str(model)),
        num_faces=1,
        min_face_detection_confidence=0.05,
        min_face_presence_confidence=0.05,
    )
    with FaceLandmarker.create_from_options(options) as found:

        def detect(img: np.ndarray) -> np.ndarray | None:
            image = mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(img))
            faces = found.detect(image).face_landmarks
            if not faces:
                return None
            h, w = img.shape[:2]
            return np.array([[p.x * w, p.y * h] for p in faces[0]])

        yield detect


def head_centre(head: Head) -> np.ndarray:
    return (head.joints["LeftEye"] + head.joints["RightEye"]) / 2 + np.array([0.0, 0.0, -0.08])


def turned(cam: Camera, angle_deg: float, centre: np.ndarray) -> Camera:
    """The camera seeing the head turned by angle about the vertical axis through centre
    (positive: the face turns towards +X, the left profile's camera)."""
    rot = rodrigues([0.0, np.radians(angle_deg), 0.0])
    shift = cam.R @ (centre - rot @ centre)
    if cam.f is None:
        return Camera(cam.R @ rot, cam.t, cam.size, None, cam.s, cam.c + cam.s * shift[:2])
    return Camera(cam.R @ rot, cam.t + shift, cam.size, cam.f)


def detect_render(
    detect: Detect, cam: Camera, scene: Scene, atlas: np.ndarray, centre: np.ndarray
) -> tuple[np.ndarray | None, Maps | None, int | None]:
    """The landmarks on the head rendered through cam; when MediaPipe finds no face (a pure
    profile), on the head turned towards the camera in 5° steps up to 35°. Returns the points,
    the maps of the render they were found on and the turn."""
    for angle in TURNS:
        img, maps = render(turned(cam, angle, centre) if angle else cam, scene, atlas)
        points = detect(img)
        if points is not None:
            return points, maps, angle
    return None, None, None


def initial_camera(head: Head, view: str, size: int, points: np.ndarray) -> Camera:
    eyes = (head.joints["LeftEye"] + head.joints["RightEye"]) / 2
    s = 0.5 * size / (2 * (head.vertices[:, 1].max() - eyes[1]))
    R = R_VIEW[view]
    if view == "front":
        c = (points[468] + points[473]) / 2 - s * (R @ eyes)[:2]
    else:
        c = points[LEFT_IRIS] - s * (R @ head.joints["LeftEye"])[:2]  # the visible iris
    return Camera(R, np.zeros(3), size, None, s, c)


@dataclass(frozen=True)
class View:
    """A picture's camera and what the texture's warp needs from its fit."""

    name: str  # "front" or "left"
    camera: Camera
    points: np.ndarray  # the picture's landmarks (478, 2)
    head_points: np.ndarray  # the head's landmarks, lifted (478, 3): the front render's
    lifted: np.ndarray  # (478,) the triangle under each (-1: none)
    pairs: np.ndarray  # the profile's landmark pairs for the warp
    maps: Maps  # the head rendered through the camera
    px_per_mm: float
    render_points: np.ndarray  # the front render's landmarks (pixels)
    outline: tuple[np.ndarray, np.ndarray] | None  # profile: silhouette points, their targets
    report: dict[str, Any]


def _robust(errors: np.ndarray) -> float:
    return float(np.mean(np.minimum(errors, 3.0) ** 2 / 2 + np.maximum(errors - 3.0, 0) * 3.0))


def _errors(cam: Camera, X: np.ndarray, points: np.ndarray, ids: np.ndarray) -> dict[str, Any]:
    e = np.linalg.norm(cam.project(X[ids])[0] - points[ids], axis=1)
    return {
        "pairs": len(ids),
        "median_px": round(float(np.median(e)), 2),
        "p90_px": round(float(np.percentile(e, 90)), 2),
    }


def fit_front(
    detect: Detect, head: Head, scene: Scene, atlas: np.ndarray, points: np.ndarray, size: int
) -> View:
    """The front picture's camera from (lifted head landmark, picture landmark) pairs."""
    cam = initial_camera(head, "front", size, points)
    centre = head_centre(head)
    focal: int | None = None
    ids = np.zeros(0, np.int64)
    for round_ in range(3):
        found, maps, _ = detect_render(detect, cam, scene, atlas, centre)
        if found is None or maps is None:
            raise CameraError(f"no face on the head's front render (round {round_ + 1})")
        X, t = lift(found, maps, scene)
        ids = np.array([i for i in range(468) if i not in FRONT_SKIP and t[i] >= 0])
        fits = {}
        for mm in (None,) if round_ == 0 else FOCALS_MM:
            f = None if mm is None else mm / 36.0 * size
            fits[mm] = fit_camera(X[ids], points[ids], cam, f=f, weights=np.ones(len(ids)))
        costs = {mm: _robust(errors) for mm, (_, errors) in fits.items()}
        focal = min(costs, key=costs.__getitem__)
        cam = fits[focal][0]
    found, maps, turn = detect_render(detect, cam, scene, atlas, centre)
    if found is None or maps is None:
        raise CameraError("no face on the head's front render")
    X, t = lift(found, maps, scene)
    px_per_mm = round(float(np.median(cam.px_per_m(X[ids]))) / 1000, 3)
    report = {
        **_errors(cam, X, points, ids),
        "focal_mm": focal,  # 36 mm across the picture; None: orthographic
        "px_per_mm": px_per_mm,
        "render_turned_deg": turn,
    }
    return View("front", cam, points, X, t, ids, raster(cam, scene), px_per_mm, found, None, report)


def picture_silhouette(picture: np.ndarray, tolerance: float = 14) -> np.ndarray:
    """The signed distance (pixels, positive outside) to the figure on the plain backdrop: what
    differs from the border's median colour, without the parts cut off from the border."""
    border = np.concatenate([picture[0], picture[-1], picture[:, 0], picture[:, -1]])
    diff = np.abs(picture.astype(np.float32) - np.median(border.astype(np.float32), 0)).max(2)
    lab, _ = ndimage.label(diff < tolerance)
    edge = np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))
    figure = ~np.isin(lab, edge[edge > 0])
    # opened and closed as OpenCV does (the border never erodes nor dilates)
    opened = ndimage.binary_erosion(figure, ELLIPSE, border_value=1)
    figure = ndimage.binary_dilation(opened, ELLIPSE)
    closed = ndimage.binary_dilation(figure, ELLIPSE)
    figure = ndimage.binary_erosion(closed, ELLIPSE, border_value=1)
    outside = ndimage.distance_transform_edt(~figure)
    inside = ndimage.distance_transform_edt(figure)
    return (outside - inside).astype(np.float32)


def silhouette_points(
    maps: Maps, scene: Scene, limits: tuple[float, float, float], step: int = 2
) -> np.ndarray:
    """The render's outline, lifted onto the head: the face's side above limits[0], the back of
    the head (right of column limits[2]) above limits[1] only (the nape and the neck differ
    most between the head and the pictures)."""
    tri, bary, _ = maps
    m = tri >= 0
    edge = m & ~(np.roll(m, 1, 0) & np.roll(m, -1, 0) & np.roll(m, 1, 1) & np.roll(m, -1, 1))
    rows, cols = np.nonzero(edge)
    front_limit, back_limit, split = limits
    keep = (rows < front_limit) & ((cols < split) | (rows < back_limit))
    rows, cols = rows[keep][::step], cols[keep][::step]
    t = tri[rows, cols]
    return np.einsum(
        "nk,nkc->nc",
        bary[rows, cols].astype(np.float64),
        scene.vertices[scene.triangles[t]],
    )


def yaw_of(cam: Camera) -> float:
    d = cam.R[2]  # the camera's forward axis in the world
    return float(np.degrees(np.arctan2(-d[0], -d[2])))


@dataclass(frozen=True)
class _Outline:
    """What the profile's camera is fitted to."""

    X: np.ndarray  # the visible features' head points (F, 3)
    uv: np.ndarray  # and their pixels (F, 2)
    sdf: np.ndarray  # the picture's outline, signed distance
    limits: tuple[float, float, float]  # silhouette_points' rows and column
    s_front: float  # the front's pixels per metre
    eye: np.ndarray  # the visible iris's head point, kept in place when the yaw changes


def _tilted(base: Camera) -> Callable[[np.ndarray], Camera]:
    """Scaled orthographic cameras pitched and rolled from base in its own frame."""

    def make(p: np.ndarray) -> Camera:
        R = rodrigues([p[0], 0.0, p[1]]) @ base.R
        return Camera(R, np.zeros(3), base.size, None, float(np.exp(p[2])), p[3:5])

    return make


def _outline_residual(
    make: Callable[[np.ndarray], Camera],
    outline: _Outline,
    w_features: np.ndarray,
    points: np.ndarray,
    b: float,
    w_points: np.ndarray,
) -> Callable[[np.ndarray], np.ndarray]:
    def residual(p: np.ndarray) -> np.ndarray:
        c = make(p)
        rf = (c.project(outline.X)[0] - outline.uv) * w_features[:, None]
        q = c.project(points)[0]
        rs = bilinear(outline.sdf, q[:, 0], q[:, 1]) * b * w_points
        prior = [0.3 * (p[2] - np.log(outline.s_front)) / SCALE_PRIOR]
        return np.concatenate([rf.ravel(), rs, prior])

    return residual


def _huber(e: np.ndarray, k: float = 4.0) -> np.ndarray:
    return np.where(e <= k, 0.5 * e * e, k * (e - 0.5 * k))


def profile_cost(
    scene: Scene, outline: _Outline, start: Camera, yaw: float, rounds: int = 2
) -> tuple[Camera, float, float, float]:
    """Pitch, roll, scale and offset fitted at a fixed yaw, the scale held near the front's
    (else the outline can shrink the head onto the picture's); the camera, the robust cost and
    the medians (features, outline), in pixels."""
    R = R_VIEW["front"] @ rodrigues([0.0, -np.radians(yaw), 0.0])
    cam = Camera(R, np.zeros(3), start.size, None, start.s, start.c)
    cam.c = start.project(outline.eye[None])[0][0] - cam.s * (cam.R @ outline.eye)[:2]
    w_features = np.ones(len(outline.X))
    ef = es = np.zeros(1)
    for _ in range(rounds):
        points = silhouette_points(raster(cam, scene), scene, outline.limits, step=3)
        w_points = np.ones(len(points))
        a, b = np.sqrt(0.5 / len(outline.X)), np.sqrt(0.5 / len(points))
        make = _tilted(cam)
        params = np.concatenate([[0.0, 0.0], [np.log(cam.s)], cam.c])
        for _ in range(3):
            residual = _outline_residual(
                make, outline, a * np.sqrt(w_features), points, b, np.sqrt(w_points)
            )
            params, _ = levenberg(residual, params, iterations=30)
            cam = make(params)
            ef = np.linalg.norm(cam.project(outline.X)[0] - outline.uv, axis=1)
            es = np.abs(bilinear(outline.sdf, *cam.project(points)[0].T))
            w_features = np.where(ef <= 4, 1.0, 4 / np.maximum(ef, 1e-9))
            w_points = np.where(es <= 4, 1.0, 4 / np.maximum(es, 1e-9))
    cost = 0.5 * _huber(ef).mean() + 0.5 * _huber(es).mean()
    return cam, float(cost), float(np.median(ef)), float(np.median(es))


def fit_profile(
    head: Head, scene: Scene, picture: np.ndarray, points: np.ndarray, front: View
) -> View:
    """The left profile's camera from its outline and the visible eye, iris, nose and lips:
    the yaw on a grid of 2.5° (then ±0.75° and ±1.5° round the best), pitch, roll, scale and
    offset fitted at each (a free fit oscillates between 65 and 71° on some heads)."""
    size = picture.shape[0]
    X, t = front.head_points, front.lifted
    head_px = 2 * (points[CHIN][1] - points[FOREHEAD][1])
    iris = points[LEFT_IRIS]
    limits = (
        points[CHIN][1] + 0.02 * head_px,
        iris[1] + 0.12 * head_px,
        iris[0] + 0.3 * head_px,
    )
    feats = np.array([i for i in PROFILE_FEATURES if t[i] >= 0])
    s_front = front.px_per_mm * 1000
    sdf = picture_silhouette(picture)
    eye = X[LEFT_IRIS] if t[LEFT_IRIS] >= 0 else X[feats].mean(0)
    outline = _Outline(X[feats], points[feats], sdf, limits, s_front, eye)
    first = initial_camera(head, "left", size, points)
    shift = (s_front - (first.s or s_front)) * (first.R @ head.joints["LeftEye"])[:2]
    start = Camera(first.R, first.t, size, None, s_front, first.c - shift)

    grid = {float(yaw): profile_cost(scene, outline, start, yaw) for yaw in PROFILE_YAWS}
    best = min(grid, key=lambda k: grid[k][1])
    near = grid[best][0]
    for yaw in (best - 1.5, best - 0.75, best + 0.75, best + 1.5):
        grid[yaw] = profile_cost(scene, outline, near, yaw)
    best = min(grid, key=lambda k: grid[k][1])
    cam, _, features_px, _ = profile_cost(scene, outline, grid[best][0], best, rounds=3)

    maps = raster(cam, scene)
    sil = silhouette_points(maps, scene, limits)
    q = cam.project(sil)[0]
    d = bilinear(sdf, q[:, 0], q[:, 1])
    gy, gx = np.gradient(sdf)
    g = np.stack([bilinear(gx, q[:, 0], q[:, 1]), bilinear(gy, q[:, 0], q[:, 1])], 1)
    g /= np.maximum(np.linalg.norm(g, axis=1, keepdims=True), 1e-6)
    target = q - d[:, None] * g  # the nearest point of the picture's outline
    pairs = np.array(sorted(set(feats.tolist()) | {i for i in PROFILE_WARP_EXTRA if t[i] >= 0}))
    px_per_mm = round(float(np.median(cam.px_per_m(X[feats]))) / 1000, 3)
    report = {
        **_errors(cam, X, points, pairs),
        "features_median_px": round(features_px, 2),
        "outline_median_px": round(float(np.median(np.abs(d))), 2),
        "outline_p90_px": round(float(np.percentile(np.abs(d), 90)), 2),
        "yaw_deg": round(yaw_of(cam), 2),
        "scale_to_front": round(float(cam.s or 0) / s_front, 4),
        "px_per_mm": px_per_mm,
    }
    return View(
        "left",
        cam,
        points,
        X,
        t,
        pairs,
        maps,
        px_per_mm,
        front.render_points,
        (sil, target),
        report,
    )


def fit_views(
    model: Path,
    head: Head,
    scene: Scene,
    atlas: np.ndarray,
    front: np.ndarray,
    left: np.ndarray,
) -> tuple[View, View]:
    """Both pictures' cameras; the pictures (S, S, 3) uint8, the profile looking left."""
    with landmarker(model) as detect:
        points = []
        for name, picture in (("front", front), ("profile", left)):
            found = detect(picture)
            if found is None:
                raise CameraError(f"no face found on the {name} picture")
            points.append(found)
        front_view = fit_front(detect, head, scene, atlas, points[0], front.shape[0])
    return front_view, fit_profile(head, scene, left, points[1], front_view)
