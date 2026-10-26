# LLM model server

<!-- markdownlint-disable MD013 -->

Text generation and editing (`text-to-text`, ADR-003) with GGUF models. The
model runs in llama.cpp: a pinned CUDA build launched as a child process on a
free port, accessed through its OpenAI-compatible API. No PyTorch required.

```sh
just setup                        # environment + llama.cpp b11312 (~0.8 GB)
just doctor                       # build, GPU visibility and downloaded models
just serve                        # Gemma 4 12B on 127.0.0.1:9106
just serve --model qwen3.8-27b    # Qwen3.8 27B
just test                         # no GPU or weights needed
```

From `model_servers/`: `just gemma` (port 9106) and `just qwen` (port 9107).
Both appear under “Text” in Studio, where the model can be selected.

## Models

| `--model` | File | Placement | Speed | Character |
| --- | --- | --- | --- | --- |
| `gemma-4-12b` (default) | Q8_0, 12.7 GB | ~6.6 GB on GPU, rest in RAM | ~5 tokens/s | Fluent, consistent |
| `qwen3.8-27b` | UD-Q4_K_M, 16.5 GB | ~6.3 GB on GPU, rest in RAM | ~3.5 tokens/s | Richer detail, but occasional grammar errors in Q4 |

llama.cpp fits as many layers on GPU as possible (`--fit`), reading the rest
from RAM. Large models therefore run, but more slowly. Loading takes about
20 seconds with a downloaded file; first start downloads 12.7 or 16.5 GB.
Both are Apache-2.0.

## Parameters

- **Answer length** (tokens) and **variability** (temperature) are primary.
- top-k and top-p. Sampling defaults follow model cards: Gemma 4 uses
  temperature 1.0, top_k 64, top_p 0.95; Qwen3.8 uses 1.0, 20, 0.95; min_p 0.
- **Think before answering** is off by default (slower for text). Reasoning is
  excluded from output; 4096 tokens are added to the answer budget for it.
  Qwen3.8 uses “low” effort (otherwise its template chooses “xhigh”, costing
  thousands of tokens).
- **System instruction** defaults to helping make a game, answering in Russian
  without introductions.
- Optional **text** input supplies material to edit or continue; the prompt
  becomes the instruction (“shorten”, “make darker”). This makes the task
  X-to-X and suitable for evolution.

Each variant is a separate request with its own seed and no prompt cache
(reusing a prefix otherwise computes it with different GPU kernels), so the
same seed repeats the answer. llama.cpp treats 4294967295 as random; it is
replaced with 0.

Output is canonicalized (ADR-003). Non-UTF-8 text or requests exceeding the
16,384-token context are request errors (422).

## Internals

- `runtime.py`: pinned llama.cpp build (`b11312`, CUDA 12.8), verified against
  GitHub checksums, installed in `~/.cache/assets-studio/llama.cpp`.
- Installation uses a lock, a temporary directory and an atomic directory
  move, so simultaneous servers do not interfere.
- The child `llama-server` receives SIGTERM if this server dies (even from
  `kill -9`), avoiding orphaned VRAM use. Failed startup stops it immediately.
  If it dies between requests (for example from VRAM contention), the next
  request restarts it.
- Reasoning (`reasoning_content`) is separated and excluded from returned text.

## Local image and video description

The already installed **Qwen3.8-27B** is a vision-language model. The `qwen`
server now loads a compatible `mmproj-F16.gguf` (0.93 GB) from the same pinned
Unsloth revision. This extends the existing server without another language
model or SDK changes. Gemma remains `text-to-text` only; no projector is declared.

Tasks appear in Studio's “Text” section and as ordinary Flow nodes:

- `image-to-text`: required `image`, optional `reference` for comparing two
  images; PNG/JPEG/WebP → `text/plain`.
- `video-to-text`: `video`, MP4/WebM up to 60 seconds; 2–6 evenly sampled,
  timestamped frames → `text/plain`. This examines samples, not continuous
  video understanding or audio analysis.

Visual-task defaults: 512 output tokens, temperature 0, thinking off. Images
are limited to a 1280-pixel side; the vision encoder uses 1024–1536 tokens per
frame. Context: 16,384 tokens, one request at a time. Short responses may be
cut off by the limit; metadata records `finish_reason=length`.

```sh
# From the monorepo root, without loading media into the agent context:
python assets_studio/agent_tools/describe_media.py \
  --path /absolute/path/image.png --prompt 'Describe visible details and uncertainty.'
python assets_studio/agent_tools/describe_media.py \
  --path /absolute/path/clip.mp4 --frames 4 --output workspace/review/report.json
# From assets_studio/: just describe-media --path /absolute/path/image.png
```

Reports are saved in the Studio library and cached by content, question, model
revision and parameters. Results include provenance, sample times and limitations;
helper stdout contains text only. The first synthetic queue smoke test correctly
identified a red square on the left, a blue circle on the right and “ALPHA 73”.
This validates connectivity, not fencing analysis quality. The model runs partly
in RAM and is slower than small VLMs.

Shared Codex and Claude instructions: [media-analysis.md](../../../../.agents/media-analysis.md).
Experimental approach review: [mocap-approach.md](../../docs/reviews/mocap-approach.md).
