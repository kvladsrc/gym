"""One page image in, UTF-8 text out. PDF rendering belongs to the caller.

The upstream model and executable Python code share a pinned HF revision.
The native Transformers infer() path avoids a custom SGLang wheel and leaves
GPU scheduling and file provenance to Studio and its existing launcher.
"""

import logging
import re
from typing import Any

from model_server_sdk.images import open_image
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    GenerationError,
    InputSpec,
    InvalidInput,
    Job,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
    ui,
)

logger = logging.getLogger("unlimited_ocr_model_server")
REPO = "baidu/Unlimited-OCR-2"
REVISION = "0480966938291445591f69f08a13b041c073a96a"  # pragma: allowlist secret
DEFAULT_PROMPT = "Multi page parsing."
_BLOCK_PREFIX = re.compile(
    r"<BLOCK>\s*(?P<label>[a-z_]+)\s*"
    r"(?:(?:<LEVEL\d*>|\[[\d,\s.\-]+\])\s*)*<FORK>",
    re.IGNORECASE,
)


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    max_tokens: int = Field(
        default=4096,
        ge=16,
        le=16384,
        description="Maximum output tokens (truncation is recorded in metadata)",
        json_schema_extra=ui(ru="Максимальная длина текста, токенов", primary=True),
    )
    crop_mode: bool = Field(
        default=False,
        description="Use image tiles for small text (needs more GPU memory)",
        json_schema_extra=ui(ru="Тайлы для мелкого текста (больше видеопамяти)", primary=True),
    )


TASK = TaskSpec(
    "image-to-text",
    Params,
    ("text/plain",),
    prompt="optional",
    inputs=(
        InputSpec(
            role="image",
            mime=["image/png", "image/jpeg", "image/webp"],
            description="One document page, rendered to an image",
            labels={"ru": "Одна страница документа в виде изображения"},
        ),
    ),
    max_count=1,
)


def download() -> str:
    """Fetch code, tokenizer and weights, excluding demos and serving wheels."""
    from huggingface_hub import snapshot_download

    return snapshot_download(
        REPO,
        revision=REVISION,
        allow_patterns=["*.py", "*.json", "*.safetensors"],
    )


def clean_text(text: str) -> str:
    """Render single-page layout blocks, keeping tables, formulas and reading order."""

    def block_prefix(match: re.Match[str]) -> str:
        label = match["label"].lower().removeprefix("cross_").removeprefix("append_")
        if label == "title":
            level = re.search(r"<LEVEL(\d+)>", match[0], re.IGNORECASE)
            return "\n\n" + "#" * min(6, max(1, int(level[1]) if level else 1)) + " "
        return "\n\n"

    text = _BLOCK_PREFIX.sub(block_prefix, text)
    text = re.sub(r"<\|ref\|>.*?<\|/ref\|>", "", text, flags=re.DOTALL)
    text = re.sub(r"<\|det\|>.*?<\|/det\|>", "", text, flags=re.DOTALL)
    for marker in ("<PAGE>", "<MERGE>"):
        text = text.replace(marker, "\n")
    for marker in ("<FORK>", "<｜end▁of▁sentence｜>", "<｜begin▁of▁sentence｜>"):
        text = text.replace(marker, "")
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    return re.sub(r"\n{3,}", "\n\n", text).strip() + "\n"


class UnlimitedOCRServer(ModelServer):
    model = ModelInfo(
        id="unlimited-ocr-2",
        name="Unlimited-OCR 2",
        revision=REVISION,
        license="MIT",
        source="https://github.com/baidu/Unlimited-OCR",
        details={
            "parameters": "3.34 B",
            "precision": "bfloat16",
            "input": "one page image; PDF rendering is external",
        },
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._model: Any = None
        self._tokenizer: Any = None

    def load(self) -> None:
        import torch
        from transformers import AutoModel, AutoTokenizer

        torch.set_grad_enabled(False)
        if not torch.cuda.is_available():
            raise RuntimeError("Unlimited-OCR 2 needs a CUDA GPU")
        directory = download()
        self._tokenizer = AutoTokenizer.from_pretrained(directory, trust_remote_code=True)
        self._model = (
            AutoModel.from_pretrained(
                directory,
                trust_remote_code=True,
                use_safetensors=True,
                low_cpu_mem_usage=True,
                torch_dtype=torch.bfloat16,
                attn_implementation="eager",
            )
            .eval()
            .to("cuda")
        )
        logger.info("Unlimited-OCR 2 ready (%s)", REVISION)

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, Params)
        image = open_image(job.inputs["image"].data)
        prompt = (job.prompt or "").strip() or DEFAULT_PROMPT
        # infer() prepends the trained page/image markers itself.
        if "<image>" in prompt or "<PAGE>" in prompt:
            raise InvalidInput("supply an instruction without <image> or <PAGE> markers")
        torch.cuda.reset_peak_memory_stats()
        try:
            with torch.inference_mode():
                result = self._model.infer(
                    self._tokenizer,
                    prompt=prompt,
                    image_file=image,
                    base_size=1024,
                    image_size=640,
                    crop_mode=job.params.crop_mode,
                    max_crops=4,
                    max_new_tokens=job.params.max_tokens,
                    # Let the global budget report finish_reason=length rather
                    # than the page guard forcing EOS and looking complete.
                    max_page_tokens=20000,
                    temperature=0.0,
                    no_repeat_ngram_size=35,
                    ngram_window=128,
                    eval_mode=True,
                    save_results=False,
                    return_dict=True,
                )
        except RuntimeError as error:
            if not is_out_of_memory(error):
                raise
            torch.cuda.empty_cache()
            raise GenerationError(
                "out of GPU memory; disable crop_mode or reduce max_tokens", retryable=False
            ) from error
        text = clean_text(result["text"])
        meta = {
            "prompt": prompt,
            "image_size": list(image.size),
            "output_tokens": len(result["token_ids"]),
            "finish_reason": result["finish_reason"],
            "truncated": result["finish_reason"] == "length",
            "peak_vram_mib": round(torch.cuda.max_memory_allocated() / 1024**2),
        }
        return [Output("text/plain", text.encode("utf-8"), meta)]
