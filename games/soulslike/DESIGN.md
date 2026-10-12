# Soulslike: test level

<!-- markdownlint-disable MD013 -->

A prototype to find the problems of the asset pipeline, not a finished game.
Every asset is made by the asset studio (`assets_studio/`), except the Mixamo
animations. What was learned: the studio's
[guides](../../assets_studio/docs/guides); where each asset comes from:
[`ASSETS.md`](ASSETS.md).

## The level: «The Last Vigil»

A dark woodland in fog, assembled by hand in Blender from the studio's
models and exported for Unity (`tools/export_level.py`).

1. **Start** at the hearth nearest the route's first waypoint; every
   hearth is a bonfire (checkpoint): resting heals the hero and brings the
   enemies back.
2. **The route**: casters along its waypoints, five kinds of enemy, each
   with her own English voice lines.
3. **The sanctuary**: the boss, the Drawn Princess (the user's drawing made
   3D), on her platform at the top of the stairs. No fog gate: she fights
   as soon as she notices the hero; her song starts 40 m away, so its
   intro plays on the way up and the chorus comes about 50 s in. Beating
   her ends the level («VICTORY ACHIEVED»).

On the Last Ember's map a second boss comes first: the Gaunt Warden (the
user's model redrawn with empty hands for the rig), at the map's middle on
the main road some 35 m past the cemetery's hearth. Each boss has her own
instrumental theme (YuE2); one beaten stays dead («GREAT FOE FELLED»).

## Mechanics

The fights are the training duel's (`training/`, `Scripts/Training`): the
hero and every creature have a duellist's body (`Fighter`: the same Mixamo
clips, blows, roll, energy, combo and adrenaline), and every creature, the
bosses too, is fought by the trained network (`FighterAgent`, the newest
`training/results/duel/Fighter/*.onnx`, copied to `Art/Brains` by the level's
build and run in the game), in the conditions it trained in:

- Its post is the centre of a 14 x 14 m square: it never leaves it. The
  network sees its place as in the training's 30 x 30 m arena (walls far
  off), centred on the post.
- It stands at its post, the network not asked at all, until the hero steps
  into its square; the hero out of it, or dead, ends the fight: it walks
  back and is whole again. The level lays the creatures out so that no
  place is in more than two squares: at most two fight at once.
- The fight's clock runs from the moment it engages, as in training, and
  stops at 45 s, half the training's 90 s limit (no time-out here).
- Its grid sees the hero's hurtboxes and only plain obstacles: trees, big
  stones, stumps, buildings, walls; not undergrowth, hedges or lanterns.
- Heights within what the network saw beside the 1.85 m hero (the training
  drew both from 1.5–2.1 m): the casters 1.75 m, the Princess 2.4 m, the
  Gaunt Warden 2.59 m.
- Health: the hero 400, a caster 200 (the training's), the Gaunt Warden
  and the Princess 800 each. The casters carry bare hands, a sword, a hammer in
  turn; the Princess a sword, the Gaunt Warden a hammer.

The hero (`Hero`, `HumanFighter`, as `training/play.sh` has it):

- Third-person camera, the mouse turns it and the hero turns to face its
  way; WASD to move.
- Left mouse: three blows of what is in hand, in turn (a combo); right
  mouse: the heavy blow; F: a kick; Shift: a quick move the way WASD points;
  Space: the roll (no invulnerability: it saves by taking the body out of
  the blow's way). A blow pressed starts at the next decision it is free at
  (5 a second, as the network's).
- 1, 2, 3: the sword, the hammer, bare hands (between actions); R: a flask
  (three); E: rest at a bonfire.
- Blows land where the animation's weapon goes: the blade, the hammer's
  head and shaft, the fist, foot or shin, swept through the clip's middle
  phases against capsules on the other's bones. A hammer stops a sword's
  blow (sparks, a ring of steel).
- Death → «YOU DIED» → back at the last bonfire, the creatures reset.
- Esc: the pause menu: resume, night or day (`DayNight`: by night a dim
  moon with faint shadows and the lanterns to see by; by day an overcast
  sky, `SkyDayGrimshaw`, and a high soft sun), volume (all, music, effects;
  kept between runs), quit.

## Style

Dark and muted, models textured by Hunyuan3D-Paint, one solid green-grey
fog and a dim moonlit sky (the Last Ember's only far off, 40 → 135 m, half
grey: a dense one in the sky's colour hung as a green haze; grass around
the hero, `GrassField`, where the ground's masks let it grow); the light comes from the bonfires, sparks rising
from them. Not realism: the models come from an 8 GB laptop GPU.

## Build

`Soulslike → Build Last Ember` in Unity rebuilds the game's scene,
`Scenes/LastEmber.unity`, from `Assets/Soulslike/Art` (the exported level,
characters, animators, voices) and the training's newest network; a missing
model becomes a grey primitive.
The Mixamo clips come from the repository's package `animations/mixamo`.
The Linux player goes to `unity/soulslike/Builds/Linux`; `./play.sh` runs
it on the discrete GPU.
