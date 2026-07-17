# Character sprite pipeline

This prototype renders a rigged 3D character into deterministic 128×128 PNG
frames for a 2D-in-3D `Sprite3D` character. Blender supplies pose, silhouette,
camera, and lighting consistency. A separate image-generation pass can later
stylize the rendered guides from an approved character concept.

The default source is the existing rigged `Ranger.glb`. It has a skeleton but
no embedded animation clips, so `render.py` applies a small procedural movement
cycle. The game reuses that cycle for walking and sprinting, changing only its
playback speed. This source is only a production-pipeline prototype; it does
not constrain the final character design.

`concepts/knight.png` is the approved visual anchor for the replacement
character. It defines identity, proportions, outfit, materials, and palette;
Blender renders define only camera direction and animation pose.

`concepts/walk_pose_reference.jpeg` is the approved animation map: eight
directions with eight coherent walking frames for each direction.

From the repository root:

```sh
nix develop .#cothic-assets
just cothic sprites-proof
```

Individual stages are also available:

```sh
just cothic sprites-render walk
just cothic sprites-pack walk
just cothic sprites-preview walk 00_south
```

Generated frames, atlases, and previews are written under
`cothic/tmp/sprites/`, which is intentionally ignored by Git. Direction rows
and animation frame counts are declared in `character.json`; the atlas uses
animation frames as columns and directions as rows.

The image-generation stage is deliberately not part of the Nix shell or the
game runtime. Approved final PNG assets will be versioned, while local model
weights, CUDA runtimes, and external API access remain outside Cothic.
