# Blog Engine Concept

## Goal

Build a minimal personal blog engine that publishes static HTML and
CSS from a small org-mode subset. The public blog must not require
JavaScript. The author workflow should feel like a lightweight CI/CD
pipeline for text: submit a post, run checks, review the result, and
publish.

The first implementation is intentionally small:

- Go codebase.
- One binary.
- One container image.
- Multiple runtime commands selected by the first CLI argument.
- One config file mounted into every component.
- PostgreSQL and S3 for the author pipeline.
- S3 only for the public web component.
- Grammar check only for the first LLM-backed check.

## Runtime Shape

The binary is invoked as:

```sh
blog-engine <cmd> --config=/path/to/config.yaml
```

Initial commands:

- `author` - private author UI and HTTP API.
- `worker` - background checker, renderer, and publisher.
- `web` - public blog server.
- `migrate` - database migrations.

All commands use the same config file. Component-specific sections are
allowed, but configuration should have one obvious source of truth
instead of being spread across flags and environment variables.

Secrets are part of the same config file for simplicity. In Kubernetes
this can be implemented as a single mounted Secret or a generated
config file assembled from Secret material.

## Components

### Author

The author component is private and owns the editing workflow. It uses
PostgreSQL for metadata and workflow state, and S3 for submitted
source files, suggested versions, diffs, and generated artifacts.

Navigation:

- `posts`
- `create`

The `posts` page shows one table with the latest revision for each
post:

```text
Title | Tags | Status | Latest check | Updated
```

The row is clickable. Opening a row shows the detailed post page with
actions, check result, original text, suggested text, and diff.

The `create` page contains:

- title input
- tags input
- source text area
- `Publish after checks pass` checkbox
- `Post` button

Editing an existing post opens the same form pre-filled from the
latest revision. Submitting an edit creates a new immutable revision
and starts a new check run. If the post is already published, the
public blog keeps serving the previous published revision until a
newer revision is explicitly published.

### Worker

The worker processes posts whose latest revision is marked `pending`
in PostgreSQL.

The first check is grammar-only:

- send the org source to the LLM grammar checker
- ask only for necessary grammar fixes, not stylistic rewriting
- store the suggested text if changes are needed
- generate a server-rendered HTML diff
- update post status

For a successful check:

- if `Publish after checks pass` is false, the post becomes `ready`
- if `Publish after checks pass` is true, the revision becomes
  `pending_publish`

For a failed check:

- grammar issues make the post `needs_changes`
- API, renderer, or internal errors make the post `error`

### Web

The public web component serves only published content. It must not
connect to PostgreSQL. Its only durable dependency is S3.

Responsibilities:

- serve published static HTML and CSS
- serve post pages
- serve the main feed page
- serve tag pages

The web component may keep an in-memory or local-disk cache of objects
loaded from S3, but S3 remains the source of truth.

Public pages should remain plain HTML and CSS.

## Data Ownership

PostgreSQL stores author workflow state:

- posts
- revisions
- checks

`posts` group immutable revisions and point at the revision shown in
the author table. A revision stores the full post snapshot: title,
tags, source, workflow status, and author options. Checks are
append-only records linked to revisions; `revisions.latest_check_id`
points at the current author-visible check. Check request and response
payloads are stored as JSON.

S3 stores larger immutable artifacts:

- submitted org source
- model-suggested org source
- rendered diff HTML
- rendered public HTML/CSS

The public web component is a small reverse proxy over published S3
artifacts. It maps incoming paths directly to public bucket object keys
and should not need database credentials.

## Post Statuses

Initial revision statuses:

- `pending` - a revision was submitted and is waiting for checks
- `checking` - checks are running for the revision
- `needs_changes` - grammar check found necessary changes
- `ready` - checks passed and the post is waiting for manual publish
- `pending_publish` - a revision is queued for publication
- `publishing` - the worker is publishing the revision
- `published` - a revision is currently published
- `error` - the pipeline failed for a technical reason
- `archived` - hidden from the active author workflow

Check results are intentionally simple:

- `success`
- `failed`

The failed result should still carry a reason so the UI can
distinguish text issues from infrastructure problems:

- `grammar_issues`
- `api_error`
- `renderer_error`
- `internal_error`

## Author Detail Page

Opening a post from the table shows a detail page instead of putting
all actions in the table.

Sections:

- metadata and status
- actions
- check summary
- original text
- suggested text, when available
- diff, when available

Actions:

- `Edit original`
- `Edit suggested`, when a suggested version exists
- `Recheck`
- `Publish`
- `Archive`

`Edit suggested` opens the create/edit form with the model-suggested
text as the source. Saving it creates a new revision. Suggested text
is never applied implicitly.

## Diff Rendering

Diffs are generated by the server or worker and rendered as static
HTML. No JavaScript is required.

Example shape:

```html
<pre class="diff">
<span class="diff-line same">  This line is unchanged.</span>
<span class="diff-line del">- This are incorrect.</span>
<span class="diff-line add">+ This is incorrect.</span>
</pre>
```

The author UI can style additions and deletions with CSS.

## Public Blog

The public blog is a minimal rendered site:

- main feed with recent posts
- individual post pages
- tag pages
- RSS or Atom feed later

No calendar archive is planned for the MVP.

Tags are clickable and lead to statically generated tag pages:

```text
/tags/go/
/tags/distributed-systems/
```

## Org-Mode Subset

The first renderer supports only a small subset:

- headings
- bold
- italic
- links
- paragraphs

Unsupported org syntax should either be escaped as plain text or
reported as a renderer error. The initial preference is to fail
clearly when unsupported syntax would produce surprising output.

## Publishing Model

Published post files are immutable after their real post content is
written. Feed files and preallocated placeholder files are mutable static
artifacts that are rewritten when new posts are published.

The public bucket uses a flat linked-list layout:

```text
index.html
posts/<key>.html
tags/<tag>/<key>.html
tags/<tag>/index.html
state.json
```

`index.html` is the static main feed with recent posts. Each
`tags/<tag>/index.html` is the static tag feed for that tag. Feed
entries contain the post title, date, tags, public URL, and an excerpt.
`state.json` is a publisher implementation detail and stores the
current head, reserved next key, and feed entries for the global list
and each tag list.

Each rendered post links left to the reserved future key and right to
the previous head. When the next post is published, it overwrites that
future placeholder with the real post content and creates a new
placeholder. Older real post files do not need to be rewritten.

Example global flow:

```text
publish A0: posts/A1.html (A0 copy) <- posts/A0.html <-> END
publish A1: posts/A2.html (A1 copy) <- posts/A1.html <-> posts/A0.html <-> END
```

Tag lists use the same mechanism under `tags/<tag>/`. The database
stores only workflow state and selected revision pointers, not rendered
public HTML.

## Open Questions

- Exact project name and public domain.
- Whether `web` should cache S3 content in memory only or also on
  local disk.
- Exact PostgreSQL schema and job lease implementation.
- Exact grammar-check prompt and response schema.
