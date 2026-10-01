-- A counter bumped on every change of a job, so that clients can tell which of
-- two states of the same job is newer (event stream vs. API responses).
ALTER TABLE jobs ADD COLUMN version INTEGER NOT NULL DEFAULT 0;
