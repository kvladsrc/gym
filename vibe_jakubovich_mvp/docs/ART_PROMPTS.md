# Studio and host prompts

All three requests used the built-in OpenAI image-generation tool on 2026-09-05.
Selected results were copied into the project; original outputs were retained.

## Empty 1990s stage

Use case: historical-scene. Asset type: final full-screen 16:9 background for
a nostalgic 2D Godot game of a Russian 1990s TV word-guessing show. An empty
1993 television studio, front-facing wide camera from a contestant's
perspective, blue velvet curtains and a scalloped burgundy velvet proscenium,
champagne-gold metallic columns, incandescent round marquee bulbs, practical
stage lights overhead, cobalt and violet theatrical lighting with warm amber
pools, glossy dark parquet stage floor reflecting lights, slightly hazy
broadcast atmosphere. Realistic detailed painted adventure-game background,
tangible affordable TV scenery, lively and charming, not a luxurious hotel or
modern LED stage. Layout needed for the actual game: left two thirds has a
quiet broad dark blue wall upper half for a separately drawn letter board;
lower left is empty floor for an actual 3D wheel. Right third is a small empty
lit performance area for an animated full-body host added separately.
Architectural depth visible around these open areas. No people at all, no
host, no wheel, no podiums blocking foreground, no letters, no text, no logos,
no interface, no watermark. Image itself is finished game environment art, not
a screenshot or UI mockup. Landscape 16:9.

## Four-pose host sprite sheet

Use case: stylized-concept. Asset type: production 2D character animation
sprite sheet on a genuinely transparent RGBA background for a Godot game.
Reference image 1 is ONLY the identity/style reference for the same
recognizable friendly Leonid Yakubovich with his thick white moustache and
combed grey hair. Generate a new FULL BODY dancing character sprite sheet,
same older man, dark charcoal tuxedo, white shirt, black bow tie, polished
black shoes. FOUR equal-size animation cells in exactly ONE horizontal row: 4
columns x 1 row. Wide 2:1 overall image, ideally 2048x1024, each cell
512x1024. Exactly one full body in the center of each cell with generous
transparent margins. Same camera, body proportions, face, suit, scale,
lighting, head height and baseline in every frame. Each body from head to
shoes entirely inside its own cell. Sequence of a gentle comedic
television-host twist dance: frame1 weight on left foot elbows bent hands to
left; frame2 centered slightly bent knees hands up chest level; frame3 weight
on right foot elbows bent hands to right; frame4 centered upright hands
relaxed preparing to repeat frame1. Clear different arm and knee poses,
restrained dignified cheerful dancing, realistic anatomy. Finely painted
realistic adventure-game character rendering, not caricature. Front facing,
warm amber key light, subtle blue rim from television spotlights. No
background, no studio, no floor, no shadow painted outside body, no extra
characters, no duplicate ghost limbs, no labels, no numbers, no grid lines, no
text, no watermark. Preserve actual alpha transparency, do not paint a
checkerboard. All four heads equal size. Image 1 background and composition
must NOT be copied.

## Alpha extraction edit

Use case: background-extraction. Edit target: the attached FOUR-frame
horizontal dancing character sprite sheet. Remove the entire white/light-grey
checkerboard that was accidentally painted into the image. Deliver all four
figures as clean cutouts on a REAL TRANSPARENT alpha background, not any
painted background, not a checkerboard visualization. Keep the same four
full-body poses, face, suit, shoes, horizontal spacing, equal 4-column 1-row
layout, and canvas composition. Preserve fine edges of hair and moustache; no
halo. Every pixel outside the four human figures must have alpha=0. PNG with
real RGBA transparency. Do not restyle, do not add a ground shadow, do not
move limbs. This will be composited on a dark blue stage in a game, so actual
transparency is essential.
