"""Pinned, unquantized Qwen3 embeddings; lazy CPU serving and batched GPU indexing.

PROFILE is stored in every database and checked when it is opened. Changing the model,
its revision, the instruction or the document template therefore needs a new database:
``yaqs rebuild --output NEW_DB`` re-embeds the stored text with the current profile.
"""

import threading
from pathlib import Path
from typing import Any

REPO = "Qwen/Qwen3-Embedding-0.6B"
REVISION = "97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3"  # pragma: allowlist secret
DIMENSIONS = 1024
INSTRUCTION = (
    "Given a game development question, retrieve relevant passages that answer the question"
)
DOCUMENT_TEMPLATE = "{title}\n{chapter}\n\n{text}"
PROFILE = {
    "model": REPO,
    "revision": REVISION,
    "dimensions": DIMENSIONS,
    "pooling": "last-token",
    "normalization": "l2-float32",
    "query_instruction": INSTRUCTION,
    "document_template": DOCUMENT_TEMPLATE,
    "quantized": False,
}
MAX_INPUT_TOKENS = 1024  # longer inputs are rejected, never silently truncated
TOKENIZER_FILES = ("config.json", "tokenizer_config.json", "tokenizer.json")
MODEL_FILES = (*TOKENIZER_FILES, "model.safetensors")


def document_text(title: str, chapter: str, text: str) -> str:
    """Embedding input of a chunk, as recorded in PROFILE."""
    return DOCUMENT_TEMPLATE.format(title=title, chapter=chapter, text=text)


def query_text(query: str) -> str:
    return f"Instruct: {INSTRUCTION}\nQuery: {query}"


def cache_status() -> dict[str, Any]:
    """Inspect the pinned local checkpoint without downloading or loading weights."""
    from huggingface_hub import try_to_load_from_cache

    files = {name: try_to_load_from_cache(REPO, name, revision=REVISION) for name in MODEL_FILES}
    present = {
        name: value
        for name, value in files.items()
        if isinstance(value, str) and Path(value).is_file()
    }
    missing = [name for name in MODEL_FILES if name not in present]
    return {
        "model": REPO,
        "revision": REVISION,
        "ready": not missing,
        "missing": missing,
        "files": present,
    }


def require_cache(*, model: bool = False) -> None:
    missing = cache_status()["missing"]
    needed = MODEL_FILES if model else TOKENIZER_FILES
    if absent := [name for name in missing if name in needed]:
        raise RuntimeError(
            "Qwen checkpoint is missing " + ", ".join(absent) + "; run yaqs download"
        )


def download() -> str:
    from huggingface_hub import snapshot_download

    return snapshot_download(
        REPO, revision=REVISION, allow_patterns=["*.json", "*.txt", "*.safetensors"]
    )


class SerializedTokenizer:
    """Calls the encoder's tokenizer under its own lock.

    A fast tokenizer keeps padding state in one Rust object: a padded query batch
    overlapping an unpadded token count raises "RuntimeError: Already borrowed".
    MCP runs synchronous tools in worker threads, so every call is serialized.
    """

    def __init__(self, encoder: "QwenEncoder") -> None:
        self._encoder = encoder

    def __call__(self, *args: Any, **kwargs: Any) -> Any:
        with self._encoder._tokenizer_lock:
            return self._encoder._load_tokenizer()(*args, **kwargs)


class QwenEncoder:
    def __init__(self, device: str = "cpu", threads: int = 4) -> None:
        if device not in {"cpu", "cuda"}:
            raise ValueError("device must be cpu or cuda")
        self.device = device
        self.threads = threads
        self._tokenizer: Any = None
        self._model: Any = None
        self._lock = threading.RLock()
        # Separate from the model lock: counting tokens need not wait for inference.
        self._tokenizer_lock = threading.RLock()
        self._serialized = SerializedTokenizer(self)

    @property
    def tokenizer(self) -> SerializedTokenizer:
        return self._serialized

    def _load_tokenizer(self) -> Any:
        with self._tokenizer_lock:
            if self._tokenizer is None:
                from transformers import AutoTokenizer

                require_cache()
                self._tokenizer = AutoTokenizer.from_pretrained(
                    REPO, revision=REVISION, local_files_only=True, padding_side="left"
                )
            return self._tokenizer

    def _load(self) -> None:
        if self._model is not None:
            return
        require_cache(model=True)
        import torch
        from transformers import AutoModel

        torch.set_num_threads(self.threads)
        if self.device == "cuda" and not torch.cuda.is_available():
            raise RuntimeError("CUDA is unavailable; use --device cpu")
        self._model = (
            AutoModel.from_pretrained(
                REPO,
                revision=REVISION,
                local_files_only=True,
                torch_dtype=torch.bfloat16 if self.device == "cuda" else torch.float32,
                attn_implementation="sdpa",
            )
            .eval()
            .to(self.device)
        )
        self._model.config.use_cache = False

    def encode(self, texts: list[str], *, query: bool = False) -> Any:
        import numpy as np
        import torch
        import torch.nn.functional as functional

        if not texts:
            return np.empty((0, DIMENSIONS), dtype=np.float32)
        with self._lock:
            self._load()
            if query:
                texts = [query_text(text) for text in texts]
            batch = self.tokenizer(texts, padding=True, truncation=False, return_tensors="pt")
            if batch["input_ids"].shape[1] > MAX_INPUT_TOKENS:
                raise ValueError(
                    f"Embedding input exceeds {MAX_INPUT_TOKENS} tokens; "
                    "no text was silently truncated"
                )
            with torch.inference_mode():
                batch = batch.to(self.device)
                last = self._model(**batch, use_cache=False).last_hidden_state[:, -1].float()
                vectors = functional.normalize(last, p=2, dim=1).cpu().numpy()
            if not np.isfinite(vectors).all():
                raise RuntimeError("Model returned non-finite embeddings")
            return vectors.astype(np.float32, copy=False)
