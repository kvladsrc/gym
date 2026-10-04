"""Print the runtime environment, the source and whether the weights,
the skeleton template and the VAE are downloaded."""

import importlib.util
import json

import torch
from huggingface_hub import get_token

from mia_model_server.server import VAE_REPO, models_directory, source_directory


def main() -> None:
    source = source_directory()
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "bpy": importlib.util.find_spec("bpy") is not None,
        "pytorch3d": importlib.util.find_spec("pytorch3d") is not None,
        "source": (source / "app_v2.py").is_file(),
        "vae_submodule": (source / "util/Hunyuan3D_21/hy3dshape").is_dir(),
        # The template is in a gated dataset: the token's account must accept it.
        "hf_token_found": get_token() is not None,
        "weights": (source / "output/best/v2/bw_joints.pth").is_file(),
        "template": (source / "data/Mixamo/bones.fbx").is_file(),
        "vae": (models_directory() / VAE_REPO / "hunyuan3d-vae-v2-1/model.fp16.ckpt").is_file(),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
