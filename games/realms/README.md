# Realms

A colourful map strategy for playing together with a child: a hex map with six
kingdoms, their borders, cities, resources and monster lairs, turns and a
treasury. Unity 6000.6.0f1 (URP), keyboard and mouse, interface in Russian and English.

The first iteration is the map: generation, borders, the look of cities,
resources and lairs, the interface, and the turn button with income. The second
is building: field buildings on resources, the town panel (growth, levels,
market, granary, tavern), buying land, the turn report and small tasks. The
third is adventure on a larger map (70 × 42) under fog of war: heroes lead
armies hired in the towns (barracks, archery range, stables), fight seven kinds
of monster groups in a tactical battle mode of its own and visit chests, piles,
shrines and watchtowers. Settlers found new towns, walls keep the monsters near
a town from growing, and a castle lets a town grow into a royal city. The
kingdoms do not fight each other. Later iterations follow the notes after
playing; [DESIGN.md](DESIGN.md) plans them.

## Commands

From the monorepo root, through the Nix environment:

```sh
nix develop -c just realms fetch-art  # Studio art -> Assets/Realms/Art
nix develop -c just realms build      # Realms/Setup, then the Linux player
nix develop -c just realms build windows  # Windows player (Mono), on Linux
nix develop -c just realms package    # both players packed into Builds/dist
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
each, without Unity's backup and debug folders.

## Controls

| Keys | Action |
| --- | --- |
| W A S D, arrows | move the map (Shift is faster) |
| Right mouse button | drag the map |
| Q and E (or Z and X), middle mouse button | turn the map (a click on the compass by the minimap faces north) |
| Backspace | turn the map back to its usual angle |
| Alt (held) | the badges of resources, places and monster groups over all seen land |
| Mouse wheel, + and − | zoom (the wheel zooms towards the cursor) |
| Left mouse button | select a cell, city, monster group or own hero; a selected cell shows its build and buy buttons |
| T, double click on an own town | the town screen: buildings, hiring, the garrison |
| R, double click on an own hero | the hero screen: level, attack, defence, the army |
| Right mouse button (click, not drag) | lay the chosen hero's way; a second right click on its end (or G) walks it, into a fight if monsters stand there |
| Right click on another own hero | walk next to him and exchange units |
| F | next hero |
| Tab | next own city |
| Space, Home | back to the capital |
| Enter | next turn: income, town growth, the turn report |
| F1, H | help |
| Click on the shield in the top bar | the flag editor |
| Esc | clear the way and the selection, then the hero; close the help or a screen; with nothing left, the menu |
| Enter, Esc in a dialog | the first or the last answer |

Clicking or dragging on the minimap moves the view.

The game opens on the main menu, the Hexcrown logo and the buttons over its
picture, with nothing loaded: «Продолжить» goes into the last game (Enter),
drawn under a loading screen with a bar,
«Новая игра» shows the map a new game would start on, with another map, a
map by its number and the neighbours' pace, then «Начать». The «Меню» button
in the top bar (or Esc) opens the menu, a column along the left edge beside
the live map: back to the game, save, load, a new game, how to play, the main
menu and quitting. In a battle the menu has only back, how to play and
«Выйти (бой начнётся заново)», and the battle waits while it is open.

The game saves itself after each turn's end, before each battle, before a new
game and on quitting («Сохранено» shows beside the turn counter), so quitting
asks nothing. It keeps three autosaves of the game in turn and those of the
two games before it; «Сохранить» writes into six slots of the players' own
and asks before replacing one. «Загрузить» lists this game's autosaves, the
earlier games and the own slots. The game loads the last save behind the main
menu when it starts; `./play.sh -seed N` starts a new game on map N instead.
The saves are under
`~/.config/unity3d/Realms/Realms/saves` (Unity's persistentDataPath).

On a town or hero screen the camera comes down beside the town or hero and
the map's panels hide. Esc (or T and R again) goes back to the map, Tab steps
to the next town and F to the next hero, Enter ends the turn.

A town's hiring tab gathers a settlers' cart; the hero who leads it gets a
button on his card that founds a town where he stands, under a name the
players type. Walls and a castle are built from the town screen's buildings
tab, below the places: they take none.

Each house and town building takes a place: 5 on the town's centre cell and 6
on each quarter. When a town is full, click an empty cell of the kingdom beside
its centre: the cell card has a «Расширить город» button that turns the cell
into a new quarter.

Every new game opens the flag editor, and a click on the kingdom's shield in
the top bar opens it again: a pattern, the colours of the field, the second
part and the emblem, and an emblem. The flag flies at once on the towns' and
heroes' banners and on every shield of the kingdom, and the next games start
with it (the player's `PlayerPrefs`; the captures skip the editor at the start
and keep the generated flags). The field's colour is the kingdom's colour on
the map and gives it its name; a kingdom that had the colour takes the
player's old one.

The hero card has exchange buttons: one for each own hero on a neighbouring
cell and one for the town the hero stands in (its garrison). The exchange
dialog moves units between the two sides with arrows: all of a kind, one, or
half. A hero walking into a town still takes its whole garrison.

In a battle a click (either button) on a cell walks the current stack there,
a click on an enemy strikes it (from the side the pointer is on) or shoots
it; D defends, A turns the auto battle on and off.

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
