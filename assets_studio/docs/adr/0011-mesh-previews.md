# ADR-011. Mesh thumbnails as stored pictures

Status: accepted.

## Context

The library showed each GLB in its list as a live 3D view (model-viewer), so
every card loaded its whole model into a WebGL context of its own. A grid of
high-poly models (millions of triangles, tens of megabytes each) froze the
browser, and browsers limit how many WebGL contexts a page may hold.

Model servers return meshes without pictures of them. Drawing them in the
studio would add a GPU or a headless renderer to a process that has neither,
and a job per mesh. The web UI already has the viewer's renderer: three.js
with model-viewer's light and materials.

## Decision

### Storage and API

- One still picture per mesh: `assets.preview_sha256` and
  `assets.preview_version`, the version of the renderer that drew it
  (migration 0010). Pictures are WebP with transparency, stored by content
  SHA-256 like asset files, in `previews/` beside `blobs/` in the data
  directory.
- `PUT /api/assets/{id}/preview?version=N` takes a PNG or WebP picture as the
  raw request body, at most 1024 pixels a side and 8 MiB. The studio
  re-encodes it to WebP, replaces the previous picture and returns the asset.
  Only a mesh has a picture (422 otherwise); a deleted asset returns 410.
- `AssetOut.preview_url` is `/api/assets/{id}/preview?v=<sha256>`, or null
  without a picture. A new picture gets a new URL, so with the current `v`
  the browser may cache it for good; any other `v` is served `no-cache`.
- Deleting an asset removes its picture unless another asset has the same
  one.

### Drawing in the web UI

- When a GLB's thumbnail comes into view and its picture is missing or older
  than the UI's `PREVIEW_VERSION`, the page queues the model. A worker draws
  one model at a time with three.js on an `OffscreenCanvas`, and the page
  uploads the picture. A thumbnail shows a spinner while its model is drawn;
  an outdated picture stays until its replacement is ready.
- The picture matches the viewer's first view: model-viewer's neutral
  environment, tone mapping and exposure, its starting orbit (front-on, from
  15° above), and the clay look of a shape without colour
  (`meta.color = "none"`). It is cut to the model's outline with a small
  margin, transparent around it, 512 pixels on the longer side.
- A model that cannot be drawn (a broken file, nothing visible, more than two
  minutes) shows a cube and is tried again when the page is next loaded. A
  browser that cannot draw in a worker (no WebGL 2 there) shows the stored
  pictures and cubes for the rest. FBX (ADR-006) is not drawn.
- The details panel and the large view keep the live model, one at a time.

## Consequences

- The first opening of a large library draws its visible models one by one.
  Later openings only download pictures.
- Raising `PREVIEW_VERSION` (new light, framing or size) redraws pictures as
  their thumbnails are shown, not all at once.
- Whichever browser opens the library first draws the pictures. A software
  renderer (no GPU) is slower but draws the same picture.
- An agent or a script may store a picture of its own with the same `PUT`,
  for example one rendered in Blender. The UI keeps any picture whose version
  is at least its own.
