"""Query embeddings on the CPU, made exactly as the index's: SigLIP 2 for meaning, DINOv2 for look.

Models load on first use and are dropped after `idle_s` seconds without a query, so a server
waiting between searches holds no model memory (about 1.7 GB for SigLIP's image tower, 1.5 GB for
its text tower and 1.2 GB for DINOv2 when loaded).
"""

import gc
import threading
import time
from typing import Any

import numpy as np
from PIL import Image

from shotsearch import LOOK_MODEL, MEANING_MODEL

LOOK_SIZE = (448, 252)  # 32 x 18 patches of 14 px, about the 600x338 store aspect; never cropped
IMAGENET_MEAN = np.array([0.485, 0.456, 0.406], dtype=np.float32)
IMAGENET_STD = np.array([0.229, 0.224, 0.225], dtype=np.float32)
LUMA = np.array([0.299, 0.587, 0.114], dtype=np.float32)
TEXT_TOKENS = 64


def look_pixels(image: Image.Image) -> np.ndarray:
    """The shot in grey, squashed to LOOK_SIZE, normalised: (3, H, W) float32."""
    rgb = np.asarray(image.convert("RGB").resize(LOOK_SIZE, Image.BICUBIC), dtype=np.float32)
    gray = (rgb / 255 * LUMA).sum(-1, keepdims=True).repeat(3, axis=-1)
    return ((gray - IMAGENET_MEAN) / IMAGENET_STD).transpose(2, 0, 1)


def unit(vectors: np.ndarray) -> np.ndarray:
    vectors = np.asarray(vectors, dtype=np.float32)
    return vectors / np.linalg.norm(vectors, axis=-1, keepdims=True)


class Encoders:
    def __init__(self, idle_s: float = 300, threads: int = 4) -> None:
        self.idle_s, self.threads = idle_s, threads
        self.models: dict[str, Any] = {}
        self.used = time.monotonic()
        self.lock = threading.RLock()
        if idle_s > 0:
            threading.Thread(target=self._unload_idle, daemon=True).start()

    def _unload_idle(self) -> None:
        while True:
            time.sleep(min(30.0, self.idle_s))
            with self.lock:
                if self.models and time.monotonic() - self.used > self.idle_s:
                    self.models.clear()
                    gc.collect()

    def _model(self, kind: str) -> Any:
        if kind in self.models:
            return self.models[kind]
        import torch
        from transformers import (
            AutoImageProcessor,
            AutoModel,
            AutoTokenizer,
            SiglipTextModel,
            SiglipVisionModel,
        )

        torch.set_num_threads(self.threads)
        if kind == "meaning_image":
            model = (
                AutoImageProcessor.from_pretrained(MEANING_MODEL),
                SiglipVisionModel.from_pretrained(MEANING_MODEL).eval(),
            )
        elif kind == "meaning_text":
            model = (
                AutoTokenizer.from_pretrained(MEANING_MODEL),
                SiglipTextModel.from_pretrained(MEANING_MODEL).eval(),
            )
        else:
            model = AutoModel.from_pretrained(LOOK_MODEL).eval()
        self.models[kind] = model
        return model

    def images(self, images: list[Image.Image]) -> dict[str, np.ndarray]:
        """Unit vectors of each image in both spaces: {"meaning": (n, D), "look": (n, D)}."""
        import torch

        with self.lock:
            self.used = time.monotonic()
            processor, siglip = self._model("meaning_image")
            dino = self._model("look")
            rgb = [image.convert("RGB") for image in images]
            with torch.inference_mode():
                pixels = processor(images=rgb, return_tensors="pt")["pixel_values"]
                meaning = siglip(pixel_values=pixels).pooler_output.numpy()
                batch = torch.from_numpy(np.stack([look_pixels(image) for image in rgb]))
                hidden = dino(pixel_values=batch).last_hidden_state
                registers = getattr(dino.config, "num_register_tokens", 0)
                look = hidden[:, 1 + registers :].mean(1).numpy()
            self.used = time.monotonic()
        return {"meaning": unit(meaning), "look": unit(look)}

    def text(self, text: str) -> np.ndarray:
        """Unit SigLIP vector of a text query."""
        import torch

        with self.lock:
            self.used = time.monotonic()
            tokenizer, model = self._model("meaning_text")
            tokens = tokenizer(
                [text], padding="max_length", max_length=TEXT_TOKENS, return_tensors="pt"
            )
            with torch.inference_mode():
                vector = model(**tokens).pooler_output.numpy()[0]
            self.used = time.monotonic()
        return unit(vector)

    def loaded(self) -> list[str]:
        return sorted(self.models)


def download() -> None:
    """Fetch both models into the Hugging Face cache."""
    from huggingface_hub import snapshot_download

    for name in (MEANING_MODEL, LOOK_MODEL):
        print(snapshot_download(name))
