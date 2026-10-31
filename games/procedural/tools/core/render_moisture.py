"""Render M3 scalar fields without hillshade, with river/lake overlays and legends."""

import argparse
import itertools
import struct
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from render_relief import load, values

STOPS = (
    (0, (185, 117, 59)),
    (0.35, (223, 207, 138)),
    (0.65, (123, 174, 135)),
    (1, (25, 95, 91)),
)


def color(value):
    for (a, ca), (b, cb) in itertools.pairwise(STOPS):
        if value <= b:
            t = max(0, (value - a) / (b - a))
            return tuple(round(x + (y - x) * t) for x, y in zip(ca, cb))
    return STOPS[-1][1]


def render(raw, output):
    size, cell, heights, _, water = load(raw)
    data = raw.with_suffix(".moisture.raw").read_bytes()
    field_size, field_cell = struct.unpack("<if", data[:8])
    if field_size != size or field_cell != cell:
        raise ValueError("Field geometry differs from heightmap")
    count = size * size
    font = ImageFont.load_default(size=18)
    for k, name in enumerate(("wetness", "moisture")):
        field = values(data[8 + k * count * 4 : 8 + (k + 1) * count * 4])
        pixels = bytearray(count * 3)
        for i, value in enumerate(field):
            rgb = (
                (32, 49, 66)
                if heights[i] <= 0
                else (55, 147, 235)
                if water[i]
                else color(value)
            )
            pixels[i * 3 : i * 3 + 3] = bytes(rgb)
        image = Image.new("RGB", (size, size + 116), "#f5f3eb")
        image.paste(Image.frombytes("RGB", (size, size), bytes(pixels)), (0, 48))
        draw = ImageDraw.Draw(image)
        draw.text(
            (16, 10),
            f"Small seed 1 Bake - {name} (0..1), {cell:g} m samples",
            font=font,
            fill="#26313b",
        )
        y = size + 64
        for x in range(320):
            draw.line((16 + x, y, 16 + x, y + 14), fill=color(x / 319))
        draw.text((16, y + 18), "0 dry", font=font, fill="#26313b")
        draw.text((277, y + 18), "1 wet", font=font, fill="#26313b")
        draw.rectangle((390, y, 410, y + 14), fill=(55, 147, 235))
        draw.text((419, y - 4), "Rivers / lakes", font=font, fill="#26313b")
        draw.text((660, y - 4), "No hillshade", font=font, fill="#26313b")
        path = output / f"seed1-{name}.png"
        image.save(path)
        print(path)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("raw", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    render(args.raw, args.output)
