"""Predict a Steam game's review count and share of positive reviews from screenshots.

Screenshots of one game go in, with its release year, Steam genres and tags. The adapter
answers two class digits; this script reads the probabilities of the ten digits the way
the evaluation did, for each screenshot, and averages them over the screenshots.

usage: python predict.py SHOT [SHOT ...] --year 2026 --genres "Action, Indie"
           --tags "Roguelike, Pixel Graphics, 2D" [--adapter REPO_OR_DIR] [--json]
"""

import argparse
import json

import numpy as np
import torch
from peft import PeftModel
from PIL import Image
from transformers import AutoProcessor, Qwen3_5ForConditionalGeneration

BASE = "Qwen/Qwen3.5-2B"
BASE_REVISION = "15852e8c16360a2fea060d615a32b45270f8a8fc"  # pragma: allowlist secret
ADAPTER = "zjgkkn/qwen3.5-2b-steam-reviews-lora"
REVIEWS = (
    "0",
    "1-2",
    "3-9",
    "10-29",
    "30-99",
    "100-299",
    "300-999",
    "1000-2999",
    "3000-9999",
    "10000+",
)
POSITIVE = (
    "under 50%",
    "50-59%",
    "60-69%",
    "70-74%",
    "75-79%",
    "80-84%",
    "85-89%",
    "90-94%",
    "95-97%",
    "98-100%",
)
POSITIVE_MID = np.array([35.0, 55.0, 65.0, 72.5, 77.5, 82.5, 87.5, 92.5, 96.5, 99.0])
FIRST_SCORED = 3  # Steam shows a share from 10 reviews on: class 3 (10-29) and up
THUMBNAIL = (600, 338)  # the size of the Steam thumbnails the adapter was trained on
QUESTION = (
    "How many reviews will this game get, and what share of them will be positive?"
    " Answer with two classes from 0 to 9."
)


def prompt(processor, year: int, genres: list[str], tags: list[str]) -> str:
    """The chat prompt the adapter was trained with; only the first five tags count."""
    text = (
        f"Game screenshot. Release year: {year}. Genres: {', '.join(genres) or 'none'}."
        f" Tags: {', '.join(tags[:5]) or 'none'}.\n{QUESTION}"
    )
    messages = [
        {"role": "user", "content": [{"type": "image"}, {"type": "text", "text": text}]}
    ]
    return processor.apply_chat_template(
        messages, add_generation_prompt=True, tokenize=False, enable_thinking=False
    )


def screenshot(path: str) -> Image.Image:
    """The picture fitted into 600x338 like the training thumbnails; never enlarged."""
    with Image.open(path) as img:
        image = img.convert("RGB")
    image.thumbnail(THUMBNAIL, Image.Resampling.LANCZOS)
    return image


def load(adapter: str = ADAPTER, device: str | None = None):
    device = device or ("cuda" if torch.cuda.is_available() else "cpu")
    processor = AutoProcessor.from_pretrained(BASE, revision=BASE_REVISION)
    model = Qwen3_5ForConditionalGeneration.from_pretrained(
        BASE, revision=BASE_REVISION, dtype=torch.bfloat16, device_map=device
    )
    model = PeftModel.from_pretrained(model, adapter).eval()
    digits = []
    for d in range(10):
        ids = processor.tokenizer(str(d), add_special_tokens=False)["input_ids"]
        if len(ids) != 1:
            raise ValueError(f"the digit {d} is not a single token: {ids}")
        digits.append(ids[0])
    return processor, model, digits, device


@torch.inference_mode()
def digit_probs(processor, model, digits, device, text: str, image: Image.Image):
    """Probabilities of the ten digits as the next token after text."""
    enc = processor(text=[text], images=[image], return_tensors="pt").to(device)
    logits = model(**enc, logits_to_keep=1).logits[0, -1].float()
    return torch.softmax(logits[digits], dim=-1).cpu().numpy()


def forecast(processor, model, digits, device, shots, year, genres, tags) -> dict:
    """Each screenshot's two reads, and their means over the screenshots."""
    text = prompt(processor, year, genres, tags)
    results = []
    for path in shots:
        image = screenshot(path)
        p_rev = digit_probs(processor, model, digits, device, text, image)
        # The share is read after the likeliest review class from 10 reviews up.
        likely = FIRST_SCORED + int(p_rev[FIRST_SCORED:].argmax())
        p_pos = digit_probs(processor, model, digits, device, f"{text}{likely} ", image)
        results.append(
            {
                "shot": path,
                "review_class": float(p_rev @ np.arange(10)),
                "review_probs": [round(float(p), 4) for p in p_rev],
                "positive": float(p_pos @ POSITIVE_MID),
                "positive_probs": [round(float(p), 4) for p in p_pos],
            }
        )
    return {
        "review_class": float(np.mean([r["review_class"] for r in results])),
        "positive": float(np.mean([r["positive"] for r in results])),
        "shots": results,
    }


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("shots", nargs="+", help="screenshots of one game")
    ap.add_argument("--year", type=int, required=True, help="the release year")
    ap.add_argument("--genres", default="", help="Steam genres, comma-separated")
    ap.add_argument(
        "--tags", default="", help="Steam tags, strongest first; five count"
    )
    ap.add_argument("--adapter", default=ADAPTER, help="the adapter's repo or folder")
    ap.add_argument(
        "--json", action="store_true", help="print the probabilities as JSON"
    )
    args = ap.parse_args()

    def names(text: str) -> list[str]:
        return [x.strip() for x in text.split(",") if x.strip()]

    processor, model, digits, device = load(args.adapter)
    out = forecast(
        processor,
        model,
        digits,
        device,
        args.shots,
        args.year,
        names(args.genres),
        names(args.tags),
    )
    if args.json:
        print(json.dumps(out, indent=1))
        return
    for r in out["shots"]:
        likely = int(np.argmax(r["review_probs"]))
        print(
            f"{r['shot']}: likeliest {REVIEWS[likely]} reviews,"
            f" mean class {r['review_class']:.2f}; positive {r['positive']:.0f}%"
        )
    k = out["review_class"]
    print(
        f"Game: mean class {k:.2f} (about {REVIEWS[round(k)]} reviews),"
        f" positive about {out['positive']:.0f}% if it gets 10 or more reviews"
    )


if __name__ == "__main__":
    main()
