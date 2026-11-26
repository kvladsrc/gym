"""A person from the reader's values: Anny's body with the face the reader read (MHR's head
variation), carried onto SOMA's mesh and skeleton (a root and
SOMA's 77 public joints) by SOMA-X, in SOMA's bind pose, and painted from the pigment sliders.

The colours follow the training renders' colour model. The skin comes from melanin, its
eumelanin share and blood per vertex of Anny's mesh, with factors on the cheeks, the nose and
under the eyes and stubble in an adult man's beard zone; the lips, eyelids and ears from MPFB2's
masks and the brows from a brow mask, in Anny's UVs. SOMA-X's own correspondence (each SOMA
vertex on an Anny triangle) carries them to SOMA's mesh, and they are baked into one texture
over SOMA's UVs. SOMA's mesh has no eyeballs: spheres at its eye joints, as big as the lid rims
allow, painted by the angle from straight ahead (sclera, iris with a darker rim, pupil).

Coordinates: metres, Y up, the face towards +Z (SOMA's and glTF's).
"""

import io
import math
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn.functional as F
from PIL import Image

from fromsoma_model_server.bake import pad, rasterize
from fromsoma_model_server.glb import Skeleton, Surface

TEXTURE = 4096  # the atlas: SOMA's three UV tiles and the eyes in its quarters
QUARTERS = {0: (0, 0), 1: (1, 0), 2: (0, 1)}  # tile -> quarter; tile 0 holds the head and neck
EYE_QUARTER = (1, 1)
INFLUENCES = 4  # joints per vertex in the file (SOMA uses up to 10; the four largest keep >= 83 %)
SEAM_UV = 0.05  # a SOMA triangle whose corners lie this far apart in Anny's UVs crosses a seam
MOUTH = (0.20, 0.05, 0.05)  # linear albedo inside SOMA's mouth bag
SOCKET = (0.04, 0.03, 0.03)  # and its eye bags (behind the eyeballs)
EYE_RADIUS_M = (0.009, 0.0135)  # the eyeballs' radius stays in this range
EYE_RINGS, EYE_SEGMENTS = 72, 48
SKIN_ROUGHNESS, EYE_ROUGHNESS = 0.55, 0.12


@dataclass(frozen=True)
class Person:
    surfaces: list[Surface]
    skeleton: Skeleton
    texture: np.ndarray  # (TEXTURE, TEXTURE, 3) uint8 sRGB
    meta: dict[str, Any]


def _smooth(e0: float, e1: float, x: torch.Tensor) -> torch.Tensor:
    t = ((x - e0) / (e1 - e0)).clamp(0, 1)
    return t * t * (3 - 2 * t)


def _coord(x: torch.Tensor, axis: Sequence[float], log: bool) -> torch.Tensor:
    lo, hi = axis[0], axis[1]
    if log:
        return (torch.log(x.clamp(lo, hi)) - math.log(lo)) / (math.log(hi) - math.log(lo)) * 2 - 1
    return (x.clamp(lo, hi) - lo) / (hi - lo) * 2 - 1


def to_srgb(linear: torch.Tensor) -> torch.Tensor:
    x = linear.clamp(0, 1)
    return torch.where(x <= 0.0031308, 12.92 * x, 1.055 * x.clamp(min=1e-12) ** (1 / 2.4) - 0.055)


class Palette:
    """The pigment colour model: linear sRGB albedos from the sliders' physical values."""

    def __init__(self, colour: Mapping[str, Any]) -> None:
        self.skin_table, self.lips_table = (
            colour["skin_table"].float(),
            colour["lips_table"].float(),
        )
        self.iris_table = colour["iris_table"].float()
        self.axes = colour["axes"]
        self.hair_eu = torch.tensor(colour["hair_eu"], dtype=torch.float32)
        self.hair_pheo = torch.tensor(colour["hair_pheo"], dtype=torch.float32)
        self.chiang = float(colour["hair_chiang"])

    def skin(
        self, melanin: torch.Tensor, eu: torch.Tensor, blood: torch.Tensor, lips: bool = False
    ) -> torch.Tensor:
        """(...) -> (..., 3): trilinear in the table (eumelanin share, blood, melanin)."""
        a = self.axes
        table = self.lips_table if lips else self.skin_table
        shape = melanin.shape
        grid = torch.stack(
            [
                _coord(melanin, a["melanin"], True),
                _coord(blood, a["blood"], True),
                _coord(eu, a["eu"], False),
            ],
            -1,
        ).reshape(1, -1, 1, 1, 3)
        out = F.grid_sample(
            table[None], grid, mode="bilinear", padding_mode="border", align_corners=True
        )
        return out[0, :, :, 0, 0].T.reshape(*shape, 3)

    def iris(self, eu: float, pheo: float) -> torch.Tensor:
        a = self.axes
        grid = torch.stack(
            [
                _coord(torch.tensor([eu]), a["iris_eu"], True),
                _coord(torch.tensor([pheo]), a["iris_pheo"], False),
            ],
            -1,
        ).reshape(1, 1, 1, 2)
        out = F.grid_sample(
            self.iris_table[None], grid, mode="bilinear", padding_mode="border", align_corners=True
        )
        return out[0, :, 0, 0]

    def hair(self, eu: float, pheo: float) -> torch.Tensor:
        return torch.exp(
            -self.chiang * torch.sqrt(eu * self.hair_eu + pheo * self.hair_pheo + 1e-8)
        )


def _corner_uvs(quads: np.ndarray, quads_uv: np.ndarray, tris: np.ndarray) -> np.ndarray:
    """The UV index of each corner of `tris` (T, 3): the corner's in the quad holding all three."""
    V = int(max(quads.max(), tris.max())) + 1

    def key(t: np.ndarray) -> np.ndarray:
        s = np.sort(t, axis=1).astype(np.int64)
        return (s[:, 0] * V + s[:, 1]) * V + s[:, 2]

    keys, owner = [], []
    for drop in range(4):
        keep = [i for i in range(4) if i != drop]
        keys.append(key(quads[:, keep]))
        owner.append(np.arange(len(quads)))
    keys, owner = np.concatenate(keys), np.concatenate(owner)
    order = np.argsort(keys, kind="stable")
    keys, owner = keys[order], owner[order]
    wanted = key(tris)
    at = np.clip(np.searchsorted(keys, wanted), 0, len(keys) - 1)
    if not np.all(keys[at] == wanted):
        raise RuntimeError(
            f"{int(np.sum(keys[at] != wanted))} of SOMA-X's Anny triangles are not in Anny's quads"
        )
    q = owner[at]
    slot = np.argmax(quads[q][:, None, :] == tris[:, :, None], axis=2)  # (T, 3)
    return quads_uv[q[:, None], slot]


def eyeball(centre: np.ndarray, radius: float) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """A UV sphere about `centre`, its pole straight ahead (+Z): (points, the angle from straight
    ahead per point, triangles facing out)."""
    theta = np.linspace(0, math.pi, EYE_RINGS + 1)[1:-1]
    phi = np.linspace(0, 2 * math.pi, EYE_SEGMENTS, endpoint=False)
    T, P = np.meshgrid(theta, phi, indexing="ij")
    dirs = np.stack([np.sin(T) * np.cos(P), np.sin(T) * np.sin(P), np.cos(T)], -1).reshape(-1, 3)
    dirs = np.concatenate([[[0.0, 0.0, 1.0]], dirs, [[0.0, 0.0, -1.0]]])
    angle = np.concatenate([[0.0], T.ravel(), [math.pi]])
    s, last = EYE_SEGMENTS, len(dirs) - 1
    ring = lambda i, j: 1 + i * s + j % s  # noqa: E731
    tris = [(0, ring(0, j), ring(0, j + 1)) for j in range(s)]
    for i in range(len(theta) - 1):
        for j in range(s):
            tris += [
                (ring(i, j), ring(i + 1, j), ring(i + 1, j + 1)),
                (ring(i, j), ring(i + 1, j + 1), ring(i, j + 1)),
            ]
    tris += [(last, ring(len(theta) - 1, j + 1), ring(len(theta) - 1, j)) for j in range(s)]
    tris = np.asarray(tris, dtype=np.int64)
    a, b, c = (dirs[tris[:, k]] for k in range(3))
    inward = np.einsum("ij,ij->i", np.cross(b - a, c - a), a + b + c) < 0
    tris[inward] = tris[inward][:, ::-1]
    return (centre + radius * dirs).astype(np.float32), angle, tris


def vertex_normals(points: np.ndarray, tris: np.ndarray) -> np.ndarray:
    a, b, c = (points[tris[:, k]].astype(np.float64) for k in range(3))
    faces = np.cross(b - a, c - a)
    normals = np.zeros((len(points), 3))
    for k in range(3):
        np.add.at(normals, tris[:, k], faces)
    length = np.linalg.norm(normals, axis=1, keepdims=True)
    return (normals / np.where(length > 0, length, 1)).astype(np.float32)


class Bodies:
    """Anny, SOMA-X and the colour data, loaded once; `build` makes a person per reading."""

    def __init__(self, package: Mapping[str, Any], soma_root: Path) -> None:
        import anny
        from anny.shape_distribution import SimpleShapeDistribution
        from soma.body.soma import SOMALayer

        self.anny = anny.create_fullbody_model(
            all_phenotypes=True, local_changes=True, remove_unattached_vertices=True
        ).to(dtype=torch.float32)
        self.ages = SimpleShapeDistribution(self.anny).morphological_age_mapping
        self.config = package["config"]
        data = package["anny"]
        if int(self.anny.template_vertices.shape[0]) != int(data["vertices"]):
            raise RuntimeError("this Anny has another mesh than the reader was trained on")
        self.fields = package["fields"].float()  # MHR's head variation on Anny's mesh
        self.eye_vertices = [v.long() for v in data["eye_vertices"]]
        self.head_vertices = data["head_vertices"].long()
        self.regions = {k: v.float() for k, v in data["regions"].items()}
        self.masks = data["masks"]
        self.mask_index = {name: i for i, name in enumerate(data["mask_names"])}
        self.neutral = package["neutral"]
        self.palette = Palette(package["colour"])
        self.layer = SOMALayer(data_root=soma_root, identity_model_type="anny", device="cpu")

        self._soma_mesh(soma_root)

        # SOMA-X's correspondence: each SOMA vertex on an Anny triangle (and the point above it)
        interp = self.layer.identity_model._to_soma_interp
        anny_tris = interp.F_src_torch.cpu().numpy().astype(np.int64)
        face = interp.face_ids.cpu().numpy()
        bary = interp.bary_coords.cpu().numpy().astype(np.float64)
        w = np.clip(bary[:, :3] + bary[:, 3:4] / 3, 0, None)  # the point above is the corners' mean
        self.corner_weights = torch.tensor(w / w.sum(1, keepdims=True), dtype=torch.float32)
        self.corners = torch.from_numpy(anny_tris[face])  # (Vs, 3) Anny vertices
        corner_uv = _corner_uvs(
            self.anny.faces.long().cpu().numpy(),
            self.anny.face_texture_coordinate_indices.long().cpu().numpy(),
            anny_tris[face],
        )
        anny_uv = self.anny.texture_coordinates.float().cpu().numpy()[corner_uv]  # (Vs, 3, 2)
        self.anny_uv = torch.tensor(
            (self.corner_weights.numpy()[..., None] * anny_uv).sum(1), dtype=torch.float32
        )
        per_tri = self.anny_uv[torch.from_numpy(self.tris)]  # (T, 3, 2)
        spread = (per_tri[:, :, None] - per_tri[:, None]).norm(dim=-1).amax((1, 2))
        self.seam = spread > SEAM_UV

    def _soma_mesh(self, soma_root: Path) -> None:
        """SOMA's mesh: quads split in two, UV st (three tiles) in the atlas, one glTF vertex per
        UV point; the mouth and eye bags and the lid rims."""
        npz = np.load(Path(soma_root) / "SOMA_neutral.npz")
        counts = npz["face_vert_counts"]
        if not np.all(counts == 4):
            raise RuntimeError("SOMA's mesh is expected to be all quads")
        quads, quads_uv = (
            npz["face_vert_indices"].reshape(-1, 4),
            npz["uv_indices_st"].reshape(-1, 4),
        )
        split = np.array([[0, 1, 2], [0, 2, 3]])
        self.tris = np.concatenate([quads[:, s] for s in split]).astype(np.int64)
        self.tris_uv = np.concatenate([quads_uv[:, s] for s in split]).astype(np.int64)
        uv = npz["uv_coord_st"].astype(np.float64)
        self.uv_vertex = np.zeros(len(uv), dtype=np.int64)
        self.uv_vertex[self.tris_uv.ravel()] = self.tris.ravel()
        tile = np.floor(uv[self.tris_uv].mean(1)[:, 0]).astype(np.int64)  # per triangle
        uv_tile = np.zeros(len(uv), dtype=np.int64)
        uv_tile[self.tris_uv.ravel()] = np.repeat(tile, 3)
        q = np.array([QUARTERS[int(t)] for t in uv_tile], dtype=np.float64)
        self.atlas_uv = np.stack(
            [(q[:, 0] + uv[:, 0] - uv_tile) / 2, (q[:, 1] + 1 - uv[:, 1]) / 2], 1
        ).astype(np.float32)
        self.segments = {
            name: npz[f"segment_{name}"].astype(np.int64) for name in ("mouth_bag", "eye_bags")
        }
        self.rim = self._rim(len(npz["mean"]))

    def _rim(self, count: int) -> np.ndarray:
        """The lid rims: the eye bags' vertices on faces that leave them (both eyes)."""
        inside = np.zeros(count, dtype=bool)
        inside[self.segments["eye_bags"]] = True
        mixed = inside[self.tris].any(1) & ~inside[self.tris].all(1)
        return np.unique(self.tris[mixed][inside[self.tris[mixed]]])

    # --- shape ----------------------------------------------------------------------------
    def to_anny_age(self, years: torch.Tensor) -> torch.Tensor:
        return self.ages.morphological_to_anny_age(years)

    def to_years(self, age: float) -> float:
        return float(self.ages.anny_to_morphological_age(torch.tensor([age]))[0])

    @torch.no_grad()
    def rest(self, phenotype: Mapping[str, float], coefficients: Sequence[float]) -> torch.Tensor:
        """Anny's rest shape (1, V, 3) with the face's MHR variation, scaled to the head's size."""
        phen = {k: torch.tensor([float(v)]) for k, v in phenotype.items()}
        coeffs = self.anny.get_phenotype_blendshape_coefficients(
            **self.anny.parse_phenotype_kwargs(phen), local_changes={}
        )
        rest = self.anny.get_rest_vertices(coeffs)
        eye_z = torch.stack([rest[:, v, 2].mean(1) for v in self.eye_vertices], 1).mean(1)
        h = 2 * (rest[:, self.head_vertices, 2].amax(1) - eye_z)
        c = torch.tensor([list(coefficients)], dtype=torch.float32)
        return (
            rest
            + torch.einsum("bj,jvc->bvc", c, self.fields)
            * (h / self.config["adult_head_m"])[:, None, None]
        )

    @torch.no_grad()
    def rig(self, rest: torch.Tensor) -> tuple[np.ndarray, Skeleton, np.ndarray]:
        """SOMA-X with `rest` in place of its own Anny shape: (vertices (Vs, 3), skeleton, dense
        skinning weights (Vs, J)), in SOMA's bind pose."""
        return self._rig("get_rest_shape", rest)

    def _rig(self, method: str, rest: torch.Tensor) -> tuple[np.ndarray, Skeleton, np.ndarray]:
        """SOMA-X's fit and bind pose with the identity model's `method` returning `rest`:
        "get_rest_shape" takes a shape on Anny's mesh, "forward" one on SOMA's."""
        model = self.layer.identity_model
        setattr(model, method, lambda *args, **kwargs: rest)
        try:
            self.layer.prepare_identity(torch.zeros(1, model.num_identity_coeffs))
        finally:
            delattr(model, method)
        vertices = self.layer._cached_rest_shape[0].detach().cpu().numpy().astype(np.float32)
        view = self.layer.public_rig_view()
        world = view.bind_transforms_world
        world = (world[0] if world.ndim == 4 else world).detach().cpu().numpy().astype(np.float64)
        parents = [int(p) for p in view.joint_parent_ids.cpu().numpy()]
        parents = [-1 if i == p else p for i, p in enumerate(parents)]
        weights = view.skinning_weights
        weights = (weights.to_dense() if weights.is_sparse else weights).detach().cpu().numpy()
        weights = weights.reshape(vertices.shape[0], -1).astype(np.float32)
        return vertices, Skeleton(list(view.joint_names), parents, world), weights

    # --- colour ---------------------------------------------------------------------------
    def albedo(self, v: Mapping[str, float]) -> dict[str, torch.Tensor]:
        """Linear albedos: "skin" per Anny vertex (V, 3), the features (3,)."""
        P, R, n = self.palette, self.regions, self.neutral
        eu = torch.tensor(v["eu"])
        lm = (
            math.log(v["melanin"])
            + math.log(v["cheek_melanin"]) * R["cheeks"]
            + math.log(v["under_eye"]) * R["under_eye"]
        )
        lb = (
            math.log(v["blood"])
            + math.log(v["cheek_blood"]) * R["cheeks"]
            + math.log(v["nose_blood"]) * R["nose"]
        )
        skin = P.skin(torch.exp(lm), eu.expand_as(lm), torch.exp(lb))
        brows = P.hair(v["brows_eu"], v["brows_pheo"])
        d = (v["stubble"] * R["beard"])[:, None]
        m, b = torch.tensor([v["melanin"]]), torch.tensor([v["blood"]])
        e = eu.reshape(1)
        return {
            "skin": skin * (1 - d) + brows * d,
            "lips": P.skin(
                torch.tensor([v["lips_melanin"]]), e, torch.tensor([v["lips_blood"]]), lips=True
            )[0],
            "eyelids": P.skin(m * n["lid_melanin"], e, b * n["lid_blood"])[0],
            "ears": P.skin(m, e, b * n["ear_blood"])[0],
            "brows": brows,
            "iris": P.iris(v["iris_eu"], v["iris_pheo"]),
        }

    def _sample_masks(self, uv: torch.Tensor) -> torch.Tensor:
        """Anny's feature masks at Anny UVs (N, 2) -> (N, 4): lips, eyelids, ears, the brows."""
        names = ("lips", "eyelids", "ears", self.neutral["brows"])
        index = torch.tensor([self.mask_index[k] for k in names])
        masks = self.masks[index].float()[None] / 255
        grid = torch.stack([uv[:, 0] * 2 - 1, 1 - uv[:, 1] * 2], -1).reshape(1, -1, 1, 2)
        return F.grid_sample(masks, grid, mode="bilinear", align_corners=False)[0, :, :, 0].T

    def _texels(
        self, colours: Mapping[str, torch.Tensor], t: torch.Tensor, mix: Any, drawn: torch.Tensor
    ) -> tuple[torch.Tensor, torch.Tensor]:
        """The drawn texels' skin albedo (N, 3) and feature masks (N, 4: lips, eyelids, ears,
        brows); t: their triangles, mix: SOMA vertex values -> texel values, drawn: (S, S)."""
        vs = (self.corner_weights[..., None] * colours["skin"][self.corners]).sum(1)  # (Vs, 3)
        vs[self.segments["mouth_bag"]] = torch.tensor(MOUTH)
        vs[self.segments["eye_bags"]] = torch.tensor(SOCKET)
        masks = mix(self._sample_masks(self.anny_uv))
        smooth = ~self.seam[t]
        masks[smooth] = self._sample_masks(mix(self.anny_uv)[smooth])
        return mix(vs), masks

    @torch.no_grad()
    def bake(self, colours: Mapping[str, torch.Tensor]) -> np.ndarray:
        """The atlas (TEXTURE, TEXTURE, 3) uint8 sRGB: SOMA's tiles and the eye's profile."""
        n = self.neutral
        xy = torch.from_numpy(self.atlas_uv).float() * TEXTURE
        tris_uv = torch.from_numpy(self.tris_uv)
        tri, bary = rasterize(xy, tris_uv, TEXTURE, TEXTURE)
        drawn = tri >= 0
        t, w = tri[drawn], bary[drawn]  # (N,), (N, 3)
        uv_vertex = torch.from_numpy(self.uv_vertex)
        corners = uv_vertex[tris_uv[t]]  # (N, 3) SOMA vertices

        def mix(values: torch.Tensor) -> torch.Tensor:
            return (w[..., None] * values[corners]).sum(1)

        albedo, masks = self._texels(colours, t, mix, drawn)
        for i, key in enumerate(("lips", "eyelids", "ears")):
            albedo = albedo + (colours[key] - albedo) * masks[:, i : i + 1]
        albedo = albedo + (colours["brows"] - albedo) * masks[:, 3:4] * n["brow_density"]
        image = torch.zeros(3, TEXTURE, TEXTURE)
        image[:, drawn] = albedo.T
        image = pad(image, drawn, 8)
        # the eyes' quarter: sclera, iris, pupil by the angle from straight ahead, down its rows
        half = TEXTURE // 2
        x0, y0 = EYE_QUARTER[0] * half, EYE_QUARTER[1] * half
        angle = (torch.arange(half, dtype=torch.float32) + 0.5) / half * math.pi
        image[:, y0 : y0 + half, x0 : x0 + half] = self.eye_profile(colours["iris"], angle).T[
            :, :, None
        ]
        return (to_srgb(image) * 255).round().byte().permute(1, 2, 0).numpy()

    def eye_profile(self, iris: torch.Tensor, angle: torch.Tensor) -> torch.Tensor:
        """Linear albedo (N, 3) of the eyeball at angles (N,) from straight ahead."""
        n = self.neutral
        ir, pupil = n["iris_rad"], n["pupil_rad"]
        sclera = torch.tensor(n["sclera"])
        a = angle[:, None]
        ring = iris * (1 - (1 - n["limbus"]) * _smooth(0.75 * ir, 0.97 * ir, a))
        eye = ring + (sclera - ring) * _smooth(ir - 0.02, ir + 0.02, a)
        return eye + (0.008 - eye) * (1 - _smooth(pupil - 0.015, pupil + 0.015, a))

    # --- the person ------------------------------------------------------------------------
    def build(
        self,
        phenotype: Mapping[str, float],
        coefficients: Sequence[float],
        pigments: Mapping[str, float],
    ) -> Person:
        rest = self.rest(phenotype, coefficients)
        vertices, skeleton, weights = self.rig(rest)
        colours = self.albedo(pigments)
        return self._assemble(vertices, skeleton, weights, colours, self.bake(colours))

    def _assemble(
        self,
        vertices: np.ndarray,
        skeleton: Skeleton,
        weights: np.ndarray,
        colours: Mapping[str, torch.Tensor],
        texture: np.ndarray,
    ) -> Person:
        """The GLB's surfaces of a rigged, painted person: the body (one glTF vertex per UV point)
        and the eyeballs, with the metadata."""
        # the body: one glTF vertex per UV point
        top = np.argsort(-weights, axis=1)[:, :INFLUENCES]
        top_w = np.take_along_axis(weights, top, axis=1)
        kept = top_w.sum(1)
        top_w = top_w / kept[:, None]
        normals = vertex_normals(vertices, self.tris)
        k = self.uv_vertex
        body = Surface(
            "body",
            vertices[k],
            normals[k],
            self.atlas_uv,
            top[k],
            top_w[k],
            self.tris_uv,
            SKIN_ROUGHNESS,
        )

        # the eyeballs: at the eye joints, touching the nearest lid rim point
        surfaces, radii = [body], {}
        names = skeleton.names
        rim = vertices[self.rim]
        half = TEXTURE // 2
        for side in ("Left", "Right"):
            j = names.index(f"{side}Eye")
            centre = skeleton.world[j, :3, 3]
            near = rim[(rim[:, 0] > 0) == (centre[0] > 0)]
            r = float(np.clip(np.linalg.norm(near - centre, axis=1).min() + 0.0003, *EYE_RADIUS_M))
            points, angle, tris = eyeball(centre, r)
            uv = np.stack(
                [
                    np.full_like(angle, (EYE_QUARTER[0] * half + half / 2) / TEXTURE),
                    (EYE_QUARTER[1] * half + angle / math.pi * half) / TEXTURE,
                ],
                1,
            ).astype(np.float32)
            normals = ((points - centre) / r).astype(np.float32)
            joints = np.zeros((len(points), INFLUENCES), dtype=np.int64)
            joints[:, 0] = j
            w = np.zeros((len(points), INFLUENCES), dtype=np.float32)
            w[:, 0] = 1
            surfaces.append(
                Surface(f"eye_{side.lower()}", points, normals, uv, joints, w, tris, EYE_ROUGHNESS)
            )
            radii[side.lower()] = round(r * 1000, 2)
        meta = {
            "height_m": round(float(vertices[:, 1].max() - vertices[:, 1].min()), 3),
            "eyeball_radius_mm": radii,
            "skin_weight_kept": {
                "min": round(float(kept.min()), 3),
                "mean": round(float(kept.mean()), 4),
            },
            "albedo_srgb": {
                key: [round(float(x), 3) for x in to_srgb(colours[key])]
                for key in ("lips", "eyelids", "ears", "brows", "iris")
            },
        }
        return Person(surfaces, skeleton, texture, meta)


def png(image: np.ndarray) -> bytes:
    out = io.BytesIO()
    Image.fromarray(image).save(out, format="PNG", optimize=False, compress_level=6)
    return out.getvalue()
