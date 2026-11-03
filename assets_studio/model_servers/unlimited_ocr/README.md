# Unlimited-OCR 2

One document page image → UTF-8 `text/plain`, using the existing
`image-to-text` task and model-server contract v1. Input role: `image`,
PNG/JPEG/WebP. Output keeps tables and formulas, with model layout
coordinates and page markers removed. PDF rendering is the caller's job;
there is no PDF input or new Studio modality.

Model: [baidu/Unlimited-OCR-2](https://huggingface.co/baidu/Unlimited-OCR-2),
MIT, 3.34 billion parameters. Code and weights are pinned together to
`0480966938291445591f69f08a13b041c073a96a`.
Upstream: [Unlimited-OCR](https://github.com/baidu/Unlimited-OCR).

## Install and run

From the monorepo root:

```sh
nix develop -c just assets_studio model_servers setup unlimited_ocr
nix develop -c just assets_studio model_servers doctor unlimited_ocr
nix develop -c just assets_studio model_servers unlimited_ocr
```

The server listens on `127.0.0.1:9132`; `--port` overrides it. Add the
`unlimited_ocr` entry from `studio.example.toml` to your Studio config and
restart Studio and the launcher. It appears in Text; the launcher starts it
when a job needs it and stops it before switching to another GPU model.

The isolated environment lives in
`~/.cache/assets-studio/venvs/unlimited_ocr`; the Hugging Face cache holds
the weights (~6.7 GB). Neither is stored in Dropbox. Installation excludes
demo images, PDFs and the custom SGLang wheel. Native Transformers inference
uses the CUDA PyTorch wheels shared with the other Studio environments.

## Parameters

- `max_tokens`: 4096 by default, 16–16384. Outputs record `finish_reason`,
  `truncated` and `output_tokens`; a token-limited page is not a complete
  transcription.
- `crop_mode`: off by default, one global 1024-pixel view. Enable for small
  text; at most four local 640-pixel tiles, with higher GPU memory use.
- Optional prompt: an OCR instruction. Empty uses the model's trained
  `Multi page parsing.` instruction for the single supplied page. The model
  inserts its own page/image placeholders. Decoding is greedy and seed
  independent; `count` is one.

Only text returned by the local model needs to be consumed by agents.
Use Studio's queue so the OCR model shares the GPU with other models.

For agents, import the page image and submit an ordinary Studio job:

```json
{
  "server": "unlimited_ocr",
  "task": "image-to-text",
  "inputs": {"image": "IMPORTED_ASSET_ID"},
  "params": {"max_tokens": 4096, "crop_mode": false},
  "count": 1,
  "seed": 42
}
```

This is the body of `POST /api/jobs`. The existing MCP `generate` tool
uses the same server/task and `input_assets={"image": "IMPORTED_ASSET_ID"}`.
Read the resulting text asset after the job succeeds.

## Checks

```sh
nix develop -c just assets_studio model_servers test unlimited_ocr
```

These tests cover the adapter without loading weights or using the GPU.

On 2026-11-02, the native Transformers path ran on an RTX 3070 Laptop GPU
(8 GiB) with PyTorch 2.8.0/CUDA 12.9 and full BF16 weights, without CPU
offload or an external FlashAttention package. A synthetic 1024×1024 page
submitted through Studio and auto-started by the launcher reproduced all
English/Russian control phrases and numbers: 131 output tokens, 26.3 s
on the first inference (including Triton kernel compilation), then 9.9 s
with the compiled kernels cached; 6838 MiB peak PyTorch allocation.
Actual memory use includes CUDA overhead as well.
The launcher also stopped the model after its idle timeout.

Local smoke artifacts: `workspace/review/unlimited-ocr/smoke.json` and
`job.json` from the monorepo root. Recognizing a synthetic page does not
measure book OCR accuracy, reading order on complex layouts, or broad
language coverage. Tiled inference is an option, not a tested 8 GiB budget
for every page; disable it if memory is exhausted.
