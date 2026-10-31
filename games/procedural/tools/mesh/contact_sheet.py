"""Compose a labelled contact sheet from mesh_compare.py output.

Usage:
    uv run --no-project --with pillow python contact_sheet.py OUT_DIR

Rows are labels (reference first), columns are the four rendered views.  Each
tile is labelled with its label, triangle count, and the reference-to-variant
95th-percentile deviation (as a percentage of the reference bbox diagonal).
"""

from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

VIEWS = (
    ("front", "front"),
    ("three_quarter", "three-quarter"),
    ("top", "top"),
    ("closeup", "close-up 2.5x"),
)

MARGIN = 24
GAP = 18
TITLE_H = 48
HEADER_H = 34
INFO_H = 68


def load_font(size: int, bold: bool = False):
    names = ("DejaVuSans-Bold.ttf", "DejaVuSans.ttf") if bold else ("DejaVuSans.ttf",)
    candidates = []
    for name in names:
        candidates.extend(
            [
                Path("/usr/share/fonts/truetype/dejavu") / name,
                Path("/usr/share/fonts/TTF") / name,
                Path("/usr/share/fonts/dejavu") / name,
                Path("/run/current-system/sw/share/fonts/truetype/dejavu") / name,
                Path("/nix/var/nix/profiles/default/share/fonts/truetype/dejavu")
                / name,
                Path.home() / ".local/share/fonts" / name,
            ]
        )

    for candidate in candidates:
        try:
            if candidate.is_file():
                return ImageFont.truetype(str(candidate), size)
        except OSError:
            pass

    # Nix installations often keep DejaVu in the store; fontconfig knows where.
    fc_match = shutil.which("fc-match")
    if fc_match:
        try:
            family = "DejaVu Sans:bold" if bold else "DejaVu Sans"
            result = subprocess.run(
                [fc_match, "--format=%{file}", family],
                check=True,
                capture_output=True,
                text=True,
                timeout=5,
            )
            font_path = result.stdout.strip()
            if font_path and Path(font_path).is_file():
                return ImageFont.truetype(font_path, size)
        except (OSError, subprocess.SubprocessError):
            pass

    try:
        return ImageFont.load_default(size=size)
    except TypeError:
        return ImageFont.load_default()


def fit_text(draw: ImageDraw.ImageDraw, text: str, font, max_width: int) -> str:
    if draw.textlength(text, font=font) <= max_width:
        return text
    ellipsis = "..."
    low, high = 0, len(text)
    while low < high:
        mid = (low + high + 1) // 2
        candidate = text[:mid].rstrip() + ellipsis
        if draw.textlength(candidate, font=font) <= max_width:
            low = mid
        else:
            high = mid - 1
    return text[:low].rstrip() + ellipsis


def safe_filename_stem(label: str) -> str:
    # Kept identical to mesh_compare.py's sanitiser.
    import re

    cleaned = re.sub(r'[<>:"/\\|?*\x00-\x1f]', "_", label).strip()
    return cleaned or "variant"


def load_entries(out_dir: Path) -> list[dict]:
    metrics_path = out_dir / "metrics.json"
    if not metrics_path.is_file():
        raise SystemExit(f"missing {metrics_path}")
    with metrics_path.open("r", encoding="utf-8") as handle:
        entries = json.load(handle)
    if not isinstance(entries, list) or not entries:
        raise SystemExit(f"{metrics_path}: expected a non-empty JSON list")

    refs = [entry for entry in entries if entry.get("label") == "reference"]
    if len(refs) != 1:
        raise SystemExit(f"{metrics_path}: expected exactly one 'reference' row")
    others = [entry for entry in entries if entry.get("label") != "reference"]
    return refs + others


def white_rgba(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    canvas = Image.new("RGBA", image.size, (255, 255, 255, 255))
    canvas.alpha_composite(image)
    return canvas.convert("RGB")


def compose(out_dir: Path) -> Path:
    entries = load_entries(out_dir)

    label_font = load_font(17, bold=True)
    metric_font = load_font(15)
    header_font = load_font(16, bold=True)
    title_font = load_font(25, bold=True)
    note_font = load_font(14)

    image_paths: dict[tuple[str, str], Path] = {}
    for entry in entries:
        label = str(entry.get("label", "?"))
        images = entry.get("images") or {}
        stem = safe_filename_stem(label)
        for key, _ in VIEWS:
            relative = images.get(key) or f"{stem}_{key}.png"
            path = Path(relative)
            if not path.is_absolute():
                path = out_dir / path
            if not path.is_file():
                raise SystemExit(f"missing render: {path}")
            image_paths[(label, key)] = path

    # Renders from one run share a size; support unusual inputs by normalising
    # every tile to the largest render.
    tile_w = max(Image.open(path).width for path in image_paths.values())
    tile_h = max(Image.open(path).height for path in image_paths.values())

    columns = len(VIEWS)
    rows = len(entries)
    sheet_w = MARGIN * 2 + tile_w * columns + GAP * (columns - 1)
    sheet_h = (
        MARGIN
        + TITLE_H
        + HEADER_H
        + rows * (tile_h + INFO_H)
        + GAP * (rows - 1)
        + MARGIN
    )

    sheet = Image.new("RGB", (sheet_w, sheet_h), "white")
    draw = ImageDraw.Draw(sheet)

    draw.text(
        (MARGIN, MARGIN - 2),
        "Mesh comparison",
        fill=(28, 30, 34),
        font=title_font,
    )
    note = "deviation = % of reference bbox diagonal"
    note_w = draw.textlength(note, font=note_font)
    draw.text(
        (sheet_w - MARGIN - note_w, MARGIN + 6),
        note,
        fill=(105, 109, 118),
        font=note_font,
    )

    header_y = MARGIN + TITLE_H
    for col, (_, heading) in enumerate(VIEWS):
        x = MARGIN + col * (tile_w + GAP)
        text_w = draw.textlength(heading, font=header_font)
        draw.text(
            (x + (tile_w - text_w) / 2, header_y),
            heading,
            fill=(50, 54, 62),
            font=header_font,
        )

    row_top = header_y + HEADER_H
    for row, entry in enumerate(entries):
        label = str(entry.get("label", "?"))
        metrics = entry.get("metrics", {})
        triangles = int(metrics.get("triangles", 0))
        p95 = float(
            metrics.get("deviation", {}).get("reference_to_variant", {}).get("p95", 0.0)
        )

        image_y = row_top + row * (tile_h + INFO_H + GAP)
        info_y = image_y + tile_h + 8

        for col, (key, _) in enumerate(VIEWS):
            x = MARGIN + col * (tile_w + GAP)
            tile = white_rgba(image_paths[(label, key)])
            if tile.size != (tile_w, tile_h):
                tile = tile.resize((tile_w, tile_h), Image.Resampling.LANCZOS)
            sheet.paste(tile, (x, image_y))
            draw.rectangle(
                (x, image_y, x + tile_w - 1, image_y + tile_h - 1),
                outline=(218, 221, 226),
                width=1,
            )

            label_text = fit_text(draw, label, label_font, tile_w - 4)
            draw.text(
                (x, info_y),
                label_text,
                fill=(24, 26, 30),
                font=label_font,
            )
            draw.text(
                (x, info_y + 22),
                f"{triangles:,} triangles",
                fill=(70, 74, 82),
                font=metric_font,
            )
            draw.text(
                (x, info_y + 42),
                f"ref->var p95  {p95 * 100.0:.3f}%",
                fill=(70, 74, 82),
                font=metric_font,
            )

    sheet_path = out_dir / "sheet.png"
    sheet.save(sheet_path, "PNG", optimize=True)
    return sheet_path


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: contact_sheet.py OUT_DIR", file=sys.stderr)
        return 2
    out_dir = Path(sys.argv[1]).expanduser().resolve()
    sheet_path = compose(out_dir)
    print(f"wrote {sheet_path} ({sheet_path.stat().st_size:,} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
