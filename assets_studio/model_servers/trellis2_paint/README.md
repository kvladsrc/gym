# TRELLIS.2 Paint

`3d-paint`: a finished game mesh and its transparent reference image → GLB
with a PBR material. The shape generator is not loaded. The mesh is not
simplified; existing UVs are preserved, or CuMesh unwraps it if UVs are absent.
After painting, `animation_tools` can bake normals from the original Hi3D mesh.

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

Source, weights and the isolated environment live under `~/.cache/assets-studio/`:
`trellis2-source`, `trellis2-paint`, `venvs/trellis2_paint`.
The first setup builds FlexGEMM, CuMesh, o-voxel and nvdiffrast; built wheels
are cached in `trellis2-paint-native/wheels`. Installing o-voxel does not fetch
its Git dependencies again. The build targets sm_86, PyTorch 2.8/CUDA 12.9/
Python 3.11.

The adapter preserves input scale, position, UVs and normals. It checks that
unwrapping preserves the surface. The only upstream patch copies read-only
`vertex_normals` before axis conversion for compatibility with modern trimesh.

Sources: [TRELLIS.2](https://github.com/microsoft/TRELLIS.2),
[weights](https://huggingface.co/microsoft/TRELLIS.2-4B).
