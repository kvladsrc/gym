# HairDeck model server

The `image-to-3d` task takes a character image and makes its hair as hair
cards: flat, double-sided strips with UVs and colours along them, the way
games draw hair. [HairDeck](https://github.com/hyz317/HairDeck) works in
stages:

1. SAM 3 finds the person, the hair and the face, and BiRefNet-matting cuts
   the person out.
2. A 448 px crop of the head goes through DINOv3 ViT-L/16 into a set
   diffusion transformer (182 M), which samples up to 192 card tokens.
3. A card VAE decodes the tokens into strips, and a colour model (183 M)
   paints 16 colours along each one.

```sh
just setup                # source, Python 3.12 + PyTorch 2.8.0/CUDA 12.9 environment
just download             # weights (~7 GB); SAM 3 and DINOv3 need HF_TOKEN (below)
just download modelscope  # or: SAM 3 and DINOv3 from ModelScope's mirrors
just doctor
just serve                # http://127.0.0.1:9133
just test                 # no GPU, weights or source needed
```

- SAM 3 ([facebook/sam3](https://huggingface.co/facebook/sam3)) and DINOv3
  ([facebook/dinov3-vitl16-pretrain-lvd1689m](https://huggingface.co/facebook/dinov3-vitl16-pretrain-lvd1689m))
  are gated on Hugging Face. Accept both licences there, then set `HF_TOKEN`
  (`HUGGINGFACE_TOKEN` also works). `just download modelscope` takes the same
  files from ModelScope; upstream checks the same SHA-256 sums either way.
- `download` takes only the release files that inference reads, about 1.5 GB:
  the card DiT, the colour model, the card VAE and the token and head-frame
  statistics. Upstream's own downloader adds SAM 3, BiRefNet-matting and
  DINOv3 (about 5.5 GB, pinned and checked) and the pinned SAM 3 and DINOv3
  sources. All of it goes in `HAIRDECK_ROOT` (`~/.cache/assets-studio/hairdeck`).
- The result is in HairDeck's head frame: Y up, the face toward +Z, metres.
  The cards are one mesh, `hair_cards`, with per-card UVs and colours in
  `COLOR_0`. `head_proxy` adds the grey reference head as a separate node, so
  you can see where the hair sits. `colors` off gives one hair colour, taken
  from the image.
- Each output depends only on its seed: the server runs upstream's stages
  per seed (noise, sampling, card colours) instead of `Pipeline.run`, which
  takes consecutive seeds and writes files. An image without a visible head
  or hair is refused. If a seed gives no cards, the job fails and names the
  seed.
- With `HAIRDECK_OFFLOAD=auto` (the default), on a GPU under 12 GB the
  stages take turns on the GPU: SAM 3, then the matting, then generation.
  The rest waits in RAM. `on` forces this mode, `off` keeps everything on
  the GPU.
- Not included: upstream's spring-bone chains, a third DiT that it only draws
  as tubes, and its VRM export through Blender. A rigged hair mesh needs
  skinning and is not part of this task.

Licences: HairDeck code and weights are under Apache-2.0. SAM 3 is under
the SAM License, DINOv3 under the DINOv3 License, BiRefNet under MIT.
