"""Print the runtime environment, what is downloaded and the offload store."""

import json
import shutil
import subprocess
from pathlib import Path

import torch
from huggingface_hub import try_to_load_from_cache
from model_server_sdk.video import VideoEncodingError, ffmpeg_binary

from model_server_sdk import offload
from wan_model_server.server import GGUF_FILE, GGUF_REPO, GGUF_REVISION, REPO, REVISION

CHECK = [
    "transformer/config.json",
    "text_encoder/model-00001-of-00003.safetensors",
    "text_encoder/model-00003-of-00003.safetensors",
    "vae/diffusion_pytorch_model.safetensors",
    "tokenizer/spiece.model",
]


def _existing(path: Path) -> Path:
    while not path.exists():
        path = path.parent
    return path


def main() -> None:
    store = offload.base_directory("wan")
    stores = sorted(p.name for p in store.iterdir() if p.is_dir()) if store.is_dir() else []
    try:
        binary = ffmpeg_binary()
        version = subprocess.run(  # noqa: S603 -- ffmpeg, fixed arguments
            [binary, "-hide_banner", "-version"], capture_output=True, text=True, check=False
        ).stdout.split("\n")[0]
        encoders = subprocess.run(  # noqa: S603 -- ffmpeg, fixed arguments
            [binary, "-hide_banner", "-encoders"], capture_output=True, text=True, check=False
        ).stdout
        ffmpeg: dict[str, object] | None = {
            "path": binary,
            "version": version,
            # Without it, every clip fails only after its generation.
            "libx264": " libx264 " in encoders,
        }
    except VideoEncodingError:
        ffmpeg = None
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "ffmpeg": ffmpeg,
        "files_missing": [
            name
            for name in CHECK
            if not isinstance(try_to_load_from_cache(REPO, name, revision=REVISION), str)
        ],
        "transformer_q8_cached": isinstance(
            try_to_load_from_cache(GGUF_REPO, GGUF_FILE, revision=GGUF_REVISION), str
        ),
        "offload_stores": {
            name: "complete" if (store / name / "complete").is_file() else "incomplete"
            for name in stores
        },
        "free_disk_gb": round(shutil.disk_usage(_existing(store)).free / 1e9),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
