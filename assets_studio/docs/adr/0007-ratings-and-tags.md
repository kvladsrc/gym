# ADR-007. Asset ratings and tags

Status: accepted.

## Context

Searching combinations (style × genre × image model × 3D model) produces dozens
of variants per asset. These need ratings and filtering by search axes;
a favorite flag alone is insufficient.

## Decision

- `assets.rating`: integer 0–5 or `NULL` (unrated), migration 0006.
  `PATCH /api/assets/{id}` with `"rating": n`; explicit `null` clears the
  rating, while an omitted field leaves it unchanged.
- Tags use `asset_tags` (present since 0001 but unused), now exposed through
  the API: `tags` in `AssetOut`; `PATCH` with `"tags": [...]` replaces all tags.
  Tags are lowercase, trimmed, up to 64 characters, conventionally
  `axis:value` (`style:cartoon`, `model:hunyuan3d`).
- `GET /api/assets?tag=…&tag=…&min_rating=n&unrated=true`: all tags must match.
- `GET /api/tags?project=…`: every tag of the library's assets with the number
  of assets that carry it, by name.
- MCP: `list_assets(tags, min_rating, unrated)`, `rate_asset`.
- UI: buttons 0–5 beside favorites, tags in asset details, library filters
  for tags and ratings. The tag filter's field lists the project's tags, most
  used first, narrowed by what is typed.

## Consequences

- Tags are assigned by whoever runs a sweep; Studio does not yet understand
  jobs with sweep axes.
