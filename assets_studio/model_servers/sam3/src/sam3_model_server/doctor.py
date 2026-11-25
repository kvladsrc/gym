"""Report PyTorch, the CUDA device, ffmpeg and the checkpoint.

python -m sam3_model_server.doctor [--download]
"""

import argparse
import shutil

from sam3_model_server.server import cached, weights


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--download", action="store_true", help="fetch the checkpoint")
    if parser.parse_args().download:
        print("checkpoint", weights(), flush=True)
        return

    import torch

    print("PyTorch:", torch.__version__)
    available = torch.cuda.is_available()
    print("CUDA:", torch.cuda.get_device_name(0) if available else "unavailable")
    print("ffmpeg:", shutil.which("ffmpeg") or "missing")
    print("Checkpoint:", cached() or "not downloaded")


if __name__ == "__main__":
    main()
