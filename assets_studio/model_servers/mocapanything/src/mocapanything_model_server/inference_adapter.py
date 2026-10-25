"""Upstream inference with its white-background input preserved after BiRefNet.

Only the input-frame preparation is replaced. Reference features, DINOv2,
the motion network and rotation decoding follow the upstream pipeline.
"""

import argparse
import runpy

import numpy as np
from PIL import Image


def prepare_white_image(
    image_path: str, bg_color: np.ndarray, rmbg_net: object = None
) -> Image.Image:
    """Crop/pad the existing foreground, without segmenting or recolouring it.

    The non-white bounds tolerate small H.264 errors around white. Pixels
    inside those bounds stay unchanged, including bright armour and weapons.
    This mode requires an already matted, white-background input.
    """
    with Image.open(image_path) as source:
        image = source.convert("RGB")
    foreground = np.any(np.asarray(image) < 248, axis=-1)
    rows, columns = np.nonzero(foreground)
    if not rows.size:
        raise ValueError("white-background frame contains no foreground")
    left, top = int(columns.min()), int(rows.min())
    right, bottom = int(columns.max()) + 1, int(rows.max()) + 1
    width, height = right - left, bottom - top
    # Match upstream load_image's square crop with 10% padding.
    side = max(width, height)
    margin = int(side * 0.1)
    pad_x = margin + (side - width) // 2
    pad_y = margin + (side - height) // 2
    color = tuple(round(float(c) * 255) for c in bg_color)
    result = Image.new("RGB", (width + 2 * pad_x, height + 2 * pad_y), color)
    result.paste(image.crop((left, top, right, bottom)), (pad_x, pad_y))
    return result


def main() -> None:
    # Parse for useful CLI errors, leaving upstream's --config argument intact.
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", required=True)
    parser.parse_args()
    from utils import common  # pyright: ignore[reportMissingImports]

    # common's extractor calls this global for each video frame. Reference
    # renders are processed separately, before this subprocess starts.
    common.prepare_image = prepare_white_image
    runpy.run_module("inference.video2pose2rot", run_name="__main__")


if __name__ == "__main__":
    main()
