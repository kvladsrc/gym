# Qwen3.5 · Steam reviews

One game screenshot, with the game's release year, Steam genres and tags, in;
a `text/plain` report of the predicted number of Steam reviews and share of
positive ones out. It uses the existing `image-to-text` task and model-server
contract v1. Input role: `image`, PNG/JPEG/WebP. There is no prompt: the
server builds the prompt the adapter was trained with from the parameters.

Model: [Qwen/Qwen3.5-2B](https://huggingface.co/Qwen/Qwen3.5-2B) pinned to
`15852e8c16360a2fea060d615a32b45270f8a8fc`, or with `--base 9b`
[Qwen/Qwen3.5-9B](https://huggingface.co/Qwen/Qwen3.5-9B) pinned to
`c202236235762e1c871ad0ccb60c8ee5ba337b9a`, both Apache-2.0. Each has its own
LoRA adapter: rank 16, alpha 32, on every attention and MLP projection of the
language model, trained on Steam store data. The 2B's is published as
[zjgkkn/qwen3.5-2b-steam-reviews-lora](https://huggingface.co/zjgkkn/qwen3.5-2b-steam-reviews-lora)
(Apache-2.0); the 9B's is not distributed. The server reads the 2B's from
`~/.cache/assets-studio/qwen_review_2b` and the 9B's from
`~/.cache/assets-studio/qwen_review` (`adapter_config.json` and
`adapter_model.safetensors` as PEFT saves them); `QWEN_REVIEW_ADAPTER` names
another directory.

The 2B's adapter was trained further on 80% of the games released in 2026.
On the other 3 315, which neither model saw in training, the 2B put 78.3% of
game pairs in the right order and the 9B 76.9%; the 2B guessed the exact
review class of a game 34% of the time, the 9B 29% (see Checks). The 2B is
the default: more accurate, it fits whole in 8 GB of GPU memory and takes
0.25 s per screenshot instead of 3.3 s.

## What it predicts

The adapter answers two class digits, such as `7 6`.

- Review count classes: 0, 1–2, 3–9, 10–29, 30–99, 100–299, 300–999,
  1000–2999, 3000–9999, 10000 or more.
- Share of positive reviews: under 50%, 50–59%, 60–69%, 70–74%, 75–79%,
  80–84%, 85–89%, 90–94%, 95–97%, 98–100%. Under 10 reviews the adapter
  answers `-`, like the store page.

The server reads the probabilities of the ten digits right after the prompt.
Then it appends the most likely review class from 10 reviews up and reads the
share digit, as the evaluation did. The report gives the likeliest review
class, the mean class, the chance of each class and the expected share of
positive reviews. The output's metadata hold all probabilities.

The screenshot is fitted into 600×338, the size of the Steam thumbnails the
adapter saw in training; it is never enlarged. Genres and tags are Steam's
English names; only the first five tags count, as in training. In the
evaluation a game's forecast was the mean over four screenshots; one
screenshot gives a noisier forecast.

## Install and run

From `model_servers/`:

```sh
just setup qwen_review
just doctor qwen_review
just qwen_review
```

Download the 2B adapter first, with the `hf` command of the server's
environment:

```sh
~/.cache/assets-studio/venvs/qwen_review/bin/hf download \
  zjgkkn/qwen3.5-2b-steam-reviews-lora adapter_config.json \
  adapter_model.safetensors --local-dir ~/.cache/assets-studio/qwen_review_2b
```

The server listens on `127.0.0.1:9146`; `--port` overrides it. Add the
`qwen_review` entry from `studio.example.toml` to your Studio config and
restart Studio and the launcher. It appears in Text; the launcher starts it
when a job needs it and stops it before switching to another GPU model.

For the 9B, in this directory: `just download 9b` fetches its base model
(~19 GB), its adapter goes into `~/.cache/assets-studio/qwen_review`,
`just doctor 9b` checks it and `just serve --base 9b` starts it.

The isolated environment lives in `~/.cache/assets-studio/venvs/qwen_review`
with the PyTorch, Transformers and flash-linear-attention versions the adapter
was trained with. The Hugging Face cache holds the base model (~4.6 GB for the
2B, ~19 GB for the 9B).

## Memory

The 2B's bfloat16 weights take 4.5 GB and fit whole: all 24 decoder layers
stay on the GPU and nothing is streamed.

The 9B's take 17.5 GB, the GPU of a laptop 8 GB. The server memory-maps the
checkpoint shards: whatever is not on the GPU costs page cache, which the
kernel can reclaim, instead of resident RAM.

- On the GPU: the vision tower, the final norm, the LoRA (160 MB, float32)
  and the first decoder layers that fit while 1.5 GiB stays free.
  `--gpu-layers N` keeps at most N of the 32 layers there.
- Each remaining layer is copied to the GPU before its forward pass and
  dropped after it.
- The embedding table (1.9 GB) stays mapped; its rows are looked up on the
  CPU. Of the output head only the ten digit rows are loaded.

## Parameters

- `year`: the release year, 2026 by default, 1980–2035.
- `genres`: Steam genres, comma-separated, e.g. `Action, Indie`; empty means
  none.
- `tags`: Steam user tags, strongest first, comma-separated; five count.
- `language`: the report's language, `ru` (default) or `en`; the metadata
  are the same.

Reading is deterministic and seed independent; `count` is one.

For agents, import the screenshot and submit an ordinary Studio job:

```json
{
  "server": "qwen_review",
  "task": "image-to-text",
  "inputs": {"image": "IMPORTED_ASSET_ID"},
  "params": {"year": 2026, "genres": "Action, Indie",
             "tags": "Roguelike, Pixel Graphics, 2D", "language": "en"},
  "count": 1,
  "seed": 42
}
```

This is the body of `POST /api/jobs`. The existing MCP `generate` tool
uses the same server/task and `input_assets={"image": "IMPORTED_ASSET_ID"}`.

## Checks

```sh
just test qwen_review  # from model_servers/
```

These tests cover the contract, the prompt, the image size, the two reads,
the report and the offload hooks on tiny modules, without weights or GPU.

On 2026-11-21 the loader (memory-mapped weights, half the decoder layers
streamed, the LoRA by hooks, the digit head, the embedding on the CPU) gave
bit-for-bit the digit probabilities of `from_pretrained` with PEFT, on the
CPU, on Qwen3.5-2B with a 2B adapter of the same layout.

The same day on an RTX 3070 Laptop GPU (8 GB) with 32 GB of RAM the 9B
model loaded in 16 s with 12 of the 32 decoder layers on the GPU. A
screenshot took 3.3 s; the first one after the load took 40 s. Peak GPU memory
was 6.5 GiB (6.7 GiB in `nvidia-smi`), resident RAM 1.5 GiB, page cache of
the mapped weights 11–12 GiB. On eight screenshots of 2026 games the digit
probabilities differed from the evaluation run on an H100 by at most 0.028,
and the likeliest class was the same on all eight. `model_server_sdk.check`
passes; its only warning is that the seed does not change the output.

On 2026-11-22, on the same laptop, `--base 2b` loaded in 11 s with all 24
decoder layers on the GPU. A screenshot took 0.25 s. Peak GPU memory was
3.3 GiB (3.7 GiB in `nvidia-smi`), resident RAM 1.5 GiB, page cache 3.7 GiB.
On eight screenshots of 2026 games the digit probabilities differed from the
2B's batched evaluation run by at most 0.027, and the likeliest class was the
same on all eight. The contract check passes with the same single warning.
Later that day the adapter trained further on 2026 replaced that one. Through
the Studio, on four held-out screenshots of 2026 games, its digit
probabilities differed from its own batched evaluation run by at most 0.026;
the likeliest class was the same on three, and on the fourth the evaluation
had the two likeliest classes tied.

The first 2B adapter was trained, like the 9B's, on the games of 2013–2025.
Both were evaluated on the same 66 288 screenshots of the 16 573 games of
2026: four per game, three and one for two of them. A game's forecast is
the mean over its screenshots. Game pairs from the same year and primary
genre whose review counts differ at least twofold were put in the right
order 77.15% of the time by the 9B and 76.52% by the 2B: 0.64 points apart,
0.38–0.91 in a 95% bootstrap over games. The exact review class of a game:
29.2% and 29.0%. The mean absolute error of the share of positive reviews, on
games with 10 or more reviews: 11.4 and 11.3 points.

Then the 2B's adapter was trained for 1 658 more steps (learning rate 5e-5)
on one screenshot of each of 13 258 games of 2026 and as many older games, so
that the older years are not forgotten. The other 3 315 games of 2026 (13 260
screenshots) were held out. On them the adapter trained further put 78.30% of
pairs in the right order, the first 2B 76.45% and the 9B 76.90%: 1.85 points
more than the first 2B (1.37–2.33 in a 95% bootstrap over games) and 1.40
more than the 9B (0.75–2.13). The exact review class: 34.1%, 28.3% and 28.7%.
The error of the share of positive reviews, on 1 422 games with 10 or more
reviews: 11.1, 11.6 and 11.7 points. The mean predicted class was 2.34, 2.82
and 2.83 against the true 2.36: the first adapters overrated the young games
of 2026 by half a class. On 2 000 validation games of 2013–2025 the adapter
trained further put 76.9% of pairs in the right order, the first one 76.7%.
