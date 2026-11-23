"""Review server boundaries: the training prompt, the image size, the two reads, the report."""

import io
from pathlib import Path
from typing import Any

import numpy as np
import pytest
import torch
from PIL import Image
from pydantic import ValidationError
from qwen_review_model_server.server import (
    POSITIVE_MID,
    TASK,
    Params,
    QwenReviewServer,
    adapter_directory,
    names,
    screenshot,
    user_text,
)

from model_server_sdk import GenerationError, InputData, InvalidInput, Job, create_app

# Class 1 is the likeliest overall; from class 3 (10+ reviews) up, class 4 is.
REVIEWS = np.array([0.02, 0.40, 0.08, 0.05, 0.25, 0.10, 0.05, 0.03, 0.01, 0.01])
POSITIVE = np.array([0.0, 0.0, 0.05, 0.05, 0.10, 0.20, 0.30, 0.20, 0.10, 0.0])


@pytest.fixture
def texts() -> list[str]:
    return []


@pytest.fixture
def server(monkeypatch: pytest.MonkeyPatch, tmp_path: Path, texts: list[str]) -> QwenReviewServer:
    monkeypatch.setattr(torch.cuda, "reset_peak_memory_stats", lambda: None)
    monkeypatch.setattr(torch.cuda, "max_memory_allocated", lambda: 3 * 1024**3)
    monkeypatch.setattr(torch.cuda, "empty_cache", lambda: None)
    instance = QwenReviewServer(adapter=tmp_path)

    def digit_probs(text: str, image: Image.Image) -> np.ndarray:
        texts.append(text)
        return POSITIVE if text.endswith(" ") else REVIEWS

    instance._chat = lambda text: f"<chat>{text}</chat>"  # type: ignore[method-assign]
    instance._digit_probs = digit_probs  # type: ignore[method-assign]
    return instance


def png(
    size: tuple[int, int] = (1920, 1080), mode: str = "RGBA", color: Any = (0, 0, 0, 0)
) -> bytes:
    buffer = io.BytesIO()
    Image.new(mode, size, color).save(buffer, format="PNG")
    return buffer.getvalue()


def job(data: bytes | None = None, **params: Any) -> Job:
    return Job(
        task="image-to-text",
        prompt=None,
        inputs={"image": InputData("image/png", data or png())},
        params=Params(**params),
        seeds=(42,),
    )


def test_existing_contract_accepts_the_declaration(tmp_path: Path) -> None:
    create_app(QwenReviewServer(adapter=tmp_path))
    assert TASK.inputs[0].mime == ["image/png", "image/jpeg", "image/webp"]
    assert TASK.output_mime == ("text/plain",)
    assert TASK.prompt == "none"
    assert TASK.max_count == 1
    assert QwenReviewServer(adapter=tmp_path).model.revision.endswith("+lora-missing")


def test_prompt_is_the_training_prompt() -> None:
    tags = names("Roguelike, Pixel Graphics,Cute, 2D, Indie, Singleplayer")
    assert user_text(2026, names(" Action ,Indie,, "), tags) == (
        "Game screenshot. Release year: 2026. Genres: Action, Indie."
        " Tags: Roguelike, Pixel Graphics, Cute, 2D, Indie.\n"
        "How many reviews will this game get, and what share of them will be positive?"
        " Answer with two classes from 0 to 9."
    )
    assert user_text(2001, [], []).startswith(
        "Game screenshot. Release year: 2001. Genres: none. Tags: none.\n"
    )


def test_year_outside_the_range_is_refused() -> None:
    with pytest.raises(ValidationError):
        Params(year=1900)


def test_screenshot_is_fitted_into_the_training_thumbnail_size() -> None:
    image, size = screenshot(png())
    assert size == (1920, 1080)
    assert image.size == (600, 338)
    assert image.getpixel((0, 0)) == (255, 255, 255)  # transparency on white, RGB
    small, _ = screenshot(png((300, 200), "RGB", (10, 20, 30)))
    assert small.size == (300, 200)  # never enlarged


def test_share_is_read_after_the_likeliest_class_with_ten_reviews_or_more(
    server: QwenReviewServer, texts: list[str]
) -> None:
    output = server.generate(job(year=2025, genres="Action", tags="Roguelike"))[0]
    first, second = texts
    assert first == "<chat>" + user_text(2025, ["Action"], ["Roguelike"]) + "</chat>"
    assert second == first + "4 "
    meta = output.meta
    assert meta["review_class_likely"] == 1
    assert meta["positive_after_class"] == 4
    assert meta["review_class_expected"] == pytest.approx(float(REVIEWS @ np.arange(10)), abs=1e-3)
    assert meta["positive_expected"] == pytest.approx(float(POSITIVE @ POSITIVE_MID), abs=0.05)
    assert meta["positive_likely"] == "85-89%"
    assert meta["image_size"] == [600, 338]
    assert meta["source_image_size"] == [1920, 1080]
    assert meta["peak_vram_mib"] == 3072


def test_report_in_russian(server: QwenReviewServer) -> None:
    assert server.generate(job())[0].data.decode() == (
        "Отзывов: скорее всего 1–2 (вероятность 40 %).\n"
        "Средний класс 2,9 по шкале от 0 (ни одного) до 9 (10 000 и больше).\n"
        "\n"
        "Вероятность каждого класса:\n"
        "0 — ни одного: 2 %\n"
        "1 — 1–2: 40 %\n"
        "2 — 3–9: 8 %\n"
        "3 — 10–29: 5 %\n"
        "4 — 30–99: 25 %\n"
        "5 — 100–299: 10 %\n"
        "6 — 300–999: 5 %\n"
        "7 — 1000–2999: 3 %\n"
        "8 — 3000–9999: 1 %\n"
        "9 — 10 000 и больше: 1 %\n"
        "\n"
        "Доля положительных, если отзывов наберётся 10 и больше"
        " (Qwen считала, что их будет 30–99): около 86 %, скорее всего 85–89 %"
        " (вероятность 30 %).\n"
    )


def test_report_in_english(server: QwenReviewServer) -> None:
    text = server.generate(job(language="en"))[0].data.decode()
    assert text.startswith(
        "Reviews: most likely 1-2 (40%).\n"
        "Mean class 2.9 on a scale from 0 (none) to 9 (10,000 or more).\n"
    )
    assert "9 — 10,000 or more: 1%\n" in text
    assert text.endswith("(read assuming 30-99 reviews): about 86%, most likely 85-89% (30%).\n")


def test_broken_image_fails_before_inference(server: QwenReviewServer, texts: list[str]) -> None:
    with pytest.raises(InvalidInput, match="image"):
        server.generate(job(data=b"not an image"))
    assert not texts


def test_out_of_gpu_memory_is_a_clear_error(server: QwenReviewServer) -> None:
    def out_of_memory(text: str, image: Image.Image) -> np.ndarray:
        raise torch.OutOfMemoryError("CUDA out of memory. Tried to allocate 2.00 GiB")

    server._digit_probs = out_of_memory  # type: ignore[method-assign]
    with pytest.raises(GenerationError, match="gpu-layers"):
        server.generate(job())


def test_the_2b_names_itself_and_finds_its_own_adapter_folder(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    monkeypatch.delenv("QWEN_REVIEW_ADAPTER", raising=False)
    monkeypatch.setenv("XDG_CACHE_HOME", str(tmp_path))
    info = QwenReviewServer(base="2b").model
    assert info.id == "qwen3_5-2b-steam-reviews"
    assert info.source == "https://huggingface.co/Qwen/Qwen3.5-2B"
    assert info.revision == "15852e8c1636+lora-missing"
    assert adapter_directory("2b") == tmp_path / "assets-studio" / "qwen_review_2b"
    assert adapter_directory("9b") == tmp_path / "assets-studio" / "qwen_review"
    monkeypatch.setenv("QWEN_REVIEW_ADAPTER", str(tmp_path / "elsewhere"))
    assert adapter_directory("2b") == tmp_path / "elsewhere"


def test_an_unknown_base_model_is_refused(tmp_path: Path) -> None:
    with pytest.raises(ValueError, match="unknown base model"):
        QwenReviewServer(adapter=tmp_path, base="4b")
