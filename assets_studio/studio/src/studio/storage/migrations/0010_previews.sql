-- A still picture of a mesh for library thumbnails, drawn by the web UI, and the
-- version of the renderer that drew it (ADR-011). NULL: not drawn yet.
ALTER TABLE assets ADD COLUMN preview_sha256 TEXT;
ALTER TABLE assets ADD COLUMN preview_version INTEGER;
