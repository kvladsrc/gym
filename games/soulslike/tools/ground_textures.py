"""The ground's detail map and decals from the studio's images.

    nix shell --impure --expr '(import (builtins.getFlake "nixpkgs") {}).python3.withPackages
        (ps: [ ps.numpy ps.pillow ])' -c python games/soulslike/tools/ground_textures.py \
        --detail <image asset id> --decal Leaves=<id> --decal Moss=<id> ...

(uv's numpy wheel finds no libstdc++ on this system.)

- The detail image becomes Art/Textures/Ground/GroundDetail.png: its
  brightness without the broad shading (a high-pass), around mid grey, so
  the ground material's ×2 detail leaves the baked colour as it is on
  average; made tileable (blended with itself shifted by half, across a
  seam-hiding cross-fade); and GroundDetailNormal.png from it as a height.
- Each decal becomes Art/Textures/Decals/<Name>.png at 512 px, its alpha
  faded to nothing towards the edges, so no square's border shows.
- Each layer (--layer Name=<id>) becomes Art/Textures/GroundLayers/<Name>.png
  for the ground shader (Soulslike/GroundLayers) to tile: its broad shading
  (a vignette, a lighter middle) divided out, so tiles do not show as a grid
  of patches, and made tileable as the detail map.
"""

import argparse
import io
import os
import urllib.request
from pathlib import Path

import numpy as np
from PIL import Image

STUDIO = os.environ.get("STUDIO_URL", "http://127.0.0.1:9000")
ART = (
    Path(__file__).resolve().parent.parent
    / "unity/soulslike/Assets/Soulslike/Art/Textures"
)


def fetch(asset: str) -> Image.Image:
    with urllib.request.urlopen(f"{STUDIO}/api/assets/{asset}/file") as response:
        return Image.open(io.BytesIO(response.read()))


def blur(a: np.ndarray, sigma: float) -> np.ndarray:
    """Gaussian blur, wrapping around (FFT)."""
    h, w = a.shape
    fy, fx = np.fft.fftfreq(h)[:, None], np.fft.fftfreq(w)[None, :]
    kernel = np.exp(-2 * (np.pi * sigma) ** 2 * (fx**2 + fy**2))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * kernel))


def tileable(a: np.ndarray) -> np.ndarray:
    """Blended with itself shifted by half: the shifted copy, seamless at
    the borders, wins there; the original in the middle."""
    h, w = a.shape
    y = np.abs(np.linspace(-1, 1, h))[:, None]
    x = np.abs(np.linspace(-1, 1, w))[None, :]
    edge = np.clip((np.maximum(x, y) - 0.55) / 0.4, 0, 1)
    shifted = np.roll(np.roll(a, h // 2, axis=0), w // 2, axis=1)
    return a * (1 - edge) + shifted * edge


def detail(asset: str, out: Path) -> None:
    rgb = (
        np.asarray(
            fetch(asset).convert("RGB").resize((1024, 1024), Image.LANCZOS),
            dtype=np.float64,
        )
        / 255
    )
    light = rgb @ np.array([0.299, 0.587, 0.114])
    light = tileable(light)
    fine = light - blur(light, 24)  # without the broad shading
    grey = np.clip(0.5 + fine / (fine.std() * 6.5), 0, 1)  # ±3 sd in [0.04, 0.96]
    out.mkdir(parents=True, exist_ok=True)
    Image.fromarray((np.dstack([grey] * 3) * 255).astype(np.uint8)).save(
        out / "GroundDetail.png"
    )
    height = blur(grey, 1.0)
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 4
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 4
    normal = np.dstack([-dx, dy, np.ones_like(dx)])
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    Image.fromarray(((normal * 0.5 + 0.5) * 255).astype(np.uint8)).save(
        out / "GroundDetailNormal.png"
    )


def layer(asset: str, out: Path) -> None:
    rgb = (
        np.asarray(
            fetch(asset).convert("RGB").resize((1024, 1024), Image.LANCZOS),
            dtype=np.float64,
        )
        / 255
    )
    flat = np.empty_like(rgb)
    light = rgb @ np.array([0.299, 0.587, 0.114])
    broad = blur(light, 90)  # the vignette and lighter middle, not the detail
    for c in range(3):
        flat[..., c] = tileable(rgb[..., c] * light.mean() / np.maximum(broad, 1e-3))
    out.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray((np.clip(flat, 0, 1) * 255).astype(np.uint8)).save(out)


def decal(name: str, asset: str, out: Path) -> None:
    image = fetch(asset).convert("RGBA").resize((512, 512), Image.LANCZOS)
    a = np.asarray(image, dtype=np.float64)
    y = np.abs(np.linspace(-1, 1, 512))[:, None]
    x = np.abs(np.linspace(-1, 1, 512))[None, :]
    fade = np.clip((1 - np.sqrt(x**2 + y**2)) / 0.35, 0, 1)  # round, gone at the edges
    a[..., 3] *= fade
    out.mkdir(parents=True, exist_ok=True)
    Image.fromarray(a.astype(np.uint8), "RGBA").save(out / f"{name}.png")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--detail")
    parser.add_argument("--decal", action="append", default=[], help="Name=asset id")
    parser.add_argument("--layer", action="append", default=[], help="Name=asset id")
    args = parser.parse_args()
    if args.detail:
        detail(args.detail, ART / "Ground")
    for item in args.decal:
        name, asset = item.split("=", 1)
        decal(name, asset, ART / "Decals")
    for item in args.layer:
        name, asset = item.split("=", 1)
        layer(asset, ART / "GroundLayers" / f"{name}.png")
    print("done")


if __name__ == "__main__":
    main()
