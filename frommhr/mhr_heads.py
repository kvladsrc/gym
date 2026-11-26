"""MHR people on SOMA's mesh, the training data of frommhr's reader.

The shape: MHR's identity (Meta's Momentum Human Rig as SOMA-X ships it, LOD1) carried onto
SOMA's mesh by SOMA-X, as a linear model made by prep.py: mean + body (MHR coefficients 0-19) +
head (20-39). MHR has no age or sex parameter and was built from adult scans only.

SOMA's mesh has no eyeballs: spheres at the eye joints (linear in the coefficients like the
shape), as large as the lid rims allow. Pictures come from heads.py's renderer and colour.py's
looks; Anny's feature masks (lips, eyelids, ears, brows) are baked into SOMA's UVs by prep.py.

Frames: SOMA's (metres, Y up, face towards +Z) in the data, Anny's (Z up, face towards -Y, the
figure's left +X) for the renderer."""

import math

import torch

from heads import EYE, Heads
from raster import vertex_normals

EYE_RADIUS_M = (0.009, 0.0135)  # the eyeballs' radius stays in this range
NB = NH = 20  # MHR's body and head coefficients


def to_anny(v):
    """SOMA/MHR frame -> Anny's (a rotation)."""
    return torch.stack([v[..., 0], -v[..., 2], v[..., 1]], -1)


def unit_sphere(rings=24, segments=32):
    """Points (N, 3) and triangles (T, 3) of a latitude-longitude sphere, poles on Z (up)."""
    pts = [torch.tensor([0.0, 0.0, 1.0])]
    for i in range(1, rings):
        t = math.pi * i / rings
        for j in range(segments):
            p = 2 * math.pi * j / segments
            pts.append(torch.tensor([math.sin(t) * math.cos(p), math.sin(t) * math.sin(p), math.cos(t)]))
    pts.append(torch.tensor([0.0, 0.0, -1.0]))
    tris = []
    ring = lambda i, j: 1 + (i - 1) * segments + j % segments
    for j in range(segments):
        tris.append([0, ring(1, j), ring(1, j + 1)])
    for i in range(1, rings - 1):
        for j in range(segments):
            a, b, c, d = ring(i, j), ring(i, j + 1), ring(i + 1, j), ring(i + 1, j + 1)
            tris += [[a, c, b], [b, c, d]]
    last = len(pts) - 1
    for j in range(segments):
        tris.append([last, ring(rings - 1, j + 1), ring(rings - 1, j)])
    return torch.stack(pts), torch.tensor(tris, dtype=torch.long)


class MhrHeads(Heads):
    """Heads' renderer on SOMA's mesh with MHR people. A person's vertices are SOMA's followed by
    the left and the right eyeball."""

    def __init__(self, data, device="cpu"):
        self.device = dev = torch.device(device)
        d = {k: (v.to(dev) if torch.is_tensor(v) else v) for k, v in data.items()}
        self.mean, self.body, self.head = to_anny(d["mean"]), to_anny(d["body"]), to_anny(d["head"])
        self.eye0, self.eye_basis = to_anny(d["eye_joints"]), to_anny(d["eye_basis"])
        self.rim = [r.to(dev).long() for r in d["rim"]]
        V, U = self.mean.shape[0], d["uv"].shape[0]
        self.V = V
        sphere, stris = unit_sphere()
        self.sphere, n = sphere.to(dev), sphere.shape[0]
        self.n_sphere = n
        tris = d["tris"].long()
        # outward normals: the sphere's winding follows SOMA's
        soma_sign = self._sign(self.mean, tris, d["head_vertices"].long())
        ball_sign = self._sign(self.sphere, stris.to(dev), torch.arange(n, device=dev))
        if ball_sign != soma_sign:
            stris = stris[:, [0, 2, 1]]
        stris = stris.to(dev)
        self.tris = torch.cat([tris, stris + V, stris + V + n])
        self.tris_uv = torch.cat([d["tris_uv"].long(), stris + U, stris + U + n])
        self.uv = torch.cat([d["uv"].float(), torch.full((2 * n, 2), -1.0, device=dev)])
        self.material = torch.cat([d["material"].long(), torch.full((2 * stris.shape[0],), EYE, device=dev)])
        self.eye_vertices = [torch.arange(V, V + n, device=dev), torch.arange(V + n, V + 2 * n, device=dev)]
        self.head_vertices = torch.cat([d["head_vertices"].long(), torch.arange(V, V + 2 * n, device=dev)])
        self.masks = d["masks"].float() / 255  # lips, eyelids, ears, brows thin / medium / thick
        self.normal_sign = soma_sign
        self.template = self.person(torch.zeros(1, NB, device=dev), torch.zeros(1, NH, device=dev))[0]

    @staticmethod
    def _sign(points, tris, which):
        n = vertex_normals(points[None], tris)[0, which]
        p = points[which]
        return 1.0 if float((n * (p - p.mean(0))).sum(-1).mean()) > 0 else -1.0

    def surface(self, b, c):
        """SOMA's vertices (B, V, 3) of body coefficients b (B, 20) and head coefficients c."""
        return self.mean + torch.einsum("bj,jvc->bvc", b, self.body) + torch.einsum("bj,jvc->bvc", c, self.head)

    def eyes(self, b, c):
        """The eye joints (B, 2, 3): left, right."""
        return self.eye0 + torch.einsum("bj,jkc->bkc", torch.cat([b, c], 1), self.eye_basis)

    def with_eyes(self, surface, centres):
        """SOMA's vertices and the eyeballs: each centred on its joint, radius 0.3 mm beyond the
        nearest lid-rim vertex of its side, within EYE_RADIUS_M."""
        radii = []
        for k in range(2):
            gap = (surface[:, self.rim[k]] - centres[:, k : k + 1]).norm(dim=-1).amin(1)
            radii.append((gap + 0.0003).clamp(*EYE_RADIUS_M))
        r = torch.stack(radii, 1)
        balls = centres[:, :, None] + r[:, :, None, None] * self.sphere
        return torch.cat([surface, balls.flatten(1, 2)], 1)

    def person(self, b, c):
        """(B, V + eyeballs, 3) in Anny's frame, MHR's A-pose; differentiable."""
        return self.with_eyes(self.surface(b, c), self.eyes(b, c))
