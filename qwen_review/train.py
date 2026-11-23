"""LoRA for Qwen3.5 (2B by default): review count and % positive from year, genres, tags
and a screenshot.

    python -I train.py recognize LABELS SHOTS_DIR OUT_JSON [--games 100]
    python -I train.py train LABELS SHOTS_DIR RUN_DIR [--epochs 1] [--max-minutes M]
    python -I train.py evaluate LABELS SHOTS_DIR RUN_DIR [--split val] [--adapter base]

LABELS is a JSONL file, one game per line: appid, name, year, genres, tags, shots
(downloaded screenshot indices), reviews (int), positive (int %, may be null below 10
reviews) and split (train, val or test). Screenshots are SHOTS_DIR/<appid>_<NN>.jpg.

The prompt gives the release year, the genres and the 5 strongest tags. The answer is
two class digits, the review class and the % positive class, "-" under 10 reviews:

    7 6     1000-2999 reviews, 85-89% positive
    2 -     3-9 reviews, too few for a %

Every downloaded screenshot (up to 4 per game) is a training example of its own with
its game's labels; all screenshots of a game are in the same split. Only the answer
tokens are scored. Evaluation reads the probabilities of the review digit and ranks by
the expected class with the pairwise metric of baseline.py (same year and genre, counts
at least 2x apart), per screenshot and for the mean over a game's screenshots; then it
forces the most likely review class and reads the % digit (games with 10+ reviews).

`evaluate` appends its predictions to RUN_DIR/eval_<split>_<adapter>_<shot>_predictions.jsonl
as it goes and continues from them when run again (the adapter name of last/ carries its
step). With --games N it takes the same random games as training's --val-games N.

Training keeps RUN_DIR/last/ (LoRA weights, optimizer, step) saved every few minutes
and resumes from it. SIGTERM (the Studio needs the GPU) saves and stops at the next
batch. Training pauses while the GPU is hotter than --max-temp (0: never).

For a big rented GPU: --model Qwen/Qwen3.5-4B or -9B (same processor and prompt),
--workers N builds the batches in N spawned processes ahead of the GPU (0, the
default, builds them in place as before), --max-steps N stops like --max-minutes,
and --no-checkpointing keeps the activations instead of recomputing them.

Part of an epoch: --schedule-steps N ends the cosine at step N (default: the end of the
epochs); give --max-steps N too. Continuing a finished LoRA on other games (a labels
file whose split marks them train): --init OLD_RUN/last starts a new RUN_DIR from those
weights with a fresh optimizer and its own warmup and cosine.

`recognize` asks the plain model which game a screenshot shows, to see whether it
could recall famous games instead of judging the picture.
"""

import argparse
import atexit
import json
import math
import os
import random
import re
import signal
import subprocess
import sys
import time
from collections import defaultdict
from collections.abc import Iterator
from pathlib import Path

import numpy as np
import torch
import torch.nn.functional as F
from PIL import Image
from torch.utils.data import DataLoader, Dataset

sys.path.insert(0, str(Path(__file__).resolve().parent))
from baseline import CLEAR_RATIO, pairwise, primary_genre

MODEL = "Qwen/Qwen3.5-2B"
DEVICE = "cuda"
END = "<|im_end|>"
REVIEW_EDGES = (0, 1, 3, 10, 30, 100, 300, 1000, 3000, 10000)
REVIEW_TEXT = (
    "0",
    "1-2",
    "3-9",
    "10-29",
    "30-99",
    "100-299",
    "300-999",
    "1000-2999",
    "3000-9999",
    "10000+",
)
POSITIVE_EDGES = (0, 50, 60, 70, 75, 80, 85, 90, 95, 98)
POSITIVE_TEXT = (
    "under 50%",
    "50-59%",
    "60-69%",
    "70-74%",
    "75-79%",
    "80-84%",
    "85-89%",
    "90-94%",
    "95-97%",
    "98-100%",
)
POSITIVE_MID = np.array([35.0, 55.0, 65.0, 72.5, 77.5, 82.5, 87.5, 92.5, 96.5, 99.0])
MIN_SCORED = 10  # a % only from 10 reviews on, like the store page
NO_SCORE = "-"
# Only the 5 strongest tags go into the prompt. Popular games collect more tags, so the
# number of tags alone predicts popularity (ridge on year + genres: 60.4% of pairs, with
# the tag count 65.7%); with 5 tags nearly every game shows the same number.
PROMPT_TAGS = 5
MAX_SHOTS = 4  # screenshots per game, each an example of its own
QUESTION = (
    "How many reviews will this game get, and what share of them will be positive?"
    " Answer with two classes from 0 to 9."
)
# Answer-token index of each class digit: "7", " ", "6".
REVIEW_AT, POSITIVE_AT = 0, 2
# Review class in the second-read inputs that workers build; predict() puts in the real one.
PLACEHOLDER = 0
RECOGNIZE_QUESTION = (
    "Which game is this screenshot from? Answer with the game's name only."
)
# LoRA on the language model only: full attention, linear attention and MLP projections.
TARGETS = (
    r"model\.language_model\.layers\.\d+\."
    r"(self_attn\.(q|k|v|o)_proj|linear_attn\.(in_proj_qkv|in_proj_z|out_proj)"
    r"|mlp\.(gate|up|down)_proj)"
)


def bucket(value: float, edges: tuple) -> int:
    return max(k for k, edge in enumerate(edges) if value >= edge)


def scored(row: dict) -> bool:
    return row["reviews"] >= MIN_SCORED


def game_shots(row: dict) -> list[int]:
    return sorted(row["shots"])[:MAX_SHOTS]


def answer(row: dict) -> str:
    k = bucket(row["reviews"], REVIEW_EDGES)
    if not scored(row):
        return f"{k} {NO_SCORE}"
    return f"{k} {bucket(row['positive'], POSITIVE_EDGES)}"


def user_text(row: dict) -> str:
    genres = ", ".join(row["genres"]) or "none"
    tags = ", ".join(row["tags"][:PROMPT_TAGS]) or "none"
    return (
        f"Game screenshot. Release year: {row['year']}. Genres: {genres}. Tags: {tags}.\n"
        f"{QUESTION}"
    )


def chat(processor, text: str) -> str:
    messages = [
        {"role": "user", "content": [{"type": "image"}, {"type": "text", "text": text}]}
    ]
    # Without enable_thinking=False the 4B and 9B templates open a thinking block; the 2B
    # template renders the same empty block either way.
    return processor.apply_chat_template(
        messages, add_generation_prompt=True, tokenize=False, enable_thinking=False
    )


def load_rows(path: Path) -> list[dict]:
    # Not splitlines(): names may hold U+2028 and other separators json.dumps keeps.
    with path.open(encoding="utf-8") as fh:
        rows = [json.loads(line) for line in fh if line.strip()]
    for row in rows:
        if scored(row) and row["positive"] is None:
            raise ValueError(f"{row['appid']}: {row['reviews']} reviews without a %")
    return rows


def shot_name(appid: int, shot: int) -> str:
    return f"{appid}_{shot:02d}.jpg"


def on_disk(shots_dir: Path, appid: int, shot: int) -> bool:
    return (shots_dir / shot_name(appid, shot)).exists()


def load_image(shots_dir: Path, appid: int, index: int) -> Image.Image:
    with Image.open(shots_dir / shot_name(appid, index)) as img:
        return img.convert("RGB")


def gpu_temperature() -> int:
    """The hottest GPU in °C; 0 if nvidia-smi is missing or reports none."""
    try:
        out = subprocess.run(
            [
                "nvidia-smi",
                "--query-gpu=temperature.gpu",
                "--format=csv,noheader,nounits",
            ],
            capture_output=True,
            text=True,
            check=False,
            timeout=60,
        )
    except (OSError, subprocess.TimeoutExpired):
        return 0
    return max((int(t) for t in out.stdout.split() if t.isdigit()), default=0)


def load_processor(name: str):
    from transformers import AutoProcessor

    processor = AutoProcessor.from_pretrained(name)
    processor.tokenizer.padding_side = "left"
    return processor


def load_model(name: str = MODEL, trainable: bool = False):
    from transformers import Qwen3_5ForConditionalGeneration

    processor = load_processor(name)
    model = Qwen3_5ForConditionalGeneration.from_pretrained(
        name, dtype=torch.bfloat16, attn_implementation="sdpa", device_map=DEVICE
    )
    model.config.use_cache = not trainable
    return processor, model


def token_ids(processor) -> dict:
    """Ids of the class digits; answers must split into digit, space, digit."""
    tok = processor.tokenizer

    def ids_of(text: str) -> list[int]:
        return tok(text, add_special_tokens=False)["input_ids"]

    digits, space = [ids_of(str(d)) for d in range(10)], ids_of(" ")
    if any(len(d) != 1 for d in digits) or len(space) != 1:
        raise ValueError(f"digits {digits} or the space {space} are not single tokens")
    digits = [d[0] for d in digits]
    for a in range(10):
        if ids_of(f"{a} ") != [digits[a], space[0]]:
            raise ValueError(f"{a!r} and a space merge")
        for b in range(10):
            if ids_of(f"{a} {b}") != [digits[a], space[0], digits[b]]:
                raise ValueError(f"'{a} {b}' does not split into digit, space, digit")
    return {"digits": digits, "space": space[0], "end": tok.convert_tokens_to_ids(END)}


def encode(processor, texts: list[str], images: list[Image.Image], device="") -> dict:
    enc = processor(text=texts, images=images, padding=True, return_tensors="pt")
    return {k: v.to(device or DEVICE) for k, v in enc.items()}


def to_device(enc: dict) -> dict:
    """A batch built by a worker (pinned memory on CUDA) on its way to the GPU."""
    return {k: v.to(DEVICE, non_blocking=True) for k, v in enc.items()}


# ---------------------------------------------------------------- training


class Stopper:
    """SIGTERM/SIGINT ask the loop to save and leave at the next batch."""

    def __init__(self) -> None:
        self.reason = ""
        signal.signal(signal.SIGTERM, self.handle)
        signal.signal(signal.SIGINT, self.handle)

    def handle(self, signum, _frame) -> None:
        self.reason = signal.Signals(signum).name


class Feed:
    """Training screenshots in random order, each once per epoch, only those on disk.

    The queue holds the epoch's unseen screenshots that are downloaded. It is rebuilt
    every `refresh` batches, so training can run while the crawl goes on and later
    downloads join in. When it runs dry with screenshots still missing, it waits up to
    `patience` seconds for them, then the epoch goes on without them. `seen` holds
    appid * 100 + shot and is saved with the checkpoint. `pending` holds what workers
    took ahead of training (take(mark=False)) until done() moves it to `seen`.
    """

    def __init__(self, examples, shots_dir, seed, refresh, patience, stop, epoch, seen):
        self.examples, self.shots_dir, self.seed = examples, shots_dir, seed
        self.refresh, self.patience, self.stop = refresh, patience, stop
        self.epoch, self.seen, self.pending = epoch, set(seen), set()
        self.queue, self.batches, self.ended = [], 0, False

    def rebuild(self) -> int:
        """Refills the queue; returns how many unseen screenshots are not on disk."""
        names = {entry.name for entry in os.scandir(self.shots_dir)}
        taken = self.seen | self.pending if self.pending else self.seen
        unseen = [
            i
            for i, (row, shot) in enumerate(self.examples)
            if row["appid"] * 100 + shot not in taken
        ]
        self.queue = [
            i
            for i in unseen
            if shot_name(self.examples[i][0]["appid"], self.examples[i][1]) in names
        ]
        random.Random(f"{self.seed}:{self.epoch}:{len(taken)}").shuffle(self.queue)
        missing = len(unseen) - len(self.queue)
        print(
            f"epoch {self.epoch}: {len(self.seen)} screenshots seen, {len(self.queue)}"
            f" queued, {missing} not downloaded yet",
            flush=True,
        )
        return missing

    def refill(self) -> bool:
        """Refills the queue, waiting up to `patience` for screenshots still downloading."""
        deadline = time.monotonic() + self.patience
        while not self.stop():
            missing = self.rebuild()
            if self.queue:
                return True
            if not missing or time.monotonic() >= deadline:
                if missing:
                    print(f"{missing} screenshots never arrived, left out", flush=True)
                self.ended = True
                return False
            wake = min(deadline, time.monotonic() + 60)
            while not self.stop() and (left := wake - time.monotonic()) > 0:
                time.sleep(min(10.0, left))
        return False

    def take(self, n: int, mark: bool = True) -> list[tuple[dict, int]]:
        """Up to n examples; fewer when the epoch ends (self.ended) or on a stop.

        They count as seen right away, or with mark=False once done() reports them.
        """
        if self.ended:
            return []
        if self.batches % self.refresh == 0:
            self.rebuild()
        self.batches += 1
        out = []
        while len(out) < n:
            if not self.queue and not self.refill():
                break
            row, shot = self.examples[self.queue.pop()]
            (self.seen if mark else self.pending).add(row["appid"] * 100 + shot)
            out.append((row, shot))
        return out

    def done(self, keys: list[int]) -> None:
        self.pending.difference_update(keys)
        self.seen.update(keys)

    def next_epoch(self) -> None:
        self.epoch += 1
        self.seen, self.pending, self.queue = set(), set(), []
        self.batches, self.ended = 0, False


def split_games(rows: list[dict], split: str, limit: int, seed: int) -> list[dict]:
    """Games of a split with screenshots; a fixed random subset if limit is set.

    Labels come in release order, so the first games of a split are the oldest.
    """
    games = [r for r in rows if r["split"] == split and r["shots"]]
    if limit and limit < len(games):
        games = random.Random(seed).sample(games, limit)
    return games


def train_batch(processor, items, shots_dir, ids, device="") -> tuple:
    """Inputs, labels (answer tokens only), logits to keep and the digit positions.

    items are (game, screenshot index). Digit positions index the per-token losses of
    answer_loss: (example, position) of every review digit and of every % digit.
    """
    texts, images, answers = [], [], []
    tok = processor.tokenizer
    for row, shot in items:
        text = answer(row) + END
        texts.append(chat(processor, user_text(row)) + text)
        images.append(load_image(shots_dir, row["appid"], shot))
        answers.append(tok(text, add_special_tokens=False)["input_ids"])
    enc = encode(processor, texts, images, device)
    labels = torch.full_like(enc["input_ids"], -100)
    keep = max(len(a) for a in answers) + 1
    review_at, positive_at = [], []
    for i, ((row, _), a) in enumerate(zip(items, answers, strict=True)):
        tail = enc["input_ids"][i, -len(a) :]
        if tail.tolist() != a or a[-1] != ids["end"]:
            raise ValueError(f"answer tokens of {row['appid']} merged with the prompt")
        labels[i, -len(a) :] = tail
        places = [(REVIEW_AT, review_at)] + (
            [(POSITIVE_AT, positive_at)] if scored(row) else []
        )
        for offset, out in places:
            if a[offset] not in ids["digits"]:
                raise ValueError(
                    f"no class digit at {offset} in {row['appid']}'s answer"
                )
            out.append((i, keep - 1 - len(a) + offset))
    return enc, labels, keep, (review_at, positive_at)


# ---------------------------------------------------------------- batch workers


def same(x):
    return x


def leave_quickly(_worker_id: int) -> None:
    """A worker skips the interpreter's shutdown when it ends.

    Told to stop while its queue thread still sends a batch taken ahead (any stop
    before an epoch's end), a spawned worker aborts in that shutdown ("terminate
    called without an active exception") and the loader raises in the main process.
    A worker has nothing to save.
    """
    atexit.register(os._exit, 0)


def loader(dataset: Dataset, sampler, workers: int) -> DataLoader:
    """Batches built by `workers` spawned processes, two each ahead, in sampler order.

    Spawned, not forked: the parent holds CUDA. A generator of its own leaves the global
    random state (LoRA dropout draws from it) as it is without workers.
    """
    return DataLoader(
        dataset,
        batch_size=None,
        sampler=sampler,
        num_workers=workers,
        collate_fn=same,
        pin_memory=DEVICE == "cuda",
        prefetch_factor=2,
        multiprocessing_context="spawn",
        generator=torch.Generator(),
        worker_init_fn=leave_quickly,
    )


class TrainBatches(Dataset):
    """train_batch in a worker; a key is a list of (game, screenshot index)."""

    def __init__(self, model: str, shots_dir: Path, ids: dict) -> None:
        self.model, self.shots_dir, self.ids = model, shots_dir, ids
        self.processor = None

    def __getitem__(self, items: list) -> tuple:
        if self.processor is None:
            self.processor = load_processor(self.model)
        keys = [row["appid"] * 100 + shot for row, shot in items]
        return keys, train_batch(self.processor, items, self.shots_dir, self.ids, "cpu")


class FeedSampler:
    """The Feed's batches as DataLoader keys, taken ahead without counting as seen.

    `last` numbers the batch whose take ended the epoch (it came up short).
    """

    def __init__(self, feed: Feed, batch: int) -> None:
        self.feed, self.batch = feed, batch
        self.count, self.last = 0, None

    def __iter__(self):
        while items := self.feed.take(self.batch, mark=False):
            self.count += 1
            if self.feed.ended:
                self.last = self.count
            yield items


class Batches:
    """Training batches of a Feed, built in place, or ahead by workers (workers > 0).

    With workers a batch's screenshots count as seen once the loop gets the batch, so a
    stop loses none of the batches still in the workers. The epoch ends with the batch
    that ended it in the Feed, or with none left, as without workers.
    """

    def __init__(self, feed, processor, shots_dir, ids, batch, workers, model) -> None:
        self.feed, self.processor, self.shots_dir = feed, processor, shots_dir
        self.ids, self.batch, self.workers, self.model = ids, batch, workers, model
        self.sampler, self.built, self.received, self.drained = None, None, 0, False

    @property
    def ended(self) -> bool:
        if not self.workers:
            return self.feed.ended
        return self.feed.ended and (self.drained or self.received == self.sampler.last)

    def next(self) -> tuple | None:
        """(screenshots, train_batch on the device), or None at the end or on a stop."""
        if not self.workers:
            items = self.feed.take(self.batch)
            if not items:
                return None
            return len(items), train_batch(
                self.processor, items, self.shots_dir, self.ids
            )
        if self.built is None:
            self.sampler = FeedSampler(self.feed, self.batch)
            dataset = TrainBatches(self.model, self.shots_dir, self.ids)
            self.built = iter(loader(dataset, self.sampler, self.workers))
        got = next(self.built, None)
        if got is None:
            self.drained = True
            return None
        self.received += 1
        keys, (enc, labels, keep, digits) = got
        self.feed.done(keys)
        labels = labels.to(DEVICE, non_blocking=True)
        return len(keys), (to_device(enc), labels, keep, digits)

    def next_epoch(self) -> None:
        self.feed.next_epoch()
        self.sampler, self.built, self.received, self.drained = None, None, 0, False


class EvalBatches(Dataset):
    """predict()'s inputs for one batch, built in a worker: the prompts with the
    screenshots, and for games with 10+ reviews the same with the PLACEHOLDER class."""

    def __init__(self, parts: list, model: str, shots_dir: Path) -> None:
        self.parts, self.model, self.shots_dir = parts, model, shots_dir
        self.processor = None

    def __len__(self) -> int:
        return len(self.parts)

    def __getitem__(self, i: int) -> tuple:
        if self.processor is None:
            self.processor = load_processor(self.model)
        part = self.parts[i]
        prompts = [chat(self.processor, user_text(r)) for r, _ in part]
        images = [load_image(self.shots_dir, r["appid"], s) for r, s in part]
        need = [k for k, (r, _) in enumerate(part) if scored(r)]
        second = None
        if need:
            texts = [prompts[k] + f"{PLACEHOLDER} " for k in need]
            second = encode(self.processor, texts, [images[k] for k in need], "cpu")
        return encode(self.processor, prompts, images, "cpu"), second


def with_class(enc: dict, classes: list[int], ids: dict) -> dict:
    """EvalBatches' second-read inputs with each row's review class in place."""
    tail = enc["input_ids"][:, -2:]
    if not (
        (tail[:, 0] == ids["digits"][PLACEHOLDER]).all()
        and (tail[:, 1] == ids["space"]).all()
    ):
        raise ValueError("the placeholder class is not the second-last token")
    enc["input_ids"][:, -2] = torch.tensor([ids["digits"][c] for c in classes])
    return enc


def answer_loss(model, enc, labels, keep) -> tuple[torch.Tensor, torch.Tensor]:
    """Mean loss over the answer tokens, and the per-token losses (detached)."""
    logits = model(**enc, logits_to_keep=keep).logits.float()
    target = labels[:, -(keep - 1) :]
    per_token = F.cross_entropy(
        logits[:, :-1].transpose(1, 2), target, ignore_index=-100, reduction="none"
    )
    mask = target != -100
    return (per_token * mask).sum() / mask.sum(), per_token.detach()


def class_entropy(classes: list[int]) -> float:
    """Loss of always answering the class shares of the training set (nats)."""
    p = np.bincount(classes, minlength=10) / len(classes)
    return float(-(p[p > 0] * np.log(p[p > 0])).sum())


def save_last(model, opt, run: Path, state: dict, seen: set) -> None:
    tmp = run / "last.tmp"
    model.save_pretrained(tmp)
    torch.save(opt.state_dict(), tmp / "optimizer.pt")
    np.save(tmp / "seen.npy", np.fromiter(seen, dtype=np.int64, count=len(seen)))
    (tmp / "state.json").write_text(json.dumps(state), encoding="utf-8")
    old = run / "last.old"
    if (run / "last").exists():
        (run / "last").rename(old)
    tmp.rename(run / "last")
    if old.exists():
        for f in sorted(old.rglob("*"), reverse=True):
            f.unlink() if f.is_file() else f.rmdir()
        old.rmdir()


def cmd_train(args: argparse.Namespace) -> None:
    from peft import LoraConfig, PeftModel, get_peft_model

    torch.set_num_threads(4)
    torch.backends.cuda.matmul.allow_tf32 = True
    rows = load_rows(Path(args.labels))
    train = [(r, s) for r in rows if r["split"] == "train" for s in game_shots(r)]
    val = split_games(rows, "val", args.val_games, args.seed)
    run = Path(args.run)
    run.mkdir(parents=True, exist_ok=True)
    resume = (run / "last" / "state.json").exists()
    if resume:
        state = json.loads((run / "last" / "state.json").read_text(encoding="utf-8"))
        base = state["args"].get("model", MODEL)
        if base != args.model:
            raise SystemExit(f"{run}/last is a LoRA of {base}, not of {args.model}")
    elif args.init:
        init_state = Path(args.init) / "state.json"
        if init_state.exists():
            old = json.loads(init_state.read_text(encoding="utf-8"))["args"]
            if old.get("model", MODEL) != args.model:
                raise SystemExit(
                    f"{args.init} is a LoRA of {old.get('model', MODEL)}, not of {args.model}"
                )
    stopper = Stopper()
    processor, model = load_model(args.model, trainable=True)
    ids = token_ids(processor)
    if args.checkpointing:
        model.gradient_checkpointing_enable(
            gradient_checkpointing_kwargs={"use_reentrant": False}
        )
    model.enable_input_require_grads()
    if resume:
        model = PeftModel.from_pretrained(model, run / "last", is_trainable=True)
    else:
        if args.init:
            model = PeftModel.from_pretrained(model, args.init, is_trainable=True)
        else:
            config = LoraConfig(
                r=args.rank,
                lora_alpha=2 * args.rank,
                lora_dropout=0.05,
                target_modules=TARGETS,
                task_type="CAUSAL_LM",
            )
            model = get_peft_model(model, config)
        state = {
            "step": 0,
            "args": {k: v for k, v in vars(args).items() if k != "func"},
        }
    wrapped = sum(1 for n, _ in model.named_modules() if n.endswith(".lora_A"))
    params = [p for p in model.parameters() if p.requires_grad]
    opt = torch.optim.AdamW(params, lr=args.lr, weight_decay=0.0)
    if resume:
        opt.load_state_dict(
            torch.load(run / "last" / "optimizer.pt", weights_only=True)
        )
    per_epoch = math.ceil(len(train) / (args.batch * args.accum))
    total = args.schedule_steps or per_epoch * args.epochs
    warmup = min(args.warmup, max(1, total // 10))

    def lr_at(step: int) -> float:
        if step < warmup:
            return args.lr * (step + 1) / warmup
        return (
            args.lr
            * 0.5
            * (1 + math.cos(math.pi * (step - warmup) / max(1, total - warmup)))
        )

    prior_review = class_entropy([bucket(r["reviews"], REVIEW_EDGES) for r, _ in train])
    prior_positive = class_entropy(
        [bucket(r["positive"], POSITIVE_EDGES) for r, _ in train if scored(r)]
    )
    print(
        f"train {len(train)} screenshots of {len({r['appid'] for r, _ in train})} games,"
        f" val {len(val)} games; {per_epoch} steps per epoch, {total} in all;"
        f" LoRA on {wrapped} layers, {sum(p.numel() for p in params) / 1e6:.1f}M trainable;"
        f" {'resumed at step ' + str(state['step']) if resume else 'new run'};"
        f" class shares alone give review_ce {prior_review:.3f}, positive_ce {prior_positive:.3f}",
        flush=True,
    )
    seen_file = run / "last" / "seen.npy"
    feed = Feed(
        train,
        Path(args.shots),
        args.seed,
        args.refresh,
        args.patience * 60,
        stop=lambda: bool(stopper.reason),
        epoch=state.get("epoch", 0),
        seen=np.load(seen_file).tolist() if resume and seen_file.exists() else (),
    )
    batches = Batches(
        feed, processor, Path(args.shots), ids, args.batch, args.workers, args.model
    )
    log = (run / "log.jsonl").open("a", encoding="utf-8")
    started = last_save = time.monotonic()
    window_loss, window_n, window_shots, window_t = 0.0, 0, 0, time.monotonic()
    digit_sums = {"review_ce": [0.0, 0], "positive_ce": [0.0, 0]}
    trained = 0  # screenshots this process trained on

    def stopping(why: str) -> None:
        """A last log entry: what this process did and the GPU memory it peaked at."""
        seconds = time.monotonic() - started
        entry = {
            "reason": why,
            "step": step,
            "shots": trained,
            "seconds": round(seconds, 1),
            "shots_per_s": round(trained / max(seconds, 1e-9), 2),
            "gpu_mb": torch.cuda.max_memory_allocated() // 2**20,
            "gpu_reserved_mb": torch.cuda.max_memory_reserved() // 2**20,
            "time": time.strftime("%H:%M:%S"),
        }
        print("stop", json.dumps(entry), flush=True)
        log.write(json.dumps({"stop": entry}) + "\n")
        log.flush()

    model.train()
    step = state["step"]
    while feed.epoch < args.epochs:
        for g in opt.param_groups:
            g["lr"] = lr_at(min(step, total - 1))
        taken = 0
        for _ in range(args.accum):
            got = batches.next()
            if got is None:
                break
            n, (enc, labels, keep, digits) = got
            taken += n
            with torch.autocast("cuda", dtype=torch.bfloat16):
                loss, per_token = answer_loss(model, enc, labels, keep)
            (loss / args.accum).backward()
            window_loss += loss.item()
            window_n += 1
            window_shots += n
            for name, places in zip(digit_sums, digits, strict=True):
                if places:
                    rows_at, cols_at = zip(*places, strict=True)
                    digit_sums[name][0] += (
                        per_token[list(rows_at), list(cols_at)].sum().item()
                    )
                    digit_sums[name][1] += len(places)
        trained += taken
        if taken:
            torch.nn.utils.clip_grad_norm_(params, 1.0)
            opt.step()
            opt.zero_grad(set_to_none=True)
            step += 1
        state.update(step=step, epoch=feed.epoch)
        now = time.monotonic()
        if taken and step % args.log_every == 0:
            entry = {
                "step": step,
                "epoch": round(feed.epoch + len(feed.seen) / len(train), 3),
                "loss": round(window_loss / window_n, 4),
                **{k: round(s / max(n, 1), 3) for k, (s, n) in digit_sums.items()},
                "lr": opt.param_groups[0]["lr"],
                "shots_per_s": round(window_shots / (now - window_t), 2),
                "gpu_mb": torch.cuda.max_memory_allocated() // 2**20,
                "gpu_reserved_mb": torch.cuda.max_memory_reserved() // 2**20,
                "temp": gpu_temperature(),
                "time": time.strftime("%H:%M:%S"),
            }
            print(json.dumps(entry), flush=True)
            log.write(json.dumps(entry) + "\n")
            log.flush()
            window_loss, window_n, window_shots = 0.0, 0, 0
            window_t = time.monotonic()
            digit_sums = {k: [0.0, 0] for k in digit_sums}
            if args.max_temp and entry["temp"] >= args.max_temp:
                print(f"GPU at {entry['temp']} °C, pausing", flush=True)
                while gpu_temperature() > args.max_temp - 8 and not stopper.reason:
                    time.sleep(10)
                window_t = time.monotonic()
        out_of_time = args.max_minutes and now - started > args.max_minutes * 60
        out_of_steps = args.max_steps and step >= args.max_steps
        limit = stopper.reason or out_of_time or out_of_steps
        if limit or now - last_save > args.save_minutes * 60:
            save_last(model, opt, run, state, feed.seen)
            last_save = time.monotonic()
            if limit:
                why = stopper.reason or ("time limit" if out_of_time else "step limit")
                print(f"saved at step {step}, stopping ({why})", flush=True)
                stopping(why)
                return
        finished = batches.ended and feed.epoch + 1 >= args.epochs
        if val and ((taken and step % args.eval_every == 0) or finished):
            model.eval()
            items = eval_items(val, "random", Path(args.shots))
            preds = [
                p
                for part in predict(
                    processor,
                    model,
                    items,
                    Path(args.shots),
                    ids,
                    args.eval_batch,
                    stop=lambda: bool(stopper.reason),
                    workers=args.workers,
                    name=args.model,
                )
                for p in part
            ]
            model.train()
            if stopper.reason:
                save_last(model, opt, run, state, feed.seen)
                print(f"saved at step {step}, stopping ({stopper.reason})", flush=True)
                stopping(stopper.reason)
                return
            result = metrics(val, preds)
            result.update(step=step, split="val", shot="random")
            print("eval", json.dumps(result), flush=True)
            log.write(json.dumps({"eval": result}) + "\n")
            log.flush()
        if batches.ended:
            model.save_pretrained(run / f"epoch_{feed.epoch + 1}")
            batches.next_epoch()
            state.update(epoch=feed.epoch)
    save_last(model, opt, run, state, feed.seen)
    print("finished", flush=True)
    stopping("finished")


# ---------------------------------------------------------------- evaluation


@torch.inference_mode()
def digit_probs(model, enc: dict, ids: dict) -> np.ndarray:
    logits = model(**enc, logits_to_keep=1).logits[:, -1].float()
    return torch.softmax(logits[:, ids["digits"]], dim=-1).cpu().numpy()


@torch.inference_mode()
def read_digits(processor, model, texts, images, ids) -> np.ndarray:
    return digit_probs(model, encode(processor, texts, images), ids)


def rounded(probs: np.ndarray) -> list[float]:
    return [round(float(x), 4) for x in probs]


def eval_items(rows: list[dict], shot: str, shots_dir: Path) -> list[tuple[dict, int]]:
    """(game, screenshot) pairs on disk: the first gallery shot, a fixed random one, or all."""
    items = []
    for row in rows:
        shots = game_shots(row)
        if shot == "first":
            shots = shots[:1]
        elif shot == "random":
            shots = [random.Random(f"eval:{row['appid']}").choice(shots)]
        items += [(row, s) for s in shots if on_disk(shots_dir, row["appid"], s)]
    return items


def predict(
    processor,
    model,
    items,
    shots_dir,
    ids,
    batch,
    stop=lambda: False,
    workers=0,
    name=MODEL,
) -> Iterator[list[dict]]:
    """Class probabilities of each (game, screenshot), one list per batch.

    The review digit is read right after the prompt. For games with 10+ reviews the %
    digit is then read after the most likely review class from 3 (10+ reviews) up:
    after a lower class the model was taught to answer "-". With workers, worker
    processes build the inputs of both reads ahead (EvalBatches) and the class goes
    into the second read's inputs here; the batches are the same as without.
    """
    first = bucket(MIN_SCORED, REVIEW_EDGES)
    parts = [items[start : start + batch] for start in range(0, len(items), batch)]
    built = None
    if workers:
        built = iter(
            loader(EvalBatches(parts, name, shots_dir), range(len(parts)), workers)
        )
    for part in parts:
        if stop():
            return
        if built is None:
            prompts = [chat(processor, user_text(r)) for r, _ in part]
            images = [load_image(shots_dir, r["appid"], s) for r, s in part]
            p_rev = read_digits(processor, model, prompts, images, ids)
        else:
            enc, second = next(built)
            p_rev = digit_probs(model, to_device(enc), ids)
        out = [
            {
                "appid": r["appid"],
                "shot": s,
                "review_class": float(p @ np.arange(10)),
                "review_probs": rounded(p),
            }
            for (r, s), p in zip(part, p_rev, strict=True)
        ]
        need = [i for i, (r, _) in enumerate(part) if scored(r)]
        if need:
            classes = [first + int(p_rev[i][first:].argmax()) for i in need]
            if built is None:
                texts = [
                    prompts[i] + f"{c} " for i, c in zip(need, classes, strict=True)
                ]
                p_pos = read_digits(
                    processor, model, texts, [images[i] for i in need], ids
                )
            else:
                p_pos = digit_probs(
                    model, to_device(with_class(second, classes, ids)), ids
                )
            for i, p in zip(need, p_pos, strict=True):
                out[i].update(
                    positive=float(p @ POSITIVE_MID), positive_probs=rounded(p)
                )
        yield out


def rank(rows: list[dict], score) -> tuple[tuple[float, int], tuple[float, int]]:
    """Pairwise accuracy and pairs over games of one year and genre: all, then clear."""
    true = np.log1p([r["reviews"] for r in rows])
    groups = [(r["year"], primary_genre(r)) for r in rows]
    score = np.asarray(score, dtype=float)
    return (
        pairwise(score, true, groups),
        pairwise(score, true, groups, CLEAR_RATIO, math.log1p(10)),
    )


def pooled(results: list[tuple[float, int]]) -> tuple[float, int]:
    total = sum(n for _, n in results)
    hits = sum(acc * n for acc, n in results if n)
    return (hits / total if total else float("nan")), total


def metrics(rows: list[dict], preds: list[dict]) -> dict:
    """Ranking and % error per screenshot and for the mean over a game's screenshots.

    Per screenshot, the k-th screenshots of all games (k = 0..3) are ranked against each
    other and the pairs of these rankings are pooled; ranking every screenshot against
    every other would hold all pairs of a big group in memory at once.
    """
    if not preds:
        return {"games": 0, "screenshots": 0}
    by_game = defaultdict(list)
    for p in preds:
        by_game[p["appid"]].append(p)
    for shots in by_game.values():
        shots.sort(key=lambda p: p["shot"])
    rows = [r for r in rows if r["appid"] in by_game]
    slots = []
    for k in range(max(len(v) for v in by_game.values())):
        part = [r for r in rows if len(by_game[r["appid"]]) > k]
        slots.append(rank(part, [by_game[r["appid"]][k]["review_class"] for r in part]))
    shot_all, shot_clear = pooled([a for a, _ in slots]), pooled([c for _, c in slots])
    game_all, game_clear = rank(
        rows, [np.mean([p["review_class"] for p in by_game[r["appid"]]]) for r in rows]
    )
    truth = {r["appid"]: r for r in rows}
    exact = np.mean(
        [
            int(np.argmax(p["review_probs"]))
            == bucket(truth[p["appid"]]["reviews"], REVIEW_EDGES)
            for p in preds
        ]
    )
    shot_errors = [
        abs(p["positive"] - truth[p["appid"]]["positive"])
        for p in preds
        if "positive" in p
    ]
    game_errors = [
        abs(np.mean([p["positive"] for p in by_game[r["appid"]]]) - r["positive"])
        for r in rows
        if scored(r)
    ]

    def r4(x: float) -> float:
        return round(float(x), 4)

    return {
        "games": len(rows),
        "screenshots": len(preds),
        "pairwise": r4(shot_all[0]),
        "pairs": shot_all[1],
        "pairwise_clear": r4(shot_clear[0]),
        "clear_pairs": shot_clear[1],
        "slot_pairwise": [r4(a[0]) for a, _ in slots],
        "game_pairwise": r4(game_all[0]),
        "game_pairs": game_all[1],
        "game_pairwise_clear": r4(game_clear[0]),
        "game_clear_pairs": game_clear[1],
        "review_answer_exact": r4(exact),
        "positive_mae": round(float(np.mean(shot_errors)), 2) if shot_errors else None,
        "game_positive_mae": round(float(np.mean(game_errors)), 2)
        if game_errors
        else None,
        "positive_games": len(game_errors),
    }


def cmd_evaluate(args: argparse.Namespace) -> None:
    """Writes predictions as it goes; a rerun after a stop continues from them."""
    from peft import PeftModel

    rows = split_games(load_rows(Path(args.labels)), args.split, args.games, args.seed)
    run = Path(args.run)
    label = args.adapter
    state = run / args.adapter / "state.json"
    if state.exists():  # last/ moves on as training goes on
        label += f"-{json.loads(state.read_text(encoding='utf-8'))['step']}"
    name = f"eval_{args.split}_{label}_{args.shot}" + (
        f"_{args.games}" if args.games else ""
    )
    run.mkdir(parents=True, exist_ok=True)
    path = run / f"{name}_predictions.jsonl"
    preds = []
    if path.exists():
        with path.open(encoding="utf-8") as fh:
            preds = [json.loads(line) for line in fh if line.strip()]
    done = {(p["appid"], p["shot"]) for p in preds}
    items = eval_items(rows, args.shot, Path(args.shots))
    on = len(items)
    items = [(r, s) for r, s in items if (r["appid"], s) not in done]
    print(
        f"{name}: {len(rows)} games, {on} screenshots on disk, {len(done)} done,"
        f" {len(items)} to go",
        flush=True,
    )
    if items:
        stopper = Stopper()
        processor, model = load_model(args.model)
        ids = token_ids(processor)
        if args.adapter != "base":
            model = PeftModel.from_pretrained(model, run / args.adapter)
        model.eval()
        with path.open("a", encoding="utf-8") as fh:
            for part in predict(
                processor,
                model,
                items,
                Path(args.shots),
                ids,
                args.eval_batch,
                stop=lambda: bool(stopper.reason),
                workers=args.workers,
                name=args.model,
            ):
                fh.write("".join(json.dumps(p) + "\n" for p in part))
                fh.flush()
                preds += part
        if stopper.reason:
            print(f"stopped ({stopper.reason}) at {len(preds)} screenshots", flush=True)
            return
    result = metrics(rows, preds)
    result.update(split=args.split, adapter=label, shot=args.shot)
    (run / f"{name}.json").write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(json.dumps(result), flush=True)


# ---------------------------------------------------------------- recognition


def normalize(name: str) -> str:
    name = re.sub(r"[™®©]", "", name.lower())
    return re.sub(r"\s+", " ", re.sub(r"[^\w]+", " ", name)).strip()


def recognized(true_name: str, said: str) -> bool:
    said = normalize(said)
    full = normalize(true_name)
    main = normalize(re.split(r":| - | – ", true_name)[0])
    return bool(said) and (full in said or (len(main) >= 4 and main in said))


@torch.inference_mode()
def cmd_recognize(args: argparse.Namespace) -> None:
    rows = [
        r
        for r in load_rows(Path(args.labels))
        if r["shots"] and on_disk(Path(args.shots), r["appid"], game_shots(r)[0])
    ]
    famous = sorted(rows, key=lambda r: -r["reviews"])[: args.games]
    rest = [r for r in rows if r not in famous]
    rng = random.Random(1)
    groups = {"famous": famous, "random": rng.sample(rest, min(args.games, len(rest)))}
    processor, model = load_model(args.model)
    report = {}
    for name, group in groups.items():
        answers = []
        for start in range(0, len(group), 8):
            part = group[start : start + 8]
            texts = [chat(processor, RECOGNIZE_QUESTION) for _ in part]
            images = [
                load_image(Path(args.shots), r["appid"], game_shots(r)[0]) for r in part
            ]
            enc = encode(processor, texts, images)
            out = model.generate(**enc, max_new_tokens=24, do_sample=False)
            said = processor.batch_decode(
                out[:, enc["input_ids"].shape[1] :], skip_special_tokens=True
            )
            answers += [
                {
                    "appid": r["appid"],
                    "name": r["name"],
                    "reviews": r["reviews"],
                    "said": s.strip(),
                    "hit": recognized(r["name"], s),
                }
                for r, s in zip(part, said, strict=True)
            ]
        hits = sum(a["hit"] for a in answers)
        report[name] = {"games": len(answers), "recognized": hits, "answers": answers}
        print(f"{name}: recognized {hits} of {len(answers)}", flush=True)
    Path(args.out).write_text(
        json.dumps(report, ensure_ascii=False, indent=1), encoding="utf-8"
    )


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    sub = parser.add_subparsers(required=True)

    p = sub.add_parser("recognize", help="does the plain model name the game?")
    p.add_argument("labels")
    p.add_argument("shots")
    p.add_argument("out")
    p.add_argument("--games", type=int, default=100)
    p.add_argument("--model", default=MODEL, help="Hugging Face id or directory")
    p.set_defaults(func=cmd_recognize)

    p = sub.add_parser("train")
    p.add_argument("labels")
    p.add_argument("shots")
    p.add_argument("run")
    p.add_argument("--epochs", type=int, default=1)
    p.add_argument("--batch", type=int, default=8)
    p.add_argument("--accum", type=int, default=2)
    p.add_argument("--lr", type=float, default=2e-4)
    p.add_argument("--rank", type=int, default=16)
    p.add_argument("--warmup", type=int, default=100)
    p.add_argument("--seed", type=int, default=20261120)
    p.add_argument(
        "--val-games", type=int, default=2000, help="random val games, one shot each"
    )
    p.add_argument("--eval-every", type=int, default=1000, help="optimizer steps")
    p.add_argument("--eval-batch", type=int, default=16)
    p.add_argument("--log-every", type=int, default=20)
    p.add_argument(
        "--refresh", type=int, default=400, help="batches between looks for new shots"
    )
    p.add_argument(
        "--patience", type=float, default=30.0, help="minutes to wait for missing shots"
    )
    p.add_argument("--save-minutes", type=float, default=5.0)
    p.add_argument(
        "--max-minutes", type=float, default=0.0, help="stop after this (0: no limit)"
    )
    p.add_argument(
        "--max-steps", type=int, default=0, help="stop after this step (0: no limit)"
    )
    p.add_argument(
        "--schedule-steps",
        type=int,
        default=0,
        help="the cosine ends at this step (0: at the end of the epochs)",
    )
    p.add_argument("--init", help="a new run starts from this LoRA directory")
    p.add_argument(
        "--max-temp", type=int, default=78, help="pause above this GPU °C (0: never)"
    )
    p.add_argument("--model", default=MODEL, help="Hugging Face id or directory")
    p.add_argument(
        "--workers",
        type=int,
        default=0,
        help="processes building batches (0: in place)",
    )
    p.add_argument(
        "--no-checkpointing",
        dest="checkpointing",
        action="store_false",
        help="keep the activations: faster, more GPU memory",
    )
    p.set_defaults(func=cmd_train)

    p = sub.add_parser("evaluate")
    p.add_argument("labels")
    p.add_argument("shots")
    p.add_argument("run")
    p.add_argument("--split", default="val")
    p.add_argument("--adapter", default="last", help="last, epoch_N or base")
    p.add_argument("--shot", default="all", choices=("first", "random", "all"))
    p.add_argument(
        "--games", type=int, default=0, help="a fixed random subset (0: all)"
    )
    p.add_argument("--seed", type=int, default=20261120, help="picks the subset")
    p.add_argument("--eval-batch", type=int, default=16)
    p.add_argument("--model", default=MODEL, help="the LoRA's base model")
    p.add_argument(
        "--workers",
        type=int,
        default=0,
        help="processes building batches (0: in place)",
    )
    p.set_defaults(func=cmd_evaluate)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
