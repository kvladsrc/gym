"""Use upstream learned modules, omitting unused SMPL mesh conversions.

The video head consumes image features; the auxiliary SMPL/SMPL-X mesh outputs
are not inputs to it. Licensed SMPL-X is used separately for native geometry.
Attention uses mathematically equivalent PyTorch SDPA at evaluation time.
"""

import gc
from pathlib import Path
from typing import Any

import pytorch_lightning as pl
import torch
import torch.nn.functional as F


def load_image_model(source: Path) -> Any:
    import open_clip
    import prompt_hmr.models as models
    from prompt_hmr.core.config import parse_args
    from prompt_hmr.models.dinov2.layers import attention
    from prompt_hmr.models.phmr import PHMR
    from prompt_hmr.utils.rotation_utils import rotation_6d_to_matrix

    class NativePHMR(PHMR):
        def __init__(
            self,
            cfg: Any,
            cam_encoder: Any,
            image_encoder: Any,
            prompt_encoder: Any,
            smpl_decoder: Any,
            **kwargs: Any,
        ) -> None:
            pl.LightningModule.__init__(self)
            self.cfg = cfg
            self.img_size = cfg.MODEL.IMG_SIZE
            self.transl_type = cfg.MODEL.TRANSL
            self.cam_encoder = cam_encoder
            self.image_encoder = image_encoder
            self.prompt_encoder = prompt_encoder
            self.smpl_decoder = smpl_decoder
            self.is_train = False

        def process_output(self, output: dict[str, Any], use_mean_hands: bool) -> dict[str, Any]:
            # Preserve network features and predictions; no auxiliary mesh conversion.
            output["rotmat"] = rotation_6d_to_matrix(output["pose"].reshape(-1, 22, 6))
            return output

    def sdpa(self: Any, x: torch.Tensor) -> torch.Tensor:
        batch, length, channels = x.shape
        q, k, v = (
            self.qkv(x)
            .reshape(batch, length, 3, self.num_heads, channels // self.num_heads)
            .permute(2, 0, 3, 1, 4)
            .unbind(0)
        )
        y = F.scaled_dot_product_attention(q, k, v, dropout_p=0)
        return self.proj_drop(self.proj(y.transpose(1, 2).reshape(batch, length, channels)))

    attention.Attention.forward = sdpa
    original_model, original_clip = models.PHMR, open_clip.create_model

    def clip_architecture(*args: Any, **kwargs: Any) -> Any:
        # The official PHMR state_dict contains these text encoder parameters.
        kwargs["pretrained"] = None
        return original_clip(*args, **kwargs)

    try:
        models.PHMR = NativePHMR
        open_clip.create_model = clip_architecture
        cfg = parse_args(["--cfg", str(source / "data/pretrain/phmr/config.yaml")])
        model = models.build_phmr(cfg)
    finally:
        models.PHMR = original_model
        open_clip.create_model = original_clip
    state = torch.load(
        source / "data/pretrain/phmr/checkpoint.ckpt", map_location="cpu", weights_only=True
    )["state_dict"]
    wanted = model.state_dict()
    unexpected = [
        k for k in state if k not in wanted and not k.startswith(("smpl.", "smplx.", "smplx2smpl"))
    ]
    if unexpected:
        raise RuntimeError(f"Unrecognized omitted checkpoint parameters: {unexpected[:10]}")
    model.load_state_dict({k: v for k, v in state.items() if k in wanted}, strict=True)
    del state
    gc.collect()
    return model.eval().cuda()
