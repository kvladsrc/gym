-- Failed and cancelled jobs can be deleted: they leave the history, while
-- retry chains and dependencies keep referring to them (ADR-004).
ALTER TABLE jobs ADD COLUMN deleted_at TEXT;
