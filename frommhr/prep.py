"""The training data on SOMA's mesh, made once on the CPU (a few minutes, about 4 GB of RAM): MHR's
identity through SOMA-X as a linear model, SOMA's triangles and materials, the eye joints, the lid
rims and Anny's feature masks carried into SOMA's UVs.

  - MHR's rest shape on SOMA's mesh (metres, Y up, face towards +Z, MHR's A-pose): mean + body
    (MHR coefficients 0-19, forward differences: linear within 0.4 mm) + head (20-39, central
    differences; MHR is linear in them within 1.8 mm at the worst vertex, 0.04 mm on average).
    The hands (40-44) do not touch the head; the 68 bone scales stay 0. The head's fields are
    then smoothed over 5 mm of the head's skin: taken vertex by vertex they tear the mouth's
    corners open on some people.
  - The eye joints: SOMA's skeleton fit regresses joint positions linearly from the vertices
    (a sparse RBF matrix), so they get the same linear model.
  - The head: vertices skinned mostly to Head, HeadEnd, Jaw and the eyes.
  - Materials: SOMA's mouth bag and eye bags (triangles wholly inside) are cavities.
  - Masks (lips, eyelids, ears, three brow shapes): Anny's (heads.py), baked into SOMA's UV
    tile 0 (head and neck) through SOMA-X's Anny correspondence.

    python prep.py [--soma-x data/soma-x] [--out data/data.pt]
"""

import argparse
from pathlib import Path

import numpy as np
import torch
import torch.nn.functional as F
from soma.body.soma import SOMALayer

from heads import EYE_CAVITY, MOUTH_CAVITY, SKIN, Heads
from raster import rasterize

DATA = Path(__file__).resolve().parent / "data"
MASK_SIZE = 2048
SEAM_UV = 0.05
HEAD_JOINTS = ("Head", "HeadEnd", "Jaw", "LeftEye", "RightEye")
MASK_NAMES = ["lips", "eyelids", "ears", "brows_thin", "brows_medium", "brows_thick"]


def linear_model(ident):
    """MHR's rest shape as mean + body + head directions, and a check against MHR on 8 people."""

    def rest(c):
        c = torch.as_tensor(c, dtype=torch.float32)
        return ident(c, torch.zeros(c.shape[0], 68), kwargs=None, global_scale=1.0)

    E = torch.zeros(1 + 20 + 40, 45)
    E[1:21, :20] = torch.eye(20)
    E[21:41, 20:40] = torch.eye(20)
    E[41:61, 20:40] = -torch.eye(20)
    S = rest(E)
    mean = S[0]
    body = S[1:21] - mean
    head = (S[21:41] - S[41:61]) / 2
    g = torch.Generator().manual_seed(1)
    c = torch.zeros(8, 45)
    c[:, :40] = torch.randn(8, 40, generator=g).clamp(-2.5, 2.5)
    exact = rest(c)
    report(mean, body, head, c, exact, "linear model")
    return mean, body, head, c, exact


def report(mean, body, head, c, exact, what):
    lin = mean + torch.einsum("bj,jvc->bvc", c[:, :20], body) + torch.einsum("bj,jvc->bvc", c[:, 20:40], head)
    err = (exact - lin).norm(dim=-1)
    print(f"{what} against MHR: max {float(err.max()) * 1000:.2f} mm, mean {float(err.mean()) * 1000:.3f} mm")


def smooth_head(head, mean, tris, material, head_vertices, npz):
    """Each head field smoothed over 5 mm of the head's skin (Gaussian kernel regression from the
    skin vertices), the inner vertices far from the skin with a wider kernel (0.7 of the distance
    to the nearest skin vertex). MHR has no mouth or eyes and SOMA-X fills its bags smoothly, so
    taken vertex by vertex the components tear the mouth's corners open on some people."""
    V = mean.shape[0]
    skin_v = np.zeros(V, dtype=bool)
    skin_v[tris[material == SKIN].ravel()] = True
    in_head = np.zeros(V, dtype=bool)
    in_head[head_vertices.numpy()] = True
    in_head[npz["segment_mouth_bag"]] = True
    in_head[npz["segment_eye_bags"]] = True
    donors = torch.from_numpy(np.nonzero(skin_v & in_head)[0])
    targets = torch.from_numpy(np.nonzero(in_head)[0])
    src = head[:, donors]
    smoothed = torch.empty(20, len(targets), 3)
    for i in range(0, len(targets), 2048):
        t_ = targets[i : i + 2048]
        d2 = torch.cdist(mean[t_], mean[donors]).pow(2)
        sigma = torch.maximum(torch.tensor(0.005), 0.7 * d2.amin(1).sqrt())
        k = torch.exp(-(d2 - d2.amin(1, keepdim=True)) / (2 * sigma[:, None] ** 2))
        k = k / k.sum(1, keepdim=True)
        smoothed[:, i : i + 2048] = torch.einsum("td,jdc->jtc", k, src)
    change = (smoothed - head[:, targets]).norm(dim=-1)
    print(
        f"smoothing moved head vertices per unit component: median {float(change.median()) * 1000:.2f} mm,"
        f" p99 {float(change.quantile(0.99)) * 1000:.2f} mm"
    )
    head = head.clone()
    head[:, targets] = smoothed
    return head


def corner_uvs(quads, quads_uv, tris):
    """The UV index of each corner of `tris` (T, 3): the corner's in the quad holding all three."""
    V = int(max(quads.max(), tris.max())) + 1

    def key(t):
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
        raise RuntimeError(f"{int(np.sum(keys[at] != wanted))} of SOMA-X's Anny triangles are not in Anny's quads")
    q = owner[at]
    slot = np.argmax(quads[q][:, None, :] == tris[:, :, None], axis=2)
    return quads_uv[q[:, None], slot]


def pad(image, filled, texels=8):
    """Grow the filled texels of image (C, H, W) by `texels` rings into the empty ones (the mean
    of their filled neighbours), so filtering at an island's edge reads its own colour."""
    image, filled = image.clone(), filled.float()[None]
    for _ in range(texels):
        total = F.avg_pool2d((image * filled)[None], 3, stride=1, padding=1)[0]
        weight = F.avg_pool2d(filled[None], 3, stride=1, padding=1)[0]
        grow = (filled == 0) & (weight > 0)
        image = torch.where(grow, total / weight.clamp(min=1e-6), image)
        filled = torch.where(grow, torch.ones_like(filled), filled)
    return image


def bake_masks(soma_x, tris, tris_uv, uv):
    """Anny's feature masks (heads.py, in Anny's UVs) carried into SOMA's UV tile 0."""
    heads = Heads(texture_size=MASK_SIZE)
    masks = (heads.masks.clamp(0, 1) * 255).round().byte().float() / 255  # (6, S, S), v up
    anny_layer = SOMALayer(data_root=soma_x, identity_model_type="anny", device="cpu")
    interp = anny_layer.identity_model._to_soma_interp
    anny_tris = interp.F_src_torch.cpu().numpy().astype(np.int64)
    face = interp.face_ids.cpu().numpy()
    bary = interp.bary_coords.cpu().numpy().astype(np.float64)
    w = np.clip(bary[:, :3] + bary[:, 3:4] / 3, 0, None)
    w = w / w.sum(1, keepdims=True)
    model = heads.model
    corner_uv = corner_uvs(
        model.faces.long().cpu().numpy(), model.face_texture_coordinate_indices.long().cpu().numpy(), anny_tris[face]
    )
    anny_uv_points = model.texture_coordinates.float().cpu().numpy()
    anny_uv = torch.tensor((w[..., None] * anny_uv_points[corner_uv]).sum(1), dtype=torch.float32)  # (V, 2)
    per_tri = anny_uv[torch.from_numpy(tris)]
    seam = (per_tri[:, :, None] - per_tri[:, None]).norm(dim=-1).amax((1, 2)) > SEAM_UV

    def sample(points):
        grid = torch.stack([points[:, 0] * 2 - 1, 1 - points[:, 1] * 2], -1).reshape(1, -1, 1, 2)
        return F.grid_sample(masks[None], grid, mode="bilinear", align_corners=False)[0, :, :, 0].T

    uv_vertex = np.zeros(len(uv), dtype=np.int64)
    uv_vertex[tris_uv.ravel()] = tris.ravel()
    tile = np.floor(uv[tris_uv].mean(1)[:, 0]).astype(np.int64)
    t0 = np.nonzero(tile == 0)[0]
    xy = torch.from_numpy(np.stack([uv[:, 0] * MASK_SIZE, (1 - uv[:, 1]) * MASK_SIZE], 1)).float()
    tri, bw = rasterize(xy[None], torch.zeros(1, len(xy)), torch.from_numpy(tris_uv[t0]), MASK_SIZE, MASK_SIZE)
    tri, bw = tri[0], bw[0]
    drawn = tri >= 0
    t, bb = torch.from_numpy(t0)[tri[drawn]], bw[drawn]
    corners = torch.from_numpy(uv_vertex)[torch.from_numpy(tris_uv)[t]]  # (N, 3) SOMA vertices
    point_uv = (bb[..., None] * anny_uv[corners]).sum(1)
    values = sample(point_uv)
    vertex_masks = sample(anny_uv)
    at_seam = seam[t]
    values[at_seam] = (bb[at_seam][..., None] * vertex_masks[corners[at_seam]]).sum(1)
    image = torch.zeros(len(MASK_NAMES), MASK_SIZE, MASK_SIZE)
    image[:, drawn] = values.T
    image = pad(image, drawn, 8)
    print("masks baked:", MASK_NAMES, "texels", int(drawn.sum()), "seam texels", int(at_seam.sum()))
    return (image.clamp(0, 1) * 255).round().byte()


def main():
    ap = argparse.ArgumentParser(description="frommhr's training data on SOMA's mesh")
    ap.add_argument("--soma-x", type=Path, default=DATA / "soma-x")
    ap.add_argument("--out", type=Path, default=DATA / "data.pt")
    args = ap.parse_args()
    torch.set_grad_enabled(False)
    torch.set_num_threads(4)

    layer = SOMALayer(data_root=args.soma_x, identity_model_type="mhr", device="cpu")
    mean, body, head, c, exact = linear_model(layer.identity_model)

    # SOMA's rig for the mean: skinning weights, joint names
    layer.prepare_identity(torch.zeros(1, 45), torch.zeros(1, 68))
    view = layer.public_rig_view()
    names = list(view.joint_names)

    # mesh, materials, head, rims
    npz = np.load(args.soma_x / "SOMA_neutral.npz")
    quads = npz["face_vert_indices"].reshape(-1, 4).astype(np.int64)
    quads_uv = npz["uv_indices_st"].reshape(-1, 4).astype(np.int64)
    split = np.array([[0, 1, 2], [0, 2, 3]])
    tris = np.concatenate([quads[:, s] for s in split])
    tris_uv = np.concatenate([quads_uv[:, s] for s in split])
    uv = npz["uv_coord_st"].astype(np.float32)
    V = mean.shape[0]
    material = np.full(len(tris), SKIN, dtype=np.int64)
    for name, mid in (("mouth_bag", MOUTH_CAVITY), ("eye_bags", EYE_CAVITY)):
        inside = np.zeros(V, dtype=bool)
        inside[npz[f"segment_{name}"]] = True
        material[inside[tris].all(1)] = mid
    weights = view.skinning_weights
    weights = (weights.to_dense() if weights.is_sparse else weights).reshape(V, -1)
    head_weight = weights[:, [names.index(n) for n in HEAD_JOINTS]].sum(1)
    head_vertices = torch.nonzero(head_weight > 0.5)[:, 0]
    print("head vertices:", len(head_vertices))
    bag = np.zeros(V, dtype=bool)
    bag[npz["segment_eye_bags"]] = True
    mixed = bag[tris].any(1) & ~bag[tris].all(1)
    rim = np.unique(tris[mixed][bag[tris[mixed]]])
    rim_left = torch.from_numpy(rim[mean[rim, 0].numpy() > 0])
    rim_right = torch.from_numpy(rim[mean[rim, 0].numpy() <= 0])

    head = smooth_head(head, mean, tris, material, head_vertices, npz)
    report(mean, body, head, c, exact, "smoothed model")

    # eye joints
    fit = layer.skeleton_transfer.fit_joint_positions
    J = fit(mean[None])[0]
    assert J.shape[0] == len(names), "the fit's joints are not the public ones"
    eyes = [names.index("LeftEye"), names.index("RightEye")]
    J0 = J[eyes]
    JB = fit(torch.cat([mean[None] + body, mean[None] + head]))[:, eyes] - J0  # (40, 2, 3)
    Je = fit(exact)[:, eyes]
    Jl = J0 + torch.einsum("bj,jkc->bkc", c[:, :40], JB)
    print(f"eye joints: linear error max {float((Je - Jl).norm(dim=-1).max()) * 1000:.3f} mm")
    for side, r, j in (("left", rim_left, J0[0]), ("right", rim_right, J0[1])):
        d = (mean[r] - j).norm(dim=-1)
        print(
            f"rim {side}: {len(r)} vertices, eye joint to rim {float(d.min()) * 1000:.1f}-{float(d.max()) * 1000:.1f} mm"
        )

    masks = bake_masks(args.soma_x, tris, tris_uv, uv)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    torch.save(
        {
            "format": "frommhr.data.v1",
            "mean": mean,
            "body": body,
            "head": head,
            "eye_joints": J0,
            "eye_basis": JB,
            "tris": torch.from_numpy(tris),
            "tris_uv": torch.from_numpy(tris_uv),
            "uv": torch.from_numpy(uv),
            "material": torch.from_numpy(material),
            "head_vertices": head_vertices,
            "rim": [rim_left, rim_right],
            "masks": masks,
            "mask_names": MASK_NAMES,
            "skin_weights_head": head_weight,
            "joint_names": names,
        },
        args.out,
    )
    print("saved", args.out)


if __name__ == "__main__":
    main()
