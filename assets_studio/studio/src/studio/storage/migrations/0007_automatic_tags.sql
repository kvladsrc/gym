-- Generated assets carry the tags of where they came from (ADR-007):
-- server:<id>, task:<task>, model:<model id>; filled in for existing ones.
INSERT OR IGNORE INTO asset_tags (asset_id, tag)
  SELECT o.asset_id, 'server:' || lower(j.server)
  FROM job_outputs o JOIN jobs j ON j.id = o.job_id;
INSERT OR IGNORE INTO asset_tags (asset_id, tag)
  SELECT o.asset_id, 'task:' || lower(j.task)
  FROM job_outputs o JOIN jobs j ON j.id = o.job_id;
INSERT OR IGNORE INTO asset_tags (asset_id, tag)
  SELECT o.asset_id, 'model:' || lower(json_extract(j.model_snapshot, '$.id'))
  FROM job_outputs o JOIN jobs j ON j.id = o.job_id
  WHERE json_extract(j.model_snapshot, '$.id') IS NOT NULL;
