# Text to speech (Russian)

<!-- markdownlint-disable MD013 -->

## XTTS-v2

Tested September 30, 2026 on an RTX 3070 Laptop (8 GB).

| | |
| --- | --- |
| License | Coqui Public Model License 1.0.0 — non-commercial (suitable for this hobby project) |
| Revision | `6c2b0d75` |
| Weights | ~2 GB |
| Model load | ~10 s |
| Synthesis | ~0.9× real time after warmup: 17 s of speech (234 characters) in 18.4 s |
| VRAM | ~1.9 GB peak, ~2.5 GB with PyTorch cache |
| Languages | 14 (excluding Chinese, Japanese and Hindi; see server README) |
| Contract check | 10 checks, 0 errors, 0 warnings (variants reproduce bit for bit) |

Sample-based voice cloning works. Short, quiet, corrupt or overlong samples
fail with clear errors.

**Russian speech quality still requires listening**; automated checks cannot
establish it.

### Integration findings

- `transformers` 5.x breaks XTTS by removing internal functions; pinned to 4.57.6.
- Coqui's `Xtts.eval()` returns `None`, unlike `nn.Module.eval()`; the contract
  check caught this.
- Russian text of 182 characters or more is split into sentences, which needs
  spaCy; coqui does not install it, so the environment does. Chinese and
  Japanese are not offered (their packages are missing), nor is Hindi (coqui
  cannot expand its digits). Tests cover each offered language and long-text
  splitting.
- Cloning uses the model config's settings (up to 30 seconds of the sample
  define the voice, not the first six), and samples are validated by full
  decoding.

## Not tested

Silero TTS (fast, good Russian voices, no cloning), Russian F5-TTS fine-tunes,
Fish Speech / OpenAudio, Piper (CPU). These could be separate servers for
listening comparisons.
