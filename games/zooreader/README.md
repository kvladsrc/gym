# Zooreader

A local PDF reader with a small pixel-art action game in the page margins.
Move with **A / D**, **left / right arrows**, or the **mouse wheel** to set
the reading pace. Jumping and combat are automatic. Page buttons and the
page-number field also navigate directly. The Open PDF control can replace
the current book without reloading the application.

PDF parsing, fonts, worker code and image decoders are bundled locally.
No CDN or account is required to read. Optional **Hero comments** use an
OpenAI key kept for the browser session and send occasional page excerpts
to OpenAI. Leave the key empty to keep reading entirely local.

## Development

Run from the repository root:

```sh
nix develop -c just zooreader serve
nix develop -c just zooreader check
nix develop -c just zooreader test
CHROME_BIN=/path/to/google-chrome nix develop -c just zooreader smoke
```

The smoke test uses Chrome's DevTools Protocol directly, creates its own
local server and PDF fixtures, and needs no npm packages. Set `SCREENSHOT`
to preserve a browser screenshot at a chosen path. Chrome runs headless with
an isolated temporary profile and a loopback debugging endpoint.

## Structure

- `src/app.js`: PDF lifecycle, browser controls and game simulation.
- `src/render-queue.js`: serial rendering with latest-request publication.
- `src/renderer.js`: canvas presentation of the world, actors and effects.
- `src/world.js`, `combat.js`, `effects.js`: deterministic terrain and combat.
- `src/input.js`, `speech.js`: input state and optional asynchronous comments.
- `assets/`: runtime PNGs; see [the asset pipeline](ASSET_PIPELINE.md).
- `vendor/pdfjs/`: PDF.js 6.3.289 and its Apache-2.0 license, fonts, CMaps,
  WASM decoders and ICC data. `VERSION` records the npm archive integrity.

The vendored files come from the official `pdfjs-dist` npm archive
`https://registry.npmjs.org/pdfjs-dist/-/pdfjs-dist-6.3.289.tgz`.
The archive's SHA-512 was verified against npm metadata before extraction.
Only `build/pdf.min.mjs`, `build/pdf.worker.min.mjs`, `LICENSE`, `cmaps/`,
`standard_fonts/`, `wasm/`, and `iccs/` are shipped. Dependency updates must
keep the main library, worker and auxiliary data on the same version and
pass the real-browser smoke test.

## Current limits

This is still a canvas-based reader: text selection, search, PDF links,
password prompts, touch controls and saved reading positions are not
implemented. Zoom above fit-width crops the page horizontally. The game
deliberately overlays the bottom of the page; advancing reveals the text
above it. See [the review](REVIEW.md) for the changes and follow-up work.
