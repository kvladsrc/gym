# Soulslike: where the assets come from

<!-- markdownlint-disable MD013 -->

`unity/soulslike/Assets/Soulslike/Art` is not in git: everything in it is
made by the asset studio (`assets_studio/`) or exported from a `.blend`.
Ids in file names and the studio's library (`get_asset` over MCP) lead
back to each asset's job, prompt, parameters and lineage; the prompts are
kept there, not here.

## The level: `Art/Levels/LastEmber`

Assembled by the user in Blender from the studio's models
(`workspace/last_ember_abbey_artistic_300x500m.blend`) and exported with
`tools/export_level.py` (usage in its docstring): the FBX, its baked
textures, the ground's masks and `LastEmber.json` (the lights). The
models: Qwen-Image → Hunyuan3D-2mini → Hunyuan3D-Paint. The ground's
tiled images and decals are made fit for use by `tools/ground_textures.py`.

## Characters: `Art/Characters`

Qwen-Image → Hunyuan3D-2mini → Hunyuan3D-Paint → `3d-to-rig`
(Make-It-Animatable, a Mixamo skeleton); the FBX goes into Unity as it is,
Generic (`Editor/CharacterImport.cs`). The hero (`HeroKnight`, a green
knight without a cape), five enemies (`Enemy*`), the boss (`BossPrincess`,
from the user's drawing) and the second boss (`BossGaunt`, the user's
model, studio rig `01a111e8be53dcc8120dcb`). A textured GLB that is not
rigged becomes an FBX with `tools/glb_to_fbx.py`; the sword is
`Models/Nice/MagicSword`, the hammer `Models/GauntHammer`.

Animations: Mixamo packs from the shared package `dev.bonfirelit.mixamo`
(`animations/mixamo`, not published: Mixamo's terms); the clip sets are in
`Editor/CharacterAnimator.cs`.

## Sky: `Art/Textures/Sky`

By night `SkyGrimshawPainting.png`: Qwen-Image image-to-image from John
Atkinson Grimshaw's «Silver Moonlight», widened to 2:1 with mirrored edges
and repainted as a sky panorama (studio asset `01a10b73ed909e918642e4`); by
day `SkyDayGrimshaw.png`. The fog takes its colour from the sky's horizon.

## Voices: `Art/Audio/Voices`

XTTS (the studio's `voice` server), English, in the voices of the user's
recordings; lines rated below 2 in the studio stay out.

## Music and sound: `Art/Audio`

- `BossThemePrincess.wav`, `BossThemeGaunt.wav`: the bosses' themes,
  instrumental (YuE2, an epic orchestral battle style, 140 BPM; assets
  `01a111e1b565ff6333b7cf` and `01a111de44291fdc03a4e5`), loudness-normalised
  to −16 LUFS. `BossMusic.wav` (YuE) plays for a boss without a theme.
- The rest (ambience, footsteps, blows, the bonfire, …): Stable Audio Open
  1.0. `FogGate.wav` is the sound of a bonfire lit.
