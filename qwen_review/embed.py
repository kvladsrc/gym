"""Image fingerprints (pooled SigLIP2 embeddings) for the simple baseline.

    python embed.py SHOTS_DIR OUT_DIR [--model NAME] [--batch 16] [--workers 0]

Embeds every *.jpg in SHOTS_DIR in sorted order, in chunks of 1024 images.
Each finished chunk is written to OUT_DIR/chunk_NNNNN.npz (names + float16
embeddings), so a stopped run resumes where it left off. The image list is
fixed in OUT_DIR/names.json on the first run. With --workers N, N processes
read and resize the images ahead of the GPU.
"""

import argparse
import json
import os
from pathlib import Path

import numpy as np
import torch
from PIL import Image
from transformers import AutoImageProcessor, SiglipVisionModel

CHUNK = 1024


def load(path: Path) -> Image.Image:
    with Image.open(path) as img:
        return img.convert("RGB")


class Shots(torch.utils.data.Dataset):
    """Pixel values of the images, in the order given."""

    def __init__(self, shots: Path, names: list[str], processor) -> None:
        self.shots, self.names, self.processor = shots, names, processor

    def __len__(self) -> int:
        return len(self.names)

    def __getitem__(self, i: int) -> torch.Tensor:
        image = load(self.shots / self.names[i])
        return self.processor(images=[image], return_tensors="pt")["pixel_values"][0]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("shots", type=Path)
    parser.add_argument("out", type=Path)
    parser.add_argument("--model", default="google/siglip2-so400m-patch14-384")
    parser.add_argument("--batch", type=int, default=16)
    parser.add_argument("--threads", type=int, default=4, help="CPU threads")
    parser.add_argument(
        "--workers", type=int, default=0, help="processes loading images (0: in place)"
    )
    args = parser.parse_args()
    if CHUNK % args.batch:
        parser.error(f"--batch must divide {CHUNK}")

    torch.set_num_threads(args.threads)
    args.out.mkdir(parents=True, exist_ok=True)
    names_path = args.out / "names.json"
    if names_path.exists():
        names = json.loads(names_path.read_text(encoding="utf-8"))
    else:
        names = sorted(p.name for p in args.shots.glob("*.jpg"))
        names_path.write_text(json.dumps(names), encoding="utf-8")
    (args.out / "model.txt").write_text(args.model + "\n", encoding="utf-8")

    device = "cuda" if torch.cuda.is_available() else "cpu"
    dtype = torch.float16 if device == "cuda" else torch.float32
    processor = AutoImageProcessor.from_pretrained(args.model)
    model = SiglipVisionModel.from_pretrained(args.model, dtype=dtype).to(device).eval()

    chunks = range(0, len(names), CHUNK)
    todo = [
        start
        for start in chunks
        if not (args.out / f"chunk_{start // CHUNK:05d}.npz").exists()
    ]
    # Every chunk but the last holds CHUNK images and the batch divides CHUNK, so no
    # batch spans two chunks.
    order = [n for start in todo for n in names[start : start + CHUNK]]
    batches = torch.utils.data.DataLoader(
        Shots(args.shots, order, processor),
        batch_size=args.batch,
        num_workers=args.workers,
        pin_memory=device == "cuda",
        persistent_workers=False,
    )
    batches = iter(batches)
    for start in todo:
        target = args.out / f"chunk_{start // CHUNK:05d}.npz"
        part = names[start : start + CHUNK]
        vectors = []
        for _ in range(0, len(part), args.batch):
            pixels = next(batches)
            with torch.inference_mode():
                pooled = model(pixel_values=pixels.to(device, dtype)).pooler_output
            vectors.append(pooled.float().cpu().numpy().astype(np.float16))
        tmp = target.with_suffix(".tmp.npz")
        np.savez(tmp, names=np.array(part), emb=np.concatenate(vectors))
        os.replace(tmp, target)
        print(f"chunk {start // CHUNK + 1}/{len(chunks)} done", flush=True)
    print("all chunks done", flush=True)


if __name__ == "__main__":
    main()
