# Local MCP services for agents

Three HTTP MCP servers on this machine serve agents in this repository.
Claude Code reads them from `.mcp.json`; an agent without MCP uses the HTTP
API or command line given for each.

They are the owner's processes, started from the service panel
(`just panel`, <http://127.0.0.1:9090/>). If one is down, ask the owner to
start it. Do not start, stop or restart them yourself (see `AGENTS.md`).

## Assets Studio: local models, asset library, ratings

`assets-studio` at <http://127.0.0.1:9000/mcp>; REST API at
<http://127.0.0.1:9000/docs>.

- **Local models.** `list_servers` shows each model server's tasks and
  parameters. `generate` queues a job and `wait_for_job` waits for it; keep
  each wait under about 100 s and call again. Results are assets with a local
  file path. A job's output can feed the next job before it exists
  (`input_jobs`). An unavailable server is just not running: submit anyway,
  since the launcher loads one GPU model at a time and jobs may wait minutes.
  Ask the owner before queueing many jobs.
- **The GPU for your own work** (training, embeddings, GPU tests): run it as
  `just --justfile assets_studio/justfile gpu-hold <command>`. The launcher's
  model finishes the jobs queued for it and stops, and no model starts until
  the command ends; the owner's jobs wait meanwhile, so keep it short.
- **Library.** Every asset and job belongs to a project (`list_projects`,
  `create_project`, `move_asset`). `import_asset` adds a local file or URL;
  `get_asset` gives the file path, metadata and the job that made it (prompt,
  parameters, inputs, seed).
- **Owner's ratings.** Ratings from 0 to 5 are set by the owner in the
  Studio UI and are the accepted measure of quality. Read them
  (`list_assets(min_rating=…)`, `get_asset`) to find approved examples and to
  learn which models and prompts work. Do not rate assets unless asked.
  `rate_asset(tags=…)` replaces all tags, so keep the existing `server:`,
  `task:` and `model:` tags.
- Before generating, read `assets_studio/docs/guides/` for proven settings,
  prompts and pitfalls; before looking at media, `.agents/media-analysis.md`.

## YAQS: game development books (RAG)

`yaqs` at <http://127.0.0.1:9033/mcp>; browser UI at the same address;
command line `just yaqs search "question"`.

- `search_knowledge` finds passages in the game development books: semantic
  and cross-language by default, `mode=lexical` for exact words. Cite the book,
  page and chunk ids that results give.
- `get_passage` reads a hit with its neighbours, `get_page` reads a page;
  `list_books` and `knowledge_stats` describe the library.
- Search the books only, the default corpus; do not use `corpus=kb` or `all`
  unless the owner asks.
- A semantic search loads the embedding model on the CPU, about 16 GB of
  RAM, unloaded again after five minutes without one. Check free memory first
  (the panel shows it), especially while Studio jobs run.

## Shot Search: Steam screenshots like ours

`shotsearch` at <http://127.0.0.1:9150/mcp>; browser UI at the same
address; command line `just shotsearch search frame.jpg --sheet`.

- `search_screenshots` takes image paths or URLs, shot ids (`APPID_NN`), or one
  English text. It returns the nearest Steam store screenshots with each
  game's real reviews and a qwen_review forecast for that screenshot.
- `ranking`: `look` (DINOv2: layout, camera, forms, light; colour ignored),
  `meaning` (SigLIP 2: what is shown) or `both`. `sort`: `similarity`,
  `reviews` or `forecast`. Filters: years, Steam tags and genres, minimum
  reviews, games the forecaster never saw.
- `sheet=true` adds one labelled contact sheet of the query and the results:
  look at it rather than at each image.
- `get_game` lists one game's screenshots; `index_info` describes the index.
- Results are references for composition and staging, never assets to copy.
  Reviews and forecasts are hints; the plan, the book and the player decide.

`.mcp.json` also lists Blender and Unity editor servers; they are outside this
note.
