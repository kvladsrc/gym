"""Pigment colours as linear sRGB albedo (a white reflector gives 1 under D65): skin from melanin
and blood, eyebrow hair from its two melanins, the iris from the melanin in its stroma.

Skin, after Donner & Jensen (2006), simplified: an epidermis THICKNESS mm thick holding melanin
(volume fraction `melanin`, of it the share `eu` eumelanin, the rest pheomelanin) over a dermis
holding blood (volume fraction `blood`, oxygen saturation `oxy`). The dermis is semi-infinite, its
total diffuse reflectance from the dipole (Jensen et al. 2001, relative index 1.4); the epidermis
is a Kubelka-Munk layer over it (K = 2 mu_a, S = 0.75 mu_s' x EPI_SCATTER). Absorption: Jacques'
melanin power laws and skin baseline, Prahl's haemoglobin table (hemoglobin.csv); reduced
scattering: Donner & Jensen's epidermis, the dermis half of it. EPI_SCATTER = 0.25 is our
calibration: with D&J's full epidermal scattering the epidermis veils the dermis in a bluish white
and no melanin level reaches skin's chroma (b* stayed under 11 where measured skin has 13-20).
The skin surface's own gloss is the renderer's sheen, not part of the albedo.

Eyebrow hair: d'Eon et al.'s (2011) eumelanin and pheomelanin absorption per RGB channel and the
colour of a mass of such fibres after Chiang et al. (2016), azimuthal roughness 0.3. Typical
eumelanin: black ~8, brown ~1.3, blond ~0.15; red hair ~0.3 with pheomelanin ~1.5.

Iris: the stroma as a Kubelka-Munk layer over the black pigment epithelium. Its fine fibres scatter
like small particles (Rayleigh, lambda^-4: blue without pigment); its melanin (volume fractions in
a 0.1 mm layer) absorbs exponentially in wavelength (eumelanin falling e-fold per 100 nm,
pheomelanin per 70 nm, both as the skin's at 500 nm) and its granules scatter evenly, so dense
melanin looks brown. Both are our calibration: with the power laws and no granule scattering
dense iris melanin only turned the blue grey.

Spectra are integrated with Wyman, Sloan and Shirley's (2013) fit of the CIE 1931 colour matching
functions under a 6504 K black body, normalised so that a white reflector gives (1, 1, 1)."""

from pathlib import Path

import numpy as np

NM = np.arange(380.0, 781.0, 5.0)
THICKNESS = 0.1  # mm, the epidermis
EPI_SCATTER = 0.25
ETA = 1.4


def _lobe(x, mu, s1, s2):
    t = (x - mu) / np.where(x < mu, s1, s2)
    return np.exp(-0.5 * t * t)


CMF = np.stack(
    [
        1.056 * _lobe(NM, 599.8, 37.9, 31.0)
        + 0.362 * _lobe(NM, 442.0, 16.0, 26.7)
        - 0.065 * _lobe(NM, 501.1, 20.4, 26.2),
        0.821 * _lobe(NM, 568.8, 46.9, 40.5) + 0.286 * _lobe(NM, 530.9, 16.3, 31.1),
        1.217 * _lobe(NM, 437.0, 11.8, 36.0) + 0.681 * _lobe(NM, 459.0, 26.0, 13.8),
    ]
)  # (3, L)
LIGHT = NM**-5 / np.expm1(1.4388e7 / (NM * 6504.0))
XYZ_TO_RGB = np.array(
    [[3.2404542, -1.5371385, -0.4985314], [-0.9692660, 1.8760108, 0.0415560], [0.0556434, -0.2040259, 1.0572252]]
)
RGB_TO_XYZ = np.linalg.inv(XYZ_TO_RGB)
WHITE = RGB_TO_XYZ.sum(1)  # D65
_TO_RGB = XYZ_TO_RGB @ (CMF * LIGHT)
_TO_RGB /= _TO_RGB.sum(1, keepdims=True)  # (3, L): a white reflector -> (1, 1, 1)

_hb = np.loadtxt(Path(__file__).resolve().parent / "hemoglobin.csv", delimiter=",")
_HB = 2.303 * (150 / 64500) / 10  # molar extinction -> mm^-1 of whole blood (150 g/L, 64.5 kg/mol)
MU_OXY = _HB * np.interp(NM, _hb[:, 0], _hb[:, 1])
MU_DEOXY = _HB * np.interp(NM, _hb[:, 0], _hb[:, 2])
MU_EU = 6.6e10 * NM**-3.33  # mm^-1, inside the melanosomes
MU_PHEO = 2.9e14 * NM**-4.75
MU_BASE = 0.0244 + 8.53 * np.exp(-(NM - 154) / 66.2)  # bloodless, melanin-free tissue
MUS_EPI = 14.74 * NM**-0.22 + 2.2e11 * NM**-4.0  # reduced scattering, mm^-1
MUS_DERM = 0.5 * MUS_EPI
_FDR = -1.440 / ETA**2 + 0.710 / ETA + 0.668 + 0.0636 * ETA
_A = (1 + _FDR) / (1 - _FDR)

HAIR_EU = np.array([0.419, 0.697, 1.37])
HAIR_PHEO = np.array([0.187, 0.4, 1.05])
_CHIANG = 5.969 - 0.215 * 0.3 + 2.532 * 0.3**2 - 10.73 * 0.3**3 + 5.574 * 0.3**4 + 0.245 * 0.3**5
IRIS_SCATTER = 0.35  # the stroma's S d at 500 nm: R(450) ~ 0.35 without pigment
IRIS_GRANULES = 0.5  # S d per unit of melanin
IRIS_BACK = 0.02  # the pigment epithelium's reflectance
IRIS_EU = 68.0 * np.exp(-(NM - 500) / 100)  # mm^-1
IRIS_PHEO = 44.0 * np.exp(-(NM - 500) / 70)


def _col(v):
    return np.asarray(v, dtype=float)[..., None]


def spectrum_rgb(r):
    """(..., L) reflectance -> (..., 3) linear sRGB."""
    return r @ _TO_RGB.T


def _dipole(mua, mus):
    a = mus / (mua + mus)
    s = np.sqrt(3 * (1 - a))
    return a / 2 * (1 + np.exp(-4 / 3 * _A * s)) * np.exp(-s)


def _km(K, S, d, back):
    """Kubelka-Munk reflectance of a layer (K, S per mm, d mm thick) over a background `back`."""
    a = 1 + K / S
    b = np.sqrt(a * a - 1)
    c = 1 / np.tanh(b * S * d)
    return (1 - back * (a - b * c)) / (a - back + b * c)


def skin_spectrum(melanin, eu=0.7, blood=0.02, oxy=0.75, thickness=THICKNESS):
    m, e, b, o = _col(melanin), _col(eu), _col(blood), _col(oxy)
    mua_epi = m * (e * MU_EU + (1 - e) * MU_PHEO) + (1 - m) * MU_BASE
    mua_derm = b * (o * MU_OXY + (1 - o) * MU_DEOXY) + (1 - b) * MU_BASE
    return _km(2 * mua_epi, 0.75 * EPI_SCATTER * MUS_EPI, thickness, _dipole(mua_derm, MUS_DERM))


def skin(melanin, eu=0.7, blood=0.02, oxy=0.75, thickness=THICKNESS):
    """Linear sRGB albedo (..., 3) of skin; the arguments broadcast together."""
    return spectrum_rgb(skin_spectrum(melanin, eu, blood, oxy, thickness))


def hair(eu, pheo=0.0):
    """Linear sRGB colour (..., 3) of a mass of hair from its eumelanin and pheomelanin."""
    return np.exp(-_CHIANG * np.sqrt(_col(eu) * HAIR_EU + _col(pheo) * HAIR_PHEO))


def iris(eu, pheo=0.0):
    """Linear sRGB albedo (..., 3) of the iris from its stroma's eumelanin and pheomelanin."""
    e, p = _col(eu), _col(pheo)
    K = 2 * 0.1 * (e * IRIS_EU + p * IRIS_PHEO) + 0.002
    S = IRIS_SCATTER * (NM / 500) ** -4 + IRIS_GRANULES * (e + p)
    return spectrum_rgb(_km(K, S, 1.0, IRIS_BACK))


def lab(rgb):
    """Linear sRGB (..., 3) -> CIELAB (D65)."""
    xyz = np.asarray(rgb, dtype=float) @ RGB_TO_XYZ.T / WHITE
    f = np.where(xyz > (6 / 29) ** 3, np.cbrt(xyz), xyz / (3 * (6 / 29) ** 2) + 4 / 29)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def srgb(x):
    x = np.clip(np.asarray(x, dtype=float), 0, 1)
    return np.where(x <= 0.0031308, 12.92 * x, 1.055 * x ** (1 / 2.4) - 0.055)


def linear(x):
    x = np.asarray(x, dtype=float)
    return np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4)


def de94(lab1, ref):
    """CIE94 colour difference (graphic arts weights) of lab1 (..., 3) from the reference ref (3,):
    chroma and hue differences count less on strongly coloured references."""
    d = lab1 - ref
    c1, c2 = np.hypot(lab1[..., 1], lab1[..., 2]), np.hypot(ref[..., 1], ref[..., 2])
    dc = c1 - c2
    dh2 = np.maximum((d[..., 1:] ** 2).sum(-1) - dc**2, 0)
    return np.sqrt(d[..., 0] ** 2 + (dc / (1 + 0.045 * c2)) ** 2 + dh2 / (1 + 0.015 * c2) ** 2)


def fit(target, colour, grids, exposure=(1.0,), spread=0.1, rounds=2, prior=None):
    """The parameters whose colour (times an exposure) is nearest `target` (linear sRGB) by CIE94.
    colour(**params) -> (N, 3) for (N,) arrays; grids: {name: (lo, hi, n, "log" | "lin")};
    exposure: the scales tried, each costing (ln k / spread)^2 like a squared dE. Each round
    refines a grid of the same size over the neighbours of the best point. prior(**params) -> (N,)
    adds its cost (squared dE units). Returns (params, exposure, dE, near): near = {name: (lo,
    hi)} over the points within dE + 1 of the best."""
    names = list(grids)
    spans = {k: (np.log(lo), np.log(hi)) if kind == "log" else (lo, hi) for k, (lo, hi, n, kind) in grids.items()}
    t = lab(target)
    ks = np.asarray(exposure, dtype=float)
    near = {}
    for r in range(rounds):
        axes = [np.linspace(*spans[k], grids[k][2]) for k in names]
        mesh = [m.ravel() for m in np.meshgrid(*axes, indexing="ij")]
        params = {k: np.exp(v) if grids[k][3] == "log" else v for k, v in zip(names, mesh)}
        c = colour(**params)  # (N, 3)
        cost = np.stack([de94(lab(c * k), t) ** 2 + (np.log(k) / spread) ** 2 for k in ks], 1)  # (N, K)
        if prior is not None:
            cost = cost + prior(**params)[:, None]
        i, j = np.unravel_index(np.argmin(cost), cost.shape)
        de = float(de94(lab(c[i] * ks[j]), t))
        if r == 0:
            ok = de94(lab(c * ks[j]), t) <= de + 1
            near = {k: (float(params[k][ok].min()), float(params[k][ok].max())) for k in names}
        best = {k: float(params[k][i]) for k in names}
        for k, ax in zip(names, axes):  # the next round: one step either side of the best
            step = ax[1] - ax[0]
            v = np.log(best[k]) if grids[k][3] == "log" else best[k]
            spans[k] = (max(v - step, ax[0]), min(v + step, ax[-1]))
    return best, float(ks[j]), de, near
