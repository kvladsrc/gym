"""Train frommhr's reader: front + left-profile colour pictures of random MHR people on SOMA's mesh
(mhr_heads.py with prep.py's data), rendered on the fly at DINOv2's own 518 px -> MHR's 20 head
coefficients + the 14 pigment sliders of colour.py.

MHR has no age or sex and was built from adults only, so the reader has no other inputs: the
body's coefficients (0-19, N(0, 1) clipped at 2.5) vary in training, and the loss rebuilds the
head with the true body, so the reader reads the head alone.

Each view's head is turned a little (up to 6 degrees sideways, 5 up or down, 3 tilted), then each
picture is shifted, scaled, turned in its plane, sometimes blurred or halved in resolution,
gamma-shifted and noised. A DINOv2 ViT-S/14 sees both views; a small CNN (EyeReader) reads the
front picture's eye strip from its pixels, since the transformer's features held no iris colour;
an MLP reads [CLS, mean patch token] of each view and the CNN's features. The loss: the head's
skin rebuilt from the predicted coefficients against the truth, both centred and scaled to an
adult's head size (each picture is framed to its own head), mean distance in mm, + 0.5 x the
coefficients' squared error + 5 x the sliders' squared error + 0.02 x their colours' CIE76
difference (skin, cheeks, nose, under the eyes, lips, brows, iris at display levels). The EMA
weights are validated every 15 minutes on 256 fixed people; best.pt goes by val_mm + 0.02 dE.

    python train.py [--data data/data.pt] [--run data/run] [--hours 7] [--batch 32]
                    [--init CKPT_OR_PACKAGE] [--size 518] [--checkpointing] [--device cuda]

The learning rate warms up over 500 steps, then follows a cosine to the end by the wall clock
(--hours from the start, or --deadline HH:MM), so a run stopped and resumed still ends annealed.
Resumes from RUN/last.pt. On CUDA, pauses between steps keep the GPU at --max-temp or cooler.
--init starts from another run's weights (a train.py checkpoint or an exported package) instead
of DINOv2's; --steps N stops after N steps (a smoke test)."""

import argparse
import datetime as dt
import json
import math
import signal
import subprocess
import time
from pathlib import Path

import timm
import torch
import torch.nn.functional as F
from torch import nn

import colour as C
from heads import ADULT_HEAD, SKIN, Looks
from mhr_heads import NB, NH, MhrHeads

DATA = Path(__file__).resolve().parent / "data"
SIZE = 518
BACKBONE = "vit_small_patch14_dinov2.lvd142m"
FORMAT = "frommhr.reader.v1"
DE_WEIGHT = 0.02  # one unit of colour difference counts as 0.02 mm of shape
INSIDE_LIMIT = 0.00002  # pictures whose mouth tore open (heads.render's "inside") are left out of the shape loss
# the front picture's eye strip, fractions of its size: the framing puts the eyes at 0.4 of the
# height and the head's middle across; the turns, shifts and scaling move them by up to ~15 px
EYE_STRIP = (0.4 - 24 / 224, 0.4 + 24 / 224, 0.5 - 64 / 224, 0.5 + 64 / 224)  # rows, columns


def _rot(axis, a):
    c, s = torch.cos(a), torch.sin(a)
    z, o = torch.zeros_like(a), torch.ones_like(a)
    m = {"x": [o, z, z, z, c, -s, z, s, c], "y": [c, z, s, z, o, z, -s, z, c], "z": [c, -s, z, s, c, z, z, z, o]}[axis]
    return torch.stack(m, -1).view(-1, 3, 3)


class Generator:
    """Random MHR people (body and head coefficients ~ N(0, 1), clipped) and their pictures."""

    def __init__(self, heads, colour, clip=2.5, turn=(6.0, 5.0, 3.0), size=SIZE):
        self.heads, self.colour, self.clip, self.turn = heads, colour, clip, turn
        self.size = size  # rendered in parts of the memory of 32 people at 224 px
        self.part = max(1, round(32 * (224 / size) ** 2))
        dev = heads.device
        skin = torch.zeros(heads.template.shape[0], dtype=torch.bool, device=dev)
        skin[heads.tris[heads.material == SKIN].flatten()] = True
        head = torch.zeros_like(skin)
        head[heads.head_vertices] = True
        self.idx = (skin & head).nonzero()[:, 0]  # the head's skin

    def shape(self, b, c):
        """(B, V + eyeballs, 3), differentiable in c."""
        return self.heads.person(b, c)

    def normalised(self, shape):
        """The head's skin (B, Vh, 3), centred and scaled to an adult's head size, mm."""
        h = self.heads.head_size(shape)
        v = shape[:, self.idx]
        return (v - v.mean(1, keepdim=True)) * (ADULT_HEAD * 1000 / h)[:, None, None]

    def turned(self, verts):
        B, dev = verts.shape[0], verts.device
        yaw, pitch, roll = (torch.deg2rad((torch.rand(B, device=dev) * 2 - 1) * a) for a in self.turn)
        R = _rot("z", yaw) @ _rot("x", pitch) @ _rot("y", roll)
        centre = verts[:, self.heads.head_vertices].mean(1, keepdim=True)
        return (verts - centre) @ R.transpose(1, 2) + centre

    @torch.no_grad()
    def pictures(self, verts, look, turn=True):
        """(B, 2, 3, S, S) front and left pictures in [0, 1] and the torn-mouth share."""
        B, n = verts.shape[0], self.part
        if B > n:  # large pictures: a few people at a time (every look entry is per person)
            parts = [
                self.pictures(verts[a : a + n], {k: x[a : a + n] for k, x in look.items()}, turn)
                for a in range(0, B, n)
            ]
            return torch.cat([p[0] for p in parts]), torch.cat([p[1] for p in parts])
        pics, inside = [], torch.zeros(B, device=verts.device)
        for vi, view in enumerate(("front", "left")):
            v = self.turned(verts) if turn else verts
            lk = {k: x[:, vi : vi + 1] if x.dim() >= 2 and x.shape[1] == 2 else x for k, x in look.items()}
            out = self.heads.render(v, lk, views=(view,), size=self.size, supersample=2, colour=True)
            pics.append(out[view])
            inside = torch.maximum(inside, out["inside"])
        return torch.stack(pics, 1), inside

    @torch.no_grad()
    def batch(self, B):
        dev = self.heads.device
        b = torch.randn(B, NB, device=dev).clamp(-self.clip, self.clip)
        c = torch.randn(B, NH, device=dev).clamp(-self.clip, self.clip)
        verts = self.heads.person(b, c)
        look = Looks()(B, device=dev)
        # stubble on half of a random half (MHR has no sex to tie it to); the age only gates it
        look, sliders = self.colour(look, (torch.rand(B) < 0.5).float(), torch.full((B,), 40.0))
        x, inside = self.pictures(verts, look)
        target = {"b": b, "c": c, "shape": self.normalised(verts), "colour": sliders}
        return augment(x, gamma=0.12), target, inside <= INSIDE_LIMIT, look


def gaussian_blur(z, sigma):
    """z (n, C, H, W), sigma (n,) pixels, per picture."""
    n, ch, r = z.shape[0], z.shape[1], 3
    xs = torch.arange(-r, r + 1, device=z.device, dtype=z.dtype)
    k = torch.exp(-0.5 * (xs[None] / sigma.clamp(min=1e-3)[:, None]) ** 2)
    k = (k / k.sum(1, keepdim=True)).repeat_interleave(ch, 0)
    y = z.reshape(1, n * ch, *z.shape[-2:])
    y = F.conv2d(F.pad(y, (r, r, 0, 0), mode="replicate"), k.view(n * ch, 1, 1, -1), groups=n * ch)
    y = F.conv2d(F.pad(y, (0, 0, r, r), mode="replicate"), k.view(n * ch, 1, -1, 1), groups=n * ch)
    return y.view_as(z)


def augment(x, gamma=0.3):
    """x (B, 2, 3, S, S) in [0, 1]: per picture a small shift, scale and turn in the image plane,
    a blur on half, half resolution on a fifth, a gamma (2^+-gamma) and a little noise."""
    shape = x.shape
    B, V, S = shape[0], shape[1], shape[-1]
    ch = shape[2] if x.dim() == 5 else 1
    n, dev = B * V, x.device
    z = x.reshape(n, ch, S, S)
    s = 1 + (torch.rand(n, device=dev) * 2 - 1) * 0.07
    a = torch.deg2rad((torch.rand(n, device=dev) * 2 - 1) * 3)
    t = (torch.rand(n, 2, device=dev) * 2 - 1) * 0.05
    theta = torch.stack(
        [
            torch.stack([s * torch.cos(a), -s * torch.sin(a), t[:, 0]], -1),
            torch.stack([s * torch.sin(a), s * torch.cos(a), t[:, 1]], -1),
        ],
        1,
    )
    grid = F.affine_grid(theta, (n, ch, S, S), align_corners=False)
    z = F.grid_sample(z, grid, mode="bilinear", padding_mode="border", align_corners=False)
    z = gaussian_blur(z, torch.rand(n, device=dev) * 1.2 * (torch.rand(n, device=dev) < 0.5))
    low = torch.rand(n, device=dev) < 0.2
    if low.any():
        z[low] = F.interpolate(
            F.interpolate(z[low], scale_factor=0.5, mode="area"), size=(S, S), mode="bilinear", align_corners=False
        )
    z = z.clamp(1e-4, 1) ** (2 ** ((torch.rand(n, 1, 1, 1, device=dev) * 2 - 1) * gamma))
    z = z + torch.randn_like(z) * torch.rand(n, 1, 1, 1, device=dev) * 0.02
    return z.clamp(0, 1).view(shape)


class EyeReader(nn.Module):
    """The eye strip straight from its pixels: four 3x3 convolutions (one stride 2, an 11 px view;
    an iris is ~10 px at 224), then each feature's mean and maximum over the strip. (B, 3, h, w)
    normalised -> (B, 2 * width)."""

    def __init__(self, width=128):
        super().__init__()
        self.net = nn.Sequential(
            nn.Conv2d(3, 32, 3, padding=1),
            nn.GELU(),
            nn.Conv2d(32, 64, 3, padding=1),
            nn.GELU(),
            nn.Conv2d(64, 64, 3, stride=2, padding=1),
            nn.GELU(),
            nn.Conv2d(64, width, 3, padding=1),
            nn.GELU(),
        )
        self.dim = 2 * width

    def forward(self, z):
        h = self.net(z).flatten(2)
        return torch.cat([h.mean(-1), h.amax(-1)], 1)


def eye_strip(z):
    """(..., S, S) -> the EYE_STRIP part."""
    S = z.shape[-1]
    r0, r1, c0, c1 = (round(f * S) for f in EYE_STRIP)
    return z[..., r0:r1, c0:c1]


class Reader(nn.Module):
    """(B, 2, 3, S, S) front and left pictures in [0, 1] -> (B, 20 + 14): MHR's head coefficients
    and the sliders' logits."""

    def __init__(self, pretrained=True, size=SIZE):
        super().__init__()
        self.size = size  # DINOv2's own is 518 (37 x 37 patches)
        self.backbone = timm.create_model(BACKBONE, pretrained=pretrained, img_size=size, num_classes=0)
        d = self.backbone.num_features
        self.eyes = EyeReader()
        self.head = nn.Sequential(
            nn.Linear(4 * d + self.eyes.dim, 1024),
            nn.GELU(),
            nn.Linear(1024, 1024),
            nn.GELU(),
            nn.Linear(1024, NH + C.NS),
        )
        nn.init.zeros_(self.head[-1].weight)
        nn.init.zeros_(self.head[-1].bias)  # starts at the mean head and every slider's middle
        self.register_buffer("mean", torch.tensor([0.485, 0.456, 0.406]).view(1, 3, 1, 1))
        self.register_buffer("std", torch.tensor([0.229, 0.224, 0.225]).view(1, 3, 1, 1))

    def forward(self, x):
        B, V, S = x.shape[0], x.shape[1], x.shape[-1]
        z = (x.reshape(B * V, 3, S, S) - self.mean) / self.std
        t = self.backbone.forward_features(z)
        p = self.backbone.num_prefix_tokens
        f = torch.cat([t[:, 0], t[:, p:].mean(1)], -1).reshape(B, -1)
        f = torch.cat([f, self.eyes(eye_strip(z.view(B, V, 3, S, S)[:, 0])).to(f.dtype)], 1)
        return self.head(f)


def split(out):
    """(c (B, 20), sliders (B, 14) in [0, 1])."""
    out = out.float()
    return out[:, :NH], torch.sigmoid(out[:, NH : NH + C.NS])


def losses(gen, out, target, ok):
    c, u = split(out)
    pred = gen.normalised(gen.shape(target["b"], c))
    dist = (pred - target["shape"]).norm(dim=-1).mean(1)  # (B,) mm on an adult's scale
    w = ok.float()
    norm = w.sum().clamp(min=1)
    l_shape = (dist * w).sum() / norm
    l_c = ((c - target["c"]) ** 2).mean(1)
    l_par = (l_c * 0.5 * w).sum() / norm
    l_u = ((u - target["colour"]) ** 2).mean()  # every picture counts, torn mouths too
    l_de = torch.stack(list(gen.colour.delta_e(u, target["colour"]).values()), 1).mean()
    total = l_shape + l_par + l_u * 5 + l_de * DE_WEIGHT
    with torch.no_grad():
        parts = {
            "shape_mm": float(l_shape),
            "c": float((l_c * w).sum() / norm),
            "sliders": float(l_u),
            "dE": float(l_de),
        }
    return total, parts, dist.detach()


def autocast(dev):
    return torch.autocast(dev.type, dtype=torch.bfloat16, enabled=dev.type == "cuda")


def gpu_state():
    try:
        out = subprocess.run(
            ["nvidia-smi", "--query-gpu=temperature.gpu,power.draw", "--format=csv,noheader,nounits"],
            capture_output=True,
            text=True,
            timeout=5,
            check=False,
        ).stdout
        t, p = (float(x) for x in out.strip().split(",")[:2])
        return t, p
    except (OSError, ValueError, subprocess.SubprocessError):  # no nvidia-smi, or no GPU
        return float("nan"), float("nan")


def make_val(gen, n=256, seed=1234):
    """A fixed validation set (its pictures in half precision, in RAM above 224 px); the RNG is
    restored afterwards."""
    cuda = torch.cuda.is_available()
    cpu_rng, cuda_rng = torch.get_rng_state(), torch.cuda.get_rng_state() if cuda else None
    torch.manual_seed(seed)
    xs, targets, oks, looks = [], [], [], []
    for _ in range(n // 32):
        x, t, ok, look = gen.batch(32)
        xs.append(x.half() if gen.size <= 224 else x.half().cpu())
        targets.append(t), oks.append(ok), looks.append(look)
    torch.set_rng_state(cpu_rng)
    if cuda:
        torch.cuda.set_rng_state(cuda_rng)

    def cat(items):
        if isinstance(items[0], dict):
            return {k: cat([i[k] for i in items]) for k in items[0]}
        return torch.cat(items)

    return torch.cat(xs), cat(targets), torch.cat(oks), cat(looks)


@torch.no_grad()
def validate(model, gen, val):
    x, target, ok, _ = val
    was = model.training
    model.eval()
    dev = gen.heads.device
    dists, base, des, stubble, cerr = [], [], [], [], []
    for i in range(0, x.shape[0], 32):
        t = {k: v[i : i + 32] for k, v in target.items()}
        with autocast(dev):
            out = model(x[i : i + 32].to(dev).float())
        _, _, dist = losses(gen, out, t, ok[i : i + 32])
        dists.append(dist)
        mean_head = gen.normalised(gen.shape(t["b"], torch.zeros_like(t["c"])))
        base.append((mean_head - t["shape"]).norm(dim=-1).mean(1))
        c, u = split(out)
        cerr.append((c - t["c"]).pow(2).mean(1))
        des.append(gen.colour.delta_e(u, t["colour"]))
        stubble.append((C.values(u)["stubble"] - C.values(t["colour"])["stubble"]).abs())
    model.train(was)
    d, b0 = torch.cat(dists)[ok], torch.cat(base)[ok]
    v = {
        "val_mm": round(float(d.mean()), 3),
        "val_mm_mean_head": round(float(b0.mean()), 3),
        "val_c_rmse": round(float(torch.cat(cerr)[ok].mean().sqrt()), 3),
    }
    de = {k: torch.cat([e[k] for e in des]) for k in des[0]}
    for k, e in de.items():
        v[f"dE_{k}"] = round(float(e.mean()), 2)
    v["dE"] = round(float(torch.stack(list(de.values())).mean()), 3)
    v["stubble_err"] = round(float(torch.cat(stubble).mean()), 3)
    v["score"] = round(v["val_mm"] + DE_WEIGHT * v["dE"], 4)
    return v


@torch.no_grad()
def preview(model, gen, val, path, count=6):
    """Sheet at half size: input (front, left) | prediction | truth, rendered alike (no turn, the
    same light; the prediction in its own colours)."""
    from PIL import Image, ImageDraw

    x, target, _, look = val
    was = model.training
    model.eval()
    dev = gen.heads.device
    with autocast(dev):
        out = model(x[:count].to(dev).float())
    model.train(was)
    c, u = split(out)
    lk = {k: v[:count] for k, v in look.items()}
    b = target["b"][:count]
    pred, _ = gen.pictures(gen.shape(b, c), gen.colour.paint(lk, u), turn=False)
    true, _ = gen.pictures(gen.shape(b, target["c"][:count]), lk, turn=False)
    S = gen.size // 2
    sheet = Image.new("RGB", (6 * S + 20, count * (S + 16)), "white")
    d = ImageDraw.Draw(sheet)
    for r in range(count):
        y = r * (S + 16) + 16
        for k, img in enumerate([x[r, 0], x[r, 1], pred[r, 0], pred[r, 1], true[r, 0], true[r, 1]]):
            img = (img.float().clamp(0, 1) * 255).round().byte().cpu().permute(1, 2, 0).numpy()
            sheet.paste(Image.fromarray(img).resize((S, S), Image.LANCZOS), (k * S + (k // 2) * 10, y))
        d.text((4, y - 14), "input | prediction | truth", fill=(0, 0, 0))
    sheet.save(path)


def resize_positions(sd, model):
    """Weights of another picture size: the model's own pretrained position table (DINOv2's at
    this size) plus what that run's training changed in it, resampled to this size. `model` must
    hold the pretrained table (built with pretrained=True)."""
    from timm.layers import resample_abs_pos_embed

    key = "backbone.pos_embed"
    old, mine = sd[key], model.state_dict()[key]
    if old.shape == mine.shape:
        return
    p = model.backbone.num_prefix_tokens

    def side(t):
        return [round(math.sqrt(t.shape[1] - p))] * 2

    start = resample_abs_pos_embed(mine, side(old), num_prefix_tokens=p)  # what timm gave that run
    sd[key] = mine + resample_abs_pos_embed(old - start, side(mine), num_prefix_tokens=p)
    print(f"position table {side(old)} -> {side(mine)} patches", flush=True)


def init_weights(model, path, dev):
    """Start from a train.py checkpoint's EMA weights or an exported package's."""
    path = Path(path)
    if path.suffix == ".safetensors":
        import package

        sd = {k: v.to(dev) for k, v in package.load(path)["weights"].items()}
    else:
        sd = torch.load(path, map_location=dev)["ema"]
    resize_positions(sd, model)
    model.load_state_dict(sd)


def end_time(args, state):
    """The wall-clock end: --deadline, else a resumed run's own, else --hours from now."""
    now = dt.datetime.now()
    if args.deadline:
        hh, mm = (int(x) for x in args.deadline.split(":"))
        end = now.replace(hour=hh, minute=mm, second=0, microsecond=0)
        return (end if end > now else end + dt.timedelta(days=1)).timestamp()
    if state.get("deadline"):
        return state["deadline"]
    return (now + dt.timedelta(hours=args.hours)).timestamp()


def main():
    ap = argparse.ArgumentParser(description="Train frommhr's reader on rendered MHR people")
    ap.add_argument("--data", type=Path, default=DATA / "data.pt", help="prep.py's output")
    ap.add_argument("--run", type=Path, default=DATA / "run", help="log, previews and checkpoints")
    ap.add_argument("--hours", type=float, default=7.0, help="the run's length by the wall clock")
    ap.add_argument("--deadline", default="", help="HH:MM: end at this time instead")
    ap.add_argument("--batch", type=int, default=32)
    ap.add_argument("--steps", type=int, default=0, help="stop after this many steps (smoke test)")
    ap.add_argument("--lr", type=float, default=4e-5, help="the backbone's; the head's is 10x")
    ap.add_argument("--eyes-lr", type=float, default=1e-3, help="EyeReader's (it starts from scratch)")
    ap.add_argument("--max-temp", type=float, default=75, help="CUDA: pause between steps above this, C")
    ap.add_argument("--init", default="", help="start from this checkpoint's or package's weights")
    ap.add_argument("--size", type=int, default=SIZE, help="picture pixels, a multiple of 14")
    ap.add_argument("--checkpointing", action="store_true", help="recompute the backbone's activations (less memory)")
    ap.add_argument("--device", default="cuda" if torch.cuda.is_available() else "cpu")
    ap.add_argument("--val", type=int, default=256, help="validation people (a multiple of 32)")
    args = ap.parse_args()
    run = args.run
    run.mkdir(parents=True, exist_ok=True)
    torch.backends.cuda.matmul.allow_tf32 = True
    torch.backends.cudnn.allow_tf32 = True
    dev = torch.device(args.device)
    cuda = dev.type == "cuda"

    t_init = time.time()
    heads = MhrHeads(torch.load(args.data, map_location="cpu", weights_only=False), device=dev)
    gen = Generator(heads, C.ColourLooks(heads), size=args.size)
    val = make_val(gen, n=args.val)
    model = Reader(size=args.size).to(dev)
    ema = Reader(pretrained=False, size=args.size).to(dev)
    if args.checkpointing:
        model.backbone.set_grad_checkpointing(True)
    last = run / "last.pt"
    if args.init and not last.exists():
        init_weights(model, args.init, dev)
        print(f"started from {args.init}", flush=True)
    ema.load_state_dict(model.state_dict())
    ema.eval().requires_grad_(False)
    groups = []
    for part, lr in (("backbone", args.lr), ("head", args.lr * 10), ("eyes", args.eyes_lr)):
        named = [(n, p) for n, p in model.named_parameters() if n.split(".")[0] == part]
        groups.append({"params": [p for n, p in named if p.ndim >= 2], "lr": lr, "base_lr": lr, "weight_decay": 0.05})
        groups.append({"params": [p for n, p in named if p.ndim < 2], "lr": lr, "base_lr": lr, "weight_decay": 0.0})
    opt = torch.optim.AdamW(groups, betas=(0.9, 0.999))

    state = {
        "step": 0,
        "t0": time.time(),
        "best": float("inf"),
        "deadline": 0.0,
        "format": FORMAT,
        "sliders": [list(s) for s in C.SLIDERS],
        "size": args.size,
        "batch": args.batch,
        "lr": args.lr,
        "eyes_lr": args.eyes_lr,
        "init": Path(args.init).name if args.init else "",
    }
    if last.exists():
        saved = torch.load(last, map_location=dev, weights_only=False)
        model.load_state_dict(saved["model"]), ema.load_state_dict(saved["ema"]), opt.load_state_dict(saved["opt"])
        state.update(saved["state"])
        print(f"resumed at step {state['step']}", flush=True)
    state["deadline"] = end_time(args, state)
    torch.manual_seed(int(time.time()))

    def write_log(record):
        with open(run / "log.jsonl", "a") as log:
            log.write(json.dumps(record) + "\n")

    print(
        f"ready in {time.time() - t_init:.0f}s; training until {dt.datetime.fromtimestamp(state['deadline']):%Y-%m-%d %H:%M}",
        flush=True,
    )
    if state["step"] == 0:
        v = validate(ema, gen, val)
        v.update(step=0, time=dt.datetime.now().strftime("%H:%M:%S"))
        write_log(v)
        print(v, flush=True)

    stop = {"flag": False}
    signal.signal(signal.SIGTERM, lambda *_: stop.update(flag=True))

    def save(path=last):
        tmp = Path(str(path) + ".tmp")
        torch.save({"model": model.state_dict(), "ema": ema.state_dict(), "opt": opt.state_dict(), "state": state}, tmp)
        tmp.rename(path)

    def keep(v, name):
        torch.save({"ema": ema.state_dict(), "state": state, "val": v}, run / name)

    pause, temp, power = 0.0, float("nan"), float("nan")
    t_save = t_val = time.time()
    t_preview = time.time() - 3000  # a first preview after 10 minutes
    acc, n_acc, t_acc = {}, 0, time.time()
    warm = 500
    first = state["step"]
    while True:
        now = time.time()
        done = now >= state["deadline"] or (args.steps and state["step"] - first >= args.steps)
        if stop["flag"] or done:
            break
        frac = min(1.0, max(0.0, (now - state["t0"]) / (state["deadline"] - state["t0"])))
        factor = min(1.0, (state["step"] + 1) / warm) * (0.03 + 0.97 * 0.5 * (1 + math.cos(math.pi * frac)))
        for g in opt.param_groups:
            g["lr"] = g["base_lr"] * factor

        x, target, ok, _ = gen.batch(args.batch)
        with autocast(dev):
            pred = model(x)
        loss, parts, _ = losses(gen, pred, target, ok)
        opt.zero_grad(set_to_none=True)
        loss.backward()
        gnorm = float(torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0))
        opt.step()
        decay = min(0.999, (1 + state["step"]) / (10 + state["step"]))
        with torch.no_grad():
            for pe, pm in zip(ema.parameters(), model.parameters()):
                pe.lerp_(pm, 1 - decay)
        state["step"] += 1
        for k, v in parts.items():
            acc[k] = acc.get(k, 0) + v
        acc["gnorm"] = acc.get("gnorm", 0) + gnorm
        acc["kept"] = acc.get("kept", 0) + float(ok.float().mean())
        n_acc += 1

        if cuda and state["step"] % 20 == 0:
            temp, power = gpu_state()
            if temp >= args.max_temp + 2:
                pause = min(pause + 0.05, 2.0)
            elif temp <= args.max_temp - 2:
                pause = max(pause - 0.02, 0.0)
        if pause:
            time.sleep(pause)
        if state["step"] % 50 == 0 or (args.steps and state["step"] - first <= 3):
            rate = n_acc * args.batch / (time.time() - t_acc)
            rec = {
                "step": state["step"],
                "time": dt.datetime.now().strftime("%H:%M:%S"),
                "lr": round(factor, 4),
                **{k: round(v / n_acc, 4) for k, v in acc.items()},
                "people_per_s": round(rate, 2),
            }
            if cuda:
                rec.update(
                    temp=temp,
                    power=power,
                    pause=round(pause, 2),
                    gpu_gb=round(torch.cuda.max_memory_allocated() / 2**30, 2),
                )
            write_log(rec)
            print(rec, flush=True)
            acc, n_acc, t_acc = {}, 0, time.time()
        if time.time() - t_val > 900:
            v = validate(ema, gen, val)
            v.update(step=state["step"], time=dt.datetime.now().strftime("%H:%M:%S"))
            write_log(v)
            print(v, flush=True)
            if v["score"] < state["best"]:
                state["best"] = v["score"]
                keep(v, "best.pt")
            t_val = time.time()
        if time.time() - t_preview > 3600:
            preview(ema, gen, val, run / f"preview_{state['step']:06d}.png")
            t_preview = time.time()
        if time.time() - t_save > 300:
            save()
            t_save = time.time()

    save()
    if not stop["flag"]:
        v = validate(ema, gen, val)
        v.update(step=state["step"], time=dt.datetime.now().strftime("%H:%M:%S"), final=True)
        write_log(v)
        print(v, flush=True)
        if v["score"] < state["best"]:
            state["best"] = v["score"]
            keep(v, "best.pt")
        keep(v, "final.pt")
        preview(ema, gen, val, run / f"preview_{state['step']:06d}.png")
    print("stopped" if stop["flag"] else "done", flush=True)


if __name__ == "__main__":
    main()
