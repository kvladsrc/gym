# Procedural island: design

Status: draft for implementation, 2026-10-30. Builds on
[`RESEARCH.md`](RESEARCH.md), a second, implementation-oriented search and a
numeric prototype (section [Algorithm choice](#algorithm-choice)). Reviewed
by Codex the same day; its findings are folded in.

## Goal

Generate one island level from a seed and a few parameters: relief that
looks eroded, rivers and lakes that always drain to the sea, and vegetation
placed by its environment. It must look plausible when walking through it in
third person, and it must stay simple enough to implement, test and debug in
a few milestones.

The level is baked once, on demand, by a procedure that may take minutes;
the game ships the baked result. Nothing is painted by hand: a seed and the
recipe's parameters are the whole input.

Placeholders are primitives (cylinders, cones, spheres, cubes) with flat
colors. Assets from `assets_studio/` replace them later through one prefab
reference per species; no generator code changes for that.

Not in v1: generation at game start, streamed worlds, caves and overhangs, water
simulation (floods, moving rivers), plant growth over time, runtime GPU
placement, final art and water shaders.

## Tools

State on 2026-10-30:

| Tool | State | Role |
| --- | --- | --- |
| Unity 6000.6.0f1, URP 17.6, `unity/procedural` | Opened by the user from Hub | Everything is generated here, in C# |
| Unity MCP (`com.unity.pipeline` 0.9.1-exp.1) | Works through `.mcp.json` server `unity-procedural`: initialize, `tools/list` (166 tools), `scene list_open` | Drive the Editor: menu items, `eval`, console, tests, captures |
| Unity CLI `unity command --project-path …` | Works | Fallback, and for tools whose MCP names collide |
| Blender MCP | Answers `get_scene_info` | Not needed: no step of the generator uses Blender |
| Studio MCP | Available | Only when placeholders are replaced |
| Codex (`env -u OPENAI_API_KEY codex exec`) | Allowed by the user | Delegated reviews; its vision checks screenshots and debug maps |

Notes:

- **Unity Search indexing fills the disk in a new project.** The first
  indexing pass grew `Library/Search/*.index` to 54 GB, filled the disk and
  the Editor exited. Fixed by deleting `Library/Search`, renaming
  `UserSettings/Search.index` to `Search.index.disabled` and setting
  `indexOnEditorStartup = false`. Check `df -h /` after large imports.
- **Colliding MCP tool names.** Pipeline 0.9.1 exposes names such as
  `status`, `get`, `create` and `run` in several groups; through MCP they fail
  with "names several commands". Unique tools work (`eval`, `invoke_menu`,
  `get_hierarchy`, `list_open`, `screenshot`). For the others use
  `unity command <group> <name>`. Native MCP tools appear in Claude Code
  after a client restart.
- **Players use Vulkan, natively on Wayland.** With OpenGL through XWayland
  the NVIDIA driver raised Xid 13 "Const out of Bound" within a minute of
  play; frames then waited 5 s each and the process grew to 13 GB.
  `play.sh` adds `-force-wayland`; Linux graphics APIs are Vulkan, then
  OpenGL Core. Players run in the background and cap at 60 FPS.
- **No GPU Resident Drawer.** The Editor runs on OpenGL here and logs
  "current platform does not support BatchBufferTarget". Rely on Terrain's
  tree and detail instancing and on the SRP Batcher.
- Generation runs on the CPU, on one worker thread beside the Editor. The
  laptop GPU stays free for Studio models.

## Scope

Decided with the user on 2026-10-30:

1. **Scale**: an island of 1–2 km, explored on foot in third person.
2. **Climate**: temperate to start: conifer and broadleaf forest, meadows,
   reeds, beaches, rocky slopes. It matches the woodland assets the studio
   already made for `games/soulslike`.
3. **When**: a one-off bake on demand, allowed to take minutes. Not at
   game start, not streamed. Quality wins over speed in the bake; a fast
   preview exists only for choosing parameters.
4. **Control**: a seed and parameters, no hand-painted maps. Parameters are
   high-level and hit their targets by construction where possible
   (section [Recipe](#recipe)).

| Parameter | Small (development, tests) | Medium |
| --- | --- | --- |
| World size | 1024 × 1024 m | 2048 × 2048 m |
| Simulation grid (relief) | 256² (4 m) | 512² (4 m); 1024² (2 m) if it looks better |
| Terrain heightmap | 1025² (1 m) | 2049² (1 m) |
| Highest peak | 100 m | 160 m |
| Sea depth at the map edge | 30 m | 50 m |
| Preview (relief, water, no plants) | ≤ 10 s | ≤ 30 s |
| Full bake | ≤ 2 min | ≤ 10 min |

- The core stays free of Editor-only APIs anyway: it is easier to test, and
  batch mode can run it.
- Same seed and recipe give the same island on the same machine and build.
  Bitwise equality across platforms is not required.

## Algorithm choice

The research report proposes Fischer et al. 2022 for relief and rivers.
A second search compared it with simpler alternatives, and a numeric
prototype ([`tools/island_proto.py`](tools/island_proto.py)) measured three
of them on the same island mask.

### Candidates

<!-- markdownlint-disable MD013 -->

| # | Approach | Realism | Implementation | Verdict |
| --- | --- | --- | --- | --- |
| 1 | Noise × island mask, then hydrology: Priority-Flood, receivers, flow accumulation | Rivers follow noise; their profiles are not concave | Smallest | Scaffold for M1; the hydrology core is reused by everything |
| 2 | Rivers first: random spanning forest from outlets, height grown from the water with noise (Fischer 2022; Red Blob's river growing) | Network guaranteed, but ridges and valleys are noise growth, not erosion. Red Blob's "elevation from rivers" did not work out | Small, but needs markers for coasts and regions | Borrow lakes-on-rivers only |
| 3a | Uplift + stream power, time-stepped implicit solver (Braun & Willett 2013; Cordonnier 2016; Schott 2023 is the interactive GPU version) | Dendritic valleys, ridges, concave profiles | 1 plus one sweep per iteration; `dt`, `K`, iteration count to tune | Fallback |
| 3b | Uplift + stream power, steady state solved directly along the network: `h = h_receiver + U·d / A^m` | As 3a, closer to theory, steadier metrics | 1 plus one sweep per iteration; only `m` and the uplift map to tune | **Chosen for the macro relief** |
| 4 | Analytical stream-power erosion with hillslopes and landslides (Tzathas 2024) | Higher than 3b | No public code found | Read the paper if 3b's ridges look too sharp |
| 5 | Hydrology construction trees (Génevaux 2013) | High | High | No |
| 6 | Dendry (2019), plate-local rivers (2026 preprint) | Local evaluation for endless worlds | Medium–high | Only if the world must stream |
| 7 | Particle hydraulic erosion (Beyer 2015; Lague, MIT) | Gullies, fans at metre scale | Small | Optional detail pass |
| 8 | GPU pipe-model erosion (Mei 2007) | Detailed | Medium, GPU | No: the GPU is shared with Studio |
| 9 | Suitability maps → density → spaced placement, layered (Onrust 2017; Lane & Prusinkiewicz 2002; Horizon Zero Dawn, GDC 2017) | Forest edges, riparian belts, groves, clearings | Small | **Chosen for vegetation** |
| 10 | Competition and self-thinning simulation (Deussen 1998) | Natural size mix and spacing | Small | v2 |
| 11 | EcoLearn (Kapp 2020); ecosystem–erosion coupling (Cordonnier 2017) | Highest | ML and data, or long simulations | No |
| 12 | DeadWood (Peytavie 2024) | Measured gain in perceived realism | A subset is trivial: logs and snags | Subset in the vegetation milestone |
| 13 | River ribbons from the network, hydraulic geometry, carved channels; lakes at the spill level; a sea plane | Adequate | Small | **v1 water** |
| 14 | Procedural Riverscapes (Peytavie 2019) | Animated flow, rapids, foam | Medium–high | v2 ideas: flow-aligned UVs, speed from discharge |

<!-- markdownlint-enable MD013 -->

### Prototype measurements

Python/NumPy, 256² cells, one island mask per seed (7 and 11). Channels are
cells draining at least 0.2 % of the land. 3a: 40 iterations, `dt = 25`;
3b: 15 iterations.

<!-- markdownlint-disable MD013 -->

| Metric | Real rivers | 1: noise | 3a: time-stepped | 3b: steady state |
| --- | --- | --- | --- | --- |
| Concavity θ in S ∝ A^−θ (± s.e.) | roughly 0.35–0.7 | 0.14, 0.17 (±0.02) | 0.72, 0.60 (±0.03) | 0.58, 0.53 (±0.02) |
| Hack h in L ∝ A^h, all channel cells | roughly 0.5–0.6 | 0.45, 0.44 | 0.60, 0.60 | 0.59, 0.59 |
| Hack h across river mouths (22–41 basins) | roughly 0.5–0.6 | 0.48, 0.39 | 0.54, 0.48 | 0.57, 0.57 |
| Highest Strahler order | — | 3, 3 | 3, 3 | 4, 4 |
| Land cells in pits | — | 1.7 %, 1.0 % | 0 | 0 |
| θ with channels at 0.5 % (seed 7) | — | 0.13 | 0.93 | 0.60 |
| θ after 80 / 30 iterations (seed 7) | — | — | 0.73 | 0.61 |
| Time, pure Python, one thread | — | one pass | 8–10 s | 3–4 s |

<!-- markdownlint-enable MD013 -->

Reading the numbers:

- Noise rivers barely steepen upstream (θ ≈ 0.15); that is why such rivers
  look wrong. Both stream-power variants give realistic slope–area and
  length–area scaling.
- 3b stays near θ = m/n = 0.5 (uplift that varies in space shifts it), does
  not drift with the channel threshold or the iteration count, and runs in
  less than half the time. In 3a θ depends on the threshold (0.72 → 0.93).
- 3b's receivers still change in 1.2–1.5 % of cells after 15 iterations and
  0.3–0.4 % after 30: drainage divides keep moving slightly. Stop by an
  iteration count or by that fraction, not by height change.
- The fits are descriptive: pooled over nested catchments, one resolution,
  one mask family. They show a large qualitative gap, not calibrated
  realism. The Unity report computes them per basin as a regression check.

### Decision

One regular grid and one hydrology core, used by every stage:

1. Scaffold the island with approach 1 (M1): mask, noise relief, hydrology,
   lakes, rivers, Unity Terrain, water placeholders, report.
2. Replace the noise relief with 3b (M2). Keep 3a behind a setting for
   comparison. Nothing downstream changes.
3. Lakes and rivers are extracted from the final heightmap, and hydrology is
   computed again after rivers are carved, so water follows the terrain that
   is actually built.
4. Vegetation by approach 9, with the DeadWood subset.

## Pipeline

```text
seed + IslandRecipe
 1 Shape      island mask, uplift U, sea floor              sim grid
 2 Relief     loop: route → steady-state sweep              sim grid
 3 Upsample   bicubic + detail noise, coast profile         heightmap
 4 Lakes      bowls on rivers → flood → basins, levels      heightmap
 5 Rivers     channels → water profile → carve              heightmap
 6 Hydrology  again on the carved terrain: Q, wetness       heightmap
 7 Moisture   wetness + distance to water (+ rain, v2)
 8 Surface    sand / grass / rock / gravel weights
 9 Plants     suitability → density → layered placement
10 Build      TerrainData, water meshes, instances, colliders
11 Report     invariants, metrics, timings → JSON
```

Each stage reads earlier maps and writes its own. Its random generator is
seeded by `hash(seed, stage name)`, so changing plant parameters does not
change the relief. Stage results are saved under
`artifacts/bake/<recipe hash>/`, so a bake resumes from the first changed or
failed stage.

Two modes:

- **Preview**: stages 1–6 at half the heightmap resolution, Terrain and
  water built, no plants. For choosing a seed and parameters.
- **Bake**: everything at full resolution with the quality options on, then
  the report.

## Recipe

The recipe is a committed asset: a seed and parameters a designer would
name. Each parameter is met by construction or by a cheap calibration, not
by trying seeds.

<!-- markdownlint-disable MD013 -->

| Parameter | Default | How it is met |
| --- | --- | --- |
| Seed | — | Hashed with the stage name for every stage |
| Preset | Small | World size and grid resolutions |
| Land fraction | 0.45 | Sea level of the mask set at that quantile; the largest landmass kept, islets below a minimum size removed |
| Coast roughness | medium | Amplitude and octaves of the warped coastline noise |
| Elongation, orientation | 1.0, from the seed | Anisotropic scale of the radial falloff |
| Mountain massifs | 2 | Number of uplift blobs placed inside the land, ridged noise between them |
| Highest peak | preset | Bisection on the relief scale; met within ±5 % |
| Ruggedness | medium | `m`, detail-noise amplitude, talus angle |
| River density | medium | Channel threshold set at the catchment quantile that gives the target river length per km² |
| Lakes | 2 | Number of bowls dug on rivers |
| Beach share of the coast | 0.4 | Coast cells with uplift below that quantile get a beach profile |
| Forest cover | 0.45 | Species densities rescaled in one or two passes until achieved cover is within ±3 % |
| Species set | temperate | List of `Species` assets |

<!-- markdownlint-enable MD013 -->

Bad seeds are rejected by looking at the preview and the report. An
automatic "try N seeds, keep the best by metrics" can come later.

## Stages

### Hydrology core

Shared by stages 2, 4, 5 and 6. No epsilon: physical fill levels and the
routing graph are kept separate.

1. **Priority-Flood** (Barnes et al. 2014): seed a min-heap with sea cells
   next to land; pop the lowest; for each unvisited neighbour set
   `fill = max(h, fill[popped])` and `parent = popped`. Ties pop first-in
   first-out. `fill` is the physical water level of pits and lakes (flat
   there); `fill > h` marks flooded cells.
2. **Receivers**: steepest descent on `fill` among strictly lower
   neighbours; where there is none (lake surfaces, flats) the flood parent.
   The graph is acyclic and every land cell reaches the sea.
3. **Stack** (Braun & Willett 2013): breadth-first from land cells whose
   receiver is sea, over donor lists. Receivers come before donors; no sort.
4. **Accumulation**: in reverse stack order, `Q[receiver] += Q[cell]`,
   starting from `Q = rain × cell area` (rain = 1 in v1).

Cost per pass: the flood is O(N log N), the rest O(N). Stack, accumulation
and the relief sweep depend on earlier cells, so they stay sequential. The
core uses its own binary heap with an insertion counter for FIFO ties.

### 1. Shape and uplift

- Land mask: `1 − r/R + a·(fBm − 0.5)` with domain warping, `r` stretched by
  the elongation; coastRoughness adds embayments and headlands; sea within
  5 % of the map edge. Sea level is the mask quantile that gives the recipe's
  land fraction; the largest landmass is kept.
- Uplift: a low base plus distinct, possibly elongated massifs at seeded
  places, so lowlands and saddles exist between them and along much of the
  coast. It replaces a painted map: massif count, elongation and ruggedness
  from the recipe shape it.
- Sea floor: depth from the exact Euclidean distance to the coast, smoothed,
  down to the edge depth.

### 2. Relief (stream power with hillslopes)

On the simulation grid, in two phases (implementation details and measured
numbers: [`tools/core/README.md`](tools/core/README.md)):

```text
growth, from small noise, until channels stop changing:
    route h with the hydrology core (receivers, stack, area A)
    for cell in stack order (receivers first):
        h[cell] = h[receiver] + distance · s·U[cell]·A[cell]^−m      # sea: 0
shaping, on the settled network:
    slope = s·U·g(A), g(A) = A/A_c·A_c^−m below the channel head A_c,
            A^−m above it, capped at the critical slope
    s found by bisection so the highest point equals the recipe's peak
    then light diffusion of hillslopes
```

- Growth is the steady state of `∂h/∂t = U − K·A^m·S` with `n = 1`; `K` only
  scales heights. Without the hillslope branch, slope ∝ A^−m is largest on
  the smallest catchments and nearly every hillslope cell hit the 38° cap
  (83 % rock at a 200 m peak). The branch gives the slope–area relation of
  real landscapes, rising from divides to channel heads and falling along
  channels (Montgomery & Foufoula-Georgiou 1993): convex hilltops, gentle
  lowlands.
- `m = 0.65`; `A_c` from ruggedness. Receivers change only for a clearly
  steeper descent (5 % hysteresis); growth stops after three stable
  iterations, with channel flips at 0 on the test seeds.
- A jittered-node variant (irregular sampling as in Cordonnier et al. 2016)
  was tried and not kept: it did not raise the Hack exponent enough.
- Fallback 3a (Fastscape order) exists as a ReliefMode for comparison.

### 3. Upsample and detail

- Bicubic (Catmull–Rom) to the heightmap resolution, after smoothing the
  macro relief.
- fBm detail of a few metres, scaled by local relief and faded out near
  channels so the noise does not create new valleys or dams.
- Beaches: a 32 m ramp on the beachShare of the coast with the lowest
  uplift, from exact Euclidean distances (no grid-direction striping);
  cliffs elsewhere. The achieved sandy share of the coastline is reported.
- Floodplains: along low-gradient reaches above a discharge, an alluvial
  valley floor of width ∝ Q^0.5 just above the river surface, blended into
  the slopes.
- Micro-relief (recipe `microRelief`, 0..1): on rock (24–38° and steeper)
  a terraced height along strata dipping with noise, 1.5–4 m apart (a
  monotone map of height, so no new pits) plus ridged cracks; scree bumps
  on 15–30° below rock; soil hummocks at 6, 3 and 1.5 m elsewhere. Smooth
  within 2–10 m of channels, on beaches and at the shore. Small bakes use a
  0.5 m heightmap (2049²).
- Particle hydraulic erosion (Beyer 2015) is implemented but off: it added
  granular roughness, raised slope p90 and rock share and cost about 3 s.

### 4. Lakes

Relief has no depressions of its own, so lakes are made where a river runs
in a narrow valley with real inflow, away from the coast:

1. Build a natural dam across the valley (bank to bank, bowed, a few metres
   above the bed) from a fixed pre-edit baseline.
2. Flood: the lake fills the valley upstream and its shore follows the
   contours (irregular shore deposits soften it). Level = spill height of the
   one protected outlet over the dam's low point. Every lake must keep a
   qualifying inflow; side pockets are filled.
3. Spill levels are grouped with a 0.1 mm tolerance and compared in double
   precision, so Unity's Mono and CoreCLR agree.

Levels and outlets are checked again after carving (stage 6); a lake whose
level changed or that lost its outlet is a test failure.

### 5. Rivers

1. Channel cells: catchment above the threshold derived from the recipe's
   river density. Segments run between sources, confluences, lakes and
   mouths; Strahler order per segment.
2. Geometry (downstream hydraulic geometry, Leopold & Maddock 1953):
   `width = W_max·(Q/Q_max)^0.5`, `depth = D_max·(Q/Q_max)^0.4`, minimum
   width two heightmap cells, tapered at sources. `W_max` and `D_max` are
   settings.
3. **Water surface first.** Walk upstream from each mouth (level 0) and
   each lake outlet (lake level); at every point the surface is
   `max(downstream surface, terrain − depth/2)`, then a 10 m arc-length
   filter. It never rises downstream. A tributary starts at the surface of
   the river it joins. Steep steps are marked as rapids or waterfalls. The
   bed is `surface − depth`.
4. Carve once, from the original heights, along the smoothed and slightly
   sinuous polyline (not raster cells): a parabolic section, banks blended
   back to the terrain, containment berms where needed.
5. Mesh: a ribbon at the water surface along the same polyline, `u` across,
   `v` along the length in units of width (ready for a flow texture).
   Mouths extend into the sea; confluences share the surface height.

### 6. Hydrology again

Run the core on the carved heightmap. Unexpected new pits (not lakes) are
filled; the count goes to the report. On top of the final routing:

- Discharge `Q` in m² of catchment (rain-weighted in v2).
- Topographic wetness `TWI = ln(a / tan β)` (Beven & Kirkby 1979):
  `a = catchment area / cell width` in metres; `β` from central differences
  on the physical heights with a floor of 0.5°. Catchment for TWI uses
  multiple flow directions (all lower neighbours, weights ∝ slope^1.1 as in
  Freeman 1991), accumulated in a topological (Kahn) order of all descending
  edges, so wetness has no grid-aligned streaks; channels keep the single
  receiver. `Wetness` is TWI normalized by its 5th–95th land percentiles;
  water cells get 1.

### 7. Moisture

v1: `moisture = clamp01(0.50·TWI' + 0.35·exp(−d_water/40 m) +
0.15·(1 − z/z_max))` plus ±0.025 noise at 96 m. `TWI'` is TWI normalized
by its 5th and 95th percentiles over land; `d_water` is the exact Euclidean
distance to the river channels (tapered capsules along the curves) and
lakes. The sea is not a moisture source.

v2: wind-driven rain as in mapgen4: carry humidity along the wind, rain
falls more on rising slopes, leaving a rain shadow on the lee side. The rain
map then weights `Q`, so windward rivers grow larger.

### 8. Surface

Four Terrain layers with flat-color textures, blended with smoothsteps and
normalized to sum 1 per cell:

| Layer | Rule |
| --- | --- |
| Sand | Beach ramps; low, gentle ground near the sea |
| Rock | Slope from a smoothed (4 m) height field, blended over rise/run 0.46–0.90 (25–42°), with seeded 45 m variation |
| Gravel | Within the analytic distance from the river curve to its banks; lake shores |
| Grass/soil | Everything else; darkened under forest in v2 |

Per-point slopes made the rock layer amplify centimetre-scale height ribbing
into visible bands; the smoothed slope field removed them.

Layers carry procedural 512² textures made in the Editor
(`ProceduralTerrainTextures`): albedo with colour variation, a normal map
and a URP mask map (AO, height, smoothness) from a tileable height field per
material (sand ripples, clumpy soil, cracked rock, pebbles). Terrain Lit
blends layers by height (`_TERRAIN_BLEND_HEIGHT`, transition 0.2). Studio
textures replace them later.

### 9. Vegetation

The core places plants; the Unity side only maps species ids to prefabs
(`SpeciesCatalog`), so Studio models replace placeholders without code
changes. Species and their numbers: `SpeciesTable.Temperate` in
`Generation/Vegetation.cs`.

<!-- markdownlint-disable MD013 -->

| Species | Layer | Built as | Footprint / crown | Where it grows |
| --- | --- | --- | --- | --- |
| Pine | Canopy | Terrain tree | 1.8 / 2.2 m | Drier ground (moisture 0.2–0.55), slope ≤ 28°, ≥ 5–20 m from the sea |
| Broadleaf | Canopy | Terrain tree | 2.6 / 3.5 m | Moist lowlands (0.45–0.85), slope ≤ 20°, below 55 % of the peak |
| Alder | Canopy | Terrain tree | 2 / 2.5 m | 2–15 m from water, moisture ≥ 0.7, slope ≤ 10° |
| Snag | Canopy | Terrain tree | 1.5 m | 6 % of pines and broadleaves inside forest (cover > 0.5 within ~15 m) |
| Shrub | Understory | Terrain tree | 1 m | Forest edges and under canopy (shade-tolerant) |
| Log | Prop | GameObject | 3 m | 0.05 per forest tree, near it, along the contour, pitched with the slope |
| Rock (boulder) | Prop | GameObject, convex collider | 1.5 m | Half buried, slope-aligned, gathered on and below rock (rock within ~8 m); may interpenetrate |
| Slab | Prop | GameObject, convex collider | 1.5 m | Flat, sunk a quarter deep, on rock and scree |
| Stone | Prop | Instanced, no collider | 0.3 m | Dense on gravel and scree, scattered elsewhere |
| Mound | Prop | Instanced, no collider | 1.2 m | Grassy hummocks on moist meadows |
| Grass | Ground | Terrain detail | — | Not on sand or rock; thinned to 15 % under full canopy |
| Reed | Ground | Terrain detail | — | Within ~2–5 m of water, flat, moisture ≥ 0.8 |

<!-- markdownlint-enable MD013 -->

Suitability of species `s` at point `x` is a product of trapezoids over
elevation, slope, moisture, distance to water and distance to the sea,
times a surface-layer affinity: `v_s(x) = Π trapezoid(f(x)) · Σ affinity ·
weight`. Target density in plants per m²: `D_s = v_s × maxDensity_s ×
cluster_s(x)`, where `cluster_s` is low-frequency noise remapped to
[0.2, 1] for groves and clearings.

Placement per layer, larger layers first (the global-to-local model of
Lane & Prusinkiewicz 2002):

1. Candidates: one jittered point per 2 m cell (4 m for rocks), each with
   its own seeded random numbers, so calibration passes are comparable.
2. Keep a candidate with probability `Σ_s D_s × s²`; pick its species in
   proportion to `D_s`; scale grows with suitability; random yaw.
3. Spacing with final, scaled radii through a spatial hash, symmetric for
   every pair: `distance ≥ 0.9·(r_a + r_b)`. Shade-tolerant plants and
   props under a canopy tree keep only trunk clearance. Footprints are about
   0.8 of the crown so crowns can close.
4. Forest cover = share of dry land under crowns on a 2 m raster. The canopy
   is placed again with densities scaled by `target / achieved` until it is
   within ±3 % (at most six passes); snags, logs, understory and rocks come
   after the last pass.
5. Grass and reeds become Terrain detail coverage maps (0..1, stored 0–255
   in coverage scatter mode).

Forest cover defaults to 30 %. Tree clusters fall to 0 (real clearings) and
the chance to keep a candidate is capped at 0.6, so even a grove's heart is
not packed into a wall of trees; the flat-map ceiling is then about 57 %.

Props may be buried (`Bury`, share of the footprint below ground), laid on
the terrain normal (`AlignToSlope`; Unity rotation Euler(0, Yaw, 0) ·
Euler(Roll, 0, Tilt)) and allowed to interpenetrate (`Spacing` below 0.9).
Logs are tested as 6 m capsules against trunks. Stones and mounds are drawn
by `InstancedProps` (64 m chunks, distance and frustum culling).

Trees and shrubs stand `RootDepth` (0.1–0.3 m) below the lowest ground on
their root flare (radius twice the trunk's), so no base floats on a slope;
the terrain keeps these heights (`SetTreeInstances` without snapping). Their
models continue below the pivot, the ground line, by about a metre (deep
roots): placeholders have longer trunks, Studio trees get their trunk base
stretched down on import, so the downhill side shows trunk and roots.

Small seed 1 at 0.5 m: forest cover 28 % for a 30 % target; about 4.7 k
pines, 1.1 k broadleaves, 0.1 k alders, 0.2 k snags, 8 k shrubs, 0.1 k logs,
0.7 k boulders, 0.3 k slabs, 4.5 k stones and 0.5 k mounds.

v2: a short self-thinning pass (Deussen 1998) over the same spatial hash.

### 10. Build

Jobs see only unmanaged data: settings and species are copied into structs
and `NativeArray`s before scheduling. After the jobs complete, the main
thread converts results into Unity's managed arrays:

- **Terrain**: one `Terrain`, origin `y = −seaDepth`, `size.y` = sea depth +
  highest peak, so sea level is world `y = 0`. Heights go in as a
  `float[y, x]` normalized to [0, 1] by `size.y`, in one `SetHeights` call
  (or `SetHeightsDelayLOD` plus `SyncHeightmap`). Four `TerrainLayer`s;
  alphamaps `float[y, x, layer]` summing to 1. One tile up to 4097².
- **Trees and shrubs**: a `TreePrototype` per species; `TreeInstance` with
  position normalized to [0, 1] in x and z, height from the terrain, yaw
  only. Collisions through the `TerrainCollider`'s tree colliders: a trunk
  capsule on the prefab.
- **Grass and reeds**: `DetailPrototype` with `usePrototypeMesh`,
  `useInstancing` and `renderMode = VertexLit`; coverage scatter mode, the
  detail map holding 0–255 coverage. No colliders.
- **Rocks and deadwood**: GameObjects under one parent (they need full
  rotation); box or capsule colliders; SRP Batcher-compatible materials.
  Measure draw calls.
- **Water**: a sea plane, river ribbons and lake meshes (lake cells at the
  lake level) with transparent flat-color URP materials.
- **Navigation**: free-fly camera and a capsule walker with
  `CharacterController` to check scale (45° slope limit, 0.3 m steps, as in
  the Studio engine guide).

Collider rules follow the Studio
[engine guide](../../assets_studio/docs/guides/engine.md): trunk capsules
for trees, none for grass.

## Unity architecture

```text
games/procedural/
  DESIGN.md  RESEARCH.md  README.md (M1)
  tools/island_proto.py          numeric prototype (this document)
  tools/core/                    dotnet build and NUnit run of the core
  artifacts/                     ignored: reports, maps, bake checkpoints
  unity/procedural/Assets/Procedural/
    Generation/   Procedural.Generation.asmdef  pure C#, no engine refs
    World/        Procedural.World.asmdef       Terrain, meshes, instances
    Editor/       Procedural.Editor.asmdef      menu, inspector, batch entry
    Tests/        Procedural.Tests.EditMode / .PlayMode
    Recipes/      IslandRecipe assets, one per level (committed)
    Species/      Species assets (committed)
    Placeholders/ primitive prefabs and flat materials (committed)
    Scenes/Island.unity                         only the generator object
    Generated/    ignored: the baked level (TerrainData, meshes, prefab)
```

- Core: plain C# 9 on .NET Standard 2.1 with no engine references, so the
  same files build in Unity and in `tools/core` (`dotnet test` runs the
  EditMode tests in about a second, without the Editor). It runs on a
  worker thread; Unity objects are made on the main thread afterwards.
  Burst comes in only where profiling shows a need; Small bakes take
  seconds without it.
- Data: `Grid` (float array, size, cell size), `IslandResult` with the
  heightmap, surface weights, discharge, lake ids, `Lake`s and
  `RiverSegment`s (polylines with surface, bed, width, depth, discharge).
- Stages: static methods over arrays. `IslandPipeline` runs them, times them
  and saves the outputs of stages 1–3 as checkpoints.
- Recipe: `IslandRecipe` (seed + the parameters of section
  [Recipe](#recipe)), from which per-stage settings are derived; presets
  Small and Medium.
- Entry points: menu `Procedural/Preview Island` and
  `Procedural/Bake Island`, the same buttons on an `IslandGenerator`
  component, and `Procedural.Editor.Batch.Bake(recipePath)` for
  `-executeMethod` when the Editor is closed.
- Long runs: a bake advances stage by stage without blocking the Editor
  (progress bar, cancel) and keeps `artifacts/bake/status.json` current:
  stage, progress, timings, errors. MCP starts it with `invoke_menu` and
  polls that file, as with `build run` and `build status`; a long
  synchronous `eval` would time out while the work goes on. Starting a bake
  while one runs is refused.
- Output: the baked level in `Generated/` (TerrainData, water meshes, a
  level prefab placed in `Scenes/Island.unity`), the report JSON in
  `artifacts/reports/`, debug maps in `artifacts/maps/` (hillshade,
  discharge, wetness, surface, plant density, lake ids).
- Git: commit code, recipes, species, placeholder prefabs and materials,
  the scene and `.meta` files. The baked level is ignored and rebuilt from
  its committed recipe after a fresh checkout, before building the player.
  If bakes grow too long for that, decide then where baked levels live.

Rough size: hydrology core ~250 lines, relief ~100, upsample and coast ~150,
lakes ~200, rivers ~450, wetness, moisture and surface ~200, vegetation
~400, builders ~450, Editor ~200, tests ~450; about 3k lines of C#.

## Replacing placeholders with Studio assets

Each `Species` references a prefab and declares its footprint radius, height
range, collider kind and whether it is a Terrain tree, a detail or a
GameObject. A Studio asset replaces a placeholder by swapping the prefab and
adjusting these numbers; placement does not change. Studio models arrive
when the user asks for them; the import rules are in the Studio engine guide
(FBX texture names, matte materials, welded decimation, size-based triangle
budgets).

## Validation

Numbers first. Visual checks only for what numbers cannot show. All checks
run on the final, built heightmap, not on intermediate routing surfaces.

### Invariants

EditMode tests on 64–128² grids with fixed seeds, plus hand-made cases:
broad flats, two lakes with equal spill heights, nested basins, a bowl that
merges two lakes, asymmetric confluences, a detail-noise dam across a
channel.

- Determinism: the same seed and settings give equal hashes of all maps
  and instances.
- Drainage: every land cell's receiver chain reaches the sea; no cycles;
  the sum of discharge at mouths equals total rain; the number of pits
  filled after carving is reported and bounded.
- Rivers, sampled along every polyline on the final heightmap: the water
  surface never rises downstream; the bed lies below the surface; mouths
  are at sea level; at confluences tributary and main surfaces are equal;
  the terrain beside the channel is above the surface (no spills).
- Lakes: level equals the spill level after carving; exactly one outlet.
- Plants: none below water or on slopes beyond the species limit; spacing
  holds for every pair; achieved density within the configured tolerance.

PlayMode tests: a capsule collides with rotated and scaled tree prototypes;
the walker cannot pass through trunks and does not fall through terrain.

### Metrics (report; warn outside the range)

| Metric | Target |
| --- | --- |
| Concavity θ per basin, outside lakes | 0.35–0.7 |
| Hack exponent along main stems of basins ≥ 5 % of land (median) | 0.5–0.65 |
| Highest Strahler order | ≥ 3 (Small), ≥ 4 (Medium) |
| Land slope p50 / p90 | 8–20° / ≤ 35° |
| Rock share of land | 3–20 % |
| Lowland share (slope < 5°, below 25 % of the peak) | 15–35 % |
| Coast sinuosity (length / equal-area circle) | ≥ 1.4 |
| Facets (within 1° of the critical slope), D8 anisotropy | low; warn above 5 % / 0.1 |
| Mean moisture by landform: water-adjacent (≤ 15 m) > valley floor > hillslope > ridge | ordered |
| TWI grid anisotropy | warn above 0.1 |
| Lakes | configured count ± 1, area share 0.5–3 % |
| Land fraction of the map | 0.35–0.6 |
| Forest cover of the land | recipe target ± 3 % (default 45 %) |
| Recipe targets | land fraction and lake count exact; forest cover ±3 %; river density ±10 % |

Across-mouth Hack is reported for information only: on a small radial
island most coastal basins span coast to divide, so their lengths vary little
and the exponent drops for geometric reasons.

`Passed` means the physical invariants; quality targets outside their range
are warnings. Timings per stage, including Terrain uploads and collider
creation, and peak memory go into the same report and are compared with the
presets' preview and bake budgets.
M1 benchmarks full-resolution hydrology and Terrain upload first, before
more stages are built on them.

### Visual checks

Top-down debug maps and a few fixed camera views per milestone, saved under
`artifacts/`. Ask a specific question (for example, "do rivers end in the
sea or in lakes; any river crossing a ridge?") through Codex vision, which
the user allowed for this project, or through the local VLM
(`.agents/media-analysis.md`). A description is an observation, not proof;
invariants and metrics decide.

## Milestones

| # | Content | Done when |
| --- | --- | --- |
| M0 ✓ | Packages, asmdefs, folders, empty EditMode test | Test runs through `unity test` or MCP; Editor stays up; `Library/Search` stays small |
| M1 ✓ | Recipe, shape, noise relief, hydrology core, lakes, rivers, Terrain, sea, river and lake meshes, fly camera, preview and asynchronous bake with status file and checkpoints, report, debug maps | Invariant tests pass; a bake started through MCP finishes and is polled to the end; Small preview ≤ 10 s; hydrology and Terrain upload timed |
| M2 ✓ | Stream-power relief with hillslopes (3a behind a setting), upsample with detail, beaches, floodplains, dammed lakes, curve-carved rivers; particle erosion tried | θ and Hack in range on 5 seeds; recipe targets met; Small preview ≤ 10 s. Done 2026-10-30 except main-stem Hack (0.35–0.41 on Small seeds 1–5, accepted with a warning) |
| M3 ✓ | Wetness (MFD), moisture, surface layers | Sand, rock and gravel where the rules say (checked from maps) |
| M4 ✓ | Vegetation with placeholders, deadwood, rocks, colliders, walker | Plant invariants and PlayMode collision tests pass; forest cover in range |
| M5 | Medium preset, profiling, resume after a failed or cancelled stage, tree distance and fog, first Studio asset swap when requested | Medium bake ≤ 10 min; a cancelled bake resumes; one species swapped without code changes. Done 2026-10-30 except the asset swap: Medium preview 20 s and bake 63 s in Unity; a bake cancelled at Lakes resumed from the Shape, Relief and Upsample checkpoints (47 s) |
| M6 (in progress) | Ground detail: 0.5 m heightmap for bakes; micro-relief by surface (outcrops, scree, hummocks); more surface layers from moisture and canopy (forest floor, mud, moss, scree, river gravel) with low-frequency variation and height-based blending; ground clutter species (ferns, flowers, litter, pebbles); textures and models from Studio when requested | Placeholder colours and meshes until Studio assets are requested |

## Risks

| Risk | Mitigation |
| --- | --- |
| Unity Search indexing fills the disk again | Index stays disabled; watch `df` after large imports |
| GPU driver faults in the player | Vulkan on native Wayland; `Player.log` records frame rate, memory and focus every 10 s; `-soak <s>` walks the island unattended |
| Relief is spiky or too uniform | Shape uplift (massifs, ridges), talus clamp, mild smoothing; read Tzathas 2024 for hillslope terms |
| D8 grid artifacts (straight 45° channels) | Bicubic upsample, smoothed river meshes, MFD wetness |
| Carving or detail noise breaks drainage | Hydrology again after carving; tests on the final heightmap |
| Rivers too many or too few | Threshold as a fraction of land area; Strahler-based filtering |
| Rivers narrower than heightmap cells look jagged | Minimum width of 2 cells; 1 m heightmap |
| Long bakes load the laptop | CPU only, capped worker count, checkpoints and cancel; benchmark at M1 |
| A parameter does not do what its name says | Each recipe target is checked in the report; parameters that cannot be met by construction are calibrated, not retried with new seeds |
| Pipeline package is experimental | CLI and batch-mode entry points do not depend on MCP |

## References

<!-- markdownlint-disable MD013 -->

- Fischer et al. 2022, [Procedural Generation of Landscapes with Water Bodies Using Artificial Drainage Basins](https://cgvr.cs.uni-bremen.de/papers/cgi22/CGI22.pdf).
- Barnes, Lehman, Mulla 2014, [Priority-Flood](https://arxiv.org/abs/1511.04463), Computers & Geosciences 62; [reference code](https://github.com/r-barnes/Barnes2013-Depressions).
- Braun & Willett 2013, an O(n) implicit solver for the stream power equation, Geomorphology 180–181; implemented in [Fastscapelib](https://fastscapelib.readthedocs.io/) and [Landlab](https://landlab.readthedocs.io/en/latest/generated/api/landlab.components.stream_power.fastscape_stream_power.html).
- Cordonnier et al. 2016, Large Scale Terrain Generation from Tectonic Uplift and Fluvial Erosion, Computer Graphics Forum 35(2).
- Cordonnier, Bovy, Braun 2019, [flow routing in topographies with depressions](https://esurf.copernicus.org/articles/7/549/2019/), Earth Surface Dynamics 7.
- Schott et al. 2023, [Large-scale Terrain Authoring through Interactive Erosion Simulation](https://aparis69.github.io/public_html/projects/schott2023_Uplift.html); [code, MIT](https://github.com/H-Schott/StreamPowerErosion).
- Tzathas et al. 2024, [Physically-based Analytical Erosion for fast Terrain Generation](https://diglib.eg.org/handle/10.1111/cgf15033), Computer Graphics Forum 43(2).
- Red Blob Games: [mapgen4](https://www.redblobgames.com/maps/mapgen4/), [river drainage basins](https://www.redblobgames.com/x/1723-procedural-river-growing/), [elevation from rivers](https://www.redblobgames.com/x/1724-elevation-from-rivers/).
- Génevaux et al. 2013, [Terrain Generation Using Procedural Models Based on Hydrology](https://dl.acm.org/doi/10.1145/2461912.2461996).
- Beyer 2015, implementation of a method for hydraulic erosion (TU Munich); [Lague's Unity version, MIT](https://github.com/SebLague/Hydraulic-Erosion).
- Mei, Decaudin, Hu 2007, Fast Hydraulic Erosion Simulation and Visualization on GPU.
- Leopold & Maddock 1953, The Hydraulic Geometry of Stream Channels, USGS Professional Paper 252.
- Beven & Kirkby 1979, topographic wetness index (TOPMODEL); [RichDEM notes on depression filling](https://richdem.readthedocs.io/en/latest/depression_filling.html).
- Hack 1957, stream length and basin area; Flint 1974, slope–area relation.
- Onrust et al. 2017, [Ecologically Sound Procedural Generation of Natural Environments](https://onlinelibrary.wiley.com/doi/10.1155/2017/7057141).
- Lane & Prusinkiewicz 2002, [Generating Spatial Distributions for Multilevel Models of Plant Communities](https://algorithmicbotany.org/papers/eco.gi2002.html).
- Deussen et al. 1998, Realistic Modeling and Rendering of Plant Ecosystems.
- van Muijden 2017, [GPU-Based Procedural Placement in Horizon Zero Dawn](https://www.guerrilla-games.com/read/gpu-based-procedural-placement-in-horizon-zero-dawn).
- Kapp et al. 2020, [Data-driven Authoring of Large-scale Ecosystems](https://perso.liris.cnrs.fr/apeytavi/website/publication/hal-03007259/).
- Peytavie et al. 2019, [Procedural Riverscapes](https://pubs.cs.uct.ac.za/id/eprint/1344/).
- Peytavie et al. 2024, [DeadWood](https://pubs.cs.uct.ac.za/id/eprint/1689/).
- Unity 6.6 scripting reference: [TerrainData.SetHeights](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/TerrainData.SetHeights.html), [SetAlphamaps](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/TerrainData.SetAlphamaps.html), [SetDetailLayer](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/TerrainData.SetDetailLayer.html), [DetailPrototype.useInstancing](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/DetailPrototype-useInstancing.html); [Burst type support](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/csharp-type-support.html); [Collections types](https://docs.unity3d.com/Packages/com.unity.collections@2.6/manual/collection-types.html).

<!-- markdownlint-enable MD013 -->
