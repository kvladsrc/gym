# Zooreader asset pipeline

## Current artwork

The September 2026 refresh replaces all 14 runtime PNGs with a shared
pixel-art direction: a blond, red-vest, teal-trouser hero; teal and ochre
insects; muted grass, snow and sandstone platforms. The source images were
generated with the built-in **imagegen** tool, with transparent backgrounds
for characters. No API key or image API script was used for this refresh.

Runtime files live in `assets/`. The reviewed source images and intermediate
frames live in ignored `asset-work/refresh-2026-09/` as `hero-atlas.png`,
`crawler.png`, `flyer.png`, `grass.png`, `snow.png`, and `desert.png`.
Sources are working artifacts, not required to build or serve the app.
Keep them when continuing art iteration; a fresh clone ships the packed PNGs.

## Runtime contract

| Asset | Format | Direction |
| --- | --- | --- |
| Hero poses | 96×96, transparent, 12 colors | Left |
| Hero run | 384×96, four distinct 96×96 cells | Left |
| Crawler | 512×64, four 128×64 cells | Right |
| Flyer | 256×64, four 64×64 cells | Right |
| Grenade | 24×24, transparent | Rotated by renderer |
| Terrain | Three opaque 64×64 tiles | Front elevation |

The renderer mirrors actors as needed. Running uses four generated frames;
other hero states use single poses with canvas motion and effects. The hero
atlas has four columns and three rows: run frames; idle/jump/jetpack/throw;
flamethrower/pistol/knife/grenade item. One shared atlas keeps the character
identity visible during generation.

## Packing and review

From the repository root, after placing the reviewed source set:

```sh
nix develop -c just zooreader import-art
nix develop -c just zooreader check
```

`scripts/import-art.mjs` is an importer for this specific reviewed set,
not an arbitrary sprite-sheet detector. It crops the hero's 362-pixel cells,
uses individually inspected enemy gutters, preserves source alpha, reduces
palettes without dithering, and packs runtime files. Enemy scales remain
fixed across their cycles; the crawler uses a shared floor baseline.
Terrain is reduced to 32×32 then enlarged with nearest-neighbour sampling.
The unused area above the generated snow shelf is cropped away.

Before replacing sources or accepting a new generation, inspect identity,
facing direction, frame gutters, scale, baseline, wing motion, stride and
legibility at the actual 60-pixel hero size. Automated checks validate every
animation frame, transparency, dimensions, hero palette and logical grid;
they cannot establish artistic quality or seamless movement.

## Prompt set used for the refresh

### Hero atlas

```text
Use case: stylized-concept. Asset type: production pixel-art sprite atlas for
Zooreader, a tiny side-view platform game over a PDF. Create ONE sprite atlas,
exactly 4 equal columns by 3 equal rows, transparent background, no grid lines
or labels. All 12 cells have identical generous padding. Original chunky
action hero, blond flat-top hair, dark sunglasses, brick-red vest, dark teal
trousers, tan gloves, charcoal boots, small brass jetpack. Strong dark ink
outline, deliberate large pixel clusters, 12 flat colors, authentic polished
32x32 logical pixel sprites enlarged, no gradients or dithering. ALL hero
poses face SCREEN LEFT, same size/head/outfit, entire silhouette inside its
cell. Row 1: four frames of a leftward RUN cycle, contact, passing, opposite
contact, opposite passing; feet share baseline, keep head steady and alternate
legs clearly. Row 2: idle standing, airborne jump knees bent, jetpack hover
knees slightly bent (no flame), throwing grenade left (no separate
projectile). Row 3: braced flamethrower pointing left (no flame), pistol
aiming left, knife slash left (no effect), one small olive grenade item
centered. Orthographic side view; consistent identity across all 11 hero
drawings. Clean transparent alpha, no ground shadows, no text, no background
objects. Image dimensions ideally 1536x1152, exactly aligned uniform cells.
```

### Crawler

```text
Use case: stylized-concept. Create one production pixel-art animation sprite
sheet: four equal square cells in one horizontal row on genuinely transparent
background. A charming but menacing chunky armored bookworm/leech crawler for
a retro action platform game. Faces SCREEN RIGHT in every frame. Dark teal
segmented carapace, sage highlights, ochre belly, two small amber eyes, tiny
dark legs. Entire body low wide silhouette, 2:1 aspect ratio, same head
identity and proportions across all four frames. Four visibly different
crawling poses: stretched, compressing, bunched, extending. Consistent floor
baseline and padding in each cell. Authentic deliberate 32-pixel-high chunky
pixel clusters, flat limited 12-color palette, dark ink outlines, no
antialiasing, no texture noise, no gradient, no shadow. No letters, no
dividing lines, no other creatures. Friendly readable silhouettes, compatible
with blond red-vest teal-trouser action hero. Sheet aspect ratio 4:1.
```

### Flyer

```text
Use case: stylized-concept. Production pixel-art sprite animation sheet,
exactly four equal columns in one row on transparent background. A tiny chunky
moth/gnat enemy facing SCREEN RIGHT, dark ink outline, ochre abdomen, dark
teal head and thorax, two little amber eyes, pale mint wings. Four consecutive
wingbeat poses: wings up, diagonal up, wings out, wings down. Keep body fixed
at same center and scale in every cell, leave 15 percent padding around wings,
no clipping. Each cell square. Charming retro 16-bit pixel art with large
clean pixel clusters and 12 flat colors, no gradients, no shadows, no labels,
no dividing lines. Readable at 50 pixels tall, harmonious with a red-vest
teal-trouser action hero and teal armored crawler. Exactly 4 distinct complete
insects, equal spacing, entire silhouettes separated by transparent gutters.
Landscape aspect ratio 3:1.
```

### Grass

```text
Use case: stylized-concept. One seamless side-scrolling platform terrain tile,
square front elevation, absolutely flat orthographic 2D, fills entire square
without any transparent margins. Polished chunky pixel art for a quiet retro
PDF-reader action game. Grass biome: top one-eighth a straight continuous
moss-green grass surface with restrained sage highlight, bottom seven-eighths
warm dark earthy brown compact soil with a few large muted ochre stone
clusters and roots. Left and right edges tile seamlessly; top perfectly
horizontal. Large clean 32x32 logical pixel clusters enlarged, limited
10-color palette, dark teal-brown outline accents, no gradients, no tiny
noise, no outlines around whole tile, no text, no objects, no perspective or
cube, no sky. Understated beautiful handcrafted 16-bit terrain.
```

### Snow

```text
Use case: stylized-concept. One square side-view platform terrain tile, flat
orthographic front face filling the entire image edge to edge, seamless
repeating horizontally. Snow biome for a quiet retro pixel-art game over a
PDF. Top one-eighth a straight flat ivory snow shelf with pale mint underside,
below that desaturated blue-grey frozen earth, a few broad slate stone
clusters and short ice cracks. Polished chunky 16-bit pixel art, large 32x32
logical pixel clusters enlarged, 10 flat colors, subdued palette harmonious
with dark teal and ochre characters. No dithering, no gradients, no tiny
noise, no sky, no objects, no perspective, no cube, no border or text, no
transparent margins. Surface across top must be perfectly level.
```

### Desert

```text
Use case: stylized-concept. One square seamless horizontally repeating
side-scrolling platform tile, orthographic front elevation, entire square
filled edge to edge. Desert biome. Top eighth is a perfectly flat horizontal
pale warm sand shelf directly at the top edge, below is muted terracotta
sandstone with a few broad honey-ochre stone clusters and subtle horizontal
strata. Quiet refined 16-bit pixel art for a PDF-reader game, deliberate large
32x32 logical pixel clusters enlarged, 10 flat colors, dark brown-teal shadow
accents. No gradients, no fine noise, no sky above surface, no objects or
cactus, no text, no border, no perspective or cube, no transparent padding.
Understated palette coordinated with teal, ochre and brick-red game
characters.
```

## Existing optional API workflow

The older `asset-hero-concept` and `asset-hero-full` recipes remain available
for explicitly requested API-based experiments. They require
`OPENAI_API_KEY`, generate a concept followed by individual edits, and replace
only hero PNGs after validation. They use magenta-background removal rather
than the built-in tool's alpha output. `test-assets` exercises this workflow
against an offline mock API. It is separate from the atlas importer above.
