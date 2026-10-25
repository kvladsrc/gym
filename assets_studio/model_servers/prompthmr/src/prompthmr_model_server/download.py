"""Fetch pinned public checkpoints; reuse the user's licensed SMPL-X model."""

import hashlib
import os
from pathlib import Path

import gdown

from prompthmr_model_server.server import source_directory

# Official folders linked by scripts/fetch_data.sh and the BEDLAM2 README.
FILES = (
    (
        "1uQwMCkkqtyQIwuBeWwE83XcOlISKbTHB",  # pragma: allowlist secret
        "phmr/checkpoint.ckpt",
        # pragma: allowlist nextline secret
        "a3ef04ef8a12c3068682b03c62c95f8959cd8554424e105c63bf97f6c8e97e99",
    ),
    (
        "1P3EEmBDeRRORhBkUhwPZclZxd7U3Z-yo",  # pragma: allowlist secret
        "phmr/config.yaml",
        # pragma: allowlist nextline secret
        "31142aaac12a7ef60adba77c85addffad1ea466a6555717b24c3fbed079a2447",
    ),
    (
        "1a0IqV5IBA5L0H9BYbLRVLm3KwQDPXFO0",  # pragma: allowlist secret
        "phmr_vid/phmr_b1b2.ckpt",
        # pragma: allowlist nextline secret
        "2a36132715b5db0ea2acb6f1f92bbf963c9cf0fb1c3aea8d0f73dfede0b9e5e5",
    ),
    (
        "1t282bZ_VmTSmB4GnivJRyVhLIsvhyscx",  # pragma: allowlist secret
        "phmr_vid/prhmr_release_002.yaml",
        # pragma: allowlist nextline secret
        "58cd9c28907e31cd1757c9282e5b75ceb00a6ac54facadee03dae5a4a0725fd9",
    ),
)


def main() -> None:
    source = source_directory()
    for file_id, name, expected in FILES:
        target = source / "data/pretrain" / name
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            gdown.download(id=file_id, output=str(target), quiet=False, resume=True)
        with target.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        if digest != expected:
            raise RuntimeError(f"checkpoint checksum mismatch: {target}")
        print("verified", name)
    body = Path(
        os.environ.get(
            "SMPLX_MODEL_DIR",
            str(source.parent / "gvhmr-source/inputs/checkpoints/body_models/smplx"),
        )
    ).expanduser()
    if not (body / "SMPLX_NEUTRAL.npz").is_file():
        raise FileNotFoundError(
            "Set SMPLX_MODEL_DIR to your licensed SMPL-X neutral model directory"
        )
    link = source / "data/body_models/smplx"
    link.parent.mkdir(parents=True, exist_ok=True)
    if not link.exists():
        link.symlink_to(body, target_is_directory=True)


if __name__ == "__main__":
    main()
