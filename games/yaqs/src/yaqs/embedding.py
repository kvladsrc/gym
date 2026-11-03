"""Pinned, unquantized Qwen3 embeddings; lazy CPU serving and batched GPU indexing."""

import json
import threading
import urllib.error
import urllib.request
from typing import Any

REPO = "Qwen/Qwen3-Embedding-0.6B"
REVISION = "97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3"  # pragma: allowlist secret
DIMENSIONS = 1024
INSTRUCTION = (
    "Given a game development question, retrieve relevant passages that answer the question"
)
PROFILE = {
    "model": REPO,
    "revision": REVISION,
    "dimensions": DIMENSIONS,
    "pooling": "last-token",
    "normalization": "l2-float32",
    "query_instruction": INSTRUCTION,
    "document_template": "{title}\n{chapter}\n\n{text}",
    "quantized": False,
}
TOKENIZER_FILES = ("config.json", "tokenizer_config.json", "tokenizer.json")
MODEL_FILES = (*TOKENIZER_FILES, "model.safetensors")


def cache_status() -> dict[str, Any]:
    """Inspect the pinned local checkpoint without downloading or loading weights."""
    from pathlib import Path

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
            "Qwen checkpoint is missing "
            + ", ".join(absent)
            + "; run just yaqs setup or yaqs download"
        )


def download() -> str:
    from huggingface_hub import snapshot_download

    return snapshot_download(
        REPO, revision=REVISION, allow_patterns=["*.json", "*.txt", "*.safetensors"]
    )


def check_gpu_idle(studio_url: str = "http://127.0.0.1:9000") -> None:
    """Give Studio priority over this short-lived indexing process."""
    try:
        with urllib.request.urlopen(
            studio_url + "/api/jobs?status=queued&status=waiting_model&status=running&limit=1000",
            timeout=5,
        ) as response:
            jobs = json.load(response)
    except urllib.error.URLError as error:
        if not isinstance(error.reason, ConnectionRefusedError):
            raise RuntimeError("Cannot check the Studio GPU queue") from error
        jobs = []
    if jobs:
        raise RuntimeError("Studio has active jobs; retry indexing when its queue is empty")


class QwenEncoder:
    def __init__(self, device: str = "cpu", threads: int = 4) -> None:
        if device not in {"cpu", "cuda"}:
            raise ValueError("device must be cpu or cuda")
        self.device = device
        self.threads = threads
        self._tokenizer: Any = None
        self._model: Any = None
        self._lock = threading.RLock()

    @property
    def tokenizer(self) -> Any:
        with self._lock:
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
        if self.device == "cuda":
            check_gpu_idle()
            if not torch.cuda.is_available():
                raise RuntimeError("CUDA is unavailable; use --device cpu")
            free, total = torch.cuda.mem_get_info()
            if total - free - torch.cuda.memory_reserved() > 768 * 1024**2:
                raise RuntimeError("GPU is occupied; use CPU or retry after Studio releases it")
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
            if self.device == "cuda":
                check_gpu_idle()
            self._load()
            if query:
                texts = [f"Instruct: {INSTRUCTION}\nQuery: {text}" for text in texts]
            batch = self.tokenizer(texts, padding=True, truncation=False, return_tensors="pt")
            if batch["input_ids"].shape[1] > 1024:
                raise ValueError(
                    "Embedding input exceeds 1024 tokens; no text was silently truncated"
                )
            with torch.inference_mode():
                batch = batch.to(self.device)
                last = self._model(**batch, use_cache=False).last_hidden_state[:, -1].float()
                vectors = functional.normalize(last, p=2, dim=1).cpu().numpy()
            if not np.isfinite(vectors).all():
                raise RuntimeError("Model returned non-finite embeddings")
            return vectors.astype(np.float32, copy=False)
