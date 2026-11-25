"""Shot Search: Steam screenshots like yours, found by meaning or by look, with a review forecast.

Two embedding spaces cover every screenshot of the index:

- meaning: SigLIP 2 (google/siglip2-so400m-patch14-384), learnt from images and captions, so
  neighbours share what is shown: setting, characters, genre. Text queries live here too.
- look: DINOv2 with registers (facebook/dinov2-with-registers-large), learnt from images alone,
  on the shot in grey and pooled over all patches, so neighbours share how the frame is built:
  layout, forms, depth, light and dark, but not the colour grading.

Each shot carries a review forecast made by a screenshot model for its game and the game's real
Steam reviews.
"""

import os
from pathlib import Path

SPACES = ("meaning", "look")
RANKINGS = ("both", "meaning", "look")
SORTS = ("similarity", "reviews", "forecast")

MEANING_MODEL = "google/siglip2-so400m-patch14-384"
LOOK_MODEL = "facebook/dinov2-with-registers-large"

# The forecast's review-count classes, as in the forecaster's training.
REVIEW_RANGES = (
    "none",
    "1-2",
    "3-9",
    "10-29",
    "30-99",
    "100-299",
    "300-999",
    "1,000-2,999",
    "3,000-9,999",
    "10,000 or more",
)


def default_home() -> Path:
    """Index, uploads and sheets live here (env: SHOTSEARCH_HOME)."""
    if home := os.environ.get("SHOTSEARCH_HOME"):
        return Path(home)
    cache = os.environ.get("XDG_CACHE_HOME") or str(Path.home() / ".cache")
    return Path(cache) / "shotsearch"


def review_range(review_class: float) -> str:
    """Words for an expected review class: the range of its nearest class."""
    return REVIEW_RANGES[min(max(round(review_class), 0), len(REVIEW_RANGES) - 1)]
