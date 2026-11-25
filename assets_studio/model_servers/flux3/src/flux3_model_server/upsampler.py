"""FLUX 3's prompt upsampler in llama.cpp.

FLUX 3 Image Klein is trained on full scene captions; its card says to always
expand the request first. The upsampler is Black Forest Labs' fine-tune of
Qwen3.5-9B (Apache-2.0), here as a GGUF file in the pinned llama.cpp build of
the `llm` server. It runs as a child process for one job's caption and exits
before the image model needs the GPU. Requests, system prompts, reference
preprocessing and decoding follow BFL's reference code (flux3/pup.py).
"""

import base64
import io
import json
import logging
import math
import os
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

from llm_model_server import runtime
from llm_model_server.server import _die_with_parent, _free_port
from PIL import Image, ImageOps

from model_server_sdk import GenerationError, InvalidInput

logger = logging.getLogger("flux3_model_server")

# Greedy decoding; a presence penalty for text-to-image only (reference code).
MAX_TOKENS = 3584
PRESENCE_PENALTY = {"t2i": 1.5, "i2i": 0.0}
# The reference engine's context: system prompt, references and caption.
CONTEXT_TOKENS = 8192
LOAD_TIMEOUT_S = 600
REQUEST_TIMEOUT_S = 1800

I2I_SCAFFOLD = (
    "Reference images follow in order (the first image, the second image, ...).\n"
    "User request: {instruction}\n"
    "Output the caption."
)
# References as the upsampler saw them in training: sides a multiple of 32,
# a pixel budget by their number, JPEG at quality 92.
REFERENCE_MULTIPLE = 32
MAX_PIXELS_BY_REFERENCES = {1: 1024 * 1024, 2: 724 * 724}
JPEG_QUALITY = 92
# A 1024² reference is 1024 image tokens (32 px each); llama.cpp would scale
# it down below its own default limit.
IMAGE_MAX_TOKENS = 1024


def request_text(prompt: str, canvas: str | None) -> str:
    """The user turn of a text-to-image request. The system prompt reads an
    optional first line "TARGET CANVAS: W:H" and composes for that shape."""
    prompt = prompt.strip()
    return f"TARGET CANVAS: {canvas}\n{prompt}" if canvas else prompt


def reference_size(width: int, height: int, count: int) -> tuple[int, int]:
    """The size a reference is resized to for the upsampler (reference code)."""

    def floor(value: float) -> int:
        return max(REFERENCE_MULTIPLE, math.floor(value / REFERENCE_MULTIPLE) * REFERENCE_MULTIPLE)

    budget = MAX_PIXELS_BY_REFERENCES[count]
    w = max(REFERENCE_MULTIPLE, round(width / REFERENCE_MULTIPLE) * REFERENCE_MULTIPLE)
    h = max(REFERENCE_MULTIPLE, round(height / REFERENCE_MULTIPLE) * REFERENCE_MULTIPLE)
    if w * h > budget:
        scale = math.sqrt(width * height / budget)
        w, h = floor(width / scale), floor(height / scale)
    if w * h > budget:
        if w == REFERENCE_MULTIPLE:
            h = floor(budget / w)
        else:
            w = floor(budget / h)
    return w, h


def reference_jpeg(image: Image.Image, count: int) -> bytes:
    """A reference as the upsampler sees it: RGB over white, resized with
    LANCZOS to its budget, JPEG at quality 92."""
    image = ImageOps.exif_transpose(image)
    if image.mode in ("RGBA", "LA", "PA") or (image.mode == "P" and "transparency" in image.info):
        rgba = image.convert("RGBA")
        image = Image.alpha_composite(Image.new("RGBA", rgba.size, (255, 255, 255, 255)), rgba)
    image = image.convert("RGB")
    size = reference_size(image.width, image.height, count)
    if image.size != size:
        image = image.resize(size, Image.Resampling.LANCZOS)
    buffer = io.BytesIO()
    image.save(buffer, format="JPEG", quality=JPEG_QUALITY)
    return buffer.getvalue()


def messages(
    system_prompts: dict[str, str], prompt: str, references: list[Image.Image], canvas: str | None
) -> tuple[str, list[dict[str, Any]]]:
    """(mode, chat): the system prompt of the mode and the user turn."""
    if not references:
        return "t2i", [
            {"role": "system", "content": system_prompts["t2i"]},
            {"role": "user", "content": request_text(prompt, canvas)},
        ]
    images = [
        {
            "type": "image_url",
            "image_url": {
                "url": "data:image/jpeg;base64,"
                + base64.b64encode(reference_jpeg(image, len(references))).decode()
            },
        }
        for image in references
    ]
    text = {"type": "text", "text": I2I_SCAFFOLD.format(instruction=prompt.strip())}
    return "i2i", [
        {"role": "system", "content": system_prompts["i2i"]},
        {"role": "user", "content": [*images, text]},
    ]


class Upsampler:
    def __init__(self, weights: str, projector: str, system_prompts: str) -> None:
        self.weights = weights
        self.projector = projector
        self.system_prompts: dict[str, str] = json.loads(Path(system_prompts).read_text())

    def caption(
        self, prompt: str, references: list[Image.Image], canvas: str | None
    ) -> tuple[str, dict[str, Any]]:
        """The full caption for the request and the run's numbers."""
        mode, chat = messages(self.system_prompts, prompt, references, canvas)
        body = {
            "messages": chat,
            "max_tokens": MAX_TOKENS,
            # Greedy, as the reference code decodes (temperature 0).
            "temperature": 1.0,
            "top_k": 1,
            "top_p": 0.95,
            "min_p": 0.0,
            "presence_penalty": PRESENCE_PENALTY[mode],
            "repeat_penalty": 1.0,
            "seed": 0,
            "cache_prompt": False,
            "chat_template_kwargs": {"enable_thinking": False},
        }
        started = time.monotonic()
        process, url = self._start()
        loaded = time.monotonic()
        try:
            reply = _post(url, body)
        finally:
            _stop(process)
        choice = reply["choices"][0]
        text = (choice["message"].get("content") or "").strip()
        if not text:
            raise GenerationError("the prompt upsampler returned no caption")
        if choice.get("finish_reason") == "length":
            raise GenerationError(f"the prompt upsampler did not finish in {MAX_TOKENS} tokens")
        timings = reply.get("timings", {})
        return text, {
            "upsampler_seconds": round(time.monotonic() - started, 1),
            "upsampler_load_seconds": round(loaded - started, 1),
            "upsampler_tokens": int(timings.get("predicted_n", 0)),
            "upsampler_tokens_per_s": round(float(timings.get("predicted_per_second", 0.0)), 1),
        }

    def _start(self) -> tuple[subprocess.Popen[bytes], str]:
        port = _free_port()
        url = f"http://127.0.0.1:{port}"
        command = [
            str(runtime.server_binary()),
            *("--model", self.weights, "--mmproj", self.projector),
            *("--host", "127.0.0.1", "--port", str(port)),
            *("--ctx-size", str(CONTEXT_TOKENS), "--parallel", "1"),
            *("--image-max-tokens", str(IMAGE_MAX_TOKENS)),
            "--jinja",
            "--no-webui",
        ]
        logger.info("starting the prompt upsampler (llama.cpp %s)", runtime.BUILD)
        # Its output goes to this server's terminal, where its errors are seen.
        process = subprocess.Popen(  # noqa: S603 -- our pinned binary, fixed arguments
            command,
            env={**os.environ, "LD_LIBRARY_PATH": runtime.library_path()},
            preexec_fn=_die_with_parent(),
        )
        try:
            deadline = time.monotonic() + LOAD_TIMEOUT_S
            while not _healthy(url):
                if process.poll() is not None:
                    raise GenerationError(
                        f"the prompt upsampler exited with code {process.returncode}",
                        retryable=True,
                    )
                if time.monotonic() > deadline:
                    raise GenerationError(
                        f"the prompt upsampler did not load in {LOAD_TIMEOUT_S} s"
                    )
                time.sleep(0.5)
        except BaseException:
            _stop(process)  # never leave it holding the GPU
            raise
        return process, url


def _healthy(url: str) -> bool:
    try:
        with urllib.request.urlopen(f"{url}/health", timeout=5) as response:  # noqa: S310
            return response.status == 200
    except (urllib.error.URLError, OSError):
        return False


def _post(url: str, body: dict[str, Any]) -> dict[str, Any]:
    request = urllib.request.Request(  # noqa: S310 -- the child on loopback
        f"{url}/v1/chat/completions",
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT_S) as response:  # noqa: S310
            return json.loads(response.read())
    except urllib.error.HTTPError as error:
        detail = error.read().decode(errors="replace")[:500]
        if error.code == 400 and "exceed_context_size" in detail:
            raise InvalidInput(f"the prompt is too long for the upsampler: {detail}") from error
        raise GenerationError(f"the prompt upsampler answered {error.code}: {detail}") from error
    except (urllib.error.URLError, OSError) as error:
        raise GenerationError(f"the prompt upsampler did not answer: {error}") from error


def _stop(process: subprocess.Popen[bytes]) -> None:
    if process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()
