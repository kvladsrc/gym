# ZooReader Hero Asset Pipeline

ZooReader uses static images for actions and a four-frame cycle for running.
The canvas supplies the remaining motion and effects: idle bob,
squash/stretch, horizontal mirroring, jetpack flame, knife arc, projectiles,
and impacts. The run is generated as one landscape image rather than four
independent requests so the model can keep identity, direction, scale, and
pose continuity visible in a single composition.

## Runtime Contract

Each hero frame is a transparent `96x96` PNG area. Action files contain one
complete pose; `player-run.png` is a horizontal `384x96` sheet containing four
`96x96` frames. The canonical source faces screen-left; the renderer mirrors
it when the hero faces right. The renderer disables image smoothing so the
logical pixels remain crisp at the roughly `60x60` draw size.

- Idle: `assets/player.png`, relaxed combat-ready stance.
- Run: `assets/player-run.png`, four-frame
  contact/passing/contact/passing cycle.
- Jump: `assets/player-jump.png`, one compact airborne pose.
- Jetpack: `assets/player-jetpack.png`, one stable hover pose.
- Grenade: `assets/player-grenade.png`, one release pose.
- Flamethrower: `assets/player-flamethrower.png`, one braced firing pose.
- Pistol: `assets/player-pistol.png`, one restrained-recoil pose.
- Knife: `assets/player-knife.png`, one side-on slash pose.

Every pose and run frame must preserve the same identity, palette, apparent
body scale, camera, baseline, and screen-left orientation. Keep generous
padding so hair, hands, weapons, and boots do not clip.

## Visual Direction

The hero is an original cozy 16-bit adventure-RPG interpretation of a broad
retro action archetype. Use a short chunky body, slightly oversized square
head, blocky blond flat-top, sunglasses as one dark band, broad red torso,
compact trousers, oversized gloves, and large boots. Do not copy an existing
game or character's exact likeness, outfit, logos, or other protected details.

Design on a `32x32` logical grid with a strong outline, large color clusters,
8-12 flat colors, and no feature smaller than two logical pixels. The local
normalizer trims generated outer whitespace and fits each complete silhouette
inside a centered `26x26` content box, leaving three logical pixels of safety
padding. Avoid realistic anatomy, small equipment details, hatching,
dithering, gradients, and fabric texture. Silhouette, head, hair, glasses,
torso, gloves, and boots must remain readable at the in-game draw size of
roughly `60x60`.

## Generation Workflow

Run the recipes from the repository root through the Nix development shell.
`OPENAI_API_KEY` must be present in the environment.

1. Generate one canonical concept:

   ```sh
   just zooreader asset-hero-concept
   ```

   The recipe makes one image request and prints the full source and the
   pixel-normalized `96x96` concept contact-sheet paths. It records this
   concept as the active reference under ignored `asset-work/` storage.
   Runtime assets are not changed.

2. Inspect the concept. It must have one complete original character, a strong
   screen-left silhouette, a uniform magenta background, and sufficient outer
   padding. If it is not suitable, run `asset-hero-concept` again; the newest
   successful concept becomes active.

3. Generate the full pose set from the accepted concept:

   ```sh
   just zooreader asset-hero-full
   ```

   Idle is derived locally from the concept. The other seven states use seven
   sequential image edits, all with the same canonical reference. Six requests
   produce one static pose each. The run request produces one `2048x1024`
   landscape source with four equal columns; it never makes four unrelated
   frame requests. Completed API sources are retained so an interrupted run
   resumes without paying for successful states again.

4. The full recipe removes the magenta background and reduces each frame to a
   centered `26x26` content box on a `32x32` logical pixel grid with a 12-color
   maximum before nearest-neighbour upscaling to `96x96`. It validates every
   static image and all four run frames, writes a full contact sheet and
   animated run preview, and only then replaces `assets/player*.png`.

5. Inspect the printed full contact sheet for identity, direction, scale, and
   baseline consistency, then run:

   ```sh
   just zooreader check
   ```

The API key is sent only in the Authorization header and is never written to a
manifest. Prompts, model, quality, usage, raw sources, normalized poses, and
contact sheets remain in ignored `asset-work/` storage.

## Model And Image Processing

The workflow defaults to `gpt-image-2` with `high` quality for both the concept
and all state edits. GPT Image 1.x is rejected because it does not meet this
pipeline's quality and reference-preservation requirements. The optional
`OPENAI_IMAGE_MODEL` and `OPENAI_IMAGE_QUALITY` environment variables can
override the defaults for deliberate experiments.

`gpt-image-2` processes edit references at high fidelity automatically, so the
workflow does not send an `input_fidelity` parameter. The model does not
currently support transparent output, so every prompt requests one pose on a
flat `#ff00ff` background. ImageMagick removes that background locally with a
small fuzz tolerance plus a magenta-hue despill, downsamples to the logical
grid, thresholds alpha to a hard pixel edge, limits the palette without
dithering, and then performs the deterministic nearest-neighbour upscale.

The generator uses the single-prompt Image API because each operation creates
or edits one image. See the official
[Image generation guide](https://developers.openai.com/api/docs/guides/image-generation)
and [GPT Image 2 model page](https://developers.openai.com/api/docs/models/gpt-image-2).

## Review Checklist

Automated checks can validate filenames, dimensions, visible alpha, and clear
corners. They cannot reliably judge recurring-character identity or art
direction. Before accepting a generated set, check that:

- all static images and all four run frames depict the same person and outfit;
- every pose and frame faces screen-left;
- head and body proportions remain stable;
- the character occupies a consistent amount of every frame;
- the run reads as a looping contact/passing/contact/passing stride rather
  than four unrelated poses;
- weapons, hair, hands, and boots are not clipped;
- no magenta halo, text, extra character, panel, or motion effect remains.
