"""A small triangle rasteriser in plain PyTorch (CPU or CUDA, no compiled extension).

Orthographic views of whole batches: every triangle's pixel box is expanded into
(triangle, pixel) pairs, the pairs inside the triangle compete in a z-buffer through one
scatter_reduce(amin) over a key of quantised depth and triangle index, and the winners'
barycentric coordinates are recomputed per pixel. Pixel centres sit at (x + 0.5, y + 0.5);
x grows to the right, y downwards."""

import torch

INT64_MAX = torch.iinfo(torch.int64).max


def rasterize(xy, depth, tris, height, width, max_pairs=6_000_000):
    """xy (B, V, 2) pixel coordinates, depth (B, V) (smaller is nearer), tris (T, 3) long.

    Returns tri (B, H, W) long, -1 where nothing is drawn, and bary (B, H, W, 3)."""
    B, T = xy.shape[0], tris.shape[0]
    corners = xy[:, tris]  # (B, T, 3, 2)
    zc = depth[:, tris]  # (B, T, 3)
    lo, hi = corners.amin(2), corners.amax(2)
    x0 = torch.ceil(lo[..., 0] - 0.5).clamp(min=0).long()
    x1 = torch.floor(hi[..., 0] - 0.5).clamp(max=width - 1).long()
    y0 = torch.ceil(lo[..., 1] - 0.5).clamp(min=0).long()
    y1 = torch.floor(hi[..., 1] - 0.5).clamp(max=height - 1).long()
    a, b, c = corners.unbind(2)
    area = (b[..., 0] - a[..., 0]) * (c[..., 1] - a[..., 1]) - (b[..., 1] - a[..., 1]) * (c[..., 0] - a[..., 0])
    nx, ny = (x1 - x0 + 1).clamp(min=0), (y1 - y0 + 1).clamp(min=0)
    count = torch.where(area.abs() > 1e-12, nx * ny, torch.zeros_like(nx))

    tri_out = torch.full((B, height, width), -1, dtype=torch.long, device=xy.device)
    per_item = count.sum(1)
    start = 0
    while start < B:  # batches of items whose pairs fit in memory
        stop = start + 1
        total = int(per_item[start])
        while stop < B and total + int(per_item[stop]) <= max_pairs:
            total += int(per_item[stop])
            stop += 1
        sl = slice(start, stop)
        n = count[sl].reshape(-1)
        g = stop - start
        pair_tri = torch.repeat_interleave(torch.arange(g * T, device=xy.device), n)
        offset = torch.cumsum(n, 0) - n
        local = torch.arange(pair_tri.numel(), device=xy.device) - offset[pair_tri]
        w = nx[sl].reshape(-1)[pair_tri]
        px = x0[sl].reshape(-1)[pair_tri] + local % w
        py = y0[sl].reshape(-1)[pair_tri] + local // w
        A, Bc, C = (v.reshape(-1, 2)[pair_tri] for v in (a[sl], b[sl], c[sl]))
        wts = _bary(A, Bc, C, area[sl].reshape(-1)[pair_tri], px, py)
        inside = (wts >= -1e-6).all(1)
        pair_tri, px, py, wts = pair_tri[inside], px[inside], py[inside], wts[inside]
        z = (wts * zc[sl].reshape(-1, 3)[pair_tri]).sum(1)
        if z.numel():
            zmin, zmax = z.min(), z.max()
            q = ((z - zmin) / (zmax - zmin + 1e-12) * (2**31 - 1)).long()
            item = pair_tri // T
            key = q * 2**32 + (pair_tri % T)
            pixel = (item * height + py) * width + px
            zbuf = torch.full((g * height * width,), INT64_MAX, dtype=torch.long, device=xy.device)
            zbuf.scatter_reduce_(0, pixel, key, reduce="amin")
            hit = zbuf != INT64_MAX
            tri_out[sl].view(-1)[hit] = zbuf[hit] % 2**32
        start = stop

    # barycentrics of the winning triangle at every drawn pixel
    bary = torch.zeros((B, height, width, 3), dtype=xy.dtype, device=xy.device)
    drawn = tri_out >= 0
    item, py, px = drawn.nonzero(as_tuple=True)
    t = tri_out[drawn]
    A, Bc, C = a[item, t], b[item, t], c[item, t]
    bary[drawn] = _bary(A, Bc, C, area[item, t], px, py)
    return tri_out, bary


def _bary(a, b, c, area, px, py):
    cx, cy = px.to(a.dtype) + 0.5, py.to(a.dtype) + 0.5
    wa = ((b[:, 0] - cx) * (c[:, 1] - cy) - (b[:, 1] - cy) * (c[:, 0] - cx)) / area
    wb = ((c[:, 0] - cx) * (a[:, 1] - cy) - (c[:, 1] - cy) * (a[:, 0] - cx)) / area
    return torch.stack([wa, wb, 1 - wa - wb], 1)


def interpolate(attr, tris, tri, bary):
    """attr (B, V, C) per vertex, or (N, C) shared by the batch with tris indexing it.
    Returns (B, H, W, C); zeros where tri < 0."""
    B, H, W = tri.shape
    out = torch.zeros((B, H, W, attr.shape[-1]), dtype=bary.dtype, device=bary.device)
    drawn = tri >= 0
    item = drawn.nonzero(as_tuple=True)[0]
    corner = tris[tri[drawn]]  # (P, 3)
    values = attr[corner] if attr.dim() == 2 else attr[item[:, None], corner]
    out[drawn] = (bary[drawn][..., None] * values).sum(1)
    return out


def vertex_normals(verts, tris):
    """Area-weighted vertex normals, (B, V, 3)."""
    v0, v1, v2 = (verts[:, tris[:, k]] for k in range(3))
    fn = torch.cross(v1 - v0, v2 - v0, dim=-1)
    n = torch.zeros_like(verts)
    for k in range(3):
        n.index_add_(1, tris[:, k], fn)
    return torch.nn.functional.normalize(n, dim=-1)
