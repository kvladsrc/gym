# Assets in the engine

<!-- markdownlint-disable MD013 -->

Examples use Unity 6 (URP), but the underlying issues apply to any engine.

## Import

- **FBX textures.** Embedded textures need filenames: packed images without
  paths export under the media folder name (`rigged.fbm`), which the engine
  does not recognize, leaving models white. Assign unique image names
  (`<model>.png`, `texture-<hash>.png`) and extract them beside the model on import.
- **Matte materials.** Paint bakes light and shadow into the texture; default
  imported gloss looks plastic (smoothness ~0.1, metallic 0).
- Enable **GPU instancing** on level materials: hundreds of grass/tree copies
  become one draw call per mesh.
- **Floating characters.** The character controller holds its capsule above
  ground (skin width ~8 cm); lower the model by that amount.

## Collisions

- Use `MeshCollider` from the model's own mesh, without hand-made invisible
  bodies. Hunyuan3D meshes are watertight: characters do not fall through,
  and the controller climbs uneven terrain (up to 45°, 0.3 m steps).
- Use simplified colliders only for moving objects (convex), trees (trunk
  capsule; branches should not support the player), grass (none) and very
  large terrain.
- Placement “ground” must include terrain surfaces only, not everything;
  otherwise the boss ends up on the chapel roof.

## Levels assembled in Blender

A convenient workflow is to assemble Studio GLBs manually and export by script:

1. **Originals, not reduced copies.** Paint meshes are split at UV seams.
   Decimating without welding tears every seam: a tree drops from 40,000 to
   4,200 triangles with about 5,000 open edges. Do not reduce models in Blender
   without Merge by Distance first. Export can restore originals automatically:
   identify Studio models by texture bytes, then fit the original to the copy's
   bounding box (copies are usually recentered and sometimes scaled).
2. **Weld → reduce to a size-based budget** (grass ~3K, props 6–10K, trees ~12K,
   buildings ~30K triangles), once per shared mesh, keeping seams intact.
3. **Procedural Blender materials** (noise, color ramps, image tinting through
   Mix) do not transfer to FBX. Bake base color to a texture (once per shared
   mesh). An 8192² bake holds gigabytes of float buffers; 4096 is enough
   (300 m → ~7 cm per pixel).
4. **Inverted surfaces.** Blender shows both sides; games render front faces
   only. Upside-down terrain becomes invisible and impassable. Flip surfaces
   facing mostly downward on export.
5. **Collections → parents**, lights → sidecar JSON (type, color, power): the
   engine can distinguish terrain, undergrowth and forest.
6. **Game code should know names, not coordinates** (bonfires, waypoints,
   arena steps). Re-export can then rebuild everything automatically.

## Performance

- Dual-GPU laptops (Optimus): without PRIME offload, games use the integrated
  GPU (`__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia` for
  OpenGL; Vulkan selects the discrete GPU itself). Check the game's `Renderer` log.
- A 3,500-model level rendered 7M triangles and 5,000 draw calls. Improvements:
  camera distance matched to fog (160 m), layer draw distances (undergrowth
  45 m, trees 140 m), no undergrowth shadows, automatic mesh LODs, and 2 shadow
  cascades instead of 4.
- Editor statistics overcount (editor scene, shadows, depth prepass); count
  triangles in the camera frame.

## Lighting and atmosphere

- Post-processing helps hide generation flaws: SSAO grounds models, light
  grain masks mottled textures, soft distance blur, warm light/cool shadows.
- Match fog to the sky horizon color; add low particle fog around the player
  (attach it to the player on large levels, otherwise it is too sparse).
