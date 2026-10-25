"""One rigging job, in its own process, in the SkinTokens source directory
(the server runs it; upstream's demo.py run_rig without the Gradio demo and
without its bpy helper server: bpy is used here, on the main thread).

    python -m skintokens_model_server.rig input.glb output.fbx PARAMS_JSON SEED

The last line of output is ``RESULT {json}``: the rig's description.
"""

import json
import os
import random
import sys
from pathlib import Path

from asset_processing.blender import exit_now

from skintokens_model_server.server import CHECKPOINT, RESULT_PREFIX, Params


def seed_everything(seed: int) -> None:
    import numpy as np
    import torch

    random.seed(seed)
    np.random.seed(seed % 2**32)
    torch.manual_seed(seed)


def rig(source: Path, output: Path, params: Params, seed: int) -> dict[str, object]:
    import torch

    os.environ["XFORMERS_IGNORE_FLASH_VERSION_CHECK"] = "1"
    sys.path.insert(0, os.getcwd())
    from asset_processing.blender import glb_to_fbx
    from src.data.dataset import (  # pyright: ignore[reportMissingImports]
        DatasetConfig,
        RigDatasetModule,
    )
    from src.data.transform import Transform  # pyright: ignore[reportMissingImports]
    from src.data.vertex_group import voxel_skin  # pyright: ignore[reportMissingImports]
    from src.rig_package.parser.bpy import (  # pyright: ignore[reportMissingImports]
        transfer_rigging,
    )
    from src.server.spec import get_model  # pyright: ignore[reportMissingImports]
    from src.tokenizer.parse import get_tokenizer  # pyright: ignore[reportMissingImports]

    torch.set_grad_enabled(False)
    seed_everything(seed)
    model = get_model(CHECKPOINT)
    tokenizer = get_tokenizer(**model.tokenizer_config)
    transform = Transform.parse(**model.transform_config["predict_transform"])
    datapath = {"data_name": None, "loader": "bpy", "filepaths": {"articulation": [str(source)]}}
    config = DatasetConfig.parse(
        shuffle=False,
        batch_size=1,
        num_workers=0,  # bpy in this process, on its main thread
        pin_memory=True,
        persistent_workers=False,
        datapath=datapath,
    ).split_by_cls()
    module = RigDatasetModule(
        predict_dataset_config=config,
        predict_transform=transform,
        tokenizer=tokenizer,
        process_fn=model._process_fn,
    )
    batch = next(iter(module.predict_dataloader()["articulation"]))
    batch = {k: v.to("cuda") if isinstance(v, torch.Tensor) else v for k, v in batch.items()}
    # Predict the skeleton too (upstream's use_skeleton=False).
    batch.pop("skeleton_tokens", None)
    batch.pop("skeleton_mask", None)
    batch["generate_kwargs"] = {
        "max_length": 2048,
        "top_k": 5,
        "top_p": 0.95,
        "temperature": params.temperature,
        "repetition_penalty": 2.0,
        "num_return_sequences": 1,
        "num_beams": params.num_beams,
        "do_sample": True,
    }
    asset = model.predict_step(batch, skeleton_tokens=None, make_asset=True)["results"][0].asset
    if asset is None:
        raise RuntimeError("no rig predicted")
    if params.smooth_skin:
        voxel = asset.voxel(resolution=196)
        asset.skin *= voxel_skin(
            grid=0,
            grid_coords=voxel.coords,
            joints=asset.joints,
            vertices=asset.vertices,
            faces=asset.faces,
            mode="square",
            voxel_size=voxel.voxel_size,
        )
        asset.normalize_skin()
    peak_gb = torch.cuda.max_memory_allocated() / 2**30
    # Onto the input itself: its texture and scale are kept (use_transfer).
    rigged = output.with_suffix(".glb")
    # SkinTokens predicts joints, not bone directions: its bones' frames are
    # the identity, so tails derived from them and the lengths all point one
    # way, out of the body, and their axes are useless to animate. Without the
    # lengths upstream's exporter points each bone at its only child and
    # continues end bones along their parent.
    asset.lengths = None
    transfer_rigging(
        source_asset=asset, target_path=str(source), export_path=str(rigged), group_per_vertex=4
    )
    counts = glb_to_fbx(rigged, output)
    joints = 0 if asset.joints is None else len(asset.joints)
    return {**counts, "joints": joints, "peak_vram_gb": round(peak_gb, 2)}


def main() -> None:
    source, output, params, seed = sys.argv[1:5]
    result = rig(Path(source), Path(output), Params.model_validate_json(params), int(seed))
    print(RESULT_PREFIX + json.dumps(result), flush=True)
    exit_now()  # bpy's teardown would crash


if __name__ == "__main__":
    main()
