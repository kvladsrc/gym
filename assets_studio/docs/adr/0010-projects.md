# ADR-010. Projects

Status: accepted.

## Context

One library serves several games, and each game has thousands of variants,
sweeps and VLM reports. Kind, rating and tag filters (ADR-007) stop helping
when every list mixes all of them. Tags could stand in for projects, but they
are a poor fit:

- a tag is not exclusive: an asset can carry `project:a` and `project:b` at
  once, or neither;
- a job has no tags, so the queue and the history could not be filtered;
- every generation, upload and frame would have to remember to add the tag,
  and a result made without it would get lost in the mix.

## Decision

A project is a first-class record: `projects(id, title, created_at,
deleted_at)`. Every asset and every job belongs to exactly one project
(`assets.project`, `jobs.project`, foreign keys). Migration 0009 creates the
`default` project and puts everything that exists into it.

### Rules

- A job's results go to the job's project. Its inputs may come from any
  project: lineage crosses projects.
- A video frame goes to the video's project.
- Moving an asset moves only that asset. Its job stays in the project where
  it ran, and so does its history.
- Without an explicit id, the id is made from the title (Cyrillic
  transliterated: "Выучка" → `vyuchka`), with `-2`, `-3` and so on when it is
  taken. An explicit id is a slug. A rename changes only the title.
- Only an empty project can be deleted: no live assets and no unfinished
  jobs. `default` can never be deleted. Deletion is soft (ADR-004): the
  finished jobs keep the id, and the id is never given out again.
- The project is checked in the same transaction that adds a job or an asset,
  moves an asset or deletes a project, so a concurrent delete cannot leave
  orphans.

### API and MCP

- `GET/POST /api/projects`, `PATCH/DELETE /api/projects/{id}`. Conflicts
  (the id is taken, or the project is not empty) return 409; a bad title or id
  returns 422.
- Creating a job, uploading or importing takes `project`, `default` when it is
  omitted, so clients unaware of projects keep working. An unknown or deleted
  project is rejected.
- `GET /api/assets` and `GET /api/jobs` take a `project` filter and return
  every project's items without one. A filter by an id that never existed
  returns 404; a deleted project's history can still be listed.
- `PATCH /api/assets/{id}` with `project` moves an asset.
- MCP adds `list_projects`, `create_project` and `move_asset`. `generate` and
  `import_asset` take `project`, and `list_assets` filters by it.

### Web UI

The open project is chosen in the top bar and remembered in the browser. The
library, the history and the sections show only that project, and new jobs and
uploads go to it. The queue stays shared, as the GPU is: a job of another
project is labelled with that project. The input picker can show every project.
An asset is moved from the details panel. The Projects page creates, renames,
opens and deletes projects.

### Flow

A graph has a `project` (the last field, so graphs saved before it read back
in the same key order with `default`). Its jobs and their results go there.
The preflight check rejects a project the studio does not know. The asset
picker shows the graph's project by default.

## Consequences

- Tags stay for labels that cut across projects: style, stage, review state.
- One database and one queue: no project has its own storage or GPU, and a
  result can be reused in another project without copying.
- Agents that pass no project put everything into `default`, as before.
  Agent tools that create many assets (VLM reports, sweeps) should be given a
  project of their own.
