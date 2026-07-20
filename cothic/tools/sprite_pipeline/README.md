# Character sprite pipeline

The player character is an 8x8 atlas: eight movement directions as rows,
eight animation frames as columns. The current cell size is 512x512 (atlas
4096x4096), matching the pixel density of the decor and wildlife sprites
(~200 px per world unit). The game derives `pixel_size` from the cell size,
so the atlas resolution can change without scene edits. The row order is
fixed by the game (`player_controller.gd`):

```text
0 south  1 south_west  2 west  3 north_west
4 north  5 north_east  6 east  7 south_east
```

Only five rows are generated (south, south_west, west, north_west, north).
The remaining three are exact horizontal mirrors, assembled deterministically:

```text
05_north_east <- 03_north_west
06_east       <- 02_west
07_south_east <- 01_south_west
```

## Style anchors

- `concepts/canonical_views.png` - the original Cothic character style:
  proportions, soft painterly shading, palette. Key frames and strips must
  follow it; do not let the model drift to a sharper anime look.
- `concepts/knight.png` - outfit and material identity (armor pieces,
  scarf, cape, boots).

## Strip generation contract

Each direction strip is one image-generation request that contains the
approved character key frame as a style reference and shows one full walking
cycle. The model output must satisfy:

- Layout `8x1` (one row of eight frames) or `4x2` (two rows of four), with
  equal cells; frame order is reading order (left to right, top to bottom).
- Transparent or uniform background, no grid lines or cell borders.
- The character is centered in every cell at a consistent scale, feet near
  the cell bottom.
- At least 512x512 per frame: for example a 4x2 strip at 2048x1024, or an
  8x1 strip at 4096x512. Detail is lost when the model draws smaller.

Cells are fitted into 512x512 during assembly (override with `--size`), so
higher-resolution strips downscale cleanly.

## Assembly

Drop the strips into `cothic/tmp/sprites/knight/<animation>/strips/` as
`<direction>.png` (or as `<direction>/` directories with eight pre-sliced
frame PNGs), then from the repository root:

```sh
nix develop .#cothic-assets
just cothic sprites-assemble walk
```

The script slices the five strips, mirrors the remaining rows, and packs the
atlas to `cothic/tmp/sprites/knight/walk.png`. Useful flags (passed after the
animation name):

```sh
just cothic sprites-assemble walk --layout 4x2
just cothic sprites-assemble walk --trim   # re-anchor feet to cell bottom
```

Review the atlas, then copy it to `assets/sprites/knight/` under the next
versioned name and point `scenes/player.tscn` at it.

## Legacy Blender flow

`render.py` + `pack.sh` render the rigged `Ranger.glb` with a procedural
movement cycle into deterministic pose guides. This was the production
prototype before the strip workflow; the animation base is now an external
mannequin reference (`concepts/walk_pose_reference.jpeg`), so the Blender
stage is kept for reference only. The `sprites-render`, `sprites-pack`, and
`sprites-proof` recipes belong to that flow.

`concepts/knight.png` remains the approved visual anchor for the character:
identity, proportions, outfit, materials, and palette.

Generated frames, atlases, and previews are written under
`cothic/tmp/sprites/`, which is intentionally ignored by Git. Approved final
PNG assets are versioned, while local model weights, CUDA runtimes, and
external API access remain outside Cothic.
