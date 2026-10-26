# ADR-002. Storage and job queue

Status: accepted (M1a), implemented in M1b (`studio/`). ADR-004 replaces tabs
with servers and sections.

## Context

Studio stores assets, provenance and model-server job queues (ADR-001). One
Studio process runs on one laptop. Users start model servers; Studio does not
know when they will appear and does not monitor resources.

## Decision

### Data location

The data directory is `$XDG_DATA_HOME/assets-studio` (default
`~/.local/share/assets-studio`), overridable in config. It must not be inside
Dropbox. Contents:

```text
studio.sqlite3           # metadata and queue
blobs/ab/abcdef….png     # asset files addressed by content SHA-256
```

Files are written to a temporary path in the same directory and atomically
renamed. Identical content is stored once. Files are never overwritten or modified.

### SQLite

Standard `sqlite3`, WAL mode, `foreign_keys=ON`, explicit SQL without an ORM.
Numbered migrations (`storage/migrations/NNNN_*.sql`) evolve the schema;
`PRAGMA user_version` stores its version. Timestamps are UTC ISO 8601.
IDs are ULID-like strings sortable by creation time.

```sql
CREATE TABLE assets (
  id          TEXT PRIMARY KEY,
  kind        TEXT NOT NULL CHECK (kind IN ('image', 'mesh', 'audio')),
  blob_sha256 TEXT NOT NULL,
  mime        TEXT NOT NULL,
  size_bytes  INTEGER NOT NULL,
  origin      TEXT NOT NULL CHECK (origin IN ('generated', 'upload', 'url')),
  source_url  TEXT,
  title       TEXT,
  favorite    INTEGER NOT NULL DEFAULT 0,
  meta        TEXT NOT NULL DEFAULT '{}',   -- JSON: seed, dimensions, duration…
  created_at  TEXT NOT NULL
);

CREATE TABLE asset_tags (
  asset_id TEXT NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
  tag      TEXT NOT NULL,
  PRIMARY KEY (asset_id, tag)
);

CREATE TABLE jobs (
  id              TEXT PRIMARY KEY,
  tab             TEXT NOT NULL,            -- configured tab ID
  task            TEXT NOT NULL,
  prompt          TEXT,
  params          TEXT NOT NULL DEFAULT '{}',  -- as requested
  count           INTEGER NOT NULL CHECK (count >= 1),
  seed            INTEGER,                  -- effective seed after success
  status          TEXT NOT NULL CHECK (status IN (
                    'queued', 'waiting_model', 'running',
                    'succeeded', 'failed', 'cancelled')),
  error_code      TEXT,                     -- contract or Studio code; see below
  error_message   TEXT,
  retryable_failures INTEGER NOT NULL DEFAULT 0,  -- consecutive retryable failures
  model_snapshot  TEXT,                     -- JSON: response model
  effective_params TEXT,                    -- JSON: resolved params
  timing          TEXT,
  idempotency_key TEXT UNIQUE,
  retry_of        TEXT REFERENCES jobs(id),
  created_at      TEXT NOT NULL,
  started_at      TEXT,
  finished_at     TEXT
);
CREATE INDEX jobs_status ON jobs(status, created_at);

CREATE TABLE job_dependencies (
  job_id     TEXT NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
  depends_on TEXT NOT NULL REFERENCES jobs(id),
  -- Which predecessor output to supply, and under which input role.
  output_index INTEGER NOT NULL DEFAULT 0,
  input_role   TEXT NOT NULL,
  PRIMARY KEY (job_id, input_role)
);

CREATE TABLE job_inputs (
  job_id   TEXT NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
  role     TEXT NOT NULL,
  asset_id TEXT NOT NULL REFERENCES assets(id),
  PRIMARY KEY (job_id, role)
);

CREATE TABLE job_outputs (
  job_id   TEXT NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
  position INTEGER NOT NULL,
  asset_id TEXT NOT NULL REFERENCES assets(id),
  PRIMARY KEY (job_id, position)
);

-- An input role is filled by an existing asset (job_inputs) or
-- a predecessor output (job_dependencies), never both:
-- validated at job creation.

CREATE TABLE lineage (
  parent_id TEXT NOT NULL REFERENCES assets(id),
  child_id  TEXT NOT NULL REFERENCES assets(id),
  relation  TEXT NOT NULL CHECK (relation IN ('input', 'mutation')),
  job_id    TEXT REFERENCES jobs(id),
  PRIMARY KEY (parent_id, child_id, relation)
);
```

Evolution tables (`evolutions`, `candidates`) are deferred to M5, when UI needs
will be clearer.

### Queue

```text
queued ──► waiting_model ──► running ──► succeeded
   │            │               └──────► failed ──(retry)──► new job
   └────────────┴──► cancelled
```

- The **dispatcher** is a background task in Studio. At most one job runs per
  **server address**: servers serialize requests anyway, and multiple tabs
  can point to one server (for example text-to-image and image-to-image).
- For a server, choose the earliest `created_at` job whose dependencies all
  `succeeded`. Waiting dependencies do not block later independent jobs.
  A `failed` or `cancelled` dependency makes the job `failed/dependency_failed`.
- Unreachable server or `loading`/`error` → `waiting_model`. Send once
  `/v1/info` reports `ready`.
- `busy` is normal (for example from curl or another application): return to
  `waiting_model`, without a retry limit.
- `retryable: true` errors return to `waiting_model`; three consecutive such
  failures produce `failed` with the last code and message.
- Other errors produce `failed` with contract `error_code` and `error_message`.
- Connection loss during generation (server crash) → `failed/connection_lost`.
  No automatic retry: completion is unknown and retrying might crash it again.
- No response timeout by default: generation may take minutes. A tab may
  configure `timeout_s`; expiration produces `failed/timeout`.
- At Studio startup, `running` jobs become `failed/interrupted`: their responses
  are lost, and resubmission without user knowledge could duplicate generation.
- Creation can immediately return `failed/dependency_failed` if a predecessor
  failed between request validation and insertion. API and MCP clients must
  inspect the created job's status.
- Retrying a failed job always creates a **new** job with `retry_of` (UI “Retry”
  or API); history is not rewritten.
- Cancellation is allowed in `queued` and `waiting_model`. Contract v1 cannot
  interrupt generation already submitted to a server.
- `idempotency_key`: repeating `POST /api/jobs` with the same key returns the
  existing job rather than creating another.
- Results commit in one transaction: blobs are on disk first, then `assets`,
  `job_outputs`, `lineage` and `succeeded` status are stored together.

Studio adds these `error_code` values to contract codes (ADR-001):
`dependency_failed`, `connection_lost`, `timeout`, `interrupted`,
`invalid_response` (noncompliant server response).

## Alternatives

- **ORM (SQLAlchemy).** The schema is small; explicit SQL is easier for people
  and models to read, and migrations are plain SQL files.
- **Separate worker process with leases/heartbeats.** Needed when Studio
  launched models. With one process, `running` after restart unambiguously
  means interruption.
- **Human-readable asset paths beside the database** (`assets/<name>.png`).
  Content addressing avoids name collisions and duplicates; export provides
  readable names.
