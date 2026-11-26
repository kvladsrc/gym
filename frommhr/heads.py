"""The renderer of the training pictures: orthographic front and left-profile close-ups of heads
on a plain backdrop, in random light, from per-vertex skin colours and texture masks.

Anny (NAVER's parametric body, Apache-2.0) supplies the feature masks in its MakeHuman UVs:
MPFB2's lip, eyelid and ear masks (CC0) and three eyebrow shapes drawn here on its template;
prep.py bakes them into SOMA's UVs for the MHR people of mhr_heads.py, which renders with this
class's `render`. The eyes get a sclera, an iris with a darker rim and a pupil, the lid rims
lashes; the light is a key and a fill with wrapped diffuse, sky and ground light, screen-space
ambient occlusion and a sheen. Looks draws the random light and picture settings; colour.py
turns a look into a colour one from pigment sliders.

Anny's frame: Z up, the figure faces -Y, its left is +X; metres."""

import math

import anny
import torch
import torch.nn.functional as F
from anny.face_segmentation import get_face_segmentation_mask
from PIL import Image

from raster import interpolate, rasterize, vertex_normals

DATA = anny.paths.get_anny_root_dir() / "data"
SKIN, EYE, EYE_CAVITY, MOUTH_CAVITY, TONGUE = range(5)
MATERIALS = {
    "eye_front.L": EYE,
    "eye_back.L": EYE,
    "eye_front.R": EYE,
    "eye_back.R": EYE,
    "eye_cavity.L": EYE_CAVITY,
    "eye_cavity.R": EYE_CAVITY,
    "mouth_cavity": MOUTH_CAVITY,
    "tongue": TONGUE,
}
FEATURES = ("lips", "eyelids", "ears")  # MPFB2's masks
RGB_LOOK = ("lips", "eyelids", "ears", "brows", "iris", "sclera")  # (B, 3) albedos in a colour look
BROWS = {  # centre line above the eye at (inner end, peak, tail), half width at (inner end, tail); eyeball radii
    "thin": ((1.40, 1.70, 1.25), (0.20, 0.07)),
    "medium": ((1.35, 1.62, 1.20), (0.30, 0.10)),
    "thick": ((1.30, 1.55, 1.15), (0.42, 0.16)),
}
IRIS = math.radians(31)  # the iris's angular radius about the eyeball's centre
ADULT_HEAD = 0.22  # m, an adult's h (twice the eye-to-crown height)
VIEWS = {  # forward (into the picture), up; right = forward x up
    "front": ((0.0, 1.0, 0.0), (0.0, 0.0, 1.0)),
    "left": ((-1.0, 0.0, 0.0), (0.0, 0.0, 1.0)),  # the figure's left side: the face looks left
}


class Heads:
    def __init__(self, device="cpu", texture_size=2048):
        self.device = torch.device(device)
        m = anny.create_fullbody_model(all_phenotypes=True, local_changes=True, remove_unattached_vertices=True)
        self.model = m.to(device=self.device, dtype=torch.float32)

        quads = m.faces.long().cpu()
        quads_uv = m.face_texture_coordinate_indices.long().cpu()
        segment = {
            label: torch.as_tensor(get_face_segmentation_mask(m, [label])).bool().cpu()
            for label in ["head"] + list(MATERIALS)
        }
        material = torch.full((len(quads),), SKIN, dtype=torch.long)
        for label, mid in MATERIALS.items():
            material[segment[label]] = mid
        split = [[0, 1, 2], [0, 2, 3]]
        self.tris = torch.cat([quads[:, s] for s in split]).to(self.device)
        self.tris_uv = torch.cat([quads_uv[:, s] for s in split]).to(self.device)
        self.material = material.repeat(2).to(self.device)
        self.uv = m.texture_coordinates.to(self.device, torch.float32)
        self.eye_vertices = [
            torch.unique(quads[segment[f"eye_front.{s}"] | segment[f"eye_back.{s}"]]).to(self.device) for s in "LR"
        ]
        head = segment["head"].clone()
        for label in MATERIALS:
            head |= segment[label]
        self.head_vertices = torch.unique(quads[head]).to(self.device)
        # outward normals: flip if the template's head normals point inwards
        tmpl = self.model.template_vertices.to(self.device, torch.float32)[None]
        self.template = tmpl[0]
        n = vertex_normals(tmpl, self.tris)[0, self.head_vertices]
        p = tmpl[0, self.head_vertices]
        self.normal_sign = 1.0 if float((n * (p - p.mean(0))).sum(-1).mean()) > 0 else -1.0
        self.masks = self._masks(texture_size)

    def head_size(self, verts):
        """h (B,): twice the height of the crown above the eyes' centres, m."""
        eye_z = torch.stack([verts[:, v, 2].mean(1) for v in self.eye_vertices], 1).mean(1)
        return 2 * (verts[:, self.head_vertices, 2].amax(1) - eye_z)

    # --- texture masks ----------------------------------------------------------------------
    def _masks(self, size):
        """(C, S, S) in Anny's UVs (v up): MPFB2's lips, eyelids and ears masks, then our eyebrow shapes."""
        chans = []
        for name in FEATURES:
            im = (
                Image.open(DATA / "mpfb2" / "textures" / f"mpfb_{name}.jpg")
                .convert("L")
                .resize((size, size), Image.BILINEAR)
            )
            chans.append(torch.frombuffer(bytearray(im.tobytes()), dtype=torch.uint8).float().view(size, size) / 255)
        chans = [c.to(self.device) for c in chans]
        texels = self._texels(size)
        chans += [self._brow(*texels, *shape) for shape in BROWS.values()]
        return torch.stack(chans)

    def _texels(self, size):
        """The template's point and normal under every texel, and whether the texel is skin."""
        tmpl = self.model.template_vertices.to(self.device, torch.float32)[None]
        xy = torch.stack([self.uv[:, 0] * size, (1 - self.uv[:, 1]) * size], -1)[None]
        tri, bary = rasterize(xy, torch.zeros_like(xy[..., 0]), self.tris_uv, size, size)
        p = interpolate(tmpl[0], self.tris, tri, bary)[0]
        n = interpolate(self.normal_sign * vertex_normals(tmpl, self.tris)[0], self.tris, tri, bary)[0]
        skin = (tri[0] >= 0) & (self.material[tri[0].clamp(min=0)] == SKIN)
        return p, n, skin

    def _brow(self, p, n, skin, centres, halves):
        """An eyebrow over each eye of the template: an arch in the frontal plane, in units of the
        eyeball's radius r about its centre, from the nose (dx = -1.5) to the temple (dx = 2), only
        on skin facing forward in front of the eye."""
        tmpl = self.model.template_vertices.to(self.device, torch.float32)
        out = torch.zeros(p.shape[:2], device=self.device)
        for verts in self.eye_vertices:
            e = tmpl[verts].mean(0)
            r = (tmpl[verts] - e).norm(dim=-1).mean()
            side = (p[..., 0] > 0) == bool(e[0] > 0)
            dx = (p[..., 0].abs() - e[0].abs()) / r
            dz = (p[..., 2] - e[2]) / r
            centre = _interp(dx, [-1.5, 0.7, 2.0], centres)
            half = _interp(dx, [-1.5, 2.0], halves)
            along = _smooth(-1.5, -1.2, dx) * (1 - _smooth(1.7, 2.0, dx))
            across = 1 - _smooth(0.6 * half, half, (dz - centre).abs())
            front = (p[..., 1] < e[1]) & (n[..., 1] < -0.2)
            out = torch.maximum(out, along * across * (side & front & skin))
        return out

    # --- pictures ---------------------------------------------------------------------------
    @torch.no_grad()
    def render(self, verts, look, views=("front", "left"), size=256, supersample=2, scale=None, colour=False):
        """Grey-scale close-ups of heads (verts (B, V, 3)) in looks from `Looks`.

        colour: the look's albedos are linear RGB instead: RGB_LOOK's (B, 3) and "skin_v" (B, 3, V),
        the skin per vertex (in place of "skin" and the blotches); "exposure" (B, views) is the
        linear level at which frontal skin of albedo 1 shows (the gain no longer follows the skin,
        so dark skin looks dark), "tint" (B, views, 3) the light's colour, "background" (B, views) the
        albedo of a grey backdrop lit like the face. The pictures are then (B, 3, size, size).

        Returns {view: (B, 1 or 3, size, size)} sRGB in [0, 1], and "inside": (B,) the largest share,
        over the views, of drawn pixels in holes onto the mouth cavity or the tongue at least 3
        supersampled pixels wide (about 2 mm at 256 px): shapes that tore the mouth open. The
        lips' seam, a line about a pixel thick, does not count. Framing per head: the picture is 1.5 h high (or `scale` pixels per metre),
        its middle 0.15 h below the eyes and, across, the head's own middle."""
        B, dev = verts.shape[0], verts.device
        S = size * supersample
        eye_c = torch.stack([verts[:, v].mean(1) for v in self.eye_vertices], 1)  # (B, 2, 3)
        h = self.head_size(verts)
        hv = verts[:, self.head_vertices]
        eye_z = eye_c[..., 2].mean(1)
        above_chin = (hv[..., 2] > (eye_z - 0.5 * h)[:, None])[..., None]
        lo = torch.where(above_chin, hv, torch.inf).amin(1)
        hi = torch.where(above_chin, hv, -torch.inf).amax(1)
        centre = 0.5 * (lo + hi)
        centre[:, 2] = eye_z - 0.15 * h
        rel = verts - centre[:, None]
        px_per_m = S / (1.5 * h) if scale is None else torch.full((B,), float(scale) * supersample, device=dev)
        normals = self.normal_sign * vertex_normals(verts, self.tris)
        rows = torch.linspace(-0.5, 0.5, S, device=dev)[None, None, :, None]
        if colour:
            skin_v = look["skin_v"].transpose(1, 2)  # (B, V, 3)
        else:  # uneven skin: four plane waves over the template's surface (no seams, follows the face)
            waves = torch.sin(
                torch.einsum("vc,bkc->bvk", self.template, look["blotch_waves"]) + look["blotch_phase"][:, None]
            )
            skin_v = (look["skin"][:, None] * (1 + look["blotch"][:, None] * waves.mean(-1) * 2))[
                ..., None
            ]  # (B, V, 1)
        out = {"inside": torch.zeros(B, device=dev)}
        for vi, name in enumerate(views):
            fwd, up = (torch.tensor(v, device=dev) for v in VIEWS[name])
            right = torch.linalg.cross(fwd, up)
            xy = torch.stack([S / 2 + px_per_m[:, None] * (rel @ right), S / 2 - px_per_m[:, None] * (rel @ up)], -1)
            tri, bary = rasterize(xy, rel @ fwd, self.tris, S, S)
            drawn = tri >= 0
            mat = torch.where(drawn, self.material[tri.clamp(min=0)], -1)
            points = interpolate(verts, self.tris, tri, bary)
            n = F.normalize(interpolate(normals, self.tris, tri, bary), dim=-1)
            uv = interpolate(self.uv, self.tris_uv, tri, bary)
            depth = torch.where(drawn, interpolate((rel @ fwd)[..., None], self.tris, tri, bary)[..., 0], torch.inf)
            occlusion = _occlusion(depth, n, fwd, up, right, px_per_m)
            base = interpolate(skin_v, self.tris, tri, bary).permute(0, 3, 1, 2)  # (B, C, H, W)
            albedo = self._albedo(look, mat, uv, points, eye_c, base)
            albedo = self._lashes(look, albedo, mat, points, eye_z, px_per_m)
            radiance = self._light(look, vi, albedo, n, mat, fwd, up, right, occlusion)
            if colour:  # albedo 1 on skin facing the camera shows at the exposure, in the light's colour
                irradiance = self._light(
                    look, vi, torch.ones_like(radiance[:, :1]), n, torch.full_like(mat, -1), fwd, up, right, occlusion
                )[:, 0]  # no sheen
                frontal = (mat == SKIN) & ((n * -fwd).sum(-1) > 0.85)
                level = torch.nanmedian(torch.where(frontal, irradiance, torch.nan).flatten(1), 1).values
                level = torch.nan_to_num(level, nan=1.0).clamp(min=1e-6)
                tint = look["tint"][:, vi]  # (B, 3)
                gain = (look["exposure"][:, vi] / level)[:, None] * tint
                img = _srgb((radiance * gain[..., None, None]).clamp(0, 1))
                bg = _srgb((look["background"][:, vi, None] * look["exposure"][:, vi, None] * tint).clamp(0, 1))[
                    ..., None, None
                ]
            else:
                skin = (mat == SKIN).float()[:, None]
                mean = (radiance * skin).flatten(1).sum(1) / skin.flatten(1).sum(1).clamp(min=1)
                gain = _linear(look["exposure"][:, vi]) / mean.clamp(min=1e-6)  # the mean skin at the look's level
                img = _srgb((radiance * gain[:, None, None, None]).clamp(0, 1))
                bg = look["background"][:, vi, None, None, None]
            bg = bg + look["gradient"][:, vi, None, None, None] * rows
            img = torch.where(drawn[:, None], img, bg.clamp(0, 1))
            out[name] = F.avg_pool2d(img, supersample) if supersample > 1 else img
            torn = ((mat == MOUTH_CAVITY) | (mat == TONGUE)).float()[:, None]
            k = (
                2 * max(1, round(S / 448)) + 1
            )  # erosion: the seam's line goes (about 1.4 mm: 3 px at 2 x 224, 5 at 2 x 518)
            holes = -F.max_pool2d(-torn, k, stride=1, padding=k // 2)
            share = holes.flatten(1).sum(1) / drawn.flatten(1).sum(1).clamp(min=1)
            out["inside"] = torch.maximum(out["inside"], share)
        return out

    def _albedo(self, look, mat, uv, points, eye_c, base):
        """Linear albedo (B, C, H, W): C = 1 (luminance) for a grey look, 3 (RGB) for a colour one;
        base: the skin's albedo per pixel (B, C, H, W). The look's levels are (B,) or (B, C)."""
        B, H, W = mat.shape
        grid = torch.stack([uv[..., 0] * 2 - 1, 1 - uv[..., 1] * 2], -1)
        m = F.grid_sample(self.masks[None], grid.reshape(1, B * H, W, 2), align_corners=False)[0].view(-1, B, 1, H, W)

        def level(k):
            v = look[k]
            return (v[:, None] if v.dim() == 1 else v)[..., None, None]  # (B, 1 or C, 1, 1)

        a = base
        for i, k in enumerate(FEATURES):
            a = a + (level(k) - a) * m[i]
        brows = m[len(FEATURES) :, :, 0].permute(1, 0, 2, 3)  # (B, shapes, H, W)
        brow = brows.gather(1, look["brow_shape"].view(B, 1, 1, 1).expand(B, 1, H, W))  # (B, 1, H, W)
        a = a + (level("brows") - a) * brow * level("brow_density")
        # the eyes: sclera, iris with a darker rim, pupil, by the angle from straight ahead (-Y)
        # about each eyeball's centre
        left = points[..., 0:1] > 0
        centre = torch.where(left, eye_c[:, None, None, 0], eye_c[:, None, None, 1])
        angle = torch.acos((-F.normalize(points - centre, dim=-1)[..., 1]).clamp(-1, 1))[:, None]
        iris = level("iris") * (1 - (1 - level("limbus")) * _smooth(0.75 * IRIS, 0.97 * IRIS, angle))
        eye = iris + (level("sclera") - iris) * _smooth(IRIS - 0.02, IRIS + 0.02, angle)
        eye = eye + (0.008 - eye) * (1 - _smooth(level("pupil") - 0.015, level("pupil") + 0.015, angle))
        mat = mat[:, None]
        a = torch.where(mat == EYE, eye, a)
        for mid, value in ((EYE_CAVITY, 0.015), (MOUTH_CAVITY, 0.02), (TONGUE, 0.06)):
            a = torch.where(mat == mid, torch.full_like(a, value), a)
        return a

    def _lashes(self, look, albedo, mat, points, eye_z, px_per_m):
        """Darken the skin along the lid rims (lashes, lid margins): skin pixels within a lash
        width of the eye in the picture, the upper lid more than the lower."""
        width = look["lash_width"] * 0.0015 * px_per_m  # pixels
        grown = ((mat == EYE) | (mat == EYE_CAVITY)).float()[:, None]
        dist = torch.full(mat.shape, float("inf"), device=mat.device)
        for k in range(1, int(width.max().ceil()) + 1):
            grown = F.max_pool2d(grown, 3, stride=1, padding=1)
            dist = torch.where((grown[:, 0] > 0) & torch.isinf(dist), torch.full_like(dist, float(k)), dist)
        w = width[:, None, None]
        near = 1 - _smooth(0.5 * w, w, dist)
        upper = torch.where(points[..., 2] > eye_z[:, None, None], 1.0, 0.35)
        return albedo * (1 - look["lashes"][:, None, None] * near * upper * (mat == SKIN))[:, None]

    def _light(self, look, vi, albedo, n, mat, fwd, up, right, occlusion):
        """Linear radiance (B, C, H, W) of albedo (B, C, H, W): a key and a fill light with wrapped
        diffuse, sky and ground light, all dimmed by the ambient occlusion, a white sheen of the key
        on the skin and a glint on the eyes (none where mat is neither)."""

        def level(k):
            return look[k][:, vi, None, None]

        def direction(az, el):  # degrees about the camera's axis, (B, 1, 1, 3)
            az, el = torch.deg2rad(look[az][:, vi]), torch.deg2rad(look[el][:, vi])
            d = (
                (torch.cos(el) * torch.sin(az))[:, None] * right
                - (torch.cos(el) * torch.cos(az))[:, None] * fwd
                + torch.sin(el)[:, None] * up
            )
            return d[:, None, None]

        key, fill = direction("key_az", "key_el"), direction("fill_az", "fill_el")
        wrap = level("wrap")

        def diffuse(d):
            return (((n * d).sum(-1) + wrap) / (1 + wrap)).clamp(min=0)

        nz = (n * up).sum(-1)
        irradiance = (
            diffuse(key)
            + level("fill") * diffuse(fill)
            + level("sky") * (0.5 + 0.5 * nz)
            + level("ground") * (0.5 - 0.5 * nz)
        )
        irradiance = irradiance * (1 - level("ao") * occlusion)
        ndh = (n * F.normalize(key - fwd, dim=-1)).sum(-1).clamp(min=0)
        lit = ((n * key).sum(-1) > 0).float()
        sheen = torch.where(mat == EYE, 0.4 * ndh**300, level("sheen") * ndh ** level("gloss"))
        sheen = sheen * lit * ((mat == SKIN) | (mat == EYE))
        return albedo * irradiance[:, None] + sheen[:, None]


class Looks:
    """Random grey-scale looks. Albedo levels are linear luminance; light and picture settings are
    drawn for every view apart (the reference pictures are separate images)."""

    def __call__(self, batch, views=2, generator=None, device="cpu"):
        def u(lo, hi, per_view=False):
            shape = (batch, views) if per_view else (batch,)
            return lo + (hi - lo) * torch.rand(shape, generator=generator)

        def logu(lo, hi, per_view=False):
            return torch.exp(u(math.log(lo), math.log(hi), per_view))

        skin = logu(0.04, 0.55)  # very dark to very light skin
        key_az = u(-75, 75, True)
        look = {
            "skin": skin,
            "lips": skin * u(0.5, 1.1),
            "eyelids": skin * u(0.8, 1.05),
            "ears": skin * u(0.9, 1.05),
            "blotch": u(0, 0.12),  # unevenness: waves 4-12 cm long in random directions
            "blotch_waves": F.normalize(torch.randn(batch, 4, 3, generator=generator), dim=-1)
            * (2 * math.pi / u(0.04, 0.12)[:, None, None]),
            "blotch_phase": 2 * math.pi * torch.rand(batch, 4, generator=generator),
            "brows": torch.minimum(logu(0.02, 0.45), 0.95 * skin),
            "brow_shape": torch.randint(len(BROWS), (batch,), generator=generator),
            "brow_density": u(0.5, 1.0),
            "lashes": u(0.2, 0.8),
            "lash_width": u(0.6, 1.4),
            "iris": torch.minimum(logu(0.015, 0.3), (2 * skin).clamp(min=0.05)),  # dark skin: mostly dark eyes
            "limbus": u(0.3, 0.9),
            "sclera": u(0.35, 0.7),
            "pupil": torch.deg2rad(u(8, 13)),
            "key_az": key_az,
            "key_el": u(-5, 60, True),
            "fill": u(0, 0.6, True),
            "fill_az": -torch.sign(key_az) * u(20, 90, True),
            "fill_el": u(-10, 40, True),
            "sky": u(0.05, 0.35, True),
            "ground": u(0, 0.12, True),
            "wrap": u(0, 0.35, True),
            "ao": u(0.3, 0.8, True),
            "sheen": u(0, 0.12, True),
            "gloss": u(4, 30, True),
            "exposure": u(0.35, 0.75, True),  # sRGB level of the mean skin
            "background": u(0.05, 0.95, True),
            "gradient": u(-0.15, 0.15, True),
        }
        return {k: v.to(device) for k, v in look.items()}


def _occlusion(depth, n, fwd, up, right, px_per_m, samples=12):
    """Screen-space ambient occlusion (B, H, W) in [0, 1]: the share of neighbours, within 4% of
    the picture's height, that stand more than 2 mm in front of the pixel's tangent plane (and less
    than 3 times their distance), blurred over 1.2% of the height: the 12 fixed directions alone
    draw mottled streaks in shallow hollows such as the neck's. Anny's flat triangles stray about
    1 mm from the smooth normals' planes, which a smaller margin draws as dark lines along the
    edges. depth: along the view, m, inf where nothing is drawn."""
    H = depth.shape[1]
    nr, nu, nf = (n * right).sum(-1), (n * up).sum(-1), (n * fwd).sum(-1).clamp(max=-0.15)
    m = px_per_m[:, None, None]
    occ = torch.zeros_like(depth)
    for k in range(samples):
        a = 2 * math.pi * (k + 0.5) / samples
        r = 0.04 * H * math.sqrt((k + 0.5) / samples)
        dx, dy = round(r * math.cos(a)), round(r * math.sin(a))
        plane = depth - (nr * dx - nu * dy) / m / nf  # the tangent plane's depth at the neighbour
        gap = plane - _shift(depth, dx, dy)
        occ += ((gap > 0.002) & (gap < 3 * r / m)).float()
    facing = (n * fwd).sum(-1) < -0.15
    return _blur(torch.where(facing, occ / samples, 0.0), torch.isfinite(depth), 0.012 * H)


def _blur(x, mask, sigma):
    """Gaussian blur of x (B, H, W) within mask (normalised convolution); zero outside."""
    r = max(1, int(3 * sigma))
    k = torch.exp(-0.5 * (torch.arange(-r, r + 1, device=x.device, dtype=x.dtype) / sigma) ** 2)
    k = k / k.sum()
    m = mask.to(x.dtype)[:, None]
    num, den = x[:, None] * m, m
    for kernel, pad in ((k.view(1, 1, 1, -1), (r, r, 0, 0)), (k.view(1, 1, -1, 1), (0, 0, r, r))):
        num, den = F.conv2d(F.pad(num, pad), kernel), F.conv2d(F.pad(den, pad), kernel)
    return (num / den.clamp(min=1e-6))[:, 0] * mask


def _shift(t, dx, dy):
    """t[..., y + dy, x + dx] at every (y, x); inf beyond the edges."""
    H, W = t.shape[-2:]
    p = F.pad(t, (abs(dx), abs(dx), abs(dy), abs(dy)), value=float("inf"))
    return p[..., abs(dy) + dy : abs(dy) + dy + H, abs(dx) + dx : abs(dx) + dx + W]


def _interp(x, xs, ys):
    """Piecewise-linear interpolation of a tensor, constant beyond the ends."""
    out = torch.full_like(x, ys[0])
    for x0, x1, y0, y1 in zip(xs[:-1], xs[1:], ys[:-1], ys[1:]):
        t = ((x - x0) / (x1 - x0)).clamp(0, 1)
        out = torch.where(x >= x0, y0 + t * (y1 - y0), out)
    return out


def _smooth(e0, e1, x):
    t = ((x - e0) / (e1 - e0)).clamp(0, 1)
    return t * t * (3 - 2 * t)


def _srgb(x):
    return torch.where(x <= 0.0031308, 12.92 * x, 1.055 * x.clamp(min=1e-12) ** (1 / 2.4) - 0.055)


def _linear(x):
    return torch.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4)
