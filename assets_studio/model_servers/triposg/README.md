# TripoSG model server

> Retired from the active Studio catalog; weights and local environment removed.

The `image-to-3d` task turns an image into a shape (GLB without color). Based on
[TripoSG](https://github.com/VAST-AI-Research/TripoSG) from VAST (Tripo's authors):
a 1.5B rectified-flow transformer samples a shape latent from the image, and a
VAE reconstructs the surface on a 512³ grid. Like Hunyuan3D, it is a diffusion
model: different seeds give different variants. Add color with `3d-paint`
(Hunyuan3D-Paint).

```sh
just setup     # source, Python 3.11 + PyTorch 2.8/CUDA 12.9 environment;
               # build diso in nix develop .#assets-studio-cuda
just download  # weights (~8 GB fp32, network only)
just doctor
just serve     # http://127.0.0.1:9115
just test      # no GPU or weights needed
```

- Commercial Tripo 2.x/3.x models are closed (API only). TripoSG is Tripo's
  open model; its README requires 8 GB VRAM.
- Published weights are fp32; the server keeps them entirely on GPU in fp16
  (~4 GB).
- Images are prepared as in training: background removed, object cropped and
  centered in a white square with 10% padding. Background removal uses rembg
  (IS-Net, as in Hunyuan3D), rather than TripoSG scripts' BRIA RMBG-1.4, whose
  license is non-commercial.
- diso (CUDA marching cubes) builds with nixpkgs' nvcc (12.9), hence PyTorch
  for CUDA 12.9, as with Hunyuan3D-Paint.

License: MIT (code and weights).
