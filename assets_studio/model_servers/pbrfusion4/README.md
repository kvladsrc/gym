# PBRFusion 4 model server

The prompt-free `image-to-image` task turns a flat material texture (ground,
stone, bark, cloth seen straight on) into its **normal map** or **height map**
(PNG, the image's own size).
[PBRFusion 4](https://huggingface.co/NightRaven109/PBRFusion4) is Lotus-D: the
UNet of Stable Diffusion 2 trained to give the height of a texture in one step,
without noise or sampling. It reads form where a "brightness is height" filter
reads paint: a dark stone among light ones is still a stone, not a pit.

```sh
just setup     # Python 3.11 + PyTorch 2.8/CUDA 12.9 environment
just download  # the weights (~4.3 GB, network only)
just doctor
just serve     # http://127.0.0.1:9145
just test      # no GPU or weights needed
```

- `output`: `normal` (default) or `height` (grey, white is high). The model
  predicts the height; the normals are computed from it, as in the authors'
  ComfyUI node: a bilateral filter, a share of the image's brightness, Sobel
  slopes.
- `tileable`: for a texture that repeats without seams. The model sees it with
  an eighth of each side wrapped around, the two predictions of every border
  strip are cross-faded and the filters wrap, so the map tiles as well. The
  authors' node has no such mode.
- `strength`: steepness of the normals. 1 equals the authors' setting for a
  1024 px texture; unlike theirs it does not change with the image's size.
- `brightness_mix` (default 0.3, the node's): how much of the image's own
  brightness joins the height before the normals. It adds the finest detail
  and turns painted light and shadow into relief; 0 leaves the model's height
  alone.
- `convention`: `opengl` (green up: Unity, Blender, glTF) or `directx` (green
  down: Unreal).
- `size`: the longest side the model sees (default 1024, the authors' limit);
  the map is returned at the image's size.
- It is not for pictures of objects or scenes, and it gives no roughness or
  metalness.
- Results are seed-independent: the VAE's latent is its mean, not a sample as
  in the reference. Contract check:
  `just check-server http://127.0.0.1:9145 --allow-seed-independent`.
- After the model everything is computed on floating-point heights; the
  reference rounds the height to 8 bits first, which leaves steps on gentle
  slopes.

The weights are one file holding the UNet (fp32), the VAE and the text encoder
(fp16) with their configurations; the server loads them as fp16, tensor by
tensor, and keeps only the empty prompt's embedding of the text encoder.

License: Apache-2.0 (weights and the reference code,
[COMFYUI-PBRFusion4](https://github.com/Night1099/COMFYUI-PBRFusion4)).
`PBRFusion4_Albedo.safetensors` in the same repository is a separate LoRA for
FLUX.2 klein 4B (removing baked light from a texture) and is not used here.
