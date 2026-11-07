# TRELLIS.2 Paint

`3d-paint`: a finished mesh, such as Hi3D's result simplified to the game
budget (its `max_faces`), and its transparent reference image → GLB with a
PBR material. The shape generator is not loaded. The mesh is not simplified;
existing UVs are preserved, or CuMesh unwraps it if UVs are absent. To go
lower afterwards, `animation_tools` reduces the painted mesh (`3d-retopo` with
`decimate` keeps the paint) and can bake normals from a denser Hi3D mesh. The
chain's settings: [3D guide](../../docs/guides/3d.md).

```sh
just setup
just download
just test
just serve                  # 127.0.0.1:9131
```

Tested on an RTX 3070 Laptop 8 GB with a 512 internal material grid, 12 steps
and a 2048 texture: about 3 GiB VRAM for an 80K mesh. Components move between
CPU and GPU by phase; published fp16/bf16 weights are used without quantization.
For an opaque reference, remove the background with BiRefNet first.

The paint is darker than its reference: on 13 props and trees the mean
brightness fell by a third to a half, and the hue drifted toward olive. From
a flat, unlit reference a sand-coloured boulder came out rust-red with three
times the saturation. Classifier-free guidance 3 and 6 did not change the
colours. `match_colours` moves the base colour's Oklab mean and spread, measured
over the surface by area, to those of the reference foreground. The spread
grows at most threefold: on an 1800-triangle tree the model painted almost a
single colour, and stretching that only amplified noise. The output metadata
records both means and the scale.

Source, weights and the isolated environment live under `~/.cache/assets-studio/`:
`trellis2-source`, `trellis2-paint`, `venvs/trellis2_paint`.
The first setup builds FlexGEMM, CuMesh, o-voxel and nvdiffrast; built wheels
are cached in `trellis2-paint-native/wheels`. Installing o-voxel does not fetch
its Git dependencies again. The build targets PyTorch 2.8/CUDA 12.9/Python 3.11
and the GPU architecture in `TORCH_CUDA_ARCH_LIST` (8.6 unless set, see
`../common.just`); after changing it, delete the cached wheels.

The adapter preserves input scale, position, UVs and normals. It checks that
unwrapping preserves the surface. The only upstream patch copies read-only
`vertex_normals` before axis conversion for compatibility with modern trimesh.

Sources: [TRELLIS.2](https://github.com/microsoft/TRELLIS.2),
[weights](https://huggingface.co/microsoft/TRELLIS.2-4B).
