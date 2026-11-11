# Animation tools

Deterministic rig and animation processing through Blender, exposed as an
ordinary Studio server. No neural networks, weights or CUDA required. This
server is not used for preview rendering.

```sh
just setup
just doctor
just serve                  # 127.0.0.1:9124
just test                   # contract and error handling
just integration            # real FBX and Blender, no GPU models
```

Studio and the launcher know the server as `animation_tools`. `BLENDER` selects
the executable; otherwise it is found in PATH and the standard Nix profile.
The Python environment is in `~/.cache/assets-studio/venvs/animation_tools`.

## Tasks

`3d-retopo` reduces a detailed GLB mesh, such as Hi3D's million triangles, and
drops loose specks. `method` offers `decimate` (target in triangles), which
keeps a painted mesh's UVs and material, so it can follow painting;
`quadriflow` and `autoremesher` (target in quads; actual counts may differ)
rebuild the surface and drop the paint (`material_dropped` in the job
metadata). AutoRemesher works directly on the surface,
without an initial voxel remesh. GLB stores triangles; the original quad count
is retained in job metadata. Decimation cannot collapse non-manifold edges
(Hi3D's thin branches have thousands); when it stops more than 10 % above
the target the job fails instead of returning a much heavier mesh.

AutoRemesher's own settings are parameters too (`edge_scaling`,
`sharp_edge`, `smooth_normal`, `adaptivity`, `anisotropy`; the CLI's
defaults), ignored by the other methods. Every method reports `deviation`:
distances between the detailed surface and the result as shares of the
bounding box diagonal (`lost`: source points far from the result, such as a
missing finger; `added`: the other way), the share of source points further
than 0.5 % and the box around them.

AutoRemesher requires an installed CLI. The default is
`~/.cache/assets-studio/autoremesher/squashfs-root/AppRun`; use `AUTOREMESHER`
for another path. AppImage 1.2.0 requires an X display even in CLI mode.
The standard local Studio inherits it from the desktop.

`3d-bake-normals` bakes details from the source high-resolution mesh into a
normal map for an already painted low-resolution mesh with UVs. To compare
painters, use the same high-resolution source and bake settings.

`3d-bake-colour` moves a painted mesh's base colour onto another mesh of the
same shape, such as a coarser LOD: Hi3D at a lower `max_faces` gives the
shape, the painted LOD0 its colours. Separate paints per LOD differ, and the
painter left a 1,800-triangle tree almost one colour. A mesh without UVs is
unwrapped with Smart UV Project. Only the base colour carries over; the result
is matte and non-metal. The colour is baked as emission: Cycles' diffuse
colour is the base colour times (1 - metallic), and TRELLIS.2 marks whole tree
crowns as metal, which baked black. A 1,800-triangle tree bakes in about 5 s
on the CPU.

Three animation tasks cover rig preparation, motion transfer using Mixamo
bone names (Kimodo-SOMA, Mixamo), and native SMPL-X motion transfer. Trimming,
window stitching and weapon attachment were removed on 2026-10-22: the stance
procedure trims segments before capture, Kimodo generates transitions, and
pose fitting places the sword.

- **prepare-rig**: character FBX/GLB with a skeleton → FBX without old animation,
  in its own rest pose. Checks bones, a single root, mesh binding and vertices
  without weights; normalizes weights. Preserves topology, proportions, rest
  pose, scale and coordinates. It does not use a neural network to fix poor
  joint placement or convert arbitrary animals to Mixamo skeletons.
- **retarget-motion**: rig FBX/GLB + animation FBX/GLB → animated character FBX.
  Rig preparation happens automatically; a separate `prepare-rig` is optional.
  Supports humanoids with Mixamo bone names, including MIA, Kimodo and Studio
  mocap. Prefixes such as `mixamorig:` do not interfere with matching.

`root_motion=in-place` retains vertical movement, roll and pitch, removing
horizontal travel and heading changes. `stationary` removes only horizontal
travel, keeping turns and attack rotation. `world` keeps travel and heading.

`method=auto` transfers full rotations with corrections for different rest-pose
bone directions. It preserves end-bone rotations, including wrists, and limb
twist. Translation is scaled using rest-pose leg lengths, not first-frame
crouching. `positions` is a fallback: bone directions come from joints, without
twist transfer. It cannot recover end-joint rotations from a representation
that lacks them.

For general animal skeletons, MoCapAnything transfers rotations itself within
`video-to-motion`; retargeting to an arbitrary different animal is not
implemented here.

## Usage

- MIA → GVHMR or MoCapAnything: pass the rigging result as `rig` and the video
  as `video`. Their preparation operations are already built into the servers.
- MIA + Kimodo: pass the MIA rig and Kimodo skeletal animation to
  `retarget-motion`. Previously this required running `retarget.py` manually.
- Old animated rigs are also accepted: animation and pose are reset before
  transfer. The chosen character's mesh and weights are retained.
- A Mixamo clip can replace Kimodo. Matching finger bones transfer for
  characters with fingers; missing ones retain their rest pose.

Both operations are available in Studio UI and [Studio Flow](../../flow/README.md).
Results and provenance are stored in the shared library.

The Blender regression creates a T-pose target with old animation, an A-pose
source and wrist-twisting motion. It checks weight normalization, old-clip
removal, ten frames without loss, wrist rotation transfer and translation for
`world`, `stationary` and `in-place`, plus vertical movement and turn preservation
for MoCapAnything's general skeleton.

Also tested on actual MIA humanoids and SkinTokens/UniRig quadrupeds: preparation
needs no per-model Blender script. A complete saved MIA + Kimodo + guard-transfer
graph returned a 120-frame animated FBX; every pose was finite and the weight
sum error was <1e-6.

## Native SMPL-X motion

`retarget-native-motion`: FBX/GLB rig + `studio.smplx-motion.v1` (`text/plain`)
→ FBX. Motion parameters and BVH are retained separately before transfer.
Defaults: `root_motion=stationary`, `rest_alignment=bone-directions`. Transfers
22 body joints; fingers and face are absent from this profile.

The SMPL profile preserves neutral orientations of the pelvis, spine, neck,
head and **clavicles**, while still transferring their actual rotations.
Aligning neutral SMPL and Mixamo clavicle directions previously raised the
shoulders unnecessarily even at zero pose; this is fixed. Mixamo/Kimodo
transfer is unchanged.

`rest_alignment=matched-neutral` also preserves neutral limb directions. Use
it only when the target's original surface is already in the same neutral
pose as SMPL. It is not universal for arbitrary A-poses.

For meshes, bones, weights and transfer, see the
[animation pipeline](../../docs/animation-pipeline.md).

The SMPL profile also preserves neutral foot and toe orientations. The angle
from ankle to toe reflects anatomy and footwear; aligning it previously added
about 23° to the guard even at zero SMPL pose. Actual foot/toe rotations are
retained, covered by Blender regressions.
