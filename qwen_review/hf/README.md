---
base_model: Qwen/Qwen3.5-2B
library_name: peft
license: apache-2.0
language:
- en
pipeline_tag: image-text-to-text
tags:
- lora
- peft
- qwen3.5
- video-games
- steam
---

# Qwen3.5-2B · Steam reviews (LoRA)

A LoRA adapter for [Qwen/Qwen3.5-2B](https://huggingface.co/Qwen/Qwen3.5-2B).
From one screenshot of a game, with its release year, Steam genres and user
tags, it predicts how many Steam user reviews the game gets and what share of
them are positive.

It judges by the look and the description, not by the game: many good games
get few reviews. Read a forecast as "games that look like this usually get
about this many reviews", and compare games of one release year and genre.

Code for data collection, training and evaluation:
[kvladsrc/gym/qwen_review](https://github.com/kvladsrc/gym/tree/main/qwen_review)
on GitHub.

Play against it in [Shot Call](https://shotcall.bonfirelit.dev/), a browser
game: of two Steam games of one genre released in 2026, pick the one with
more reviews.

Not affiliated with or endorsed by Valve. Steam is a trademark of Valve
Corporation.

## Answer

Two class digits, such as `7 6`:

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

Under 10 reviews Steam shows no share, and the adapter answers `-` instead of
the second digit.

Rather than generate, read the probabilities of the ten digit tokens right
after the prompt: their mean, the expected class, is a finer forecast. For the
share, append the likeliest review class from 10 reviews up, such as `4`,
then a space, and read the digits again. `predict.py` does both.

## Prompt

The screenshot, fitted into 600×338 (never enlarged), and this user message
through the chat template with `enable_thinking=False`:

<!-- markdownlint-disable MD013 -->
```text
Game screenshot. Release year: 2026. Genres: Action, Indie. Tags: Roguelike, Pixel Graphics, 2D, Difficult, Singleplayer.
How many reviews will this game get, and what share of them will be positive? Answer with two classes from 0 to 9.
```
<!-- markdownlint-enable MD013 -->

Genres and tags are Steam's English names, tags strongest first; only five
count. Without genres or tags, write `none`.

## Use

```sh
pip install torch transformers peft pillow
python predict.py shot1.jpg shot2.jpg shot3.jpg --year 2026 \
    --genres "Action, Indie" --tags "Roguelike, Pixel Graphics, 2D"
```

One screenshot gives a noisy forecast; the script averages several, and
`--json` prints all probabilities. Tested with Python 3.12, PyTorch 2.11,
Transformers 5.23 and PEFT 0.22; fits on an 8 GB GPU in bfloat16.

## Evaluation

On games held out from training. A game's forecast is the mean over its
screenshots. A pair of games of the same year and primary genre whose review
counts differ at least twofold is ordered right when the game with more
reviews has the higher forecast; chance is 50%.

| | 2026: 3 315 games, all screenshots | 2013–2025: 2 000 games, one screenshot each |
| --- | --- | --- |
| Pairs in the right order | 78.3% | 76.9% |
| Pairs differing fourfold, the bigger game with 10+ reviews | 84.1% | 81.8% |
| Exact review class | 34.1% | 32.3% |
| Review class at most one off | 74.1% | 72.5% |
| Error of the share of positive reviews, games with 10+ reviews | 11.1 points | 11.3 points |

## Training

The games released on Steam in 2013–2026, listed by the FronkonGames Steam
Games Dataset (MIT): up to four screenshots each (Steam's 600×338
thumbnails), the release year, genres and user tags, and the review count and
share of positive reviews the store page showed in November 2026. Trained on
105 059 games of 2013–2025 and 13 258 of 2026; the data is not distributed.

LoRA rank 16, alpha 32, on the attention and MLP projections of every
language-model layer; the vision tower is frozen, and the loss counts only the
answer tokens.

## Limitations

- The review counts are a snapshot: the games of 2026 had been on sale for
  weeks or months.
- It sees one screenshot, the year, the genres and five tags; price, trailers,
  marketing and the game itself are invisible to it.
- It knows only Steam: its audience, tags and genres of 2013–2026.

## License

Apache-2.0, like the base model.
