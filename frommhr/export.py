"""Pack a reader checkpoint (train.py) as one safetensors package (package.py) for the Assets Studio
server frommhr and predict.py: its EMA weights and everything else they need, so neither holds
training code or MHR's files:

- the constants the network was trained with (size, outputs, slider ranges, eye strip);
- MHR's identity on SOMA's mesh as prep.py made it: the mean, the body's 20 directions, the
  head's 20 (smoothed), SOMA's triangles, and the bone scales that move vertices (their rest-pose
  directions, from MHR's model through SOMA-X; linear within 0.6 mm over +-1.5);
- for the server's fit of the body to Anny's: the mean and the body and scale directions in
  SOMA's bind pose, the vertices the fit uses (not the head, hands, mouth or eye bags) and the
  priors;
- the colour tables (colour.py through pigments.py), the regional colour weights per SOMA vertex
  (colour.regions on MHR's mean), Anny's feature masks in SOMA's first UV tile (prep.py) and the
  neutral look of the features the sliders do not cover.

    python export.py [--ckpt data/run/best.pt] [--data data/data.pt] [--soma-x data/soma-x]
                     [--out data/frommhr.safetensors]

About a minute on the CPU, about 3 GB of RAM (MHR's model)."""

import argparse
import datetime as dt
import json
from pathlib import Path

import numpy as np
import torch
from soma.body.soma import SOMALayer

import colour as C
import package as pack
import pigments as P
from heads import IRIS
from mhr_heads import MhrHeads
from train import BACKBONE, EYE_STRIP, FORMAT, SIZE

DATA = Path(__file__).resolve().parent / "data"
SCALE_STEP = 0.5
PRIOR_BODY = PRIOR_SCALES = 0.01  # m^2 per unit^2 (about 1 cm rms on Anny's adults)
HAND_JOINTS = ("Hand", "Thumb", "Index", "Middle", "Ring", "Pinky")


def bind_directions(layer, mean, directions):
    """Central differences of SOMA's bind pose along each rest-pose direction (unit steps)."""
    model = layer.identity_model

    def bind(rest):
        model.forward = lambda *a, **k: rest[None]
        try:
            layer.prepare_identity(torch.zeros(1, model.num_identity_coeffs))
        finally:
            del model.forward
        return layer._cached_rest_shape[0].clone()

    return bind(mean), torch.stack([(bind(mean + d) - bind(mean - d)) / 2 for d in directions])


def scale_directions(soma_x):
    """The rest-pose directions of MHR's 68 bone scales that move vertices, and their indices."""
    mhr = SOMALayer(data_root=soma_x, identity_model_type="mhr", device="cpu")
    sc = torch.zeros(136, 68)
    sc[torch.arange(68), torch.arange(68)] = SCALE_STEP
    sc[68 + torch.arange(68), torch.arange(68)] = -SCALE_STEP
    rests = torch.cat(
        [
            mhr.identity_model(torch.zeros(17, 45), sc[i : i + 17], kwargs=None, global_scale=1.0)
            for i in range(0, 136, 17)
        ]
    )
    scales = (rests[:68] - rests[68:]) / (2 * SCALE_STEP)
    active = [k for k in range(68) if float(scales[k].norm(dim=-1).max()) > 1e-4]
    return scales[active], active


def main():
    ap = argparse.ArgumentParser(description="Pack a frommhr checkpoint for the server")
    ap.add_argument("--ckpt", type=Path, default=DATA / "run" / "best.pt")
    ap.add_argument("--data", type=Path, default=DATA / "data.pt")
    ap.add_argument("--soma-x", type=Path, default=DATA / "soma-x")
    ap.add_argument("--out", type=Path, default=DATA / "frommhr.safetensors")
    ap.add_argument(
        "--init", default=None, help="how the run started, for the package (default: the checkpoint's note)"
    )
    args = ap.parse_args()
    torch.set_grad_enabled(False)
    torch.set_num_threads(4)
    saved = torch.load(args.ckpt, map_location="cpu", weights_only=False)
    state = saved["state"]
    assert state.get("format") == FORMAT, "a train.py checkpoint"
    assert [tuple(s) for s in state["sliders"]] == list(C.SLIDERS), "slider ranges differ from colour.py's"
    data = torch.load(args.data, map_location="cpu", weights_only=False)

    heads = MhrHeads(data)
    looks = C.ColourLooks(heads)
    V = data["mean"].shape[0]
    regions = {k: v[:V].float() for k, v in C.regions(heads).items()}
    scales, active = scale_directions(args.soma_x)

    # the bind pose's directions and the vertices of the body's fit
    layer = SOMALayer(data_root=args.soma_x, identity_model_type="anny", device="cpu")
    mean, body = data["mean"].float(), data["body"].float()
    mean_bind, body_bind = bind_directions(layer, mean, body)
    _, scale_bind = bind_directions(layer, mean, scales)
    view = layer.public_rig_view()
    names = list(view.joint_names)
    weights = view.skinning_weights
    weights = (weights.to_dense() if weights.is_sparse else weights).reshape(V, -1)
    hand = [i for i, n in enumerate(names) if any(k in n for k in HAND_JOINTS)]
    npz = np.load(args.soma_x / "SOMA_neutral.npz")
    use = torch.ones(V, dtype=torch.bool)
    use[data["head_vertices"].long()] = False
    use[weights[:, hand].sum(1) > 0.2] = False
    for seg in ("mouth_bag", "eye_bags"):
        use[torch.from_numpy(npz[f"segment_{seg}"].astype(np.int64))] = False

    package = {
        "format": FORMAT,
        "weights": {k: v.float() if v.is_floating_point() else v for k, v in saved["ema"].items()},
        "config": {
            "backbone": BACKBONE,
            "size": int(state.get("size", SIZE)),
            "known": False,
            "phenotype": [],
            "origin": [],
            "free": [],
            "age_bins": [],
            "coefficients": 20,
            "eye_strip": list(EYE_STRIP),
            "eye_width": 128,
            "head": [1024, 1024],
            "sliders": [list(s) for s in C.SLIDERS],
        },
        "training": {
            "source": args.ckpt.name,
            "run": args.ckpt.parent.name,
            "step": state.get("step"),
            "validation": saved.get("val"),
            "init": args.init if args.init is not None else state.get("init", ""),
            "exported": dt.datetime.now().isoformat(timespec="seconds"),
        },
        "model": {
            "mean": mean,
            "body": body,
            "head": data["head"].float(),
            "scales": scales.float(),
            "scale_index": active,
            "tris": data["tris"].int(),  # SOMA's triangles (quads split), for a mesh of the head
        },
        "fit": {
            "mean_bind": mean_bind,
            "body_bind": body_bind,
            "scale_bind": scale_bind,
            "use": use,
            "prior_body": PRIOR_BODY,
            "prior_scales": PRIOR_SCALES,
        },
        "regions": regions,
        "masks": data["masks"],  # (6, 2048, 2048) uint8 in SOMA's first UV tile, row 0 at v = 1
        "mask_names": list(data["mask_names"]),
        "colour": {
            "skin_table": looks.skin_table.cpu(),
            "lips_table": looks.lips_table.cpu(),
            "iris_table": looks.iris_table.cpu(),
            "axes": {
                "melanin": list(C.MEL),
                "eu": list(C.EU),
                "blood": list(C.BLOOD),
                "iris_eu": list(C.IRIS_EU),
                "iris_pheo": list(C.IRIS_PHEO),
            },
            "hair_eu": list(map(float, P.HAIR_EU)),
            "hair_pheo": list(map(float, P.HAIR_PHEO)),
            "hair_chiang": float(P._CHIANG),
        },
        "neutral": {
            "brows": "brows_thin",
            "brow_density": 0.9,
            "limbus": 0.5,
            "pupil_rad": 0.19,
            "iris_rad": float(IRIS),
            "sclera": [0.62, 0.56, 0.52],
            "lid_melanin": 1.15,
            "lid_blood": 1.3,
            "ear_blood": 1.6,
        },
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    pack.save(args.out, package)
    print(json.dumps({k: package[k] for k in ("format", "config", "training")}, indent=1, default=str))
    print(
        f"scales kept {len(active)}, fit vertices {int(use.sum())} of {V}; {args.out}: {args.out.stat().st_size / 2**20:.1f} MiB"
    )


if __name__ == "__main__":
    main()
