# Shot Search

Find Steam store screenshots like yours, for people and agents. Give an
image, a few images, a screenshot from earlier results or a text, and get
the nearest screenshots of other games with each game's real Steam reviews
and a review forecast for that screenshot.

- **Look** (DINOv2 with registers, on the shot in grey): how the frame is
  built. Layout, camera, depth, forms, light and dark, screen composition.
  Colour grading does not count.
- **Meaning** (SigLIP 2): what is shown. Setting, characters, genre, mood.
  English text queries search this space.
- **Both** (default): the mean of the two.
- Filters by year, Steam tags and genres, review count, and games the
  forecaster never saw; ordering by similarity, real reviews or forecast.
- An MCP server for agents and a browser UI on one port; a JSON command line.
- Runs on the CPU. Models load on the first query (about 4 GB of memory
  with all of them) and are dropped after five idle minutes.

Similarity is reported as **closeness**: 0 is the median of two random Steam
screenshots, 1 the median of two screenshots of one game, per space.

## Data

The index is built from a scoring run of
[`qwen_review`](../../qwen_review): SigLIP 2 and DINOv2 embeddings and a
forecast for every screenshot, plus the games' store data. Neither the
screenshots nor the run are part of this repository. A run folder holds:

```text
siglip/chunk_*.npz          names + emb (pooled SigLIP 2 image embeddings)
dino/chunk_*.npz            names + gray_mean (DINOv2 mean patch token, grey)
studio/*_predictions.jsonl  appid, shot, review_class, positive
```

`shotsearch build RUN SHOTS LABELS...` turns it into an index of about 2 GB
(`~/.cache/shotsearch/index`, or `$SHOTSEARCH_HOME/index`). The forecast is
the expected review-count class: 0 none, 1 = 1-2 reviews, 2 = 3-9,
3 = 10-29, 4 = 30-99, 5 = 100-299, 6 = 300-999, 7 = 1,000-2,999,
8 = 3,000-9,999, 9 = 10,000 or more. For games the forecaster trained on
(`game_seen_in_training`), it leans towards the known answer.

## Use

Requires [uv](https://docs.astral.sh/uv/) and
[just](https://just.systems/man/en/). From the repository root:

```sh
just shotsearch setup     # environment in ~/.cache/shotsearch, both models
just shotsearch build     # index from qwen_review/data
just shotsearch search frame.jpg --ranking look --sort reviews --sheet
just shotsearch serve     # http://127.0.0.1:9150/ and MCP at /mcp
```

The MCP server offers `search_screenshots`, `get_game` and `index_info`.
Each result has a local `image` path; `sheet=true` adds one labelled
contact sheet of the query and the results.

## Development

```sh
just shotsearch check     # ruff and the tests (no models, no GPU)
```
