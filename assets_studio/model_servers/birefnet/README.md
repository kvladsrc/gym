# BiRefNet model server

The prompt-free `image-to-image` task returns the same image with a transparent
background (PNG). [BiRefNet](https://github.com/ZhengPeng7/BiRefNet) (CAAI AIR
2024) separates the subject from the background using a Swin-L encoder and a
decoder that repeatedly checks the full-resolution image, preserving fine
features such as manes, legs and branches. In the UI, use “Remove background”
on an image.

```sh
just setup     # Python 3.11 + PyTorch 2.8/CUDA 12.9 environment
just download  # weights for all variants (~2.7 GB, network only)
just doctor
just serve     # http://127.0.0.1:9121
just test      # no GPU or weights needed
```

- Image-to-3D and painting servers use cut-out images as supplied: their own
  rembg (IS-Net) does not run if transparency is already present.
- `variant` selects weights; one variant stays on GPU (~0.9 GB fp16), and
  switching loads another in a couple of seconds:
  - `dynamic` (default): trained on sizes 256…2304; preserves image aspect
    ratio and resolution (up to 2304 on the long side).
  - `general`: 1024 square; `hr`: 2048 square.
  - `matting`, `dynamic-matting`, `hr-matting`: soft alpha for hair and fur;
    the edge is translucent and slightly blurred.
- `refine_foreground` recalculates edge colors without the background tint
  (blur fusion, matching the author's `refine_foreground`); otherwise a mane
  cut out of grass retains a green fringe.
- Results are seed-independent. Contract check:
  `just check-server http://127.0.0.1:9121 --allow-seed-independent`.
- Model code (`birefnet.py`) arrives with the weights (transformers remote
  code), pinned to the same revision as the weights.

Measurement (RTX 3070 Laptop, 1920×1080 image of a horse in a paddock, fence
post between its legs): all six variants removed the post and retained the
mane and ears; 1.3–2.8 seconds per image including variant switching, about
1 GB VRAM.

License: MIT (code and weights).

For video, `video-to-video` takes an MP4 up to 60 seconds and returns an MP4
with the subject on white, retaining dimensions and frame rate. Processing
is frame by frame without temporal mask smoothing. Pass the output to
MoCapAnything or GVHMR's `video` input using Studio/Flow dependencies. For
MoCapAnything, set `background_removed=true` to avoid repeated segmentation.
