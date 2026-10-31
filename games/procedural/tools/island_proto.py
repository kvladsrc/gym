"""Numeric prototype for DESIGN.md: three ways to make island relief.

A  noise relief, hydrology afterwards
B  uplift + implicit stream power, time-stepped (Braun & Willett 2013)
C  uplift + stream-power steady state, solved directly along the network

All share one island mask and one hydrology core: Priority-Flood without
epsilon, steepest-descent receivers (flood parents on flats), a donor-list
stack and flow accumulation. Prints JSON metrics, optionally coarse ASCII
maps; no images.

    uv run --no-project --with numpy --with scipy \
        python -I games/procedural/tools/island_proto.py --size 256
"""

import argparse
import heapq
import json
import time
from collections import deque

import numpy as np
from scipy.ndimage import zoom

SQRT2 = 2**0.5
NB = [
    (-1, -1, SQRT2),
    (-1, 0, 1.0),
    (-1, 1, SQRT2),
    (0, -1, 1.0),
    (0, 1, 1.0),
    (1, -1, SQRT2),
    (1, 0, 1.0),
    (1, 1, SQRT2),
]


def fbm(rng, n, base=3, octaves=5, persistence=0.5):
    """Value-noise fBm in [0, 1]."""
    out = np.zeros((n, n))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        g = base * 2**o
        z = zoom(rng.random((g + 4, g + 4)), n / g, order=3)
        out += amp * z[:n, :n]
        total += amp
        amp *= persistence
    out /= total
    return (out - out.min()) / (out.max() - out.min())


def island_shape(rng, n):
    """Positive on land: radial falloff plus coastline noise."""
    yy, xx = (np.mgrid[0:n, 0:n] / (n - 1)) * 2 - 1
    r = np.hypot(xx, yy)
    shape = 1 - r / 0.8 + 0.45 * (fbm(rng, n) - 0.5)
    shape[r > 0.95] = -1
    return shape


def flood(h, ocean):
    """Priority-Flood without epsilon (Barnes et al. 2014).

    Returns the fill level (flat over pits and lakes) and, for each land
    cell, the neighbour it was flooded from. Ties pop first-in first-out,
    so parents on flats lead to the outlet by a shortest path.
    """
    n = h.shape[0]
    f = h.ravel().copy()
    f[ocean.ravel()] = 0.0
    parent = np.full(n * n, -1)
    seen = ocean.ravel().copy()
    inner = (
        np.roll(ocean, 1, 0)
        & np.roll(ocean, -1, 0)
        & np.roll(ocean, 1, 1)
        & np.roll(ocean, -1, 1)
    )
    heap = [(0.0, k, int(c)) for k, c in enumerate(np.flatnonzero(ocean & ~inner))]
    heapq.heapify(heap)
    tick = len(heap)
    while heap:
        z, _, c = heapq.heappop(heap)
        cy, cx = divmod(c, n)
        for dy, dx, _ in NB:
            ny, nx = cy + dy, cx + dx
            if 0 <= ny < n and 0 <= nx < n:
                k = ny * n + nx
                if not seen[k]:
                    seen[k] = True
                    f[k] = max(f[k], z)
                    parent[k] = c
                    heapq.heappush(heap, (f[k], tick, k))
                    tick += 1
    return f.reshape(n, n), parent


def receivers(f, parent, ocean):
    """Steepest descent on the fill level; the flood parent on flats."""
    n = f.shape[0]
    pad = np.pad(f, 1, constant_values=np.inf)
    best = np.zeros((n, n))
    rcv = np.full((n, n), -1)
    dist = np.ones((n, n))
    iy, ix = np.mgrid[0:n, 0:n]
    for dy, dx, d in NB:
        s = (f - pad[1 + dy : 1 + dy + n, 1 + dx : 1 + dx + n]) / d
        better = s > best
        best = np.where(better, s, best)
        rcv = np.where(better, (iy + dy) * n + (ix + dx), rcv)
        dist = np.where(better, d, dist)
    rcv, dist = rcv.ravel(), dist.ravel()
    flat = (best.ravel() <= 0) & ~ocean.ravel()
    rcv[flat] = parent[flat]
    py, px = np.divmod(parent[flat], n)
    fy, fx = np.divmod(np.flatnonzero(flat), n)
    dist[flat] = np.where((py != fy) & (px != fx), SQRT2, 1.0)
    rcv[ocean.ravel()] = -1
    return rcv, dist


def stack(rcv, ocean):
    """Receivers before donors: breadth-first from the sea over donor lists."""
    oc = ocean.ravel()
    land = np.flatnonzero(~oc)
    r = rcv[land]
    by_rcv = np.argsort(r, kind="stable")
    keys, donors = r[by_rcv], land[by_rcv]
    cells = np.arange(rcv.size)
    start = np.searchsorted(keys, cells)
    end = np.searchsorted(keys, cells, side="right")
    queue = deque(land[oc[r]])
    order = []
    while queue:
        c = queue.popleft()
        order.append(c)
        queue.extend(donors[start[c] : end[c]])
    assert len(order) == land.size, "a land cell does not drain to the sea"
    return np.array(order)


def accumulate(order, rcv, dist):
    """Upstream area (cells) and longest upstream path (cells, incl. self)."""
    area = np.ones(rcv.size)
    length = np.ones(rcv.size)
    for i in order[::-1]:
        r = rcv[i]
        if r >= 0:
            area[r] += area[i]
            length[r] = max(length[r], length[i] + dist[i])
    return area, length


def route(h, ocean):
    f, parent = flood(h, ocean)
    rcv, dist = receivers(f, parent, ocean)
    order = stack(rcv, ocean)
    area, length = accumulate(order, rcv, dist)
    return f, rcv, dist, order, area, length


def strahler(order, rcv, chan):
    so = np.zeros(chan.size, dtype=int)
    cnt = np.zeros(chan.size, dtype=int)
    for i in order[::-1]:
        if not chan[i]:
            continue
        so[i] = 1 if so[i] == 0 else so[i] + (cnt[i] >= 2)
        r = rcv[i]
        if r >= 0 and chan[r]:
            if so[i] > so[r]:
                so[r], cnt[r] = so[i], 1
            elif so[i] == so[r]:
                cnt[r] += 1
    return so


def fit(x, y):
    """Log-log slope and its standard error."""
    (b, _), cov = np.polyfit(np.log(x), np.log(y), 1, cov=True)
    return round(float(b), 3), round(float(np.sqrt(cov[0, 0])), 3)


def metrics(h, ocean, name, channel, extra):
    land = ~ocean
    f, rcv, dist, order, area, length = route(h, ocean)
    hh = np.where(ocean, 0.0, h).ravel()
    pit = (f.ravel() - hh) > 0
    chan = (area >= channel * land.sum()) & land.ravel()
    so = strahler(order, rcv, chan)
    # Concavity on physical heights, outside pits, where the bed descends.
    slope = (hh - hh[np.maximum(rcv, 0)]) / dist
    sel = chan & ~pit & (slope > 0)
    theta, theta_se = fit(area[sel], slope[sel])
    hack, hack_se = fit(area[chan], length[chan])
    # Hack across independent basins: mouths draining >= 0.5 % of the land.
    mouth = land.ravel() & (rcv >= 0) & ocean.ravel()[np.maximum(rcv, 0)]
    big = mouth & (area >= 0.005 * land.sum())
    basin_hack = fit(area[big], length[big])[0] if big.sum() >= 3 else None
    out = {
        "variant": name,
        "land_fraction": round(float(land.mean()), 3),
        "channel_cells_fraction": round(float(chan.sum() / land.sum()), 4),
        "max_strahler": int(so.max()),
        "concavity_theta": -theta,
        "theta_se": theta_se,
        "hack_pooled": hack,
        "hack_se": hack_se,
        "hack_basins": basin_hack,
        "basins": int(big.sum()),
        "pit_cells_fraction": round(float(pit[land.ravel()].mean()), 4),
    }
    out.update(extra)
    return out, chan


def uplift_field(shape, rng):
    n = shape.shape[0]
    return np.clip(shape, 0, None) * (0.4 + 0.6 * fbm(rng, n, 2, 3))


def time_stepped(shape, rng, iters, dt, m=0.5, k=1.0):
    """B: uplift, fill pits (sediment), route, implicit erosion with n = 1."""
    n = shape.shape[0]
    ocean = shape <= 0
    land = ~ocean
    u = uplift_field(shape, rng)
    h = np.where(land, 1e-3 * rng.random((n, n)), 0.0)
    t0 = time.perf_counter()
    for _ in range(iters):
        h[land] += dt * u[land]
        f, rcv, dist, order, area, _ = route(h, ocean)
        h = f.ravel().copy()
        for i in order:  # receivers before donors
            r = rcv[i]
            fac = k * dt * area[i] ** m / dist[i]
            h[i] = (h[i] + fac * (h[r] if r >= 0 else 0.0)) / (1 + fac)
        h = h.reshape(n, n)
    return h, ocean, {"iterations": iters, "dt": dt, "seconds": _since(t0)}


def steady_state(shape, rng, iters, m=0.5, k=1.0):
    """C: rebuild heights from the network: h = h_rcv + U d / (K A^m)."""
    n = shape.shape[0]
    ocean = shape <= 0
    u = uplift_field(shape, rng).ravel()
    h = np.where(ocean, 0.0, 1e-3 * rng.random((n, n)))
    prev, changed, t0 = None, [], time.perf_counter()
    for _ in range(iters):
        _, rcv, dist, order, area, _ = route(h, ocean)
        hn = np.zeros(n * n)
        for i in order:
            r = rcv[i]
            hn[i] = (hn[r] if r >= 0 else 0.0) + u[i] * dist[i] / (k * area[i] ** m)
        if prev is not None:
            changed.append(round(float((rcv != prev).mean()), 4))
        prev, h = rcv, hn.reshape(n, n)
    extra = {
        "iterations": iters,
        "seconds": _since(t0),
        "receivers_changed_last": changed[-3:],
    }
    return h, ocean, extra


def _since(t0):
    return round(time.perf_counter() - t0, 1)


def ascii_map(h, ocean, chan, step):
    n = h.shape[0]
    hmax = h[~ocean].max()
    c = chan.reshape(n, n)
    rows = []
    for y in range(0, n, step):
        row = ""
        for x in range(0, n, step):
            blk = (slice(y, y + step), slice(x, x + step))
            if ocean[blk].mean() > 0.5:
                row += "~"
            elif c[blk].any():
                row += "#"
            else:
                row += " .:-=+*%@"[min(8, int(9 * h[blk].mean() / hmax))]
        rows.append(row)
    return "\n".join(rows)


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--size", type=int, default=256)
    p.add_argument("--seed", type=int, default=7)
    p.add_argument("--iters", type=int, default=40, help="B iterations")
    p.add_argument("--steady-iters", type=int, default=15, help="C iterations")
    p.add_argument("--dt", type=float, default=25.0)
    p.add_argument("--channel", type=float, default=0.002, help="land fraction")
    p.add_argument("--maps", action="store_true", help="print ASCII maps")
    a = p.parse_args()
    n = a.size
    shape = island_shape(np.random.default_rng(a.seed), n)
    ocean = shape <= 0
    noise = fbm(np.random.default_rng(a.seed + 1), n, base=4, octaves=6)
    variants = [
        ("A_noise", np.where(ocean, 0.0, np.clip(shape, 0, None) * (0.3 + noise)), {})
    ]
    rng = np.random.default_rng(a.seed + 2)
    h_b, _, extra_b = time_stepped(shape, rng, a.iters, a.dt)
    variants.append(("B_time_stepped", h_b, extra_b))
    rng = np.random.default_rng(a.seed + 2)
    h_c, _, extra_c = steady_state(shape, rng, a.steady_iters)
    variants.append(("C_steady_state", h_c, extra_c))
    results = []
    for name, h, extra in variants:
        res, chan = metrics(h, ocean, name, a.channel, extra)
        results.append(res)
        if a.maps:
            print(name)
            print(ascii_map(h, ocean, chan, max(1, n // 64)))
    print(json.dumps(results, indent=1))


if __name__ == "__main__":
    main()
