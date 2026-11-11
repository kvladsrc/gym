# Hexcrown: development

The game's code, tools and art pipeline. The project and its folders are still
called Realms; the game is Hexcrown. [README.md](README.md) describes the game,
[DESIGN.md](DESIGN.md) its plans and the decisions behind them.

Unity 6000.6.0f1 (URP), C#; the rules live in `Core` without Unity and are
tested with plain dotnet.

The first iteration is the map: generation, borders, the look of cities,
resources and lairs, the interface, and the turn button with income. The second
is building: field buildings on resources, the town panel (growth, levels,
market, granary, tavern), the turn report and small tasks. The third is
adventure on a larger map (70 × 42) under fog of war: heroes lead armies hired
in the towns (barracks, archery range, stables), fight seven kinds of monster
groups in a tactical battle mode of its own and visit chests, piles, shrines
and watchtowers. Settlers found new towns, and a castle lets a town grow into a
royal city. Later came the plans' steps: the crown of gems as the goal,
borders that grow from the towns' influence (land is no longer bought),
neighbours that play their own turns, lairs' raids that walls beat off, saves,
the guiding lines, the menu and English. The kingdoms do not fight each other.
[DESIGN.md](DESIGN.md) has the rules and the plans.

## Commands

From the monorepo root, through the Nix environment:

```sh
nix develop -c just realms fetch-art  # Studio art -> Assets/Realms/Art
nix develop -c just realms build      # Realms/Setup, then the Linux player
nix develop -c just realms build windows  # Windows player (Mono), on Linux
nix develop -c just realms package    # players and sources packed into Builds/dist
nix develop -c just realms play       # NVIDIA PRIME offload, native Wayland
nix develop -c just realms test       # generator and economy, no Unity
```

`fetch-art` copies the models, icons and ground textures from a local Assets
Studio. The monorepo keeps them out of Git; its public copy (`gym`) carries
them, so a checkout of that builds without the Studio.
`play` uses native Wayland when it is available.

Or directly: `games/realms/play.sh` (the player accepts `-seed N` for a given
map and `-lang ru|en|long` for a language for this run). `tools/build.sh`
builds through the Editor when the project is open there, otherwise headless
(the first headless import takes several minutes).
`tools/package.sh` packs `Builds/Linux` and `Builds/Windows` as
`Builds/dist/hexcrown-<version>-linux.tar.gz` and `-windows.zip`, one folder
each, without Unity's backup and debug folders, and the sources as
`-source.zip`: what Git keeps, the Studio art that `fetch-art` brought and the
repository's licence. `tools/package.sh source` packs only the sources.

Other arguments of the player: `-realmsCapture DIR` takes the screenshots of
`RealmsCapture` into DIR and quits, `-realmsPromo DIR` records the promo's
clips (`RealmsPromo`) into DIR as frames and quits.

## Saves

The saves are under `~/.config/unity3d/Realms/Realms/saves` on Linux (Unity's
`persistentDataPath`): three autosaves of the game in play and those of the two
games before it, and six slots of the players' own (`Core/Save.cs` writes
them, `Scripts/RealmsSave.cs` keeps them). The language, the flag and the
guide's learned gestures are in the player's `PlayerPrefs`.

## Layout

- `unity/realms/Assets/Realms/Core` — the game without Unity: hexes, map
  generation (`WorldGen`, rivers along the hex edges in `Rivers`, monster
  groups and places in `Wilds`), kingdoms, cities, resources, the economy,
  town growth, walls and land (`Economy`), settlers and new towns
  (`Settlers`), units and armies (`Army`), heroes, paths
  and fog (`Heroes`, `Adventure`), the tactical battle with its auto battle
  and odds (`Battle`), the turn, building and tasks (`GameState`).
  Tests: `Tests/EditMode`, also run by `tools/core` with plain dotnet.
- `Scripts` — map rendering (`TerrainField`, `RiverCourse`, `MapView`,
  `PropBuilder`, which merges the copies of trees and stones per model with
  `ModelBatch`), heroes on the map (`HeroView`), the battlefield
  (`BattleView`: it only shows a Core `Battle` and passes the players'
  choices to it, so another kind of battle can replace it), unit pictures
  for the interface (`Portraits`), the camera (`MapCamera`, which also turns
  and holds the close view of a screen; `ScreenLook` blurs the land behind
  it), the interface (`Hud` with `HudHeroes`, `HudScreens` for the town and
  hero screens and `HudBattle`, UI Toolkit built in code, styles in
  `UI/Hud.uss`) and the game loop (`RealmsGame`, heroes and fights in
  `RealmsHeroes`, the screens in `RealmsScreens`, settlers and new towns in
  `RealmsSettlers`, the screenshot mode in `RealmsCapture`).
- `Shaders` — terrain, sea, rivers, props and the fog of war. Borders,
  territory tint, the grid and the hover and selection rings are drawn per
  pixel from a cell texture (`RealmsCommon.hlsl`); no tonemapping, so palette
  colours appear as chosen. The ground textures (`RealmsGround.hlsl`) only add
  pattern: each layer is stored divided by its mean colour, so the palette
  keeps the colour. The fog (`Fog.shader`) is a bank of dark grey clouds over
  the unseen cells, found by following each pixel's ray from the camera
  through the clouds' height field. The colours (`Palette`) are muted for the
  land and saturated only for the kingdoms, the selection and the paths, and
  the prop shader calms the studio models' loudest paint to match; the
  interface (`UI/Hud.uss`) is dark to stay calmer than the map.
- `Editor/Setup.cs` (menu **Realms → Setup**) writes the materials, art
  prefabs, post-processing, panel settings and `Generated/Map.unity`;
  `Editor/Build.cs` builds the player. `Generated/` and `Art/` are not in the
  monorepo's Git; the public copy carries the Studio art in `Art/`.

## Art

Models and icons come from the Assets Studio project `realms`; `art.json` lists
them. Studio makes the models at the game's budget (Hi3D refine with
`max_faces`, the colour baked onto it), so `tools/fetch_art.py` copies them as
they are and only resizes the icons; `Art/manifest.json` records the sources.
Setup scales every model to a largest dimension of one unit, stands it on its
bottom centre facing the camera and converts its materials to `Realms/Prop`,
with the normal map when the model has one (Studio's `3d-bake-normals`; the
shader builds the tangent frame from screen-space derivatives, as the models
have no tangents). The iteration-2 buildings (five field buildings, market,
granary and three cottages) and the iteration-3 models (the hero, spearman,
archer, rider, goblin, bandit, skeleton, spider, barracks, archery range,
stables, chest, two shrines and the watchtower), the accepted trees and stones
(with 1024 maps) and the town wall and its tower (`PropBuilder.ScatterModels`)
come from the Studio 3D guide's chain for low-poly models with baked detail: a
dense Hi3D mesh painted with TRELLIS.2, its colour and normals baked at 2048
onto the mesh at the game's budget. The same models stand on the battlefield,
bigger, and each unit and the hero is rendered once into a picture for the
interface (`Portraits`). Setup merges each of these scatter models into one
readable mesh with one material, so the map can copy it into the merged meshes
of its chunks. A walled town repeats the wall piece along each side of its
hexagon, each copy stretched a little to fit and leaning with the ground, with a
tower on each corner and two at each gate. Without the art the map uses simple
built-in stand-ins (for a missing tree or stone, another of its kind first;
boxes and plain towers for the walls); the castle is drawn in code until its
Studio model is fetched. Monster group badges use the `lair` and `swords` icons;
the monsters themselves appear only as 3D models.

The eight ground textures (grass, dry grass, forest floor, sand, scree, rock,
snow, dirt) are also in `art.json`, with two more: the cobbles of the towns'
squares and the road strip, a band along the road twice as long as it is wide
(ruts, a grassy middle, dusty shoulders). Setup packs them into the texture
array `Generated/Ground.png` (one layer per row, height in alpha; the strip is
repeated down its layer). The terrain mixes the eight per vertex by biome,
height, slope and nearness to cities and lairs (`TerrainField`,
`GroundLayer`); mountains and rocks take triplanar rock with snow on the high
peaks. The roads lay the strip along their lines and cross-fade it where two
lines meet. Without the eight the ground stays plain colour; without the
cobbles or the strip, the squares or the roads do.

The interface art in `UI/Art` (in Git: frames, buttons, dividers and the
interface icons, such as the castle and the settlers' cart) was drawn with
Codex's built-in image generation in its ChatGPT session;
`UI/Art/manifest.json` keeps each prompt, the tool and the sizes. Setup loads
every `icon_*.png` there into the art library. The flags' 18 emblems in
`UI/Art/Emblems` are white silhouettes from the same tool (their own
`manifest.json`); Setup imports them readable, and `FlagArt` tints their alpha
into the flags, the shields and the cloth's texture array, which the
`Realms/Flag` shader waves on the map.

`.mcp.json` registers `unity-realms` for the open Editor.
