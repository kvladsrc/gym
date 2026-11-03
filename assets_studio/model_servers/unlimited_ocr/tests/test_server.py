"""OCR adapter boundaries: images, model arguments, layout and truncation."""

import io
from typing import Any

import pytest
import torch
from PIL import Image
from unlimited_ocr_model_server.server import (
    DEFAULT_PROMPT,
    TASK,
    Params,
    UnlimitedOCRServer,
    clean_text,
)

from model_server_sdk import InputData, InvalidInput, Job, create_app


class RecordingModel:
    def __init__(self) -> None:
        self.arguments: dict[str, Any] = {}

    def infer(self, tokenizer: object, **kwargs: Any) -> dict[str, Any]:
        self.arguments = kwargs
        return {
            "text": "<PAGE><BLOCK>title[0, 0, 999, 100]<FORK>Баланс игры\r\n42",
            "token_ids": [1, 2, 3],
            "finish_reason": "length",
        }


@pytest.fixture
def server(monkeypatch: pytest.MonkeyPatch) -> UnlimitedOCRServer:
    monkeypatch.setattr(torch.cuda, "reset_peak_memory_stats", lambda: None)
    monkeypatch.setattr(torch.cuda, "max_memory_allocated", lambda: 1024**2)
    instance = UnlimitedOCRServer()
    instance._model = RecordingModel()
    return instance


def job(prompt: str | None = None, data: bytes | None = None, **params: Any) -> Job:
    if data is None:
        image = Image.new("RGBA", (64, 80), (0, 0, 0, 0))
        buffer = io.BytesIO()
        image.save(buffer, format="PNG")
        data = buffer.getvalue()
    return Job(
        task="image-to-text",
        prompt=prompt,
        inputs={"image": InputData("image/png", data)},
        params=Params(**params),
        seeds=(42,),
    )


def test_existing_contract_accepts_the_image_to_text_declaration() -> None:
    create_app(UnlimitedOCRServer())
    assert TASK.inputs[0].mime == ["image/png", "image/jpeg", "image/webp"]
    assert TASK.output_mime == ("text/plain",)
    assert TASK.prompt == "optional"
    assert TASK.max_count == 1


def test_transparent_page_is_white_and_truncation_is_visible(server: UnlimitedOCRServer) -> None:
    output = server.generate(job())[0]
    assert output.data.decode() == "# Баланс игры\n42\n"
    assert output.meta["truncated"] is True
    assert output.meta["finish_reason"] == "length"
    assert output.meta["output_tokens"] == 3
    arguments = server._model.arguments
    assert arguments["image_file"].getpixel((0, 0)) == (255, 255, 255)
    assert arguments["prompt"] == DEFAULT_PROMPT
    assert arguments["crop_mode"] is False


def test_instruction_and_budget_reach_model_without_streaming(server: UnlimitedOCRServer) -> None:
    server.generate(job("Read the table.", max_tokens=123, crop_mode=True))
    args = server._model.arguments
    assert args["prompt"] == "Read the table."
    assert args["max_new_tokens"] == 123
    assert args["max_page_tokens"] > args["max_new_tokens"]
    assert args["crop_mode"] is True
    assert args["eval_mode"] is True  # no document text in server stdout
    assert args["save_results"] is False
    assert args["temperature"] == 0


@pytest.mark.parametrize("prompt", ["<image>read", "<PAGE>read"])
def test_page_placeholders_cannot_break_single_image_layout(
    server: UnlimitedOCRServer, prompt: str
) -> None:
    with pytest.raises(InvalidInput, match="markers"):
        server.generate(job(prompt))


def test_broken_image_fails_before_inference(server: UnlimitedOCRServer) -> None:
    with pytest.raises(InvalidInput, match="image"):
        server.generate(job(data=b"not an image"))
    assert not server._model.arguments


def test_layout_removal_keeps_tables_math_and_reading_order() -> None:
    text = (
        "<PAGE><|ref|>title<|/ref|><|det|>[[0, 1, 2, 3]]<|/det|># Heading\n"
        "| A | B |\n|---|---|\n| 1 | 2 |\n$E=mc^2$\n<｜end▁of▁sentence｜>"
    )
    assert clean_text(text) == "# Heading\n| A | B |\n|---|---|\n| 1 | 2 |\n$E=mc^2$\n"


def test_native_blocks_keep_heading_levels_html_tables_and_formulas() -> None:
    text = (
        "<PAGE><BLOCK>title<LEVEL2>[10, 20, 90, 30]<FORK>Баланс"
        "<BLOCK>text[10, 40, 90, 50]<FORK>Health: 100"
        "<BLOCK>table[10, 60, 90, 80]<FORK><table><tr><td>25</td></tr></table>"
        "<BLOCK>equation[10, 80, 90, 90]<FORK>$E=mc^2$"
    )
    assert clean_text(text) == (
        "## Баланс\n\nHealth: 100\n\n<table><tr><td>25</td></tr></table>\n\n$E=mc^2$\n"
    )
