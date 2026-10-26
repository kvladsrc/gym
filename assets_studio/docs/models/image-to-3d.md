# M0b. Image-to-3D

<!-- markdownlint-disable MD013 -->

## TripoSR — server removed (October 2026)

TripoSR and Stable Fast 3D were removed: neither matched Hunyuan3D-2mini →
Paint (findings below). Measurements retained for history.

Tested September 29, 2026 on an RTX 3070 Laptop (8 GB).

| | |
| --- | --- |
| License | MIT (code and weights) |
| Revisions | Source `107cefdc`, weights `5b521936` |
| Model load | 53 s (locally cached weights) |
| Generation | 6.4 s per 1024×1024 image, `mc_resolution=256` |
| Post-generation VRAM | ~3.2 GB (`nvidia-smi`, informational) |
| Platformer hero result | 32,298 vertices, 64,596 triangles, watertight, 1.3 MB GLB; matches previous prototype run |
| Contract check | 0 errors, 1 warning |

Warning: seed-independent output. TripoSR is feed-forward; identical images
yield identical meshes. Thus `max_count = 1`; 3D variants come from input-image
variants. Run checks with `--allow-seed-independent`.

Quality: silhouette/pose retained, simplified face, fingers and surface detail
(already checked in the now-removed platformer prototype). A fast shape draft.

## Comparison outcome (October 2026)

Stable Fast 3D and Hunyuan3D-2mini (license excludes the EU) were added and
compared with TripoSR on about 240 rated variants. Chosen pipeline:
**Qwen-Image → Hunyuan3D-2mini → Hunyuan3D-Paint**. Upright figures, plausible
hidden sides, source-image textures. Measurements, prompts and limitations:
[guide](../guides/3d.md). TRELLIS was not tested (likely exceeds 8 GB).

## TripoSG — on trial (October 2026)

Reddit comments claimed “Tripo 2” runs on 8 GB. No open Tripo 2 exists:
Tripo 2.0–3.x and P1.0/H3.1 are API-only. VAST's open models are TripoSR
(removed), TripoSG (shape, March 2025, MIT, README ≥8 GB) and TripoSplat
(2026, Gaussians only, no mesh). The comment most likely meant TripoSG;
added `triposg` (port 9115).

Tested October 16, 2026 on an RTX 3070 Laptop (8 GB).

| | |
| --- | --- |
| License | MIT (code and weights) |
| Revisions | Source `fc5c4099`, weights `2c1c516d` |
| Model load | 7.5 s (~7.5 GB cached fp32 weights, fp16 on GPU) |
| Generation | ~60 s per shape: 50 steps, 512³ grid |
| VRAM | Peak ~6.2 GB (`nvidia-smi`), GPU up to 63 °C |
| Mesh | 0.8–1.2M triangles, 5 of 6 not watertight |
| Contract check | 11 checks, 0 errors, 0 warnings |

Surface extraction uses diso (CUDA dual marching cubes), allocating buffers
outside PyTorch's allocator. With the transformer on GPU, orc/knight jobs ran
out of memory. diso does not check allocation failures, writes into invalid
memory and breaks the CUDA context; all subsequent jobs fail with `illegal
memory access`. The transformer and DINOv2 therefore move to RAM during extraction.

Comparison with Hunyuan3D-2mini on three “Finish the game” characters (hermit,
orc, ghost knight; same image, then same Paint with `max_faces=40000`):

- TripoSG gives sharper small details (muscles, folds, patches) but noisier
  surfaces and broken thin parts: knight fabric became strips, orc boot fur
  became threads, hermit face/beard became a lumpy mess.
- Hunyuan3D better retains silhouettes (TripoSG's orc is narrower than the
  reference) and overall coherence.
- Paint works on TripoSG shapes unchanged: axes/frame agree (Y up, front +Z,
  about 5% smaller scale).

### Random-sample comparison (`sweep:triposg1`)

30 library images sampled by category at any rating: plants, houses, animals,
characters, props, rocks and arches, five each (three rocks), and two dioramas.
Both models used defaults, then Paint (`max_faces` 60K). Comparison page:
`just sweep gallery`, four views per model (front, ¾, back, top).

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
- **Ties:** animals, props, arches, steps and characters, with minor differences
  in both directions.
- TripoSG sometimes rotates objects toward its own front (frontal chicken
  rather than the image's profile); Paint still works.

Decision: TripoSG is an alternative, not a replacement. Default Hunyuan3D-2mini;
TripoSG for branching forms and sharp architecture.

## Hunyuan3D-2.1 + Paint 2.1 — on trial (October 2026)

`hunyuan3d21` (shape) and `paint21` (PBR painting) run on 8 GB, versus official
requirements of ~10/~21 GB. Shape offloads by phase (7.1 GB peak). Painting
keeps only UNet/VAE on GPU, text encoder through accelerate hooks, DINOv2 for
one call, UNet feed-forward in 64-token chunks (6.1 GB peak for six 512 views).
Same `triposg1` sample: 2.1 + Paint 2.1 versus 2mini + Paint 2.0 (Paint 2.1 on
2mini shapes not tested).

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

Default remains 2mini: 2.1 takes 2.4× longer and worsens foliage. Use 2.1 for
ruins/architecture where 2mini invents features. Paint 2.1 is a candidate
default for game assets (PBR without baked light); test it on 2mini and TripoSG shapes.

## Contract findings

No contract v1 changes needed. Slow loading (53 seconds) correctly reports
`loading`; 1.3 MB base64 GLBs cause no issues. Deterministic models are represented
by `max_count = 1` and the checker flag.
