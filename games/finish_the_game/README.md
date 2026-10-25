# Finish the Game: the ribbon prototype

<!-- markdownlint-disable MD013 -->

A linear RPG whose geometry is its story: an ordinary world, and the
story's route through it; the camera only goes forward along the route,
turning with the road as a third-person camera would, and the hero is kept
in its frame, not by walls. The way ahead is shown on a ribbon rising from
the horizon. Three ways to make it, side by side (Tab goes to the next in play):

- **Crystal** (`CrystalWorld`): the plane's level and route (2.9 km,
  the places spread wide, no woods), an island in space
  (`Shaders/Space.shader`), no map. The level is a band 100 m wide along
  the route; one may walk 20 m of it. A (250 m round the hero): the band
  itself, undeformed. Where the route leaves A, the world goes on
  as the ribbon: the world cut along the whole route (a place as often as
  the route passes it) laid on a curve that at that point is the road
  there, so there is no seam; the further on, the smaller everything
  (conformally: narrower, its detail with it). The curve runs on along the
  ground, turning slowly to the camera's line the shortest way (at most
  half a turn: space is never wound round, and past half a turn the way
  flips at once) while narrowing so that its edges never fold; then rises
  and coils into the sky, untidy, coming over to the camera's line, the
  whole rest of the story on it, a thin ribbon in the sky; key moments are marks on it (far off a
  light, nearer their colour, then their name). The camera, the hero and
  the ribbon are on one line. The world stands still, as an ordinary game
  world; A (250 m round the hero) shows all of it, its edge fading into the
  dark, where the seams hide. The hero walks only forward (W), the camera's
  way; A and D (Q, E, or the right mouse button) turn the camera within a
  window (±30° off the road's way) that narrows near the corridor's edge
  (±10 m off the road); left alone, it eases him back to the road. When
  the road bends out of the window, the camera turns by itself, at most
  90° a second: he always goes on along the story, to the ribbon.
- **Plane** (`PlaneWorld`): a level on a plane, never bent, and a route
  over it that comes back to places (the village A three times, the city
  twice). The plane is seen only through a band along the route round the
  camera; past its end, the horizon's ribbon shows the route unrolled: a
  picture of the band along the whole route, made once from above.
- **Loop** (`LoopWorld`): one loop round a mountain whose vertices know
  their place on the loop; ahead, the world itself leaves its place and
  bends onto the ribbon, ever smaller; behind, it shrinks away.

The map shows the world from above, as it is.

## Run

Open `unity/Finish the game` in Unity 6 (6000.6) through the Hub, then from
here (or `just finish_the_game <recipe>` from the repository's root):

```sh
just scene        # make both scenes anew
just play         # play Crystal in the Editor (just play Plane / Loop: the others); just stop to leave
just shot a       # the game view to screenshots/a.png, in play mode
just build        # the Linux player (Crystal first), Builds/Linux
just run          # run it on the NVIDIA GPU (needs no Editor)
```

Crystal: WASD: walk; the mouse: look (a click holds it, Esc lets it go);
the wheel: nearer, further; E: talk; F1: the hint at the foot of the picture
off and on. The others: WASD or a click on the ground: walk; Q, E or the
right mouse button: turn the camera. M: the big map; Tab: the other world.

## The demo quest (Crystal)

What the ribbon is for. In the first wood after the start, out of the
farm's mark on the ribbon a spirit comes with a flash: a light riding down
in the ribbon by fits and starts, now and then leaping to the turn below,
down to the road and to the hero. There it takes
a likeness: the farm woman Hilda, but the finest of her, a lady in jewels
and brocade. What it says sounds like nonsense (the wheel that goes nowhere
keeps them), and it goes back up into the ribbon. A hard cut: at the farm,
in its yard, Hilda herself is searching the grass for her barn keys; told what her spirit
said, she understands it (the old cart that has not moved in ten years),
and finds them under its wheel. Talks as in Gothic II: the camera over the
shoulder, the line at the top under the speaker's name, the hero's answers
numbered at the bottom (1–9, or W, S and Enter).

For takes: F8 puts the hero on the road a little before the spirit comes,
all as at the start; F9 goes straight to the farm, the spirit met.

Its assets come from the asset studio: `art/queue_quest.py` queues the
spirit's likeness (an edit of Hilda's own concept), the keys' concept, every
spoken line (XTTS, English) and the sounds; `art/fetch_quest.py` brings the
finished ones into `Art/Quest` and `Art/Characters`; the chosen concepts go
on to 3D with `art/studio.py model` (the keys then through
`art/convert_models.py` in Blender). The font is EB Garamond (SIL OFL,
`Assets/FinishTheGame/Fonts`).

## How it works (`Assets/FinishTheGame`)

Shared:

- `Scripts/StoryPath.cs`: the story's curve, sampled by its length: a ring
  (the loop) or an open route through waypoints (the plane); any point's
  (s, t) near a given s: s along it, t to its right.
- `Scripts/StoryWorld.cs`: what the camera, the hero and the map need of a
  world: its curve, how far ahead and behind it is shown as it is, the map.
- `Scripts/ForwardCamera.cs`: a rail camera: its focus is the farthest s
  the hero reached; it looks along the road round it (smoothed, a little
  ahead), and follows the hero to the sides along a short rail.
- `Scripts/Hero.cs`: WASD by the camera's heading, or a click on the near
  ground; no step takes him out of the frame's sides or bottom.
- `Scripts/RibbonBend.cs`: the ribbon's curve ahead, sampled each frame:
  it starts as the road where the near world ends, rises, then turns to
  the camera's heading (once risen, so that its wide edges do not fold),
  rolls up, sways. A place u metres past its start is drawn at
  a = arc (1 − e^(−u/arc)) along it, e^(−u/arc) of its size.
- `Scripts/StoryMap.cs`: the map.

The plane:

- `Scripts/PlaneLayout.cs`: the level (A, the wood, the cave, the city,
  the farm, the lake, mountains round the rim) and the route's waypoints.
- `Scripts/PlaneWorld.cs` and `Shaders/Band.hlsl`, `Shaders/BandLit.shader`:
  the level as it is; each frame the stretch of the route round the focus
  goes to the shaders, and only what is near that stretch is seen, fading
  into the fog's colour (another pass of the route through the same place
  is not in the stretch). Once, it renders the plane from above and unrolls
  the band along the whole route into a picture.
- `Scripts/HorizonRibbon.cs` and `Shaders/Horizon.shader`: that picture on
  a strip laid along the ribbon's curve, fading in where the band ends.

The crystal:

- `Scripts/CrystalWorld.cs`: the plane (as above), all of it drawn as one
  ordinary world; the story's strip (the ground along the whole route, each
  vertex with its (s, t)); where the ribbon leaves the road; the film's
  frames, pictures of each stretch of the story, taken as the game starts.
- `Scripts/RibbonBend.cs` (`sky`): the curve: the road itself lifting off
  the world a little ahead, a bridge up, then a funnel round the hero, each
  turn a little wider and higher, streaming in the wind, out to the story's
  end; the story laid along it a metre a metre at first, then at the
  dream's pace.
- `Shaders/StripLit.shader`: the strip laid on the curve: at first the world
  there as the world draws it (grass, the road's ragged earth), then a
  memory (sepia, pixels, lights), then film.
- `Scripts/StoryMarks.cs`: the key moments on the ribbon (each pass of A,
  the cave, the city, the farm; the end), drawn flat: light, colour, then
  name.
- `Scripts/ForwardCamera.cs` (`orbit`): behind the hero, turned by the player.
- `Scripts/SpiritQuest.cs`, `Scripts/RibbonSpirit.cs`, `Scripts/Villager.cs`,
  `Scripts/Dialogue.cs`: the demo quest (above): the light on the ribbon,
  the valley's people and spirits, the talks.

The loop:

- `Scripts/LoopLayout.cs`, `Scripts/LoopWorld.cs`: the loop's boxes placed
  by (s, t), each vertex keeping its (s, t) and the road's heading in its
  second UVs; the mountain and its cliffs are fixed, never on the ribbon.
- `Shaders/Ribbon.hlsl`, `Shaders/RibbonLit.shader`: by each vertex's ds
  from the focus (round the loop): near, drawn where it is; ahead, laid on
  the ribbon, heights pressed into a relief as it rises, casting no shadow;
  behind, shrunk into a point under the ground; shrunk to nothing, dropped.
