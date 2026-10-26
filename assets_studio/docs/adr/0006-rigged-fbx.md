# ADR-006. Character skeletons: the `3d-to-rig` task and FBX format

Status: accepted.

## Context

Image-generated models (TripoSR, SF3D, Hunyuan3D) are static meshes. Mixamo
animation requires a Mixamo skeleton and skin weights. Make-It-Animatable can
produce these, but outputs FBX: Unity builds a Humanoid avatar from FBX that
accepts Mixamo animations. Unity cannot open GLB without glTFast, and glTFast
does not build Humanoid avatars. Studio previously stored only GLB for 3D.

## Decision

- Add a 3D file type: `model/x-fbx` (`.fbx`), binary FBX only (signature
  `Kaydara FBX Binary`), as written by Blender, Mixamo and Unity. GLB remains
  the canonical 3D format. Studio does not convert FBX to GLB; GLB-only
  servers cannot receive FBX, and such jobs are rejected with a clear error.
- Add known task `3d-to-rig`: input `mesh` (GLB), output FBX with skeleton and
  weights. Section: 3D (by result kind, ADR-004).
- The UI does not preview FBX (model-viewer supports only glTF); it displays
  an explanation instead, with a “Download” button.
- The contract number stays unchanged: the new file type and task are
  extensions that old servers and clients do not encounter.

## Consequences

- Browser skeleton preview remains open: a companion skinned GLB (FBX2glTF)
  could be provided when needed.
- ASCII FBX is not accepted: library import identifies it as text.
