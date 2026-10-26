# 3D → rig

<!-- markdownlint-disable MD013 -->

Three `3d-to-rig` servers, all returning FBX (ADR-006):

| Server | Skeleton | Use |
| --- | --- | --- |
| `mia` (Make-It-Animatable v2) | Mixamo, 52 bones | Humanoids for Mixamo clips ([characters](../guides/characters.md)) |
| `skintokens` (SkinTokens, 2026) | Shape-specific: quadrupeds, birds, objects | Anything outside Mixamo humanoids |
| `unirig` (UniRig, SIGGRAPH 2025) | Shape-specific | Same purpose; SkinTokens predecessor |

## SkinTokens versus UniRig (October 2026)

Tested October 16, 2026 on an RTX 3070 Laptop (8 GB), using painted Hunyuan3D
models (60K triangles) from `sweep:triposg1`: horse, chicken, wolf, goblin.
Weights were checked with a stress pose: every bone randomly rotated up to
25° on each axis.

| | SkinTokens | UniRig |
| --- | --- | --- |
| Time | 29–40 s | 70–77 s (skeleton ~23 s, skin ~29 s, transfer ~11 s) |
| VRAM | ~2 GB (10 beams) | Up to ~7 GB (`nvidia-smi`, skin stage) |
| Bones: horse / chicken / wolf / goblin | 33 / 20 / 29 / 52 | 30 / 15 / 31 / 49 |
| Stress pose | Clean bending without tears | Tears/stretches chest and neck (horse, wolf), crushes chicken body |

UniRig skeletons look neater (spine along the body axis, centered legs), but
weights are noticeably worse, matching the SkinTokens paper. Decision:
`skintokens` by default for non-humanoids; keep `unirig` as a fallback.

SkinTokens README requests ≥14 GB VRAM; ten beams actually used about 2 GB.

## Workarounds

- **bpy crashes on exit.** bpy modules 4.2/4.3 exit with SIGSEGV after completing
  work. Child processes use `os._exit` (`asset_processing.blender.exit_now`).
- **SkinTokens demo bpy server** listens on all interfaces and unpickles
  request data. It is not used; bpy runs in a child process without networking.
- **UniRig checkpoints** fail with `torch.load(weights_only=True)`, default
  since PyTorch 2.6. Steps use `TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD=1`, only for
  pinned VAST checkpoints.
- **UniRig skin stage** renders depth through pyrender; headless use needs
  EGL (`PYOPENGL_PLATFORM=egl`).
- **Upstream FBX lacks textures:** both servers assemble GLB and convert it
  themselves (`asset_processing.blender`).
- **SkinTokens bones all faced one direction.** The model predicts joints,
  not bone directions. Upstream derived tails from identity rotations and
  lengths, so bones pointed outward along one world axis. Without lengths,
  upstream points bones toward their sole child, extending parents for end
  bones. glTF does not store tails; Blender guesses them. `FORTUNE` aligns
  bones with limbs (71% of SkinTokens tails inside the body, versus 51% with
  default `BLENDER`). Joints/weights remain unchanged; bone axes change,
  affecting Blender display and animation rotation.

Post-fix checks (horse / chicken / wolf / goblin): SkinTokens joints inside
body 30/33, 35/35, 29/29, 51/52; limb-aligned bones 24/33, 23/35, 24/29, 38/52
(UniRig 23/30, 12/15, 22/31, 27/49). Validation-animation page:
`~/.local/share/assets-studio/sweeps/rigs1/`.

Full compatibility and automatic preparation map:
[rigging and motion](rigging-and-motion.md). Save chains in
[Studio Flow](../../flow/README.md).
