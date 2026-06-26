# Blog Engine Progress

## Current Phase

Phase 3: publishing.

The goal of this phase is to finish the static public blog surface while
keeping the author workflow and worker pipeline small.

## Status

- [x] Architecture concept captured in `CONCEPT.md`.
- [x] One Go binary with component commands.
- [x] Shared config loader.
- [x] Author component skeleton.
- [x] Worker component skeleton.
- [x] Public web component skeleton.
- [x] Migration command skeleton.
- [x] Bazel-native local check path.
- [x] Bazel targets for the initial Go packages.
- [x] Initial unit tests for config, CLI validation, and HTML handlers.
- [x] Dev image build and push script.
- [x] Dev Helm chart with author, worker, and web components.
- [x] Local development runbook.
- [x] Terraform PostgreSQL and MinIO resources.
- [x] Flux GitOps app wiring and image automation.
- [x] Zuul check, build, and promote jobs for blog-engine.
- [x] Zuul config split into `zuul.d/`.
- [x] Author create form.
- [x] Posts table with clickable titles.
- [x] Post detail page shell with check artifacts and actions.
- [x] Immutable post revision records.
- [x] Edit flow that creates a new revision.
- [x] Pending-check worker and grammar check status transitions.
- [x] OpenAI grammar check request path.
- [x] Recheck action.
- [x] Manual publish queue and database publish marker.
- [x] Static public rendering and upload.
- [x] Published post metadata and author UI public links.
- [x] Static main feed with recent post excerpts.
- [x] Static tag feeds with recent post excerpts.

## Milestones

### Phase 0: Skeleton

- `blog-engine author --config=...`
- `blog-engine worker --config=...`
- `blog-engine web --config=...`
- `blog-engine migrate --config=...`
- typed YAML config loaded by every command
- health endpoints for HTTP components
- minimal validation in tests
- Bazel targets for the binary, libraries, and tests
- unit tests for current skeleton behavior

### Phase 1: Author Workflow

- create post form
- posts table with latest revision status
- post detail page
- immutable revision records
- local PostgreSQL schema and migrations
- S3 object layout for source artifacts

### Phase 0.5: Local And GitOps Development

- build and push `registry.your.domain/blog-engine:latest`
- install the chart into `blog-engine-dev`
- mount one Secret-backed config file into every component
- run `author`, `worker`, and `web` from the same image
- use port-forwarding for local access
- reconcile the production app through Flux
- promote `prod-*` image tags through Zuul

### Phase 2: Grammar Check Pipeline

- pending-check post polling
- pending-publish post polling
- grammar check client interface
- suggested source artifact
- static HTML diff artifact
- `success` and `failed` check results
- `grammar_issues`, `api_error`, `renderer_error`, and `internal_error`
  failure reasons

### Phase 3: Publishing

- org subset renderer
- rendered HTML uploaded as flat S3 objects
- linked-list state and placeholder pages
- web reverse proxy for public S3 objects
- static main feed page
- static post pages
- static tag feed pages

### Phase 4: Public Web

- S3-backed public serving
- in-memory or local-disk cache

## Open Decisions

- Final service name and public hostnames.
- Exact S3 bucket names.
- PostgreSQL migration tool.
- Whether unsupported org syntax blocks rendering or is escaped as text.
- OpenAI API key source for the grammar checker.
