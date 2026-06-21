# Blog Engine Progress

## Current Phase

Phase 1: author workflow.

The goal of this phase is to make the private author workflow useful before
implementing the checker and publisher pipeline.

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
- [x] Durable grammar check jobs.
- [x] Worker leases and grammar check status transitions.
- [x] OpenAI grammar check request path.
- [ ] Publish and recheck actions.

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

- durable job table
- worker leases and retries
- grammar check client interface
- suggested source artifact
- static HTML diff artifact
- `success` and `failed` check results
- `grammar_issues`, `api_error`, `renderer_error`, and `internal_error`
  failure reasons

### Phase 3: Publishing

- org subset renderer
- static public feed
- static post pages
- tag pages
- release prefix layout in S3
- atomic current release pointer

### Phase 4: Public Web

- S3-backed public serving
- in-memory or local-disk cache
- server-side keyword search over published documents
- plain HTML search results page

## Open Decisions

- Final service name and public hostnames.
- Exact S3 bucket names.
- PostgreSQL migration tool.
- Search index format.
- Whether unsupported org syntax blocks rendering or is escaped as text.
- OpenAI API key source for the grammar checker.
