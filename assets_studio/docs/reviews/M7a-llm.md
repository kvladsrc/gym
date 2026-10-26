# Review: M7a, LLM server (Gemma 4 12B, Qwen3.8 27B)

Critic: two iterations, ACCEPT.

## Iteration 1 — REVISE

Blocker: LLM tabs set `timeout_s = 3600`, but complete jobs (up to four variants
of 8192 tokens at 3 tokens/s) take longer. Studio would abandon the job while
the server generated for hours, responding busy to everything else. Removed
timeout: Studio waits by default.

Recommendations accepted in iteration 2:

- Model-card sampling, replacing temperature 0.8 and llama.cpp defaults;
  comparison repeated.
- Disable prompt cache: reused prefixes use different GPU kernels, while
  evolution needs byte-reproducible seeded results.
- Replace seed 4294967295 (llama.cpp's random sentinel) with 0.
- Qwen reasoning uses “low” rather than template-default “xhigh”, with a separate
  token allowance.
- Stop `llama-server` after failed loading and restart after crashes; find
  `prctl` for PDEATHSIG in the parent.
- Install llama.cpp under a lock through a temporary directory, verifying
  checksums while streaming.
- Canonicalize text; non-UTF-8/context overflow are request errors (422);
  fence source text with `<текст>`.

## Iteration 2 — ACCEPT

Follow-up: test crashed-child restart; any restart failure (including missing
binary) is a generation error. 422 is only for context overflow; other
llama.cpp 400 errors are server faults, not user faults. Live-verified that
`reasoning_effort: low` reaches Qwen's template (“Reasoning effort is set to low”).

SDK debt: after loading, a server cannot mark itself errored; restart and
retryable errors currently compensate.
