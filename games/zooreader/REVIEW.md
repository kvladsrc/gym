# Zooreader review — September 2026

## Fixed

- PDF.js 3.11.174 loaded code from a CDN and predates a JavaScript execution
  fix. Bundle PDF.js 6.3.289, worker, fonts and decoders locally.
- Rejected page renders left the rendering flag set forever. A serial render
  queue releases its busy state even after failure.
- Rapid page changes could publish obsolete pages or keep stale player
  positions. Publish only the latest request, carrying page-entry progress
  with it.
- Rendering cleared the visible canvas before a new page was ready. Render
  into a separate canvas and swap on success.
- Failure to extract optional text failed the whole page. Keep extraction
  independent of PDF display.
- Signed seed shifts produced negative theme indices and enemy counts. Use
  unsigned shifts; test 750 page/viewport combinations.
- Crawlers wider than a platform patrolled outside its bounds. Skip narrow
  platforms and clamp initial patrol positions.
- Typing A/D in the API-key field moved the hero; lost focus could leave
  movement active. Respect editable controls and clear keys on blur/focus
  changes.
- Wheel line/page deltas were interpreted as pixels. Normalize wheel units and
  preserve browser zoom gestures.
- Resizing changed reading progress; zoom respawned the world. Preserve
  normalized progress and rebuild terrain only when needed.
- Speech could arrive for an old page or block startup when storage was
  unavailable. Cancel obsolete requests, add timeout and tolerate unavailable
  storage.
- Asset checks missed individual enemy frames and misread opaque PNG alpha.
  Inspect every animation cell and normalize alpha inspection.
- Rendering and application orchestration shared one large file. Extract scene
  rendering and asynchronous render scheduling.

The previous PDF.js issue is documented in Mozilla's
[GHSA-wgrm-67xf-hhpq advisory](https://github.com/mozilla/pdf.js/security/advisories/GHSA-wgrm-67xf-hhpq).

## Artwork

Replace all 14 runtime PNGs with a coordinated pixel-art set: red-vest hero,
teal and ochre enemies, and subdued grass, snow and sandstone platforms.
The hero uses a shared atlas for identity consistency; enemy animation
frames have reviewed crop gutters. The flyer now has square frames so its
body is not compressed horizontally by the renderer. The built-in imagegen
tool generated the source art; ImageMagick performs deterministic packing.

## Validation

- Asset workflow integration tests, runtime regression tests, PNG contracts
  and targeted repository pre-commit hooks: `just zooreader check`.
- Browser checks using a generated four-page PDF: loading, rapid navigation,
  zoom, resize and DPR changes, editable input, invalid-file recovery,
  JavaScript exceptions and external runtime requests: `just zooreader smoke`.
- Inspect the packed contact sheet and actual browser screenshot as well as
  machine checks; dimensions and hashes cannot judge animation quality.

## Follow-up priorities

1. Add a PDF text layer for selection, search and accessible document reading.
2. Add horizontal pan at larger zoom and touch reading controls.
3. Add a quiet reading mode and persistent document progress.
4. Improve combat collision detection for fast projectiles and grenade splash.
5. Validate encrypted, very large and unusual-font PDFs with dedicated fixtures.

Keep these as focused product changes. A framework migration or a complete
game-engine rewrite is not needed for the current scale of the application.
