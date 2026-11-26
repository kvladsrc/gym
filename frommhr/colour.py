"""Colour looks for the training pictures: skin, lips, eyebrows and irises from pigment sliders
through pigments.py, for Heads.render(colour=True).

The reader reads SLIDERS from the pictures:
  - the skin: melanin (volume fraction in the epidermis), its eumelanin share, blood in the dermis;
  - factors by region: cheeks (melanin, blood), nose (blood), under the eyes (melanin);
  - the lips: melanin and blood, with a thin, well-oxygenated epidermis;
  - the eyebrow hair: eumelanin and pheomelanin; the iris stroma: eumelanin and pheomelanin;
  - stubble: the share of the beard zone covered by hair of the eyebrows' colour (adult men).
Everything else is random and not read: uneven skin (waves in melanin and blood over the
surface), eyelids and ears (a little more melanin and blood), the sclera, the exposure (the
display level of albedo 1 on skin facing the camera; about 1.55 for evenly lit studio portraits),
the light's colour (black body 5500-7500 K, a little green or magenta) and a grey backdrop lit
like the face or, in one picture of five, at any level.

Sliders are normalised to [0, 1], log or linear over their range. Priors: melanin log-uniform;
blood log-normal about 0.02; the eumelanin share uniform from a floor that rises with melanin (0
below 0.05, 0.6 at 0.45: dark skin is mostly eumelanin); regional factors log-normal about typical
values; the lips' melanin the skin's times 1-6, at least 0.025-0.09; eyebrows mostly dark on darker
skin; pheomelanins none in 60 %; the iris's eumelanin log-uniform (blue to hazel lies within
0-0.1), mostly brown on darker skin; stubble none on women, children and half of the men. Pigment
colours come from lookup tables on the device (trilinear, differentiable), so losses can compare
colours (`delta_e`)."""

import math

import numpy as np
import torch
import torch.nn.functional as F

import pigments as P
from heads import SKIN
from raster import vertex_normals

SLIDERS = (  # name, low, high, scale
    ("melanin", 0.003, 0.45, "log"),
    ("eu", 0.0, 1.0, "lin"),
    ("blood", 0.006, 0.1, "log"),
    ("cheek_melanin", 0.75, 1.3, "log"),
    ("cheek_blood", 0.8, 3.0, "log"),
    ("nose_blood", 0.8, 2.5, "log"),
    ("under_eye", 0.9, 1.8, "log"),
    ("lips_melanin", 0.01, 0.6, "log"),
    ("lips_blood", 0.03, 0.6, "log"),
    ("brows_eu", 0.03, 10.0, "log"),
    ("brows_pheo", 0.0, 1.5, "lin"),
    ("iris_eu", 0.005, 0.8, "log"),  # log: blue to hazel is eu 0-0.1 (dE ~27), 0.1-0.8 only darkens brown (~16)
    ("iris_pheo", 0.0, 0.3, "lin"),
    ("stubble", 0.0, 0.6, "lin"),
)
NS = len(SLIDERS)
EXPOSURE = 1.55  # colours are compared at display levels, as in studio-lit portraits
FEATURE_LEVEL = {"iris": 0.45}  # the lids shade the iris (0.3-0.67 measured in portraits)
MEL, EU, BLOOD = (0.002, 1.0, 64), (0.0, 1.0, 11), (0.002, 1.0, 64)  # the skin tables' axes
IRIS_EU, IRIS_PHEO = (0.002, 0.8, 61), (0.0, 0.3, 31)  # the iris table's eu axis is logarithmic
LUM = np.array([0.2126, 0.7152, 0.0722])


def values(u):
    """Normalised sliders (B, NS) -> {name: (B,)}."""
    out = {}
    for i, (name, lo, hi, kind) in enumerate(SLIDERS):
        if kind == "log":
            out[name] = torch.exp(math.log(lo) + (math.log(hi) - math.log(lo)) * u[:, i])
        else:
            out[name] = lo + (hi - lo) * u[:, i]
    return out


def normalise(v):
    """{name: (B,)} -> (B, NS) in [0, 1]."""
    cols = []
    for name, lo, hi, kind in SLIDERS:
        x = v[name].float()
        if kind == "log":
            cols.append((torch.log(x.clamp(min=1e-9)) - math.log(lo)) / (math.log(hi) - math.log(lo)))
        else:
            cols.append((x - lo) / (hi - lo))
    return torch.stack(cols, 1).clamp(0, 1)


def sample(gender, years, generator=None):
    """Random sliders (B, NS) for people of these sexes (Anny's gender, 1 = female) and ages."""
    B = gender.shape[0]

    def u(lo=0.0, hi=1.0):
        return lo + (hi - lo) * torch.rand(B, generator=generator)

    def logu(lo, hi):
        return torch.exp(u(math.log(lo), math.log(hi)))

    def lognormal(mu, s, lo, hi):
        return torch.exp(math.log(mu) + s * torch.randn(B, generator=generator)).clamp(lo, hi)

    def some(p):
        return (torch.rand(B, generator=generator) < p).float()

    v = {
        "melanin": logu(0.003, 0.45),
        "blood": lognormal(0.02, 0.5, 0.006, 0.1),
        "cheek_melanin": lognormal(1.0, 0.1, 0.75, 1.3),
        "cheek_blood": lognormal(1.3, 0.35, 0.8, 3.0),
        "nose_blood": lognormal(1.2, 0.25, 0.8, 2.5),
        "under_eye": lognormal(1.15, 0.15, 0.9, 1.8),
    }
    dark = ((torch.log(v["melanin"]) - math.log(0.05)) / (math.log(0.45) - math.log(0.05))).clamp(0, 1)
    v["eu"] = 0.6 * dark + (1 - 0.6 * dark) * u()
    v["lips_melanin"] = torch.maximum(v["melanin"] * logu(1.0, 6.0), logu(0.025, 0.09)).clamp(0.01, 0.6)
    v["lips_blood"] = logu(0.04, 0.6)
    v["brows_eu"] = torch.where(
        torch.rand(B, generator=generator) < 0.9 * dark.sqrt(), logu(1.0, 10.0), logu(0.03, 10.0)
    )
    v["brows_pheo"] = u(0.0, 1.5) * some(0.4)
    # every eye colour about as often (log), mostly brown with dark skin; uniform in eu, 88 % of the
    # eyes would be near-identical browns
    v["iris_eu"] = torch.where(
        torch.rand(B, generator=generator) < 0.8 * dark.sqrt(), logu(0.15, 0.8), logu(0.005, 0.8)
    )
    v["iris_pheo"] = u(0.0, 0.3) * some(0.4)
    men = ((gender.cpu() < 0.5) & (years.cpu() >= 17)).float()
    v["stubble"] = u(0.0, 0.6) * men * some(0.5)
    return normalise(v)


def _axis(lo, hi, n, log):
    return np.exp(np.linspace(np.log(lo), np.log(hi), n)) if log else np.linspace(lo, hi, n)


def _skin_table(oxy, thickness):
    """(3, eu, blood, melanin) linear sRGB albedo."""
    E, B, M = np.meshgrid(_axis(*EU, False), _axis(*BLOOD, True), _axis(*MEL, True), indexing="ij")
    rgb = np.concatenate(
        [
            P.skin(M.ravel()[i : i + 8192], E.ravel()[i : i + 8192], B.ravel()[i : i + 8192], oxy, thickness)
            for i in range(0, M.size, 8192)
        ]
    )
    return torch.tensor(rgb.reshape(*E.shape, 3), dtype=torch.float32).permute(3, 0, 1, 2).contiguous()


def _iris_table():
    """(3, pheo, eu) linear sRGB albedo."""
    Ph, E = np.meshgrid(_axis(*IRIS_PHEO, False), _axis(*IRIS_EU, True), indexing="ij")
    rgb = P.iris(E.ravel(), Ph.ravel())
    return torch.tensor(rgb.reshape(*E.shape, 3), dtype=torch.float32).permute(2, 0, 1).contiguous()


def _tints():
    """Black bodies 4000-10000 K as linear sRGB relative to 6504 K, luminance 1: (T (n,), (n, 3))."""
    T = np.arange(4000.0, 10001.0, 50.0)

    def rgb(t):
        spec = P.NM**-5 / np.expm1(1.4388e7 / (P.NM * np.asarray(t, dtype=float)[..., None]))
        return spec @ P.CMF.T @ P.XYZ_TO_RGB.T

    out = rgb(T) / rgb(6504.0)
    return T, out / (out @ LUM)[:, None]


def _coord(x, lo, hi, log):
    if log:
        return (torch.log(x.clamp(lo, hi)) - math.log(lo)) / (math.log(hi) - math.log(lo)) * 2 - 1
    return (x.clamp(lo, hi) - lo) / (hi - lo) * 2 - 1


def lookup_skin(table, melanin, eu, blood):
    """Trilinear lookup in a skin table; arguments of one shape (...) -> (..., 3)."""
    shape = melanin.shape
    grid = torch.stack(
        [
            _coord(melanin, MEL[0], MEL[1], True),
            _coord(blood, BLOOD[0], BLOOD[1], True),
            _coord(eu, EU[0], EU[1], False),
        ],
        -1,
    ).reshape(1, -1, 1, 1, 3)
    out = F.grid_sample(table[None], grid.to(table.dtype), mode="bilinear", padding_mode="border", align_corners=True)
    return out[0, :, :, 0, 0].T.reshape(*shape, 3)


def lookup_iris(table, eu, pheo):
    shape = eu.shape
    grid = torch.stack(
        [_coord(eu, IRIS_EU[0], IRIS_EU[1], True), _coord(pheo, IRIS_PHEO[0], IRIS_PHEO[1], False)], -1
    ).reshape(1, -1, 1, 2)
    out = F.grid_sample(table[None], grid.to(table.dtype), mode="bilinear", padding_mode="border", align_corners=True)
    return out[0, :, :, 0].T.reshape(*shape, 3)


def hair(eu, pheo):
    """pigments.hair in torch: (B,) -> (B, 3)."""
    he = torch.tensor(P.HAIR_EU, dtype=torch.float32, device=eu.device)
    hp = torch.tensor(P.HAIR_PHEO, dtype=torch.float32, device=eu.device)
    return torch.exp(-P._CHIANG * torch.sqrt(eu[..., None] * he + pheo[..., None] * hp + 1e-8))


def lab(rgb):
    """Linear sRGB (..., 3) -> CIELAB (D65), torch."""
    m = torch.tensor(P.RGB_TO_XYZ, dtype=rgb.dtype, device=rgb.device)
    white = torch.tensor(P.WHITE, dtype=rgb.dtype, device=rgb.device)
    xyz = rgb @ m.T / white
    d = 6 / 29
    f = torch.where(xyz > d**3, xyz.clamp(min=d**3) ** (1 / 3), xyz / (3 * d * d) + 4 / 29)
    return torch.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def _smooth(e0, e1, x):
    t = ((x - e0) / (e1 - e0)).clamp(0, 1)
    return t * t * (3 - 2 * t)


def regions(heads):
    """Weights (V,) of the regional factors on the template, in eyeball radii r about the eyes:
    cheeks (paint.cheeks: 2.8 r below the eyes, 0.3 r out, sigma 1.2 r, facing forward), nose
    (about the tip, 2.2 r below the eyes), under the eyes (1.3 r below each eye), and the beard
    zone (below a line from the mouth corners to the sideburns, the upper lip, the chin and the
    upper neck, in front of the ears)."""
    t = heads.template
    n = heads.normal_sign * vertex_normals(t[None], heads.tris)[0]
    eyes = [t[v].mean(0) for v in heads.eye_vertices]
    r = torch.stack([(t[v] - e).norm(dim=-1).mean() for v, e in zip(heads.eye_vertices, eyes)]).mean()
    ex = (eyes[0][0].abs() + eyes[1][0].abs()) / 2 / r
    ey, ez = (eyes[0][1] + eyes[1][1]) / 2, (eyes[0][2] + eyes[1][2]) / 2
    x, y, z = t[:, 0] / r, (t[:, 1] - ey) / r, (t[:, 2] - ez) / r
    ax = x.abs()
    front = _smooth(0.1, 0.5, -n[:, 1])
    cheeks = torch.exp(-0.5 * ((ax - ex - 0.3) ** 2 + (z + 2.8) ** 2) / 1.2**2) * front
    nose = torch.exp(-0.5 * ((x / 0.75) ** 2 + ((z + 2.2) / 0.9) ** 2)) * _smooth(1.2, 2.0, -y)
    under = torch.exp(-0.5 * (((ax - ex - 0.1) / 0.7) ** 2 + ((z + 1.3) / 0.4) ** 2)) * front * _smooth(0.0, 0.5, -y)
    top = -4.0 + 2.5 * _smooth(1.6, 4.4, ax)
    jaw = _smooth(0.0, 1.1, top - z)
    moustache = (1 - _smooth(-3.05, -2.85, z)) * (1 - _smooth(1.8, 2.4, ax))
    beard = torch.maximum(jaw, moustache) * _smooth(-12.0, -10.0, z) * (1 - _smooth(3.6, 4.8, y))  # ears at 5.8 r
    skin = torch.zeros(t.shape[0], device=t.device)
    skin[heads.tris[heads.material == SKIN].flatten()] = 1
    return {"cheeks": cheeks * skin, "nose": nose * skin, "under_eye": under * skin, "beard": beard * skin}


class ColourLooks:
    def __init__(self, heads):
        self.device = heads.device
        self.template = heads.template
        self.skin_table = _skin_table(0.75, P.THICKNESS).to(self.device)
        self.lips_table = _skin_table(0.95, 0.03).to(self.device)
        self.iris_table = _iris_table().to(self.device)
        self.regions = regions(heads)
        T, rgb = _tints()
        self.tint_t, self.tint_rgb = torch.tensor(T, dtype=torch.float32), torch.tensor(rgb, dtype=torch.float32)

    def _tint(self, t, green):
        i = ((t - self.tint_t[0]) / (self.tint_t[1] - self.tint_t[0])).clamp(0, len(self.tint_t) - 1.001)
        k, f = i.floor().long(), (i - i.floor())[..., None]
        rgb = self.tint_rgb[k] * (1 - f) + self.tint_rgb[k + 1] * f
        rgb = rgb * torch.stack([torch.ones_like(green), 1 + green, torch.ones_like(green)], -1)
        return rgb / (rgb @ torch.tensor(LUM, dtype=torch.float32))[..., None]

    def __call__(self, look, gender, years, views=2, generator=None):
        """A grey look (heads.Looks) made a colour one with random sliders: (look, sliders (B, NS))."""
        B, dev = gender.shape[0], self.device

        def u(lo, hi, shape=None):
            return lo + (hi - lo) * torch.rand(shape or (B,), generator=generator)

        def waves(lo, hi, count=4):
            dirs = (
                F.normalize(torch.randn(B, count, 3, generator=generator), dim=-1)
                * (2 * math.pi / u(lo, hi))[:, None, None]
            )
            phase = 2 * math.pi * torch.rand(B, count, generator=generator)
            return (
                torch.sin(torch.einsum("vc,bkc->bvk", self.template, dirs.to(dev)) + phase.to(dev)[:, None]).mean(-1)
                * 2
            )

        freckles = u(0.0, 0.15) * (torch.rand(B, generator=generator) < 0.3)
        n_mel = u(0.0, 0.12).to(dev)[:, None] * waves(0.03, 0.10) + freckles.to(dev)[:, None] * waves(0.012, 0.03)
        n_blood = u(0.0, 0.25).to(dev)[:, None] * waves(0.03, 0.10)
        tone = u(0.02, 0.10), u(0.05, 0.18)
        sclera = torch.stack([torch.ones(B), 1 - tone[0], 1 - tone[1]], -1)
        sclera = sclera / (sclera @ torch.tensor(LUM, dtype=torch.float32))[:, None] * u(0.45, 0.75)[:, None]
        k = torch.exp(u(math.log(1.15), math.log(2.1)))[:, None] * torch.exp(
            0.04 * torch.randn(B, views, generator=generator)
        )
        t = u(5500.0, 7500.0)[:, None] + 150 * torch.randn(B, views, generator=generator)
        tint = self._tint(t, u(-0.015, 0.015)[:, None].expand(B, views))
        backdrop = torch.exp(u(math.log(0.18), math.log(0.34)))[:, None] * torch.exp(
            0.03 * torch.randn(B, views, generator=generator)
        )
        free = torch.rand(B, generator=generator) < 0.2  # a backdrop at any level, not lit like the face
        level = u(0.25, 0.95, (B, views))
        lin = torch.where(level <= 0.04045, level / 12.92, ((level + 0.055) / 1.055) ** 2.4)
        backdrop = torch.where(free[:, None], lin / k, backdrop)
        extra = {
            "n_mel": n_mel,
            "n_blood": n_blood,
            "n_lid_mel": 1.15 * torch.exp(0.1 * torch.randn(B, generator=generator)),
            "n_lid_blood": 1.3 * torch.exp(0.15 * torch.randn(B, generator=generator)),
            "n_ear_blood": 1.6 * torch.exp(0.15 * torch.randn(B, generator=generator)),
            "sclera": sclera,
            "exposure": k,
            "tint": tint,
            "background": backdrop,
            "gradient": u(-0.08, 0.08, (B, views)),
        }
        look = {key: v for key, v in look.items() if key not in ("skin", "blotch", "blotch_waves", "blotch_phase")}
        look.update({key: v.to(dev) for key, v in extra.items()})
        sliders = sample(gender, years, generator).to(dev)
        return self.paint(look, sliders), sliders

    def paint(self, look, sliders):
        """The look's albedos from sliders (B, NS); its random parts stay."""
        v = values(sliders.float())
        R = self.regions
        e = v["eu"][:, None]
        lm = (
            torch.log(v["melanin"])[:, None]
            + torch.log(v["cheek_melanin"])[:, None] * R["cheeks"]
            + torch.log(v["under_eye"])[:, None] * R["under_eye"]
            + look["n_mel"]
        )
        lb = (
            torch.log(v["blood"])[:, None]
            + torch.log(v["cheek_blood"])[:, None] * R["cheeks"]
            + torch.log(v["nose_blood"])[:, None] * R["nose"]
            + look["n_blood"]
        )
        skin = lookup_skin(self.skin_table, torch.exp(lm), e.expand_as(lm), torch.exp(lb))  # (B, V, 3)
        brows = hair(v["brows_eu"], v["brows_pheo"])
        d = (v["stubble"][:, None] * R["beard"])[..., None]
        skin = skin * (1 - d) + brows[:, None] * d
        m, b, eu = v["melanin"], v["blood"], v["eu"]
        return dict(
            look,
            skin_v=skin.transpose(1, 2).contiguous(),
            lips=lookup_skin(self.lips_table, v["lips_melanin"], eu, v["lips_blood"]),
            eyelids=lookup_skin(self.skin_table, m * look["n_lid_mel"], eu, b * look["n_lid_blood"]),
            ears=lookup_skin(self.skin_table, m, eu, b * look["n_ear_blood"]),
            brows=brows,
            iris=lookup_iris(self.iris_table, v["iris_eu"], v["iris_pheo"]),
        )

    def features(self, sliders):
        """Albedos (B, 3) of what the sliders set: skin, cheeks, nose, under the eyes, lips, brows, iris."""
        v = values(sliders.float())
        m, e, b = v["melanin"], v["eu"], v["blood"]

        def skin(mm, bb):
            return lookup_skin(self.skin_table, mm, e, bb)

        return {
            "skin": skin(m, b),
            "cheeks": skin(m * v["cheek_melanin"], b * v["cheek_blood"]),
            "nose": skin(m, b * v["nose_blood"]),
            "under_eye": skin(m * v["under_eye"], b),
            "lips": lookup_skin(self.lips_table, v["lips_melanin"], e, v["lips_blood"]),
            "brows": hair(v["brows_eu"], v["brows_pheo"]),
            "iris": lookup_iris(self.iris_table, v["iris_eu"], v["iris_pheo"]),
        }

    def delta_e(self, pred, true):
        """{feature: (B,)} CIE76 colour differences at display levels between two slider sets."""
        fp, ft = self.features(pred), self.features(true)
        out = {}
        for key in fp:
            s = EXPOSURE * FEATURE_LEVEL.get(key, 1.0)
            out[key] = ((lab(fp[key] * s) - lab(ft[key] * s)).pow(2).sum(-1) + 1e-6).sqrt()
        return out
