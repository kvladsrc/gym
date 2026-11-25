"""A labelled contact sheet of a search: the query first, then the results in order."""

import hashlib
import json
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

FONTS = (
    "/usr/share/fonts/liberation-fonts/LiberationSans-Regular.ttf",
    "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
    "/usr/share/fonts/dejavu/DejaVuSans.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
)
CELL_W, CELL_H, CAPTION, PAD, COLUMNS = 320, 180, 76, 6, 5


def _font(size: int) -> ImageFont.ImageFont:
    for path in FONTS:
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default(size)


def caption(result: dict[str, Any]) -> list[str]:
    g, f = result["game"], result["forecast"]
    close = "  ".join(f"{k} {v:.2f}" for k, v in result["closeness"].items())
    real = f"{g['reviews']} reviews" + (f", {g['positive']}%" if g["positive"] is not None else "")
    guess = f"forecast {f['reviews']} ({f['review_class']:.1f})" if f else "no forecast"
    return [f"{g['name'][:34]} ({g['year']})", f"{real}; {guess}", close]


def render(
    query: list[tuple[str, Image.Image]], results: list[dict[str, Any]], folder: Path, title: str
) -> Path:
    """Draw the sheet into FOLDER (named by its content) and return its path."""
    cells = [(img, [label]) for label, img in query[:3]]
    cells += [(Image.open(r["image"]), caption(r)) for r in results]
    rows = (len(cells) + COLUMNS - 1) // COLUMNS
    sheet = Image.new(
        "RGB",
        (COLUMNS * (CELL_W + PAD) + PAD, 34 + rows * (CELL_H + CAPTION)),
        (24, 24, 24),
    )
    draw = ImageDraw.Draw(sheet)
    draw.text((PAD, 8), title[:150], fill=(255, 220, 120), font=_font(17))
    small = _font(14)
    for k, (img, lines) in enumerate(cells):
        x, y = PAD + (k % COLUMNS) * (CELL_W + PAD), 34 + (k // COLUMNS) * (CELL_H + CAPTION)
        img = img.convert("RGB")
        img.thumbnail((CELL_W, CELL_H))
        sheet.paste(img, (x + (CELL_W - img.width) // 2, y + (CELL_H - img.height) // 2))
        for i, line in enumerate(lines):
            draw.text((x, y + CELL_H + 4 + i * 18), line, fill=(235, 235, 235), font=small)
    digest = hashlib.sha256(
        json.dumps([title, [r["shot"] for r in results]]).encode()
        + b"".join(img.tobytes()[:4096] for _, img in query)
    ).hexdigest()[:16]
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / f"{digest}.jpg"
    sheet.save(path, quality=88)
    return path
