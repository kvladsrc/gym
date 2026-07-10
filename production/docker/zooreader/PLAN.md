# Zooreader Application Plan

Temporary working plan for turning the current Zooreader proof of concept into
a small, usable application.

## Current State

- Static nginx image with a vanilla JS/canvas app split into ES modules.
- User uploads a local PDF in the browser; PDF.js renders pages to an
  offscreen canvas.
- A game canvas overlays the PDF. Player horizontal position controls PDF
  scroll position.
- Page-local terrain and enemies are generated deterministically from page
  number.
- Flux/Zuul image delivery exists separately from the application code.

## Product Direction

Zooreader should become a readable PDF viewer with a light game layer, not a
game that makes reading harder. The game layer should be close to a
zero-player auto-runner:

- the user's primary action is controlling reading pace by running left/right;
- running is scrolling;
- traversal challenges should not require platforming precision;
- the hero crosses terrain and resolves enemies automatically with an
  expanding arsenal.

## Near-Term Goals

1. Make movement feel stable and reader-first.
   - Remove jump as a required reading interaction.
   - Support `A`/`D` in addition to left/right arrows.
   - Keep generated terrain traversal deterministic for any left/right input.
2. Add basic animation and feedback.
   - Four-frame running cycle, idle bob, movement squash/stretch, attack
     overlays, projectile feedback, and impact flashes.
   - Keep action states as single readable poses; only running needs a short
     generated cycle because canvas motion alone does not convey the stride.
3. Introduce the zero-player combat loop.
   - Keep world actors split from terrain.
   - Give the hero a tiny automatic arsenal: melee, flamethrower, grenade,
     rifle.
   - Use cooldowns and range checks instead of direct user actions.
4. Add hero speech bubbles.
   - Show short retro action-hero one-liners about the current page as text.
   - Avoid direct quotes from existing characters; keep the phrasing original.
   - Keep bubbles brief and infrequent so they do not interrupt reading.
5. Improve PDF reader ergonomics.
   - Show keyboard help after upload.
   - Add explicit previous/next page controls.
   - Preserve player/page position when resizing.
6. Reduce production surprises.
   - Vendor PDF.js or add a small build step instead of relying on CDN.
   - Add a basic local smoke path for the static app.

## Larger Design Questions

- Should page progress be controlled by player position, keyboard scroll, or a
  hybrid mode?
- How much visual combat can happen without distracting from reading?
- What arsenal progression is useful: page-based, time-based, or document-based?
- Should page bubble text be deterministic, locally generated, or backed by a
  small service that summarizes page text?
- Should game state be persisted per PDF in local storage?
- Should generated terrain/enemies be based on PDF content, page number, or a
  separate document seed?

## Proposed CL Sequence

1. Movement baseline: stable left/right running and deterministic traversal.
2. Visual feedback: simple animation state, attack/impact effects.
3. Zero-player combat: enemies, weapon cooldowns, and simple targeting.
4. Hero speech bubbles: extracted page text and short non-blocking one-liners.
5. Reader controls: help text, page buttons, resize behavior.
6. App hardening: local PDF.js assets and static-app smoke test.
7. Structure cleanup: continue extracting systems as the vanilla JS app grows.

## Acceptance Bar For The MVP Upgrade

- Terrain does not require manual jumping or timing.
- Hero speech bubbles are short, optional, and never cover the main reading
  area.
- User can understand controls without reading source code.
- The app does not depend on a third-party CDN at runtime.
