# M0c. Text-to-speech (Russian)

<!-- markdownlint-disable MD013 -->

## XTTS-v2 — server ready

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
fail with clear errors. Examples (built-in female/male voices on long text,
and a cloned voice) are in `build/samples/` (not in git).

**Russian speech quality still requires listening**; automated checks cannot
establish it.

### Integration findings

- `transformers` 5.x breaks XTTS by removing internal functions; pinned to 4.57.6.
- Coqui's `Xtts.eval()` returns `None`, unlike `nn.Module.eval()`; the contract
  check caught this.

### Review

The critic found internal errors for Russian text from 182 characters: sentence
splitting requires spaCy, which coqui does not install. Chinese/Japanese were
advertised but always failed without their packages. “Faster than real time”
contradicted measurements. Fixed: add spaCy, remove Chinese/Japanese/Hindi
(digits), remeasure speed. Tests cover each advertised language and long-text
splitting. Cloning uses model-config settings (up to 30 seconds define the voice,
not the first six), and samples are validated by full decoding. Second iteration:
**ACCEPT**.

## Not tested

Silero TTS (fast, good Russian voices, no cloning), Russian F5-TTS fine-tunes,
Fish Speech / OpenAudio, Piper (CPU). These could be separate servers for
listening comparisons.
