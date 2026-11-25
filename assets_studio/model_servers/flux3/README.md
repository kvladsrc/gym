# FLUX 3 Image Klein: images and editing in 16 steps

`text-to-image` and `image-to-image`, port 9149, with Black Forest Labs'
[FLUX 3 Image Klein](https://huggingface.co/black-forest-labs/flux-3-image-klein):
a 5.6B image transformer distilled to 16 steps without guidance, a Qwen3-VL 4B
text encoder, and the model's own prompt upsampler, which expands a short
request in any language into the full English scene caption the model is
trained on.

Dropped on November 24, 2026 after review: almost every image had an obvious
error ([comparison](../../docs/models/text-to-image.md)). The code stays, so
the server can be set up again.

```sh
just setup     # the environment (Python 3.11) and the pinned llama.cpp build
just download  # the weights, ~26 GB; needs HF_TOKEN
just doctor
just serve     # http://127.0.0.1:9149
just test      # no GPU or weights needed
```

The base repository is gated: accept the licence on its Hugging Face page and
set `HF_TOKEN` (or `HUGGINGFACE_TOKEN`).

Licences: the image model is under the FLUX Kommunity License v1.0
(non-commercial use, plus commercial use of outputs by Qualifying Users with
less than US$5M annual revenue; read its terms). The prompt upsampler is
Apache-2.0.

## Tasks

- `text-to-image`: about 1 or 4 MP in seven aspect ratios (`resolution`,
  default 1024×1024). For a non-square size, the upsampler composes for that
  shape.
- `image-to-image`: an instruction (“make the cape dark red”) applied to
  `image`, at about 1 or 4 MP in its proportions (`size`, default `1K`). An
  optional second picture, `reference`, gives a subject, garment or style to
  bring in. FLUX 3 has no alpha channel: transparent pixels become white.
- `upsample` (on by default) runs the upsampler first. Turn it off only for a
  prompt that is already a full caption. Each result's metadata keeps the
  caption the image model received.

## How it fits in 8 GB

- **Upsampler.** BFL's Qwen3.5-9B fine-tune, as GGUF Q8_0 with its vision
  projector, in the `llm` server's pinned llama.cpp build. It runs as a child
  process for one job's caption, then exits before the image model needs the
  GPU. Requests, system prompts, reference resizing and greedy decoding follow
  BFL's reference code. The caption is written once per job, for every
  variant.
- **Transformer.** GGUF Q8_0 from unsloth (6.1 GB, practically lossless).
- **Text encoder.** bf16 without its unused output head (8.9 GB). It encodes
  text only; references reach the image model through the VAE.
- **Offloading.** The transformer and the encoder stream to the GPU block by
  block from a disk store (`~/.cache/assets-studio/flux3-offload`, 14 GB,
  written on the first start), as in the Qwen-Image server.
- **VAE.** float32, stays on the GPU. Images up to about 1 MP go through it
  whole, as in BFL's code. 2K images go through in its 1024 px tiles: whole,
  a 2048² decode asks for 4 GB at once and runs out of memory. At 1:1, no
  seams were visible, and the tile edges show no extra brightness steps.

Measured November 23, 2026 on an RTX 3070 Laptop (8 GB), jobs sent through the
Studio:

| | |
| --- | --- |
| Loading | ~30–50 s; the first start also writes the 14 GB store |
| Upsampler | 50–90 s per job: loading 23–46 s, ~8 tokens/s, 200–350 tokens |
| Denoising, 16 steps | 1K ~60 s (3.6 s/step); 1K edit ~100 s; 2K ~190 s (12 s/step) |
| Whole job, upsampler included | 1K 2–3 min (126–187 s); 1K edit 3 min; 2K 4.5 min |
| Peak VRAM | 5.4 GB generating, 6.9 GB in the upsampler |
| Free RAM | ≥18 GB of 31 GB throughout |

Qwen-Image 2.1 took 212–259 s for the same three 1K jobs (40 steps).

Run alone: it does not fit alongside other GPU servers.

## Integration findings

- diffusers 0.43.0 runs FLUX 3 as a modular pipeline. The server removes its
  upsampler and text-encoder blocks: llama.cpp writes the caption, and the
  encoder runs once per job.
- The GGUF file stores 14 linear weights unquantized (BF16) and the attention
  norms in float32. diffusers keeps unquantized GGUF tensors as raw bytes and
  skips the float32 norms when it reports a model's dtype, so the transformer
  reported `uint8` and the pipeline cast its latents to it (“expected mat1 and
  mat2 to have the same dtype, but got: unsigned char != BFloat16”). The
  server decodes these weights to plain bf16 at load time: the same memory,
  and the outputs are bit for bit the same.
- The encoder's token embedding needs its own offload group, as in
  Qwen-Image: the model embeds the tokens before it calls the language
  model's blocks.
- The upsampler's Q8_0 file (9.5 GB) does not fit in 8 GB: llama.cpp keeps
  about 6.7 GB of it on the GPU and the rest in system memory.
