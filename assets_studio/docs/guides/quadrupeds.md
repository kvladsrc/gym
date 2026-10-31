# Quadrupeds: a rigged creature animated from AI video (AnyMove)

<!-- markdownlint-disable MD013 -->

How a stylized wolf went from a text prompt to four game animations (gallop,
lunge, pounce, trot) on a laptop RTX 3070 (8 GB), October 2026. The motion
method is [AnyMove](https://github.com/Patch-gg/anymove) (Apache-2.0): a video
model animates the painted creature seen by two locked cameras, a joint reader
trained on that one creature finds every joint in both views, and a fit turns
the joints into bone rotations. Nothing in the result is keyframed by hand.

The tooling around AnyMove lives in `workspace/anymove_src/` (not in git):
`repo/` is the AnyMove clone with two local patches (below), `markup/` holds
the hand-markup pages, `single_view/` the Blender and Python helpers.

## Pipeline

1. Reference image (Qwen-Image) → Studio 3D chain → closed mesh.
2. Joints placed by hand on a markup page → AnyMove `rig.json` → `build_rig.py`.
3. Body and leg regions painted by hand → skin weights and leg paint.
4. Two-camera reference frame → MiniMax H3 clips on fal (paid).
5. 2,200 rendered poses → joint reader trained on the 8 GB card.
6. Each clip: read joints → fit (lighter smoothing) → `animation.glb`.

## 1. A rig-ready creature

The first wolf failed as an asset. Its reference looked at the camera, so the
mesh kept the head turned 76° to the side; the tail hung against the hind legs
and was already welded to them in the Hi3D mesh (548 edges after
retopology); and the automatic weights painted 57% of the body up to the spine.
What fixed it is the reference itself:

> Stylized 3D game-ready wolf character, a single adult wolf, complete full
> body, standing in a neutral square stance on level ground, three-quarter
> front-left view. Clean simplified shapes and a strong readable silhouette,
> like a modern stylized 3D game asset: smooth sculpted fur volumes with a few
> large tufts, no stray hairs, no long fluffy fur, matte hand-painted look with
> simple colour blocks: *(colours)*. Slightly stylized proportions: sturdy
> chunky legs, clear joints, compact body. Head and neck held straight forward
> in line with the spine, muzzle pointing straight ahead in the direction the
> body faces, looking forward and away from the viewer, head not turned toward
> the camera, mouth closed, ears upright. Tail extended straight out behind the
> body, nearly horizontal, clearly separated from the hind legs by a wide gap,
> not touching any leg. All four legs straight and vertical, clearly separated
> with visible gaps between them, paws flat on the ground. Soft even neutral
> lighting, no cast shadows, no ground plane, transparent background. Whole
> animal inside the frame from nose to tail tip. No other animals, no props,
> no text, no additional views.

- **Head in line with the spine.** “Looking forward” alone is not enough at a
  three-quarter view; “away from the viewer, not turned toward the camera” is.
- **Tail held out, clear of the legs.** A tail touching a leg is fused into one
  surface by the 3D models; no retopology separates it again.
- **Stylized, smooth fur.** The realistic wolf's fur gave a lumpy surface;
  the stylized one read cleanly and its reader trained to half the error.

Studio chain (each job takes the previous one through `input_jobs`):

| Step | Server, task | Parameters |
| --- | --- | --- |
| Reference | `qwen_image` text-to-image | 2432x1664, 40 steps, transparent; 2 variants, ~25 min |
| Cut-out | `birefnet` image-to-image | `hr`, `refine_foreground`, `largest_only` |
| Shape | `hunyuan3d21` image-to-3d | 60 steps, guidance 5, octree 512 |
| Refine | `hi3d` 3d-refine | resolution 1024, 25 steps, `keep_placement` (mesh from Hunyuan, image from the cut-out) |
| Retopology | `animation_tools` 3d-retopo | `quadriflow`, 80,000 faces |
| Texture | `trellis2_paint` 3d-paint | 2048, 20 steps, image from the cut-out |

Cut-out to painted mesh took 16 minutes. Use **QuadriFlow**, not AutoRemesher:
on the first wolf AutoRemesher grew 46 missing triangles of the Hi3D mesh into 7
holes (501, 250 and 101 boundary edges); QuadriFlow's voxel remesh closes them.

Checks before rigging, all numeric:

```sh
blender -b -P single_view/blender/heading.py -- repo/assets/<c>/model.glb   # holes, rotz_deg to face -Y
python single_view/fusion_check.py rig.json verts.npy faces.npy             # tail welded to a leg?
```

The stylized wolf: 0 boundary edges, rotz 88.5°, tail 168 mm from the nearest
leg, head within 2 cm of the midline.

## 2. Skeleton by hand

Copy `repo/assets/wolf/creature.json` (set `prep`: length 1.6 m, `rotz_deg`),
run `blender/prep_mesh.py`, then make the markup page:

```sh
python markup/transfer_joints.py repo/assets/wolf repo/assets/<c> markup/<c>/joints_auto.json  # first guess
blender -b -P repo/blender/ortho_views.py -- static.blend markup/<c>/views 700         # and xray/, left_view.py
python markup/markup_page.py markup/<c> markup/<c>/joints_auto.json "<label>"
```

The page shows the right, left, front and top views (with an x-ray toggle) and
the 36 joints of AnyMove's wolf skeleton; drag them to the pivots inside the
body, mirrored about the creature's own midline. Paste the JSON back, check it
with `single_view/blender/inside.py` (every joint inside the mesh), and write
`rig.json` with these joints and the bones and limits of `assets/wolf/rig.json`.
Then `blender/build_rig.py` builds and skins the armature.

## 3. Body and leg regions by hand

Blender's automatic weights give the upper arm and the thigh, which run inside
the body, most of the shoulder and flank: the body is dragged by the legs, and
the leg paint (computed from the weights) covers half the creature. Height and
distance cuts (“dip the wolf and look at the waterline”) gave straight borders
that never matched the armpits. What works is painting the regions:

```sh
blender -b -P single_view/blender/face_id_views.py -- rigged.blend markup/<c>/regions 600  # views + face index per pixel
blender -b -P single_view/blender/exr_ids.py -- markup/<c>/regions
blender -b -P single_view/blender/face_labels.py -- rigged.blend creature.json labels_start.npy
python markup/regions_page.py markup/<c>/regions creature.json labels_start.npy <c>_regions
```

The page shows six views (right, left, front, back, top, bottom); every pixel
knows its face, so a stroke in one view repaints those faces in all of them.
Brush classes: body and the four legs. Then:

```sh
blender -b -P single_view/blender/clean_regions.py -- rigged.blend <c>_regions.json clean.json 60   # islands < 60 faces
blender -b -P single_view/blender/apply_regions.py -- rigged_heat.blend creature.json clean.json visible_faces.npy rigged.blend 4
blender -b -P single_view/blender/poses_preview.py -- rigged.blend rig_info.json markup/<c>/poses
```

`apply_regions.py` keeps the automatic weights only on bones beside each
vertex, removes every leg bone from body vertices, keeps a leg's vertices on
that leg and its scapula or pelvis, and blends across the borders with 4
smoothing passes; the leg paint is the regions themselves. The hand-painted
regions had 130 brush specks (1–52 faces); `clean_regions.py` gives them to
the surrounding region. `poses_preview.py` renders stretch, tuck, sit and
splay poses at the joint limits: wrongly painted patches stretch visibly.
Run `single_view/blender/weight_report.py` to count leaks numerically.

## 4. Two-camera clips

```sh
blender -b -P repo/blender/render_ref.py -- rigged.blend runs/<c>/ref && python repo/tools/compose_ref.py runs/<c>/ref
python repo/tools/make_prompt.py assets/<c> "<one-sentence action>" prompts/<c>/<move>.txt
```

MiniMax H3 on fal, image-to-video: 5 s, `768P`, prompt expansion off, about
$0.26 a clip. Big moves read well; write the action so it starts and ends in
the standing stance and stays on the spot. The four that worked:

- *gallop*: “gallops on the spot like a fast video-game sprint loop: powerful
  bounding strides, the front legs reaching far forward together and the hind
  legs driving far back, …, but it does not travel forward”
- *pounce*: “crouches low on all four legs, then springs up into a powerful
  pounce, … lands back on the same spot front legs first, then settles back”
- *lunge*: “drops its head and shoulders into an aggressive crouch, lunges
  forward with a fast snapping bite, …, then jumps back to its starting spot”
- *trot*: AnyMove's wolf trot prompt.

There is no jaw bone, so a bite reads only through the head and neck.
`single_view/clip_check.py` checks a clip before capture: alignment to the
reference frame, the divider staying put, each leg colour present per view.

## 5. The joint reader on 8 GB

Render 2,200 random poses inside the joint limits (two Blender processes),
then train:

```sh
ANYMOVE_WORKERS=8 python -m mocap.train runs/<c>/dataset assets/<c>/rigged/rig_info.json runs/<c>/reader.pt 20 10 2
```

- **Rendering runs on the CPU:** the Nix Blender has no CUDA kernels. 31–35
  poses a minute, about 65 minutes; the GPU is free meanwhile.
- **Training fits in 6.5 GB** with batch 10 and gradient accumulation 2 (local
  patch to `mocap/train.py`; AnyMove's default batch 20 needs about 12 GB).
  20 epochs took 62 minutes at 100% GPU, 67 °C, about 75 W.
- Held-out error: **6.75 px front, 3.88 px side** (layout pixels, 2.6 mm each)
  for the stylized wolf; the realistic wolf with its turned head reached only
  14.3 and 6.8.
- Train after the dataset is complete. Training alongside the rendering works
  (each epoch re-reads the samples) but its last epochs then see a part of the
  set.

## 6. Capture and the smoothing of fast moves

```sh
python -m mocap.detect video.mp4 runs/<c>/reader.pt runs/<c>/ref/ref.png det.npz overlay.mp4
ANYMOVE_ACC=0.001 ANYMOVE_VEL=0.5 python -m mocap.solve det.npz rig_info.json motion.json
blender -b -P repo/blender/apply_motion.py -- rigged.blend rig_info.json motion.json render --views persp,side --glb
python -m mocap.showcase video.mp4 det.npz render showcase.mp4 "<title>"
```

The joints read correctly (overlay), but with AnyMove's smoothing the gallop's
legs barely bent: the fit's acceleration penalty (0.02) outweighs the joint
error for paws that cross tens of centimetres in a few frames at 24 fps, so
the rhythm goes into the body. Lower weights (local patch: `ANYMOVE_ACC`,
`ANYMOVE_VEL`; defaults unchanged) fix it, at the cost of somewhat more paw
jitter (acceleration sign changes 16% → 31% of frames on the gallop):

| Clip | Paw agreement between views | Fit error front / side, default smoothing | Lighter smoothing |
| --- | --- | --- | --- |
| gallop | 0.94 | 15.0 / 29.3 px | 12.3 / 5.7 px |
| lunge | 0.93 | 11.1 / 5.1 px | 10.9 / 2.7 px |
| pounce | 0.96 | 7.9 / 3.8 px | 8.0 / 2.9 px |
| trot | 0.94 | 6.6 / 3.8 px | 6.5 / 2.9 px |

All four were accepted with the lighter smoothing. The front error is higher
by design: the front view supplies left–right position (its x-only error is
1.4–2.6 px) and heights come from the side view. Rendering a clip at half
resolution takes about 13 minutes on the CPU; `animation.glb` does not depend
on the render size. `single_view/detect_cpu.py` reads a clip on the CPU (3.5
minutes) when the GPU is busy training.

## Real footage with one camera

For filmed animals, AnyMove's own reader does not apply (it knows one painted
model in a grey studio). A tested alternative: DeepLabCut SuperAnimal-Quadruped
finds 39 keypoints on real video, and `single_view/solve_single.py` fits
AnyMove's wolf skeleton under a weak-perspective camera with per-point offsets,
joint limits and a ground-contact prior; 202 frames of a Nat Geo wolf walk fit
to about 2 cm. The single view cannot recover travel or the far legs' sideways
position well, and SuperAnimal's licence for products is unclear.

## Studio gaps found

- AutoRemesher returns meshes with holes and the asset does not say so; the
  retopology metadata should report boundary edges.
- A running job cannot be cancelled (`cancel_job` only works before it is sent
  to the server), so a 2-variant 25-minute job runs to the end.
- `wait_for_job` through MCP is cut after 300 s of silence; poll `get_job`
  instead for long jobs.

## Licences

MiniMax H3 open weights carry a community licence and fal's terms apply to
hosted use; Qwen-Image is non-commercial; Hunyuan3D 2.1 excludes the EU, UK
and South Korea. Check them before shipping.
