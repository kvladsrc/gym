-- A new asset kind: body motion and poses, SMPL-X or SOMA (docs/adr/0009-motion-kind.md).
-- The table is rebuilt for the CHECK constraint, as in 0003. Existing native motions are text
-- until the studio retypes them by content after this migration (Library.retype_motions).

CREATE TABLE assets_new (
  id          TEXT PRIMARY KEY,
  kind        TEXT NOT NULL CHECK (kind IN ('image', 'mesh', 'audio', 'text', 'video', 'motion')),
  blob_sha256 TEXT NOT NULL,
  mime        TEXT NOT NULL,
  size_bytes  INTEGER NOT NULL,
  origin      TEXT NOT NULL CHECK (origin IN ('generated', 'upload', 'url')),
  source_url  TEXT,
  title       TEXT,
  favorite    INTEGER NOT NULL DEFAULT 0,
  meta        TEXT NOT NULL DEFAULT '{}',
  created_at  TEXT NOT NULL,
  deleted_at  TEXT,
  rating      INTEGER CHECK (rating BETWEEN 0 AND 5)
);
INSERT INTO assets_new (id, kind, blob_sha256, mime, size_bytes, origin, source_url, title,
                        favorite, meta, created_at, deleted_at, rating)
  SELECT id, kind, blob_sha256, mime, size_bytes, origin, source_url, title,
         favorite, meta, created_at, deleted_at, rating FROM assets;
DROP TABLE assets;
ALTER TABLE assets_new RENAME TO assets;
CREATE INDEX assets_created ON assets(created_at);
