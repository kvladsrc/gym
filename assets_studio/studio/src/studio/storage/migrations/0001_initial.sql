-- Initial schema; see docs/adr/0002-storage-and-queue.md.

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
  meta        TEXT NOT NULL DEFAULT '{}',
  created_at  TEXT NOT NULL
);
CREATE INDEX assets_created ON assets(created_at);

CREATE TABLE asset_tags (
  asset_id TEXT NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
  tag      TEXT NOT NULL,
  PRIMARY KEY (asset_id, tag)
);

CREATE TABLE jobs (
  id                 TEXT PRIMARY KEY,
  tab                TEXT NOT NULL,
  task               TEXT NOT NULL,
  prompt             TEXT,
  params             TEXT NOT NULL DEFAULT '{}',
  count              INTEGER NOT NULL CHECK (count >= 1),
  seed               INTEGER,
  status             TEXT NOT NULL CHECK (status IN (
                       'queued', 'waiting_model', 'running',
                       'succeeded', 'failed', 'cancelled')),
  error_code         TEXT,
  error_message      TEXT,
  retryable_failures INTEGER NOT NULL DEFAULT 0,
  model_snapshot     TEXT,
  effective_params   TEXT,
  timing             TEXT,
  idempotency_key    TEXT UNIQUE,
  retry_of           TEXT REFERENCES jobs(id),
  created_at         TEXT NOT NULL,
  started_at         TEXT,
  finished_at        TEXT
);
CREATE INDEX jobs_status ON jobs(status, created_at);

CREATE TABLE job_dependencies (
  job_id       TEXT NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
  depends_on   TEXT NOT NULL REFERENCES jobs(id),
  output_index INTEGER NOT NULL DEFAULT 0,
  input_role   TEXT NOT NULL,
  PRIMARY KEY (job_id, input_role)
);
CREATE INDEX job_dependencies_upstream ON job_dependencies(depends_on);

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

CREATE TABLE lineage (
  parent_id TEXT NOT NULL REFERENCES assets(id),
  child_id  TEXT NOT NULL REFERENCES assets(id),
  relation  TEXT NOT NULL CHECK (relation IN ('input', 'mutation')),
  job_id    TEXT REFERENCES jobs(id),
  PRIMARY KEY (parent_id, child_id, relation)
);
CREATE INDEX lineage_child ON lineage(child_id);
