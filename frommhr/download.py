"""Download SOMA-X's files (NVIDIA, Apache-2.0) into data/soma-x: SOMA's mesh, rig and the Anny and
MHR identity models, at the release py-soma-x 0.3.3 reads (about 820 MB, most of it MHR's model).

    python download.py [--out data/soma-x]

DINOv2's weights come through timm on first use; Anny's data ships with its package."""

import argparse
from pathlib import Path

from huggingface_hub import hf_hub_download

REPO, REVISION = "nvidia/soma-x", "v0.3.3"
FILES = (
    "Anny/SOMA_wrap.obj",
    "Anny/base_body.obj",
    "LICENSE",
    "MHR/SOMA_wrap_lod1.obj",
    "MHR/base_body_lod1.obj",
    "MHR/mhr_model_lod1.pt",
    "README.md",
    "SOMA_neutral.npz",
    "SOMA_procedural_transforms.json",
    "SOMA_template_rig.usda",
    "correctives_model.pt",
)
SOMA_X = Path(__file__).resolve().parent / "data" / "soma-x"


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--out", type=Path, default=SOMA_X)
    args = ap.parse_args()
    for name in FILES:
        if not (args.out / name).is_file():
            hf_hub_download(REPO, name, revision=REVISION, local_dir=args.out)
        print(args.out / name)


if __name__ == "__main__":
    main()
