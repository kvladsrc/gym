# ADR-004. Sections by output kind, models within sections; asset deletion

<!-- markdownlint-disable MD013 -->

Status: accepted (M8).

## Context

In ADR-002 each tab represents one model server. Two image models (SDXL, FLUX)
and two text models (Gemma, Qwen) brought this to nine tabs, making model choice
a tab choice. The user requested task-type sections with model selection inside.
Assets also could not be deleted, so failures accumulated in the library.

## Decision

### Configuration: servers only

```toml
[[servers]]
id = "flux"
url = "http://127.0.0.1:9104"
# title defaults to the model name from /v1/info
# timeout_s is unchanged (ADR-002)
```

Remove `[[tabs]]`: sections are built automatically. Old-key configurations
fail with a clear error. Existing tab IDs become server IDs, preserving job
references.

### Sections

A section corresponds to task output kind: **Images** (`image`), **3D** (`mesh`),
**Audio** (`audio`), **Text** (`text`), **Video** (`video`). Classification uses
the first `output_mime`; speech and sounds both belong to Audio. Each section
contains tasks declared by at least one server (“Text → image”, “Image → image”)
and models offering the selected task, with availability indicators. Sections
without known tasks are hidden.

Clarification: **Animations** (`animation`) is separate from 3D. Motion
generation (`…-to-motion`, `…-to-native-motion`), retargeting, trimming and weapon
attachment appear here regardless of output MIME: FBX is shared with rigs and
native motion JSON uses `text/plain`. Geometry, painting and rig preparation
stay in 3D. One server may declare tasks in both sections.

Clarification (2026-10-22): classification uses task names without an exception
list. Animations includes names ending in `motion` (`…-to-motion`,
`…-native-motion`, `…-soma-motion`, in-betweening) or `pose` (`image-to-pose`),
`retarget-…`, and `video-to-pose-observations`. The old list misclassified new
motion tasks as Text. History uses the same rules, including removed servers'
jobs. Existing assets, jobs, API and SDK need no migration; the library stays shared.

### Studio remembers servers

Capabilities come from `/v1/info`. To retain stopped models in sections,
Studio caches each server's latest response in `servers` (ID, info, timestamp),
also addressing M4's deferred persistence of last_seen across restarts. Servers
that never responded appear under “Not started yet” on the home page.

### Jobs reference servers

Jobs use `jobs.server`, formerly `jobs.tab`: a tab represented one server, so
renaming the column migrates data. API: `GET /api/servers` replaces `/api/tabs`;
`POST /api/jobs {server, task, …}`. MCP: `list_servers`, `generate(server, …)`.

### Asset deletion

`DELETE /api/assets/{id}` soft-deletes (`assets.deleted_at`). The asset disappears
from library/search and becomes a placeholder in job history. Provenance and
jobs remain intact. Delete the blob once no live asset references it. Assets
needed by queued/running jobs, as direct inputs or dependency outputs, cannot
be deleted (`409`). Creating a job with deleted inputs is rejected in the same
transaction as job insertion.

### Job deletion

`DELETE /api/jobs/{id}` hides failed or cancelled jobs (`jobs.deleted_at`,
migration 0005); other states return `409`. Like assets, jobs are marked rather
than erased: retries (`retry_of`) and dependents retain references.
`GET /api/jobs/{id}` still returns them, but `GET /api/jobs` omits them.
Deletion increments `version`; the `job` event removes the row from UI history.
Retry also soft-deletes the retried job, leaving only the new one in history.

## Consequences

- Migration 0004: `jobs.tab` → `jobs.server`, `servers` table,
  `assets.deleted_at` column.
- UI: section routes (`#/image`, `#/text`, …), form drafts per section/task,
  remembered selected model; “Send to…” targets a section accepting the asset.
- Rewrite user configuration as `[[servers]]`.

## Alternatives

- **One tab per X-to-X task** (up to nine): rejected by the user. Output-based
  sections are more compact; X-to-X evolution stays within a section.
- **Configured tab groups:** unnecessary manual setup; servers declare tasks.
- **Hard asset deletion:** breaks provenance and job history.
