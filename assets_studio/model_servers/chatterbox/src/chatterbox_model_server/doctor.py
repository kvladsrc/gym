"""Report PyTorch, the CUDA device, the weights and the Russian stress marks.

python -m chatterbox_model_server.doctor [--download]
"""

import argparse
import json
from pathlib import Path

from chatterbox_model_server.server import (
    cached,
    dictionary_dir,
    dictionary_ready,
    download_dictionary,
    make_stresser,
    weights,
)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--download", action="store_true", help="fetch what is missing")
    arguments = parser.parse_args()
    if arguments.download:
        snapshot = weights()
        print("weights", snapshot, flush=True)
        print("dictionary", download_dictionary(dictionary_dir()), flush=True)
        # Upstream's tokenizer fetches the Chinese files on first use: the Cangjie
        # table into the snapshot and the pkuseg segmenter (34 MB) into ~/.pkuseg.
        from chatterbox.models.tokenizers import MTLTokenizer

        MTLTokenizer(str(Path(snapshot) / "grapheme_mtl_merged_expanded_v1.json"))
        print("chinese segmenter", Path.home() / ".pkuseg", flush=True)
        return

    import torch

    report: dict[str, object] = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "device": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "weights": cached(),
        "dictionary": str(dictionary_dir()) if dictionary_ready(dictionary_dir()) else None,
    }
    if report["dictionary"]:
        stresser = make_stresser(dictionary_dir())
        report["stress_example"] = stresser.stress_text("Замок на двери старого замка.")
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
