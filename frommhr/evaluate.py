"""Evaluate a reader on held-out random MHR people (seed 999, not the training's validation set).

The head's skin error in mm (centred, scaled to an adult's head) with the true body, against MHR's
mean head on the true body (c = 0) and against one average head for everybody; R^2 per head
coefficient; the colour differences (CIE76) per feature; eval_people.png: input | prediction |
truth.

    python evaluate.py [--weights data/run/best.pt | data/frommhr.safetensors] [--data data/data.pt]
                       [--out data/eval] [--count 256] [--device cuda]"""

import argparse
import json
from pathlib import Path

import torch

import colour as C
import train as T
from mhr_heads import MhrHeads

DATA = Path(__file__).resolve().parent / "data"


def load(path, dev):
    """(model, what it was trained as) from a train.py checkpoint or an exported package."""
    path = Path(path)
    if path.suffix == ".safetensors":
        import package

        p = package.load(path)
        sd, size, info = p["weights"], p["config"]["size"], p["training"]
    else:
        saved = torch.load(path, map_location="cpu", weights_only=False)
        sd, size, info = (
            saved["ema"],
            saved["state"].get("size", T.SIZE),
            {"step": saved["state"]["step"], "validation": saved.get("val")},
        )
    model = T.Reader(pretrained=False, size=size).to(dev).eval()
    model.load_state_dict(sd)
    return model, info


@torch.no_grad()
def held_out(model, gen, count, dev, out):
    val = T.make_val(gen, n=count, seed=999)
    x, target, ok, _ = val
    cs, us, dist, base = [], [], [], []
    for i in range(0, x.shape[0], 32):
        t = {k: v[i : i + 32] for k, v in target.items()}
        with T.autocast(dev):
            c, u = T.split(model(x[i : i + 32].to(dev).float()).float())
        pred = gen.normalised(gen.shape(t["b"], c))
        mean_head = gen.normalised(gen.shape(t["b"], torch.zeros_like(c)))
        dist.append((pred - t["shape"]).norm(dim=-1).mean(1))
        base.append((mean_head - t["shape"]).norm(dim=-1).mean(1))
        cs.append(c), us.append(u)
    c, u, d, b0 = torch.cat(cs), torch.cat(us), torch.cat(dist), torch.cat(base)
    shapes = target["shape"][ok]
    one = (shapes - shapes.mean(0)).norm(dim=-1).mean(1)
    tc = target["c"]
    r2 = 1 - ((c - tc) ** 2).sum(0) / ((tc - tc.mean(0)) ** 2).sum(0)

    def stats(v):
        return {
            "mean": round(float(v.mean()), 3),
            "median": round(float(v.median()), 3),
            "p90": round(float(v.quantile(0.9)), 3),
        }

    de = gen.colour.delta_e(u, target["colour"])
    tv, pv = C.values(target["colour"]), C.values(u)
    report = {
        "people": int(ok.sum()),
        "shape_mm": {"model": stats(d[ok]), "mhr_mean_head_true_body": stats(b0[ok]), "one_average_head": stats(one)},
        "head_r2_per_component": [round(float(v), 2) for v in r2],
        "colour": {
            "dE76_mean": {k: round(float(v.mean()), 2) for k, v in de.items()},
            "dE76_p90": {k: round(float(v.quantile(0.9)), 2) for k, v in de.items()},
            "melanin_log_error_median": round(float((pv["melanin"] / tv["melanin"]).log().abs().median()), 3),
            "blood_log_error_median": round(float((pv["blood"] / tv["blood"]).log().abs().median()), 3),
            "stubble_error_mean": round(float((pv["stubble"] - tv["stubble"]).abs().mean()), 3),
        },
    }
    T.preview(model, gen, val, out / "eval_people.png", count=min(8, count))
    return report


def main():
    ap = argparse.ArgumentParser(description="Evaluate frommhr's reader on held-out MHR people")
    ap.add_argument("--weights", type=Path, default=DATA / "run" / "best.pt")
    ap.add_argument("--data", type=Path, default=DATA / "data.pt")
    ap.add_argument("--out", type=Path, default=DATA / "eval")
    ap.add_argument("--count", type=int, default=256, help="people (a multiple of 32)")
    ap.add_argument("--device", default="cuda" if torch.cuda.is_available() else "cpu")
    args = ap.parse_args()
    dev = torch.device(args.device)
    args.out.mkdir(parents=True, exist_ok=True)
    heads = MhrHeads(torch.load(args.data, map_location="cpu", weights_only=False), device=dev)
    model, info = load(args.weights, dev)
    gen = T.Generator(heads, C.ColourLooks(heads), size=model.size)
    report = {
        "weights": str(args.weights.name),
        "trained_steps": info.get("step"),
        "training_val": info.get("validation"),
    }
    report["held_out"] = held_out(model, gen, args.count, dev, args.out)
    (args.out / "eval.json").write_text(json.dumps(report, indent=1))
    print(json.dumps(report["held_out"], indent=1))


if __name__ == "__main__":
    main()
