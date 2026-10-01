"""The models this server can run: pinned GGUF files and how to prompt them."""

from dataclasses import dataclass


@dataclass(frozen=True)
class ModelSpec:
    key: str
    name: str
    repo: str
    revision: str
    file: str
    license: str
    source: str
    # Whether the chat template can switch step-by-step reasoning on and off.
    thinking: bool
    # Sampling the model card recommends (also in the GGUF metadata).
    temperature: float
    top_k: int
    top_p: float
    # Extra chat template arguments when thinking is on (e.g. its effort).
    thinking_kwargs: tuple[tuple[str, str], ...] = ()


MODELS = {
    spec.key: spec
    for spec in (
        ModelSpec(
            key="gemma-4-12b",
            name="Gemma 4 12B (Q8_0)",
            repo="unsloth/gemma-4-12b-it-GGUF",
            revision="fc034cfff751157913579611efad8462ac1be606",  # pragma: allowlist secret
            file="gemma-4-12b-it-Q8_0.gguf",
            license="Apache-2.0",
            source="https://huggingface.co/google/gemma-4-12B-it",
            thinking=True,
            temperature=1.0,
            top_k=64,
            top_p=0.95,
        ),
        ModelSpec(
            key="qwen3.8-27b",
            name="Qwen3.8 27B (Q4_K_M)",
            repo="unsloth/Qwen3.8-27B-GGUF",
            revision="4ca720788d1e01f1bff70c033e0d0028fd02e502",  # pragma: allowlist secret
            file="Qwen3.8-27B-UD-Q4_K_M.gguf",
            license="Apache-2.0",
            source="https://huggingface.co/Qwen/Qwen3.8-27B",
            thinking=True,
            temperature=1.0,
            top_k=20,
            top_p=0.95,
            # The template's default is "xhigh": thousands of tokens of thought.
            thinking_kwargs=(("reasoning_effort", "low"),),
        ),
    )
}
DEFAULT_MODEL = "gemma-4-12b"
