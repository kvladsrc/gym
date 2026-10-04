-- Ratings (0-5) and tags on assets, to compare the variants of a sweep
-- (style, genre, models) and keep the good ones (ADR-007).
ALTER TABLE assets ADD COLUMN rating INTEGER CHECK (rating BETWEEN 0 AND 5);
CREATE INDEX asset_tags_by_tag ON asset_tags (tag);
