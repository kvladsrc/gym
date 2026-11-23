# Characters: rig-ready concepts, rigs and animation

<!-- markdownlint-disable MD013 -->

## Pipeline

Concept (Qwen-Image) → painted mesh ([3D pipeline](3d.md), about 40K
triangles for a character) → `3d-to-rig`
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
> shadows. Entire figure visible with generous margins, centered, isolated,
> no background. No text.

Generate it with `transparent` on, as in the
[3D pipeline](3d.md#transparent-references). Why these rules matter:

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

## Faces from two pictures (frommhr)

`frommhr` and `fromsoma` build a person from two pictures of the bald head,
front and left profile ([server README](../../model_servers/fromsoma/README.md)).
How different the people come out is decided by the pictures:

- **Draw the face shape pronounced, even exaggerated.** The reader keeps only
  part of the difference between two pictures, so faces that differ a little
  in the pictures become one face. Name the shape in the prompt and push it:
  face length and width, cheekbones, jaw width and corners, chin, nose (bridge,
  length, tip), eye size and spacing, brow ridge. Skin colour, eyebrows and
  expression do not change the head's shape.
- **Measured on six adults** (front pictures and front renders of the heads,
  478 MediaPipe Face Landmarker points after a similarity fit, in % of the
  eyes-to-mouth height). Drawn with deliberately different, named shapes, the
  pictures lay 7–8 apart. frommhr's heads kept 0.75 of that for stylised
  game-render pictures and 0.90 for photographs. Pictures generated without
  shape cues lay 3.2 apart, about 0.6 of it reached the heads, and pairs at 1
  came out as one face.
- **Check the pictures before the jobs.** One face drawn in two styles differs
  by 1–2.5 on this measure, so pictures closer than about 3 give the same head.
  Redraw such pairs unless they are meant to look alike (siblings).
- **Stylised pictures are fine:** they lose a little more than photographs.
  Women's heads looked coarser than their pictures, so push feminine features
  further.
- MHR's head space holds adult heads only; a child's head needs `fromsoma`.
- **Game-like colouring.** Freckles, blush, a red nose and the lips are baked
  from the pictures (`texture=pictures`), so paint them into the pictures
  (an edit that changes only the painting keeps the shape: 0.4–1.9 on the
  measure above). The irises are not: the eyes are the server's own, their
  colour comes from the pigments and stays dull. The skin outside the face
  takes the pigments' tone, and the face's tone is pulled halfway to it.

## Hair cards (HairDeck)

`hairdeck` builds hair cards from one picture of a person with hair
([server README](../../model_servers/hairdeck/README.md)). It learnt from
renders of anime-style avatars: toon shading, outlines on most renders,
mostly front and three-quarter views. Tested on three hairstyles (long
straight, chin-length curls, short curls), each drawn four ways (game render
from the front and three-quarter, anime-style 3D avatar and 2D anime drawing
three-quarter), seeds 0–3; the best pictures also got seeds 4–7 and
guidance 3.5. "Bare" is the share of HairDeck's head proxy seen between the
cards from above and from behind:

- **Three-quarter view, head and shoulders,** plain light backdrop, soft even
  light, all of the hair in frame. It left less bare head than the front view
  for both curly styles (chin-length curls from behind: 1–9% against 4–16%).
- **Long straight hair works in every style.** The 2D anime drawing was
  cleanest: 0–1% bare from above on seeds 0–3, 0–20% on seeds 4–7.
- **Curls become wavy ribbons.** Chin-length curls came out best from the
  game render (4–19% bare from above); the anime styles were no better.
- **Short hair is the weak spot.** Short curls drawn plainly gave sparse,
  petal-like cards in every style, 26–62% bare from above on three seeds of
  four. Described as "one dense, solid cap of chunky clumps, no scalp
  showing" and drawn as an anime-style 3D avatar, they gave 7–34%; the same
  words in the game render did not help (11–53%). Even the best result is a
  shaggy cut with locks hanging down the sides, not a close cap of curls.
- **Seeds.** At guidance 2, seed 0 left the least bare head from above on
  all ten pictures of curly and short hair (of four seeds, and of eight on
  the two pictures that got eight), so a seed that worked on one picture is
  worth trying first on the next. Other seeds spread widely (chin-length
  curls, seeds 0–7: 4–24%): make four to eight and pick by the bare share
  and by eye.
- **Keep guidance at 2.** At 3.5 every picture got more cards but, by the
  median of four seeds, more bare head from above; short hair was 42–53%
  bare from above on every seed.
- Only long straight hair covered the head fully: under other hair, paint
  the scalp the colour of the hair.

**Hair on the person:** `animation_tools / 3d-fit-hair` takes the person
(`body`: `fromsoma` or `frommhr`) and the picked hair (`hair`) and returns
the person's GLB with the hair on the Head joint
([how it fits](../../model_servers/animation_tools/README.md)). The
hairstyle moves as one piece onto the scalp, so its cards keep their layers.
`thickness` 0.5–0.8 lays it closer when it looks too big on the head. Set
`braid` for a braid or ponytail, which HairDeck hangs straight down through
the back. The hair's bare spots are HairDeck's: pick the hair without them,
the fit does not fill them.

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
