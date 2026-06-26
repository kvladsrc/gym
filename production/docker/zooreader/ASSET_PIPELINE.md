# Zooreader Hero Asset Pipeline

This document defines the first production contract for the hero sprite assets.
The existing `player*.png` files are placeholders and are not visual reference.
Only their filenames and sprite-sheet dimensions are part of the current app
contract.

## Runtime Contract

Hero states are loaded from `src/assets.js` and selected in `src/app.js`.
Each animated state is a horizontal PNG sprite sheet with 4 frames.

| State | File | Size | Frame size |
| --- | --- | --- | --- |
| Idle | `assets/player.png` | 96x96 | 96x96 |
| Run | `assets/player-run.png` | 384x96 | 96x96 |
| Jump | `assets/player-jump.png` | 384x96 | 96x96 |
| Jetpack | `assets/player-jetpack.png` | 384x96 | 96x96 |
| Grenade throw | `assets/player-grenade.png` | 384x96 | 96x96 |
| Flamethrower fire | `assets/player-flamethrower.png` | 384x96 | 96x96 |
| Pistol fire | `assets/player-pistol.png` | 384x96 | 96x96 |
| 360 knife attack | `assets/player-knife.png` | 384x96 | 96x96 |

The canvas renderer mirrors the sprite when the hero faces right. Author the
base frames facing left so existing rendering stays consistent.

## Visual Direction

The hero should read as an original retro action-game protagonist inspired by
the broad archetype of Duke Nukem: confident stance, blond flat-top hair or
similar high-contrast hair mass, dark sunglasses, exposed arms, heavy boots,
compact weapons, and exaggerated readable silhouette. Do not copy protected
character details, logos, outfits, catchphrases, or exact likeness.

Match the established enemy assets:

- black-first pixel-art silhouette;
- white eyes/highlights;
- gray hatch-like volume strokes;
- restrained accent color only where it improves readability;
- transparent background;
- crisp hard-edged pixels, no soft painterly shading;
- readable at the in-game draw size of roughly 60x60.

Use `assets/leech-crawler.png` and `assets/gnat-flyer.png` as style references
for value range, contrast, and animation economy.

## Required Animations

- **Idle:** 1 canonical frame in `player.png`; the app adds a small bob.
- **Run:** 4-frame loop with strong boot stride and shoulder counter-motion.
- **Jump:** 4 frames covering launch, airborne tuck, fall, and landing-ready
  pose.
- **Jetpack:** 4 frames with the backpack/thruster visible and body lifted.
  The app draws an extra flame effect, so the sprite flame should be modest.
- **Grenade throw:** 4 frames: wind-up, pull-back, release, follow-through.
- **Flamethrower:** 4 frames: brace, nozzle forward, recoil, held fire.
- **Pistol:** 4 frames: aim, muzzle recoil, arm recovery, ready pose.
- **360 knife:** 4 frames that sell a full spin: wind-up, back-facing slash,
  forward slash, recovery.

## Generation Workflow

1. Generate a single 96x96 transparent hero design frame first.
2. Approve the silhouette against `leech-crawler.png` and `gnat-flyer.png`.
3. Generate each animation as four 96x96 frames using the approved design as
   the identity reference.
4. Assemble each state into the filenames above.
5. Run `just zooreader check-assets`.
6. Run `just zooreader check`.

Prefer generating on a flat chroma-key background and removing it locally if
native transparency is unavailable. Keep generous padding inside each 96x96
frame so weapons and limbs do not clip.

## Prompt Template

Use this as the base prompt for the hero design and adapt only the animation
line per state:

```text
Create a 96x96 pixel-art sprite for a small browser game. Transparent
background. Original retro action hero, loosely inspired by the archetype of a
blond sunglasses-wearing 1990s action protagonist, but not a copy of any
existing character. Black-first silhouette, white highlights, gray hatch-like
volume strokes, restrained accent color, crisp hard-edged pixels, no text, no
logo, no soft gradients. Match the visual language of monochrome insect enemy
sprites: chunky black forms, white eyes/highlights, minimal gray detail.
Base orientation faces left. Readable at 60x60 in-game size.

Animation: <state-specific frame description>.
Output: <single 96x96 frame or four 96x96 frames for a 384x96 horizontal
sprite sheet>.
```

State-specific animation lines:

- Idle: `standing ready, compact confident pose, weapon lowered, subtle chest
  and shoulder asymmetry`.
- Run: `four-frame running loop, heavy boots, strong arm swing, stable head
  silhouette`.
- Jump: `four-frame jump arc: launch, airborne tuck, falling, landing-ready`.
- Jetpack: `four-frame jetpack hover loop, visible backpack thruster, lifted
  legs, modest exhaust only`.
- Grenade throw: `four-frame grenade throw: wind-up, arm back, release,
  follow-through`.
- Flamethrower: `four-frame flamethrower firing pose, braced stance, forward
  nozzle, recoil and held-fire variation`.
- Pistol: `four-frame pistol firing pose, aim, muzzle recoil, arm recovery,
  ready pose`.
- 360 knife: `four-frame full spinning knife slash, each frame a different
  quarter turn, circular motion readable without relying on effects`.
