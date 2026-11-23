"""frommhr's person: one MHR head on SOMA's mesh with the body of the character's Anny
parameters, the head from the reader.

The package holds MHR's identity as a linear model on SOMA's mesh (made from MHR's own model
through SOMA-X, which carries it onto SOMA's mesh): the mean, the 20 body coefficients' directions
(MHR's 0-19), the head's 20 (MHR's 20-39, smoothed around the mouth and eyes as in training) and
the bone scales that move the body. The body: Anny's body of the given parameters is put into
SOMA's bind pose, and MHR's body coefficients and scales are fitted to it by least squares (the
head, hands, mouth and eye bags left out; a prior keeps the coefficients near MHR's mean). The
head: the reader's coefficients. Rest shape = mean + body + scales + head, then SOMA-X's
skeleton fit and bind pose, as for fromsoma.

MHR's body space, from adult scans, cannot reach Anny's bodies: the fit leaves 1-2 cm rms,
shoulders, upper arms and neck thicker and the shoulder joints 0.5-1 cm wider. So what it leaves
is added back below the head: Anny's body in full from NECK_FADE_M below the head's vertices on,
faded in over that distance, the head MHR's as the reader made it. One mesh, nothing stitched;
the body is the one fromsoma makes for the same parameters, to a few millimetres.

Colours per SOMA vertex from the pigments and the package's regional weights (cheeks, nose,
under the eyes, beard), the features (lips, eyelids, ears, brows) from masks baked into SOMA's
first UV tile, which is the atlas's first quarter texel for texel.

Coordinates: metres, Y up, the face towards +Z (SOMA's and glTF's).
"""

from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any

import numpy as np
import torch
from scipy.spatial import cKDTree

from fromsoma_model_server.glb import Skeleton
from fromsoma_model_server.person import MOUTH, SOCKET, TEXTURE, Bodies, Palette, Person

NECK_FADE_M = 0.06  # Anny's body in full this far below the head's vertices
HEAD_WEIGHT = 0.5  # the head's vertices: at least this much skinning on Head and its children


def head_vertices(weights: np.ndarray, skeleton: Skeleton) -> np.ndarray:
    """The vertices skinned mostly (HEAD_WEIGHT) to the head: Head and the joints below it (the
    jaw, the eyes, the head's end)."""
    head = {skeleton.names.index("Head")}
    for joint, parent in enumerate(skeleton.parents):
        if parent in head:
            head.add(joint)
    return weights[:, sorted(head)].sum(1) > HEAD_WEIGHT


def neck_blend(vertices: np.ndarray, head: np.ndarray, fade: float = NECK_FADE_M) -> np.ndarray:
    """How much of Anny's body each vertex takes: 0 on the head's vertices, rising smoothly to 1
    at `fade` metres from the nearest of them."""
    distance = cKDTree(vertices[head]).query(vertices)[0]
    t = np.clip(distance / fade, 0.0, 1.0)
    t[head] = 0.0
    return t * t * (3.0 - 2.0 * t)


def rigid_residual(moved: np.ndarray, kept: np.ndarray) -> np.ndarray:
    """How far each point of `moved` (N, 3) stays from `kept` after the rigid motion that best
    lays one on the other (least squares, no scaling)."""
    a, b = moved - moved.mean(0), kept - kept.mean(0)
    u, _, vt = np.linalg.svd(a.T @ b)
    flip = np.diag([1.0, 1.0, np.sign(np.linalg.det(vt.T @ u.T))])
    return np.linalg.norm(a @ (vt.T @ flip @ u.T).T - b, axis=1)


def _mm(error: np.ndarray) -> dict[str, float]:
    return {
        "rms": round(float(np.sqrt((error**2).mean())) * 1000, 1),
        "p95": round(float(np.quantile(error, 0.95)) * 1000, 1),
    }


class MhrBodies(Bodies):
    """Anny (for the body's target), SOMA-X and the package's MHR model, loaded once."""

    def __init__(self, package: Mapping[str, Any], soma_root: Path) -> None:
        import anny
        from anny.shape_distribution import SimpleShapeDistribution
        from soma.body.soma import SOMALayer

        self.anny = anny.create_fullbody_model(
            all_phenotypes=True, local_changes=True, remove_unattached_vertices=True
        ).to(dtype=torch.float32)
        self.ages = SimpleShapeDistribution(self.anny).morphological_age_mapping
        self.config = package["config"]
        model, fit = package["model"], package["fit"]
        self.mean = model["mean"].float()
        self.directions = torch.cat([model["body"], model["scales"], model["head"]]).float()
        self.nb, self.ns = model["body"].shape[0], model["scales"].shape[0]
        self.regions = {k: v.float() for k, v in package["regions"].items()}
        self.masks = package["masks"]
        self.mask_index = {name: i for i, name in enumerate(package["mask_names"])}
        self.neutral = package["neutral"]
        self.palette = Palette(package["colour"])
        self.layer = SOMALayer(data_root=soma_root, identity_model_type="anny", device="cpu")
        self._soma_mesh(soma_root)
        if self.mean.shape[0] != int(self.tris.max()) + 1:
            raise RuntimeError("the package's MHR model has another mesh than SOMA-X's")

        self._prepare_fit(fit)

    def _prepare_fit(self, fit: Mapping[str, Any]) -> None:
        """The body's least squares: bind-pose directions of the body coefficients and scales and
        a shift, on the vertices the fit uses; the prior per unknown (m^2 per unit^2)."""
        self.use = fit["use"].bool()
        self.mean_bind = fit["mean_bind"].double()
        directions = torch.cat([fit["body_bind"], fit["scale_bind"]]).double()[:, self.use]
        a = directions.reshape(directions.shape[0], -1).T
        shift = torch.zeros(a.shape[0], 3, dtype=torch.float64)
        shift.view(-1, 3, 3)[:, torch.arange(3), torch.arange(3)] = 1
        self.fit_matrix = torch.cat([a, shift], 1)
        prior = torch.tensor(
            [fit["prior_body"]] * self.nb + [fit["prior_scales"]] * self.ns + [0.0] * 3,
            dtype=torch.float64,
        )
        self.fit_factor = torch.linalg.cholesky(
            self.fit_matrix.T @ self.fit_matrix + torch.diag(prior)
        )

    # --- shape ----------------------------------------------------------------------------
    @torch.no_grad()
    def anny_body(self, phenotype: Mapping[str, float]) -> np.ndarray:
        """Anny's body of the given phenotype on SOMA's mesh in SOMA's bind pose (Vs, 3)."""
        phen = {k: torch.tensor([float(v)]) for k, v in phenotype.items()}
        coeffs = self.anny.get_phenotype_blendshape_coefficients(
            **self.anny.parse_phenotype_kwargs(phen), local_changes={}
        )
        vertices, _, _ = self._rig("get_rest_shape", self.anny.get_rest_vertices(coeffs))
        return vertices

    @torch.no_grad()
    def fit_body(self, phenotype: Mapping[str, float]) -> tuple[torch.Tensor, torch.Tensor, dict]:
        """MHR's body coefficients (20,) and scales (K,) closest to Anny's body; the fit's
        residual (mm) and Anny's height (m)."""
        return self.solve(torch.from_numpy(self.anny_body(phenotype)))

    def solve(self, target: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, dict]:
        """The least squares for a bind-pose body `target` (Vs, 3)."""
        target = target.double()
        rhs = (target - self.mean_bind)[self.use].reshape(-1)
        sol = torch.cholesky_solve((self.fit_matrix.T @ rhs)[:, None], self.fit_factor)[:, 0]
        fitted = (self.fit_matrix @ sol).reshape(-1, 3) + self.mean_bind[self.use]
        error = (fitted - target[self.use]).norm(dim=-1)
        report = {
            "residual_mm": {
                "rms": round(float(error.pow(2).mean().sqrt()) * 1000, 1),
                "p95": round(float(error.quantile(0.95)) * 1000, 1),
            },
            "anny_height_m": round(float(target[:, 1].max() - target[:, 1].min()), 3),
        }
        b, s = sol[: self.nb].float(), sol[self.nb : self.nb + self.ns].float()
        return b, s, report

    def rest_mhr(
        self, body: torch.Tensor, scales: torch.Tensor, head: Sequence[float]
    ) -> torch.Tensor:
        """MHR's rest shape on SOMA's mesh (1, Vs, 3) from body coefficients, scales and the
        head's coefficients."""
        c = torch.cat([body, scales, torch.tensor(list(head), dtype=torch.float32)])
        return (self.mean + torch.einsum("j,jvc->vc", c, self.directions))[None]

    # --- colour ---------------------------------------------------------------------------
    def _texels(
        self, colours: Mapping[str, torch.Tensor], t: torch.Tensor, mix: Any, drawn: torch.Tensor
    ) -> tuple[torch.Tensor, torch.Tensor]:
        """The skin per SOMA vertex; the features' masks are SOMA's first UV tile, the atlas's
        first quarter, texel for texel (no features elsewhere)."""
        vs = colours["skin"].clone()
        vs[self.segments["mouth_bag"]] = torch.tensor(MOUTH)
        vs[self.segments["eye_bags"]] = torch.tensor(SOCKET)
        names = ("lips", "eyelids", "ears", self.neutral["brows"])
        quarter = self.masks[torch.tensor([self.mask_index[k] for k in names])].float() / 255
        q = quarter.shape[-1]
        if q != TEXTURE // 2:
            raise RuntimeError("the masks must be the size of the atlas's quarter")
        rows, cols = drawn.nonzero(as_tuple=True)  # the drawn texels in the order of t
        inside = (rows < q) & (cols < q)
        masks = torch.zeros(len(rows), 4)
        masks[inside] = quarter[:, rows[inside], cols[inside]].T
        return mix(vs), masks

    # --- the person ------------------------------------------------------------------------
    def build_mhr(
        self,
        phenotype: Mapping[str, float],
        head: Sequence[float],
        pigments: Mapping[str, float],
    ) -> tuple[Person, dict[str, Any]]:
        """The person: Anny's body of `phenotype` with the head from `head`, painted from
        `pigments`; and the MHR values with the fit's report.

        SOMA-X's bind pose places a body by its own skeleton (Root on the ground, the body
        centred over it), which the linear fit does not follow: the legs' scales lift or lower
        the whole body (by 3-13 cm on Anny's children and adults). So the rest shape is moved by
        the mean difference to Anny's body over the fitted vertices and rigged again; the rig
        keeps the height of such a shift, and MHR's person stands where Anny's body of the same
        parameters stands.

        Then that person in the bind pose with Anny's body below the head (neck_blend) is rigged
        once more, so the skeleton is fitted to the body it rigs. The re-pose moves the head as a
        whole by a few millimetres and up to 1.5 degrees (its shape kept: head_shape_mm) and the
        body by a few millimetres; a last rig takes back the height of that. mhr_fit_mm is MHR's
        own body against Anny's over the fitted vertices, residual_mm the person's body beyond the
        neck's fade against Anny's."""
        target = torch.from_numpy(self.anny_body(phenotype)).double()
        body, scales, report = self.solve(target)
        rest = self.rest_mhr(body, scales, head)
        first, _, _ = self._rig("forward", rest)
        offset = (target - torch.from_numpy(first).double())[self.use].mean(0)
        rest = rest + offset.float()
        mhr, skeleton, weights = self._rig("forward", rest)
        anny = target.numpy()
        report["mhr_fit_mm"] = _mm(np.linalg.norm(mhr - anny, axis=1)[self.use.numpy()])
        report["placed_mm"] = [round(float(v) * 1000, 1) for v in offset]
        head_mask = head_vertices(weights, skeleton)
        blend = neck_blend(mhr, head_mask)
        goal = mhr + blend[:, None] * (anny - mhr)
        goal = torch.from_numpy(goal.astype(np.float32)).reshape(rest.shape)
        vertices, _, _ = self._rig("forward", goal)
        below = blend >= 1.0
        lift = float((anny - vertices)[below, 1].mean())
        vertices, skeleton, weights = self._rig("forward", goal + torch.tensor([0.0, lift, 0.0]))
        report["residual_mm"] = _mm(np.linalg.norm(vertices - anny, axis=1)[below])
        report["head_shape_mm"] = _mm(rigid_residual(vertices[head_mask], mhr[head_mask]))
        report["neck_fade_m"] = NECK_FADE_M
        colours = self.albedo(pigments)
        person = self._assemble(vertices, skeleton, weights, colours, self.bake(colours))
        report.update(
            mhr_body=[round(float(v), 4) for v in body],
            mhr_scales=[round(float(v), 4) for v in scales],
        )
        return person, report
