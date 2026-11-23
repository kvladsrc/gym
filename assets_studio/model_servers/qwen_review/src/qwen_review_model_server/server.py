"""One game screenshot, with the game's release year, genres and tags, in; the predicted
number of Steam reviews and share of positive ones out, as a text report.

A LoRA adapter on Qwen3.5 was trained to answer two class digits, such as "7 6": the
class of the review count and the class of the share of positive reviews. The server
reads them as the evaluation did. First it reads the probabilities of the ten digits
right after the prompt. Then it appends the most likely review class from 10 reviews up
and reads the share digit: under 10 reviews the adapter answers "-", like the store page.

There are two adapters of the same layout, one on Qwen3.5-2B (the default) and one on
Qwen3.5-9B; `--base` picks the base model. The 2B's is published on Hugging Face and goes in
~/.cache/assets-studio/qwen_review_2b; the 9B's is not distributed and goes in
~/.cache/assets-studio/qwen_review.
"""

import hashlib
import logging
import os
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Literal

import numpy as np
from model_server_sdk.images import open_image
from PIL import Image
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
    InputSpec,
    Job,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
    ui,
)

logger = logging.getLogger("qwen_review_model_server")


@dataclass(frozen=True)
class Base:
    """A base model, pinned, the folder its adapter goes in under the Studio cache, and
    the adapter's Hugging Face repo if it is published."""

    repo: str
    revision: str
    parameters: str
    adapter_folder: str
    adapter_repo: str = ""


BASES = {
    "9b": Base(
        "Qwen/Qwen3.5-9B",
        "c202236235762e1c871ad0ccb60c8ee5ba337b9a",  # pragma: allowlist secret
        "9.4 B",
        "qwen_review",
    ),
    "2b": Base(
        "Qwen/Qwen3.5-2B",
        "15852e8c16360a2fea060d615a32b45270f8a8fc",  # pragma: allowlist secret
        "2.3 B",
        "qwen_review_2b",
        "zjgkkn/qwen3.5-2b-steam-reviews-lora",
    ),
}
DEFAULT_BASE = "2b"  # more accurate than the 9B on 2026, fits the GPU whole, ~13x faster


def adapter_directory(base: str) -> Path:
    """QWEN_REVIEW_ADAPTER, else the base's folder in the Studio cache (as common.just finds it)."""
    named = os.environ.get("QWEN_REVIEW_ADAPTER")
    if named:
        return Path(named).expanduser()
    cache = Path(os.environ.get("XDG_CACHE_HOME") or Path.home() / ".cache")
    return cache / "assets-studio" / BASES[base].adapter_folder


# The training images were Steam's screenshot thumbnails, fitted into 600×338.
THUMBNAIL = (600, 338)
PROMPT_TAGS = 5  # only the five strongest tags were in the training prompts
QUESTION = (
    "How many reviews will this game get, and what share of them will be positive?"
    " Answer with two classes from 0 to 9."
)
REVIEW_TEXT = (
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
POSITIVE_TEXT = (
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
FIRST_SCORED = 3  # 10-29 reviews: the first class with a share of positive reviews

# The report's wording.
REVIEW_RANGES = {
    "en": (
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
    ),
    "ru": (
        "ни одного",
        "1–2",
        "3–9",
        "10–29",
        "30–99",
        "100–299",
        "300–999",
        "1000–2999",
        "3000–9999",
        "10 000 и больше",
    ),
}
POSITIVE_RANGES = {
    "en": POSITIVE_TEXT,
    "ru": tuple(
        text.replace("under ", "меньше ").replace("-", "–").replace("%", " %")
        for text in POSITIVE_TEXT
    ),
}


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    year: int = Field(
        default=2026,
        ge=1980,
        le=2035,
        description="The game's release year",
        json_schema_extra=ui(ru="Год выхода игры", primary=True),
    )
    genres: str = Field(
        default="",
        max_length=300,
        description="Steam genres in English, comma-separated, e.g. Action, Indie; empty for none",
        json_schema_extra=ui(
            ru="Жанры Steam по-английски через запятую, например Action, Indie", primary=True
        ),
    )
    tags: str = Field(
        default="",
        max_length=500,
        description="Steam user tags in English, strongest first, comma-separated; five count",
        json_schema_extra=ui(
            ru="Метки Steam по-английски через запятую, сильнейшие первыми; учтутся пять",
            primary=True,
        ),
    )
    language: Literal["ru", "en"] = Field(
        default="ru",
        description="Language of the text report",
        json_schema_extra=ui(ru="Язык отчёта"),
    )


TASK = TaskSpec(
    "image-to-text",
    Params,
    ("text/plain",),
    prompt="none",
    inputs=(
        InputSpec(
            role="image",
            mime=["image/png", "image/jpeg", "image/webp"],
            description="One screenshot of the game",
            labels={"ru": "Один скриншот игры"},
        ),
    ),
    max_count=1,
)


def download(base: str = DEFAULT_BASE) -> str:
    """Fetch the pinned base model into the Hugging Face cache."""
    from huggingface_hub import snapshot_download

    model = BASES[base]
    return snapshot_download(
        model.repo,
        revision=model.revision,
        allow_patterns=["*.json", "*.safetensors", "*.txt", "*.jinja"],
    )


def base_directory(base: str) -> Path:
    from huggingface_hub import snapshot_download

    model = BASES[base]
    try:
        return Path(snapshot_download(model.repo, revision=model.revision, local_files_only=True))
    except Exception as error:  # the hub raises its own "not cached" errors
        raise FileNotFoundError(
            f"{model.repo} {model.revision[:12]} is not downloaded:"
            f" run `just download {base}` in model_servers/qwen_review"
        ) from error


def names(text: str) -> list[str]:
    """Comma-separated names, trimmed, empty ones dropped."""
    return [name.strip() for name in text.split(",") if name.strip()]


def user_text(year: int, genres: list[str], tags: list[str]) -> str:
    """The prompt the adapter was trained with, word for word."""
    return (
        f"Game screenshot. Release year: {year}. Genres: {', '.join(genres) or 'none'}."
        f" Tags: {', '.join(tags[:PROMPT_TAGS]) or 'none'}.\n{QUESTION}"
    )


def screenshot(data: bytes) -> tuple[Image.Image, tuple[int, int]]:
    """The image fitted into the training thumbnails' size, and its own size."""
    image = open_image(data)
    size = image.size
    image.thumbnail(THUMBNAIL, Image.Resampling.LANCZOS)
    return image, size


def adapter_digest(directory: Path) -> str:
    weights = directory.expanduser() / "adapter_model.safetensors"
    if not weights.is_file():
        return "missing"
    digest = hashlib.sha256()
    with weights.open("rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()[:12]


def percent(p: float) -> str:
    value = 100 * p
    return "<1" if 0 < value < 0.5 else f"{value:.0f}"


def rounded(probs: np.ndarray) -> list[float]:
    return [round(float(p), 4) for p in probs]


@dataclass(frozen=True)
class Reading:
    """The two digit reads: review classes, then the share after review class ``after``."""

    reviews: np.ndarray
    after: int
    positive: np.ndarray

    @property
    def likely(self) -> int:
        return int(self.reviews.argmax())

    @property
    def expected(self) -> float:
        return float(self.reviews @ np.arange(10))

    @property
    def positive_likely(self) -> int:
        return int(self.positive.argmax())

    @property
    def positive_expected(self) -> float:
        return float(self.positive @ POSITIVE_MID)

    def meta(self) -> dict[str, Any]:
        return {
            "review_probs": rounded(self.reviews),
            "review_class_likely": self.likely,
            "review_class_expected": round(self.expected, 3),
            "reviews_likely": REVIEW_TEXT[self.likely],
            "positive_after_class": self.after,
            "positive_probs": rounded(self.positive),
            "positive_class_likely": self.positive_likely,
            "positive_likely": POSITIVE_TEXT[self.positive_likely],
            "positive_expected": round(self.positive_expected, 1),
        }

    def text(self, language: str) -> str:
        reviews, positive = REVIEW_RANGES[language], POSITIVE_RANGES[language]
        likely, share = self.likely, self.positive_likely
        if language == "ru":
            lines = [
                f"Отзывов: скорее всего {reviews[likely]}"
                f" (вероятность {percent(self.reviews[likely])} %).",
                f"Средний класс {self.expected:.1f}".replace(".", ",")
                + f" по шкале от 0 ({reviews[0]}) до 9 ({reviews[9]}).",
                "",
                "Вероятность каждого класса:",
                *(f"{k} — {reviews[k]}: {percent(p)} %" for k, p in enumerate(self.reviews)),
                "",
                "Доля положительных, если отзывов наберётся 10 и больше"
                f" (Qwen считала, что их будет {reviews[self.after]}):"
                f" около {self.positive_expected:.0f} %, скорее всего {positive[share]}"
                f" (вероятность {percent(self.positive[share])} %).",
            ]
        else:
            lines = [
                f"Reviews: most likely {reviews[likely]} ({percent(self.reviews[likely])}%).",
                f"Mean class {self.expected:.1f} on a scale from 0 ({reviews[0]})"
                f" to 9 ({reviews[9]}).",
                "",
                "Chance of each class:",
                *(f"{k} — {reviews[k]}: {percent(p)}%" for k, p in enumerate(self.reviews)),
                "",
                "Positive share if the game gets 10 or more reviews"
                f" (read assuming {reviews[self.after]} reviews):"
                f" about {self.positive_expected:.0f}%, most likely {positive[share]}"
                f" ({percent(self.positive[share])}%).",
            ]
        return "\n".join(lines) + "\n"


class QwenReviewServer(ModelServer):
    tasks = (TASK,)

    def __init__(
        self,
        gpu_layers: int | None = None,
        adapter: Path | None = None,
        base: str = DEFAULT_BASE,
    ) -> None:
        if base not in BASES:
            raise ValueError(f"unknown base model {base!r}: one of {', '.join(BASES)}")
        self._base = base
        self._gpu_layers = gpu_layers
        self._adapter = (adapter or adapter_directory(base)).expanduser()
        self._processor: Any = None
        self._model: Any = None
        self._resident = 0
        model = BASES[base]
        self.model = ModelInfo(
            id=f"qwen3_5-{base}-steam-reviews",
            name=f"Qwen3.5-{base.upper()} · Steam reviews",
            revision=f"{model.revision[:12]}+lora-{adapter_digest(self._adapter)}",
            license=(
                f"Apache-2.0 (base model and the LoRA adapter, {model.adapter_repo})"
                if model.adapter_repo
                else "Apache-2.0 (base model); the LoRA adapter is not distributed"
            ),
            source=f"https://huggingface.co/{model.repo}",
            details={
                "parameters": f"{model.parameters} + LoRA r16",
                "precision": "bfloat16, LoRA float32",
                "offloading": (
                    "the decoder layers that fit stay on the GPU; the others are copied"
                    " there for each pass from the memory-mapped weights"
                ),
                "input": "one screenshot, fitted into 600×338 like the training images",
            },
        )

    def load(self) -> None:
        import torch
        from transformers import AutoProcessor

        from qwen_review_model_server.model import build, digit_ids, read_adapter

        torch.set_grad_enabled(False)
        if not torch.cuda.is_available():
            raise RuntimeError("the review model needs a CUDA GPU")
        adapter = read_adapter(self._adapter)
        base = base_directory(self._base)
        self._processor = AutoProcessor.from_pretrained(base)
        digits = digit_ids(self._processor.tokenizer)
        self._model, self._resident = build(
            base, adapter, digits, torch.device("cuda"), self._gpu_layers
        )
        logger.info(
            "%s with the review LoRA ready: %d of %d decoder layers on the GPU",
            BASES[self._base].repo,
            self._resident,
            len(self._model.model.language_model.layers),
        )

    def _chat(self, text: str) -> str:
        messages = [
            {"role": "user", "content": [{"type": "image"}, {"type": "text", "text": text}]}
        ]
        # The 9B template opens a thinking block unless told not to; training did so too.
        # The 2B template renders the same empty block either way.
        return self._processor.apply_chat_template(
            messages, add_generation_prompt=True, tokenize=False, enable_thinking=False
        )

    def _digit_probs(self, text: str, image: Image.Image) -> np.ndarray:
        import torch

        from qwen_review_model_server.model import digit_probs

        return digit_probs(self._model, self._processor, text, image, torch.device("cuda"))

    def read(self, prompt: str, image: Image.Image) -> Reading:
        chat = self._chat(prompt)
        reviews = self._digit_probs(chat, image)
        after = FIRST_SCORED + int(reviews[FIRST_SCORED:].argmax())
        return Reading(reviews, after, self._digit_probs(chat + f"{after} ", image))

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, Params)
        image, source_size = screenshot(job.inputs["image"].data)
        prompt = user_text(job.params.year, names(job.params.genres), names(job.params.tags))
        started = time.monotonic()
        torch.cuda.reset_peak_memory_stats()
        try:
            reading = self.read(prompt, image)
        except RuntimeError as error:
            if not is_out_of_memory(error):
                raise
            torch.cuda.empty_cache()
            raise GenerationError(
                "out of GPU memory: free the GPU or start the server with a lower --gpu-layers",
                retryable=False,
            ) from error
        meta = reading.meta() | {
            "prompt": prompt,
            "image_size": list(image.size),
            "source_image_size": list(source_size),
            "gpu_layers": self._resident,
            "seconds": round(time.monotonic() - started, 2),
            "peak_vram_mib": round(torch.cuda.max_memory_allocated() / 1024**2),
        }
        text = reading.text(job.params.language)
        return [Output("text/plain", text.encode("utf-8"), meta)]
