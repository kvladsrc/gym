"""Dependency-free PNG maps from core dumps, with identical light and metre scale."""

import argparse
import json
import math
import struct
import sys
import zlib
from array import array
from pathlib import Path


def values(data, kind="f"):
    result = array(kind)
    result.frombytes(data)
    if sys.byteorder != "little":
        result.byteswap()
    return result


def load(path):
    raw = path.read_bytes()
    size, cell = struct.unpack("<if", raw[:8])
    heights = values(raw[8:])
    stem = str(path.with_suffix(""))
    surface_path = Path(stem + ".surface.raw")
    layers = None
    lakes = None
    wet = bytearray(size * size)
    if surface_path.exists():
        raw = surface_path.read_bytes()[8:]
        count = size * size
        layers = [values(raw[k * count * 4 : (k + 1) * count * 4]) for k in range(4)]
        lakes = values(raw[count * 16 :], "i")
        for i, lake in enumerate(lakes):
            wet[i] = lake >= 0
    water_path = Path(stem + ".water.json")
    if water_path.exists():
        for river in json.loads(water_path.read_text())["rivers"]:
            for k in range(len(river["X"]) - 1):
                ax, az = river["X"][k] / cell, river["Z"][k] / cell
                bx, bz = river["X"][k + 1] / cell, river["Z"][k + 1] / cell
                dx, dz = bx - ax, bz - az
                length2 = dx * dx + dz * dz
                if length2 < 1e-12:
                    continue
                radius = max(river["Width"][k], river["Width"][k + 1]) / (2 * cell)
                for z in range(
                    max(0, int(min(az, bz) - radius)),
                    min(size - 1, math.ceil(max(az, bz) + radius)) + 1,
                ):
                    for x in range(
                        max(0, int(min(ax, bx) - radius)),
                        min(size - 1, math.ceil(max(ax, bx) + radius)) + 1,
                    ):
                        t = max(0, min(1, ((x - ax) * dx + (z - az) * dz) / length2))
                        width = (
                            river["Width"][k] * (1 - t) + river["Width"][k + 1] * t
                        ) / (2 * cell)
                        if (x - ax - t * dx) ** 2 + (
                            z - az - t * dz
                        ) ** 2 <= width * width:
                            wet[z * size + x] = 1
    return size, cell, heights, layers, wet


def render(data, mode):
    size, cell, heights, layers, wet = data
    pixels = bytearray(size * size * 3)
    palette = ((214, 193, 147), (108, 143, 78), (123, 121, 111), (151, 140, 123))
    for z in range(size):
        for x in range(size):
            i = z * size + x
            if heights[i] <= 0 or wet[i]:
                color = (44, 83, 110) if heights[i] <= 0 else (56, 132, 177)
            elif mode == "water":
                color = (233, 231, 219)
            else:
                dx = (
                    heights[z * size + min(x + 1, size - 1)]
                    - heights[z * size + max(x - 1, 0)]
                ) / (2 * cell)
                dz = (
                    heights[min(z + 1, size - 1) * size + x]
                    - heights[max(z - 1, 0) * size + x]
                ) / (2 * cell)
                shade = max(
                    0.1,
                    min(
                        1,
                        0.25
                        + 0.75
                        * (0.5 * dx + 0.70710678 + 0.5 * dz)
                        / math.sqrt(dx * dx + dz * dz + 1),
                    ),
                )
                base = (205, 214, 189)
                if mode == "surface" and layers:
                    base = tuple(
                        sum(layers[k][i] * palette[k][c] for k in range(4))
                        for c in range(3)
                    )
                    shade = 0.55 + 0.45 * shade
                color = tuple(int(c * shade) for c in base)
            pixels[i * 3 : i * 3 + 3] = bytes(color)
    return pixels


def png(output, width, height, pixels):
    def chunk(tag, data):
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data))
        )

    rows = b"".join(
        b"\0" + pixels[z * width * 3 : (z + 1) * width * 3] for z in range(height)
    )
    output.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(rows))
        + chunk(b"IEND", b"")
    )
    print(output)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("raw", type=Path)
    p.add_argument("--output", type=Path)
    p.add_argument(
        "--mode", choices=("hillshade", "surface", "water", "all"), default="hillshade"
    )
    p.add_argument(
        "--panels",
        action="store_true",
        help="Also write individual full-resolution panels.",
    )
    p.add_argument(
        "--reference",
        type=Path,
        help="Before dump; left column before, right column after; rows hillshade/surface/water.",
    )
    args = p.parse_args()
    after = load(args.raw)
    output = args.output or args.raw.with_suffix(".png")
    modes = ("hillshade", "surface", "water") if args.mode == "all" else (args.mode,)
    maps = [load(args.reference), after] if args.reference else [after]
    size = after[0]
    if any(data[0] != size or data[1] != after[1] for data in maps):
        raise ValueError("Comparison maps must share resolution and scale")
    width = size * len(maps)
    height = size * len(modes)
    pixels = bytearray(width * height * 3)
    for row, mode in enumerate(modes):
        for col, data in enumerate(maps):
            panel = render(data, mode)
            if args.panels:
                label = "before" if len(maps) == 2 and col == 0 else "after"
                png(
                    output.with_name(output.stem + "-" + label + "-" + mode + ".png"),
                    size,
                    size,
                    panel,
                )
            for z in range(size):
                start = ((row * size + z) * width + col * size) * 3
                pixels[start : start + size * 3] = panel[
                    z * size * 3 : (z + 1) * size * 3
                ]
    png(output, width, height, pixels)


if __name__ == "__main__":
    main()
