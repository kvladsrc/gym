# M7a: text-to-text (LLM)

## Outcome

`model_servers/llm` (llama.cpp b11312 + GGUF), two models with separate tabs:

- **Gemma 4 12B, Q8_0**: default (`just gemma`, port 9106).
- **Qwen3.8 27B, UD-Q4_K_M**: richer, slower (`just qwen`, port 9107).

Both passed contract checks 10/10 without warnings (prompt caching disabled,
so identical seeds repeat responses byte for byte).

## Candidates (official releases as of 2026-10-01)

Hugging Face popularity lists contain many repacks (“uncensored”, distilled);
only author-published models were considered.

| Model | Selection rationale |
| --- | --- |
| Gemma 4 12B (Google, May 2026) | Selected: Q8 nearly lossless, 6.6 GB on GPU |
| Qwen3.8 27B (Alibaba, August 2026) | Selected: latest dense model, 16.5 GB in Q4 |
| Qwen3.6 35B-A3B (MoE) | Untested: 22 GB in Q4, barely fits free RAM |
| GLM-5.3-Flash | 321B parameters, unsuitable for a laptop |
| Gemma 4 26B-A4B, 31B | Untested; dense 31B is slower than Qwen3.8 27B |

## Runtime

llama.cpp publishes prebuilt Linux CUDA 12.8 binaries; no compiler is needed,
and driver 595 supports them. The build is pinned with GitHub checksums.
llama.cpp assigns layers itself (`--fit`).

| | Gemma 4 12B Q8 | Qwen3.8 27B Q4 |
| --- | --- | --- |
| Load time | 18 s | 21 s |
| VRAM | 6.6 GB | 6.3 GB |
| Free RAM while running | ≥19 GB | ≥17 GB |
| Speed | 4.8–5.4 tokens/s | 3.1–3.5 tokens/s |

## Comparison (seeds 7 and 11)

Seven tasks: tavern names, blacksmith line, item description, shortening text,
side quest, branching dialogue and pause menu. Texts are in the session log.
Findings:

- **Gemma 4 12B**: fluent and consistent but simpler; omitted “Resume” in the
  pause menu. One token corruption (“в егонутри”, a malformed Russian phrase).
- **Qwen3.8 27B**: more vivid place names and merchant characterization, with
  imagery such as “unpleasantly warm, as though the breath of a sleeping beast
  had frozen within it”. But Q4 makes Russian gender/government errors:
  “Костяной Гнезд”, “управляет топь”, “густой от спора”.

Repeated with model-card sampling (temperature 1.0, top_k 64 for Gemma and 20
for Qwen, min_p 0); the first run used temperature 0.8 and llama.cpp defaults.
Gemma became more vivid (“pulsing in time with its owner's slowing heartbeat”),
without corrupted tokens. Qwen3.8 retained grammatical errors (“Тихий Казнь”,
“жительны деревни”), attributed here to Q4 quantization rather than sampling.

No clear winner; both retained. Qwen3.8 Q5/Q6 (20–22 GB) would exceed free RAM
without swapping. Untested alternatives potentially faster than both: Qwen3.6
35B-A3B and Gemma 4 26B-A4B (MoE, 3–4B active parameters, experts in RAM).

## Verified

- Stopping the server with Ctrl+C also stops `llama-server`; `kill -9` does
  too (kernel SIGTERM via `PR_SET_PDEATHSIG`), releasing VRAM.
