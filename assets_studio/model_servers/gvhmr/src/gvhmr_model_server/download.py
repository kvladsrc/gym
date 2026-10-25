"""Download only inference checkpoints from a pinned community mirror.

The original Google Drive folder frequently exceeds its download quota.
Hashes identify the mirror's files; they do not establish upstream provenance.
The registration-gated SMPL-X body model is deliberately excluded.
"""

import hashlib
import shutil
import subprocess
from pathlib import Path

from gvhmr_model_server.server import source_directory

REPO = "camenduru/GVHMR"
REVISION = "21b32d5389e2e59c0737d4c4095bbc0b8c23f66b"  # pragma: allowlist secret
# SHA256 from the pinned repository's Git LFS pointers.
HASHES = {
    "gvhmr/gvhmr_siga24_release.ckpt": "4fae7da2de388d5da3514cb27a2d003f364dacb280e9cf88972b710e589c6b91",  # noqa: E501 -- pinned SHA256  # pragma: allowlist secret
    "hmr2/epoch=10-step=25000.ckpt": "2dcf79638109781d1ae5f5c44fee5f55bc83291c210653feead9b7f04fa6f20e",  # noqa: E501 -- pinned SHA256  # pragma: allowlist secret
    "vitpose/vitpose-h-multi-coco.pth": "50e33f4077ef2a6bcfd7110c58742b24c5859b7798fb0eedd6d2215e0a8980bc",  # noqa: E501 -- pinned SHA256  # pragma: allowlist secret
    # pragma: allowlist nextline secret
    "yolo/yolov8x.pt": "c4d5a3f000d771762f03fc8b57ebd0aae324aeaefdd6e68492a9c4470f2d1e8b",
}


def digest(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main() -> None:
    for name, expected in HASHES.items():
        target = source_directory() / "inputs/checkpoints" / name
        if target.is_file() and digest(target) == expected:
            print(f"Verified {name}", flush=True)
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        partial = target.with_name(target.name + ".part")
        url = f"https://huggingface.co/{REPO}/resolve/{REVISION}/{name}?download=true"
        print(f"Downloading {name}", flush=True)
        subprocess.run(  # noqa: S603 -- fixed curl options and pinned HTTPS host
            [
                shutil.which("curl") or "curl",
                "--fail",
                "--location",
                "--retry",
                "3",
                "--connect-timeout",
                "30",
                "--speed-limit",
                "1024",
                "--speed-time",
                "120",
                "--continue-at",
                "-",
                "--output",
                str(partial),
                url,
            ],
            check=True,
        )
        if digest(partial) != expected:
            raise ValueError(f"SHA256 mismatch for {name}; remove {partial} and retry")
        partial.replace(target)
        print(f"Verified {name}", flush=True)


if __name__ == "__main__":
    main()
