# qwen_review

A LoRA adapter for Qwen3.5 that forecasts how many Steam user reviews a game
gets, and what share of them are positive, from one screenshot with the
game's release year, Steam genres and five user tags. It judges by the look
and the description, not by the game, so read a forecast as "games that look
like this usually get about this many reviews".

The 2B adapter is published on Hugging Face as
[zjgkkn/qwen3.5-2b-steam-reviews-lora](https://huggingface.co/zjgkkn/qwen3.5-2b-steam-reviews-lora)
(Apache-2.0), with a model card and an inference script; `hf/` holds both.
Assets Studio runs it as the
[`qwen_review` server](../assets_studio/model_servers/qwen_review/README.md).

On 3 315 games of 2026 held out from training it put 78.3% of game pairs in
the right order (same year and primary genre, review counts at least twofold
apart; chance is 50%) and named the exact review class of 34.1% of the games.
The model card has the full table.

## Files

| File | Does |
| --- | --- |
| `collect.py` | the data: game list, Steam tags and review totals, screenshots, labels and splits |
| `train.py` | trains and evaluates the LoRA; checks whether the plain model recognizes games |
| `embed.py`, `baseline.py` | the simple baseline: SigLIP2 embeddings and ridge regression |
| `hf/` | what Hugging Face carries besides the weights: the model card and `predict.py` |
| `requirements.txt` | the exact packages the adapter was trained and evaluated with |
| `data/` | everything downloaded or trained; git-ignored, never published |

## Answer

The adapter answers two class digits, such as `7 6`: the review count class
and the share-of-positive class. Under 10 reviews Steam shows no share, and
the second digit is `-`, as in `2 -`.

| Class | Reviews | Positive reviews |
| --- | --- | --- |
| 0 | 0 | under 50% |
| 1 | 1–2 | 50–59% |
| 2 | 3–9 | 60–69% |
| 3 | 10–29 | 70–74% |
| 4 | 30–99 | 75–79% |
| 5 | 100–299 | 80–84% |
| 6 | 300–999 | 85–89% |
| 7 | 1000–2999 | 90–94% |
| 8 | 3000–9999 | 95–97% |
| 9 | 10000 or more | 98–100% |

Evaluation and inference do not generate. They read the probabilities of the
ten digit tokens right after the prompt; their mean, the expected class, ranks
the games. Then they append the likeliest class from 10 reviews up and read
the share digit the same way. The prompt is `user_text` in `train.py`.

## Environment

`collect.py` needs only Python 3. The rest runs in one environment, here
`~/.cache/qwen_review/venv`, with CUDA 12.8 wheels:

```sh
uv venv --python 3.12 ~/.cache/qwen_review/venv
uv pip install --python ~/.cache/qwen_review/venv/bin/python \
  -r requirements.txt --index-strategy unsafe-best-match \
  --extra-index-url https://download.pytorch.org/whl/cu128
```

On Hopper GPUs (H100, H200) flash-linear-attention 0.6.0 computes wrong
gradients with Triton 3.6.0, the version PyTorch 2.11 pins (fla issue #640):
there, add `uv pip install --no-deps triton==3.7.1`.

Run every script with `python -I` from this directory: downloaded data is
untrusted, and `data/` must never hold code.

## Data

The game list comes from the FronkonGames Steam Games Dataset (MIT): put its
`games.json` in `data/raw/`. Everything else comes from Steam's public store
endpoints, at the paces `collect.py` sets by default. The published adapter was
trained on these steps:

```sh
python3 -I collect.py index data/raw/games.json data/games.jsonl
python3 -I collect.py sample data/games.jsonl data/full/sample.json --all \
  --shots 4 --since 2013-11-01 --last-day 2026-08-20
python3 -I collect.py fetch data/full/sample.json data/full/data \
  --no-histogram --no-steamspy --store-tags --keep-alive --shot-workers 8
python3 -I collect.py labels data/full/sample.json data/full/data \
  data/full/labels.jsonl --games data/games.jsonl
```

- `sample --all` keeps every game with a release date and screenshots, without
  playtests, demos and software. Each game gets the first screenshot of its
  store gallery and three random others: 127 465 games, 509 534 screenshots of
  600×338, about 37 GB.
- `fetch` takes 20 tags per game and the review totals from Steam's store
  service, 50 games per request, and the screenshots from Steam's image
  servers. A rerun skips the files on disk; each screenshot is written through
  a `.part` file.
- `labels` writes one JSON line per game: appid, name, year, genres, tags,
  shots, reviews, positive (null under 10 reviews) and split. The review count
  and share are the ones the store page shows. Releases from 2026 on are the
  test split (16 573 games), 5% of the others validation (5 511), the rest
  training (105 059).

The review histories (`fetch` without `--no-histogram`, `labels --reviews
window`) give the reviews of a game's first months instead, at one request per
game every 1.5 s.

## Training

`train.py train` trains on every screenshot of the training games, each an
example of its own with its game's labels; only the answer tokens count. LoRA
rank 16, alpha 32, on the attention and MLP projections of every
language-model layer; the vision tower stays frozen. It saves `RUN_DIR/last/`
every few minutes and resumes from it; SIGTERM saves and stops, and it pauses
while the GPU is hotter than `--max-temp`.

The published adapter took two runs:

```sh
python -I train.py train data/full/labels.jsonl data/full/data/shots \
  data/runs/full1
python -I train.py train data/full/labels_2026.jsonl data/full/data/shots \
  data/runs/full1_2026 --init data/runs/full1/last --lr 5e-5 --warmup 50
```

1. One epoch over the games of 2013–2025 with the defaults: Qwen3.5-2B, batch
   8 × 2 accumulation steps, learning rate 2e-4 with a cosine schedule, 26 246
   steps.
2. The same weights trained further on 2026, with a fresh optimizer. In
   `labels_2026.jsonl`, a copy of `labels.jsonl`, 80% of the 2026 games are
   `train` with one random screenshot each (`shots` lists one), and as many
   random older training games, also with one screenshot, keep the older
   years from being forgotten. The other 20% of 2026 are `val`, with all
   their screenshots; every other game gets a split name `train.py` skips.
   1 658 steps.

On an RTX 3070 Laptop GPU (8 GB) training runs at 3.8 screenshots per second:
about a day and a half for the first run, two and a half hours for the second.
`--model Qwen/Qwen3.5-4B` or `-9B` takes a bigger base with the same prompt;
`--workers` and `--no-checkpointing` speed up a big GPU.

## Evaluation

```sh
python -I train.py evaluate data/full/labels_2026.jsonl data/full/data/shots \
  data/runs/full1_2026 --split val --shot all
```

`evaluate` writes each screenshot's digit probabilities to
`RUN_DIR/eval_<split>_<adapter>_<shot>_predictions.jsonl` as it goes and
continues from them when run again; the metrics go to a JSON file next to
them. A game's forecast is the mean over its screenshots. The metrics:

- pairs: two games of the same year and primary genre whose review counts
  differ at least twofold are ordered right when the one with more reviews
  has the higher expected class;
- clear pairs: counts at least fourfold apart, the bigger game with 10 or
  more reviews;
- the exact review class and the class at most one off;
- the error of the share of positive reviews, in points, on games with 10 or
  more reviews.

`train.py recognize` asks the plain model which game a screenshot shows, to
see whether it could recall famous games instead of judging the picture.

`embed.py` and `baseline.py` give the floor: ridge regression on the year and
genres, on SigLIP2 embeddings of one screenshot, and on both, with the same
pair metric, out of fold.

## Inference

```sh
python hf/predict.py shot1.jpg shot2.jpg --year 2026 \
  --genres "Action, Indie" --tags "Roguelike, Pixel Graphics, 2D"
```

`predict.py` downloads the base model and the published adapter, or takes a
local one with `--adapter data/runs/full1_2026/last`. It averages the
screenshots of one game; `--json` prints every probability.

To publish a new version, upload `hf/README.md`, `hf/predict.py` and the
run's `adapter_config.json` and `adapter_model.safetensors`, nothing else from
the run: its optimizer state and logs stay here.
