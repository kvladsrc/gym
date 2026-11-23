"""Pinned, unquantized Qwen3 embeddings; lazy CPU serving and batched GPU indexing.

PROFILE is stored in every database and checked when it is opened. Changing the model,
its revision, the instruction or the document template therefore needs a new database:
``yaqs rebuild --output NEW_DB`` re-embeds the stored text with the current profile.
"""

import json
import os
import threading
from pathlib import Path
from typing import Any

from yaqs import PROJECT

REPO = "Qwen/Qwen3-Embedding-4B"
REVISION = "5cf2132abc99cad020ac570b19d031efec650f2b"  # pragma: allowlist secret
DIMENSIONS = 2560
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
MAX_CUDA_TOKENS = 4096  # cap padded microbatches for the unquantized 4B model
TOKENIZER_FILES = ("config.json", "tokenizer_config.json", "tokenizer.json")
MODEL_FILES = (
    *TOKENIZER_FILES,
    "model.safetensors.index.json",
    "model-00001-of-00002.safetensors",
    "model-00002-of-00002.safetensors",
)
MODEL_CONFIG = PROJECT / "data/embedding-model.json"


def _model_directory() -> Path | None:
    """Optional existing shared checkpoint, without downloading another copy."""
    if location := os.environ.get("YAQS_MODEL_PATH"):
        return Path(location).expanduser().resolve()
    if MODEL_CONFIG.is_file():
        config = json.loads(MODEL_CONFIG.read_text(encoding="utf-8"))
        if config.get("model") != REPO or config.get("revision") != REVISION:
            raise ValueError("Shared embedding checkpoint differs from the pinned YAQS model")
        return Path(config["path"]).expanduser().resolve()
    return None


def document_text(title: str, chapter: str, text: str) -> str:
    """Embedding input of a chunk, as recorded in PROFILE."""
    return DOCUMENT_TEMPLATE.format(title=title, chapter=chapter, text=text)


def query_text(query: str) -> str:
    return f"Instruct: {INSTRUCTION}\nQuery: {query}"


def cache_status() -> dict[str, Any]:
    """Inspect the pinned local checkpoint without downloading or loading weights."""
    from huggingface_hub import try_to_load_from_cache

    directory = _model_directory()
    files = {
        name: str(directory / name)
        if directory is not None
        else try_to_load_from_cache(REPO, name, revision=REVISION)
        for name in MODEL_FILES
    }
    present = {
        name: value
        for name, value in files.items()
        if isinstance(value, str) and Path(value).is_file()
    }
    missing = [name for name in MODEL_FILES if name not in present]
    return {
        "model": REPO,
        "revision": REVISION,
        "shared_checkpoint": str(directory) if directory is not None else None,
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
                    _model_directory() or REPO,
                    revision=REVISION,
                    local_files_only=True,
                    padding_side="left",
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
        self._model = AutoModel.from_pretrained(
            _model_directory() or REPO,
            revision=REVISION,
            local_files_only=True,
            torch_dtype=torch.bfloat16 if self.device == "cuda" else torch.float32,
            attn_implementation="sdpa",
        ).eval()
        if self.device == "cuda":
            # Keep the vocabulary lookup on CPU so the unquantized 4B model
            # and an indexing batch fit on an 8 GiB GPU.
            self._model.layers.to("cuda")
            self._model.norm.to("cuda")
            self._model.rotary_emb.to("cuda")
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
            microbatch = len(texts)
            if self.device == "cuda":
                microbatch = max(1, MAX_CUDA_TOKENS // max(1, batch["input_ids"].shape[1]))
            parts = []
            with torch.inference_mode():
                for start in range(0, len(texts), microbatch):
                    part = {key: value[start : start + microbatch] for key, value in batch.items()}
                    if self.device == "cuda":
                        inputs = self._model.get_input_embeddings()(part.pop("input_ids")).to(
                            "cuda"
                        )
                        part = {key: value.to("cuda") for key, value in part.items()}
                        hidden = self._model(inputs_embeds=inputs, **part, use_cache=False)
                    else:
                        hidden = self._model(**part, use_cache=False)
                    last = hidden.last_hidden_state[:, -1].float()
                    parts.append(functional.normalize(last, p=2, dim=1).cpu().numpy())
            vectors = np.concatenate(parts, axis=0)
            if not np.isfinite(vectors).all():
                raise RuntimeError("Model returned non-finite embeddings")
            return vectors.astype(np.float32, copy=False)
