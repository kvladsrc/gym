-- Projects: every asset and job belongs to exactly one (docs/adr/0010-projects.md).
-- Everything made before projects existed goes to "default".
-- SQLite adds a column with a REFERENCES clause and a non-NULL default only while foreign
-- keys are off, which the migration runner does; the references are verified before commit.

CREATE TABLE projects (
  id         TEXT PRIMARY KEY,
  title      TEXT NOT NULL,
  created_at TEXT NOT NULL,
  -- Only an empty project is deleted; its finished jobs keep referring to it (ADR-004).
  deleted_at TEXT
);
INSERT INTO projects (id, title, created_at)
  VALUES ('default', 'Default', strftime('%Y-%m-%dT%H:%M:%f+00:00', 'now'));

ALTER TABLE assets ADD COLUMN project TEXT NOT NULL DEFAULT 'default' REFERENCES projects(id);
ALTER TABLE jobs ADD COLUMN project TEXT NOT NULL DEFAULT 'default' REFERENCES projects(id);
CREATE INDEX assets_by_project ON assets(project, id);
CREATE INDEX jobs_by_project ON jobs(project, id);
