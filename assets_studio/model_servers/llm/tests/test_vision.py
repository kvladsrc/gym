"""Visual requests stay bounded, correctly labelled and isolated from text tasks."""

import base64
import io
import json
import subprocess

import pytest
from llm_model_server import vision
from llm_model_server.models import MODELS
from llm_model_server.server import LlmServer
from PIL import Image

from model_server_sdk import InputData, InvalidInput, Job, Output, create_app


@pytest.fixture(autouse=True)
def media_tools(monkeypatch):
    monkeypatch.setattr(vision.shutil, "which", lambda command: command)


def png(color="red", size=(48, 32)):
    out = io.BytesIO()
    Image.new("RGB", size, color).save(out, format="PNG")
    return out.getvalue()


def test_only_models_with_matching_projectors_declare_visual_tasks():
    qwen = LlmServer(MODELS["qwen3.8-27b"])
    gemma = LlmServer(MODELS["gemma-4-12b"])
    create_app(qwen)
    create_app(gemma)
    assert {t.task for t in qwen.tasks} == {"text-to-text", "image-to-text", "video-to-text"}
    assert {t.task for t in gemma.tasks} == {"text-to-text"}
    assert qwen.spec.projector == "mmproj-F16.gguf"
    assert gemma.runtime_args() == []
    assert all(t.output_mime == ("text/plain",) for t in qwen.tasks)


def test_image_limit_preserves_aspect_ratio_and_returns_local_data_only():
    part = vision.image_part(png(size=(2560, 1280)))
    url = part["image_url"]["url"]
    assert url.startswith("data:image/png;base64,")
    image = Image.open(io.BytesIO(base64.b64decode(url.split(",", 1)[1])))
    assert image.size == (1280, 640)


def test_invalid_image_does_not_reach_inference():
    with pytest.raises(InvalidInput, match="cannot read"):
        vision.image_part(b"not an image")


def test_comparison_labels_and_prompt_are_preserved():
    job = Job(
        "image-to-text",
        "Compare hands only",
        {"image": InputData("image/png", png()), "reference": InputData("image/png", png("blue"))},
        vision.Params(),
        (5,),
    )
    chat, meta = vision.request(job)
    content = chat[1]["content"]
    assert content[0]["text"] == "Image: image"
    assert content[2]["text"] == "Image: reference"
    assert content[-1]["text"] == "Compare hands only"
    assert meta["images"] == 2
    assert "предположений" in chat[0]["content"]


def test_visual_generation_returns_only_text_and_observation_provenance(monkeypatch):
    server = LlmServer(MODELS["qwen3.8-27b"])
    calls = []

    def complete(chat, params, seed):
        calls.append((chat, params, seed))
        return Output("text/plain", b"red shape\n", {"finish_reason": "stop"})

    monkeypatch.setattr(server, "_complete", complete)
    results = server.generate(
        Job(
            "image-to-text",
            "color?",
            {"image": InputData("image/png", png())},
            vision.Params(),
            (123,),
        )
    )
    assert len(results) == 1
    assert results[0].data == b"red shape\n"
    assert results[0].meta["analysis"] == "local_vlm_observation"
    assert calls[0][2] == 123
    assert not calls[0][1].thinking


def test_video_requests_record_timestamps_and_sampling_limitations(monkeypatch):
    monkeypatch.setattr(
        vision, "sample_video", lambda data, count: ([(0.5, png()), (1.5, png())], 2)
    )
    job = Job(
        "video-to-text",
        "Describe",
        {"video": InputData("video/mp4", b"video")},
        vision.Params(video_frames=2),
        (0,),
    )
    chat, meta = vision.request(job)
    assert meta["sample_times_s"] == [0.5, 1.5]
    assert meta["audio_analyzed"] is False
    assert meta["continuous_motion_analyzed"] is False
    assert chat[1]["content"][0]["text"] == "Sample at 0.500s"


def test_video_duration_is_rejected_before_decoding_frames(monkeypatch):
    calls = []

    def run(args, **kwargs):
        calls.append(args)
        return subprocess.CompletedProcess(
            args, 0, json.dumps({"format": {"duration": 61}}).encode()
        )

    monkeypatch.setattr(vision.subprocess, "run", run)
    with pytest.raises(InvalidInput, match="60 seconds"):
        vision.sample_video(b"bad", 4)
    assert len(calls) == 1
    assert calls[0][0] == "ffprobe"


def test_video_decode_uses_uniform_bounded_samples(monkeypatch):
    calls = []

    def run(args, **kwargs):
        calls.append(args)
        payload = (
            json.dumps({"format": {"duration": 8}}).encode() if args[0] == "ffprobe" else png()
        )
        return subprocess.CompletedProcess(args, 0, payload)

    monkeypatch.setattr(vision.subprocess, "run", run)
    samples, duration = vision.sample_video(b"video", 4)
    assert duration == 8
    assert [t for t, _ in samples] == [1, 3, 5, 7]
    assert len(calls) == 5
    assert all("scale=1280:1280:force_original_aspect_ratio=decrease" in cmd for cmd in calls[1:])
