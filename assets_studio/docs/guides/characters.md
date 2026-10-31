# Characters: rig-ready concepts, rigs and animation

<!-- markdownlint-disable MD013 -->

## Pipeline

Concept (Qwen-Image) → Hunyuan3D-2mini (`max_faces` 40000) → Paint → `3d-to-rig`
(Make-It-Animatable: Mixamo skeleton, 52 `mixamorig:*` bones and weights) →
engine → Mixamo clips. Rigging is last, for humanoids only; props and terrain
need no rigs.

For non-humanoids (animals, birds, objects), use `skintokens`: shape-specific
skeletons without Mixamo clips ([comparison](../models/3d-to-rig.md)).
Quadrupeds animated from AI video with a hand-placed skeleton:
[quadrupeds.md](quadrupeds.md).

## Rig-ready concepts

> Full body single *(character)*, orthographic FRONT view at eye level,
> perfectly symmetrical neutral A-pose, arms straight diagonally down 45
> degrees away from the body, open empty hands, legs apart with a clear gap
> between the thighs down to the crotch, feet flat on the ground. *(Body,
> clothing, 2–3 colors.)* One cohesive figure. No tail, no wings, no cape, no
> skirt, no robe, no weapons, no loose straps, nothing floating. Stylized
> hand-painted game character art, strong chunky silhouette, broad clean
> color areas. Neutral matte diffuse studio lighting, no harsh cast
> shadows. Entire figure visible with generous margins, centered on plain
> white background. No text.

Why these rules matter:

- **Empty hands.** Rigging spreads held weapons across hand and forearm weights,
  making them bend like rubber. Use a separate weapon on the hand bone, positioned
  from finger bones.
- **Nothing below the waist hiding legs** (skirts, robes, long capes): the
  rig places knees where fabric resembles legs, producing short legs.
- **No tails, wings, capes or loose straps:** the rig binds them to nearby
  bones, producing webs during animation.
- **A-pose, exact front view, legs apart:** the rig finds joints from the silhouette.
- Drawings outside the template (even children's drawings) can first be edited
  image-to-image into A-pose and a simple style, then run through the pipeline.

## Mesh after rigging

The recommended pipeline returns welded meshes with smooth shading. Unwelded
meshes (independent triangle vertices: 144K vertices for 60K triangles) with
flat normals look faceted in-game with dark edge lines. Weld vertices by
position (coincident vertices have identical weights, keeping animated seams
closed) and use smooth normals.

## Animation

Build repeatable chains in [Studio Flow](../../flow/README.md): mesh → MIA,
text → Kimodo in parallel, then both outputs → `animation_tools / retarget-motion`.
Weight preparation, old-animation reset and rotation transfer happen inside
the task. For video, pass MIA rigs directly to GVHMR or MoCapAnything; this
step is already integrated. No separate clip-application scripts are needed.

Video motion transfers onto Make-It-Animatable rigs with
[GVHMR](../../model_servers/gvhmr/README.md): MP4 video + FBX/GLB rig → animated
FBX. Requires one full-body person in a continuous shot up to 10 seconds.
`camera` selects static or moving camera; `root_motion=in-place` keeps motion
in place, `world` preserves travel. Fingers and face are not animated.
Registered SMPL-X is required. Tested capture from a four-second video, both
camera modes, and FBX import into Unity 6.6 with bone-pose changes verified.

- **Generic, not Humanoid.** Unity Humanoid retargeting stiffened legs: running
  knees bent to 72° instead of Mixamo's 32°. Generic plays bone for bone
  (32…163°, matching Mixamo's site). Put Animator on the node above the hips
  (`Armature`), where clip paths begin.
- **Make clips in-place manually.** Generic root motion failed to remove hip
  drift: hips moved 2.8 m forward and snapped back every loop. Subtract linear
  ground-plane hip drift from a clip copy (for jumps also remove hip rise,
  leaving height to physics) to keep hips within ±8 cm of the body.
- **Any Mixamo clip works with any `3d-to-rig` rig:** the skeleton is shared.
  Download packs without skin; individual clips target a rig uploaded to Mixamo.
- **Derive step speed from the clip.** Scale running playback to character
  speed using the clip's own root motion; otherwise feet slide. Sprinting
  needs a separate clip rather than sped-up running.
- **Derive hit timing from the clip.** Register impact when arms and legs
  rotate fastest (sum limb-bone angular speeds across 15–85% of the clip),
  rather than at a fixed time. Works for swords, spells and kicks without
  manual annotation.
- **Multiple attacks per character** (one state per clip, selected by an index),
  never the same twice in a row, make combat less repetitive.
- **Stance is not locomotion.** Swaying stationary stances (capoeira ginga;
  its “forward” means rocking with a step) look static as walking. Use them
  for idle between attacks and ordinary clips for walking/running.
- Mixamo has only one standing roll in its packs: “falling to roll” (Action
  Adventure), first 1.4 seconds.
