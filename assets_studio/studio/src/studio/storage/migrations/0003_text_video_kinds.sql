-- New asset kinds: text and video (docs/adr/0003-contract-text-video.md).
-- SQLite cannot change a CHECK constraint, so the table is rebuilt; the
-- runner switches foreign keys off around it and verifies them afterwards.

CREATE TABLE assets_new (
  id          TEXT PRIMARY KEY,
  kind        TEXT NOT NULL CHECK (kind IN ('image', 'mesh', 'audio', 'text', 'video')),
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
INSERT INTO assets_new (id, kind, blob_sha256, mime, size_bytes, origin, source_url, title, favorite, meta, created_at)
  SELECT id, kind, blob_sha256, mime, size_bytes, origin, source_url, title, favorite, meta, created_at FROM assets;
DROP TABLE assets;
ALTER TABLE assets_new RENAME TO assets;
CREATE INDEX assets_created ON assets(created_at);
