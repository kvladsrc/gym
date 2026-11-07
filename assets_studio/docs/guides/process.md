# Process: ratings and the queue

<!-- markdownlint-disable MD013 -->

## Ratings

- **Rate 0–5 in Studio; compare by tag.** Tag the variants with what differs
  between them (model, style, prompt) and compare averages by tag and by
  combination. Paired model comparisons on the same prompt are fairer than
  averages. Ratings mix technical quality (artifacts) and taste (style); look
  for trends, not tenths of a point.
- **Rate images and 3D separately:** the best image does not always produce
  the best model (Qwen's fine details are one reason; see [3d.md](3d.md)).
- **Regenerate based on ratings:** rewrite 1–2 results to address the cause
  (porous → large masses; monotonous → individual object colors); analyze
  4–5 results and fold what worked into the prompts of the whole set.

## Queue and model servers

- `launcher` starts servers for queued jobs and keeps one model on GPU. A
  loaded server runs all ready jobs for it, even those added after other
  models' jobs: A, B, A, B, A, B requires two starts, not six. Switching is
  unavoidable only when the next A job waits for B's result (one asset's
  chain). Multiple chains are still batched by stage: all images, then all
  shapes, and so on.
- **Use dependencies for chains:** image → shape → paint → rig as one job set,
  choosing a variant at the end.
- Studio cancels waiting jobs only, not running ones.
- The API lists at most the latest 1,000 jobs. The producing job (prompt,
  fully resolved parameters, seed, inputs, model) is available at
  `GET /api/assets/{id}/job`, in `get_asset`'s MCP `job_id`, and in the library's
  “How it was made” block (including prompt and full JSON request copying).
- Library views: “Grid” or “Large” (as in a section: one asset fills the viewer,
  filtered items form a strip below, ← → navigates it; double-click a grid
  card to open it large).

## Memory (measured with 30 GB RAM and 8 GB VRAM)

- Heavy stages do not fit together: Paint ~13 GB RAM (memory-mapped weights;
  otherwise peak reaches 14.6 GB and the kernel kills the server), YuE ~9 GB,
  Blender level export up to ~8.5 GB, plus open Unity and Blender instances.
  Run heavy exports with a memory watchdog that stops them before system OOM.
- No stage warns about size. A setting fine for one stage (unsimplified shape,
  657K triangles) can cripple the next (Paint unwrap takes half an hour on CPU).
  Maintain triangle budgets throughout the chain.

## Missing Studio features (open)

- Running jobs have no progress reporting (the server knows its step), and
  results arrive all at once: four 2K images showed nothing for 45 minutes.
  MCP `wait_for_job` waits silently, and Claude Code terminates tools silent
  for 300 seconds.
- A running job cannot be stopped: Cancel removes only jobs not yet sent, and
  the contract has no way to interrupt a generation.
- Imported video and audio are converted with ffmpeg inside the import
  request, without progress: a long clip holds the request until it is done.
- No backup command: copy the data directory while the studio is stopped.
- Configuration loads only at startup; adding a server requires a restart.
- Imported files have no link to their source mesh and concept.
- No heightmap, seamless texture or sky panorama mode.
- MCP `import_asset` documentation does not list all formats (FBX is missing).
