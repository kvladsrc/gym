# Image to 3D

<!-- markdownlint-disable MD013 -->

## Current chain

Qwen-Image → `hunyuan3d21` (Hunyuan3D-2.1 Shape) → `hi3d` (Hi3D v1.0
refinement, simplified to the game budget by its `max_faces`) →
`trellis2_paint` (TRELLIS.2 Paint, PBR). Every model rated 5 so far came out
of this chain; the [3D guide](../guides/3d.md) gives its settings and
prompts. `animation_tools` can reduce a painted mesh further (`3d-retopo`
with `decimate` keeps the paint) and bake a normal map from a denser Hi3D
mesh (`3d-bake-normals`). `paint21` (Hunyuan3D-Paint 2.1) paints from the
source image instead. Memory, timing and limitations are in each server's
README:
[Hunyuan3D-2.1](../../model_servers/hunyuan3d21/README.md),
[Hi3D](../../model_servers/hi3d/README.md),
[TRELLIS.2 Paint](../../model_servers/trellis2_paint/README.md),
[animation tools](../../model_servers/animation_tools/README.md).

The comparisons below were made in October 2026 on an RTX 3070 Laptop (8 GB),
before Hi3D and TRELLIS.2 Paint were added. Hunyuan3D-2mini, Hunyuan3D-Paint
2.0 and TripoSG are retired; their servers' code stays in `model_servers/`.

## Hunyuan3D-2.1 + Paint 2.1 versus 2mini + Paint 2.0

`hunyuan3d21` (shape) and `paint21` (PBR painting) run on 8 GB, versus official
requirements of ~10/~21 GB. Shape offloads by phase (7.1 GB peak). Painting
keeps only UNet/VAE on GPU, text encoder through accelerate hooks, DINOv2 for
one call, UNet feed-forward in 64-token chunks (6.1 GB peak for six 512 views).
Same `triposg1` sample as below: 2.1 + Paint 2.1 versus 2mini + Paint 2.0.

| | 2mini + Paint 2.0 | 2.1 + Paint 2.1 |
| --- | --- | --- |
| Median shape time | 82 s | 197 s |
| Median paint time | 82 s | 107 s |
| Watertight meshes | 30 of 30 | 30 of 30 |
| Material | Color (baked shadows) | PBR: color + metallic/roughness |

- **2.1 shape improves:** ruin fidelity (no invented second gable), eagle
  feathers, sharper stone and bark.
- **2.1 shape worsens:** foliage (deciduous crown becomes smooth balloons,
  spruce tiers flat disks), smoother chicken; sometimes rotates animals away
  from the reference view (frontal horse).
- **Otherwise tied:** characters, props, steps and dioramas (both level).
- **Paint 2.1 improves:** no baked shadows, brighter/even color appropriate
  for engine lighting, separate metallic/roughness, better rune-arch/ruin colors.
- **Paint 2.1 sometimes worsens:** wolf darker than reference, goblin legs
  pinker; 2.0 shadows suggest volume when no lighting is present.

## TripoSG versus Hunyuan3D-2mini (`sweep:triposg1`)

Tripo 2.0–3.x are API-only; TripoSG (VAST, March 2025, MIT) is their open
shape model, and its README asks for 8 GB. Defaults: 50 steps on a 512³ grid,
~6.2 GB peak.

30 library images sampled by category at any rating: plants, houses, animals,
characters, props, rocks and arches, five each (three rocks), and two dioramas.
Both models used defaults, then Paint 2.0 (`max_faces` 60K), and were compared
in four views per model (front, ¾, back, top).

| | Hunyuan3D-2mini | TripoSG |
| --- | --- | --- |
| Median shape time | 82 s | 65 s |
| Median triangles (max) | 0.87M (2.0M) | 1.31M (4.6M) |
| Watertight meshes | 30 of 30 | 13 of 30 |
| Median Paint time | 82 s | 95 s (more simplification) |

- **TripoSG wins:** thin branching structures (more fine dead-tree branches,
  separated spruce tiers), architectural edges (frames, timber framing),
  image fidelity where Hunyuan invents details (a second ruined-chapel gable),
  front/back character sheets without floating debris.
- **Hunyuan wins:** dioramas stand level with flat bases and interior details,
  while TripoSG bakes isometric tilt and tears floors; organic rocks (TripoSG's
  menhir became a flat faceted slab), dense foliage (TripoSG bushes noisier
  and darker after Paint), watertightness.
- **Ties:** animals, props, arches, steps and characters, with minor
  differences in both directions.
- TripoSG sometimes rotates objects toward its own front (frontal chicken
  rather than the image's profile); Paint still works.
- On three more characters (hermit, orc, ghost knight), TripoSG gave sharper
  small details but noisier surfaces and broke thin parts (cloth into strips,
  boot fur into threads); Hunyuan kept the silhouettes and overall coherence.

TripoSG was kept as an alternative for branching forms and sharp architecture,
not as a replacement.

## The first sweep

About 240 rated variants compared TripoSR, Stable Fast 3D and Hunyuan3D-2mini
(with FLUX or Qwen-Image images). Hunyuan3D gave upright figures, plausible
hidden sides and source-image textures; TripoSR and Stable Fast 3D were
removed. Measurements: [3D guide](../guides/3d.md).
