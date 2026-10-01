-- Sections by result kind, a model chosen inside (docs/adr/0004-sections-and-deletion.md).

-- A job names its model server; a tab used to be exactly one server.
ALTER TABLE jobs RENAME COLUMN tab TO server;

-- The last /v1/info of each server, so its tasks are known while it is down.
CREATE TABLE servers (
  id      TEXT PRIMARY KEY,
  info    TEXT NOT NULL,
  seen_at TEXT NOT NULL
);

-- Deleted assets leave the library; jobs and lineage keep referring to them.
ALTER TABLE assets ADD COLUMN deleted_at TEXT;
