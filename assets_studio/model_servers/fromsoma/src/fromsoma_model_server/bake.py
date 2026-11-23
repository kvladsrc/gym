"""Texture space helpers: a small triangle rasteriser in plain PyTorch and island padding.

The rasteriser expands every triangle's pixel box into (triangle, pixel) pairs, keeps the pairs
inside the triangle and lets the lowest triangle index win a pixel; pixel centres sit at
(x + 0.5, y + 0.5), x to the right, y downwards.
"""

import torch
import torch.nn.functional as F


def rasterize(
    xy: torch.Tensor, tris: torch.Tensor, height: int, width: int, max_pairs: int = 8_000_000
) -> tuple[torch.Tensor, torch.Tensor]:
    """xy (V, 2) pixel coordinates, tris (T, 3) long -> tri (H, W) long (-1 where nothing is
    drawn) and bary (H, W, 3)."""
    corners = xy[tris]  # (T, 3, 2)
    lo, hi = corners.amin(1), corners.amax(1)
    x0 = torch.ceil(lo[:, 0] - 0.5).clamp(min=0).long()
    x1 = torch.floor(hi[:, 0] - 0.5).clamp(max=width - 1).long()
    y0 = torch.ceil(lo[:, 1] - 0.5).clamp(min=0).long()
    y1 = torch.floor(hi[:, 1] - 0.5).clamp(max=height - 1).long()
    a, b, c = corners.unbind(1)
    area = (b[:, 0] - a[:, 0]) * (c[:, 1] - a[:, 1]) - (b[:, 1] - a[:, 1]) * (c[:, 0] - a[:, 0])
    nx, ny = (x1 - x0 + 1).clamp(min=0), (y1 - y0 + 1).clamp(min=0)
    count = torch.where(area.abs() > 1e-12, nx * ny, torch.zeros_like(nx))
    owner = torch.full((height * width,), tris.shape[0], dtype=torch.long)
    start, T = 0, tris.shape[0]
    while start < T:  # groups of triangles whose pairs fit in memory
        total = torch.cumsum(count[start:], 0)
        stop = start + max(1, int((total <= max_pairs).sum()))
        n = count[start:stop]
        tri = torch.repeat_interleave(torch.arange(start, stop), n)
        local = torch.arange(tri.numel()) - (torch.cumsum(n, 0) - n)[tri - start]
        w = nx[tri]
        px, py = x0[tri] + local % w, y0[tri] + local // w
        inside = (_bary(a[tri], b[tri], c[tri], area[tri], px, py) >= -1e-6).all(1)
        tri, px, py = tri[inside], px[inside], py[inside]
        owner.scatter_reduce_(0, py * width + px, tri, reduce="amin")
        start = stop
    owner = owner.view(height, width)
    drawn = owner < T
    out = torch.where(drawn, owner, torch.full_like(owner, -1))
    bary = torch.zeros((height, width, 3), dtype=xy.dtype)
    py, px = drawn.nonzero(as_tuple=True)
    t = out[drawn]
    bary[drawn] = _bary(a[t], b[t], c[t], area[t], px, py)
    return out, bary


def _bary(
    a: torch.Tensor,
    b: torch.Tensor,
    c: torch.Tensor,
    area: torch.Tensor,
    px: torch.Tensor,
    py: torch.Tensor,
) -> torch.Tensor:
    cx, cy = px.to(a.dtype) + 0.5, py.to(a.dtype) + 0.5
    wa = ((b[:, 0] - cx) * (c[:, 1] - cy) - (b[:, 1] - cy) * (c[:, 0] - cx)) / area
    wb = ((c[:, 0] - cx) * (a[:, 1] - cy) - (c[:, 1] - cy) * (a[:, 0] - cx)) / area
    return torch.stack([wa, wb, 1 - wa - wb], 1)


def pad(image: torch.Tensor, filled: torch.Tensor, texels: int = 8) -> torch.Tensor:
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
