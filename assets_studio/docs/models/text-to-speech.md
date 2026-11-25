# Text to speech (Russian)

<!-- markdownlint-disable MD013 -->

## Chatterbox Multilingual V3

Tested November 23, 2026 on an RTX 3070 Laptop (8 GB).

| | |
| --- | --- |
| License | MIT; outputs carry Resemble AI's Perth watermark; the Russian stress package is GPL-3.0 |
| Revision | `5bb1f6ee58e5` |
| Weights | ~3.3 GB, plus a 360 MB stress dictionary |
| Model load | 36 s |
| Synthesis | ~1.0× real time: 75 s of speech for 12 game lines in 75 s (XTTS: the same lines in 39 s) |
| VRAM | ~3.3 GB after loading, ~4.2 GB with PyTorch cache after a 403-character paragraph |
| Languages | 23 |
| Contract check | 10 checks, 0 errors, 0 warnings (variants reproduce byte for byte) |

A blind listening comparison against XTTS-v2 and Qwen3-TTS (clone) on 12
lines of four characters from a game in progress, with the same voice samples
for all three: XTTS-v2 came out worse than Qwen3-TTS; Chatterbox's verdict is
pending.

### Chatterbox integration findings

- The V3 checkpoint loads only with the GitHub package at a recent commit;
  PyPI's `chatterbox-tts` 0.1.7 has no `t3_model="v3"`.
- Russian needs stress marks, as V3 was trained. Upstream adds them with
  `russian-text-stresser` only when it happens to be installed and otherwise
  speaks unstressed text without an error. The server installs it, downloads
  its dictionary with a checksum, and refuses to start without stress marks.
- The stresser creates `llmdebug.db` in the working directory when its LLM
  module is imported; the server blocks that module.
- Upstream keeps the last cloned voice as model state; requests without a
  sample restore the built-in voice.
- Upstream fetches the Chinese Cangjie table and the pkuseg segmenter (34 MB)
  on the first start; `just download` fetches them beforehand.
- A take ends at 1000 speech tokens (~40 s); texts are limited to 500 characters.

## Removed: XTTS-v2

The studio's first speech server (`voice`, September 2026) was removed on
November 23, 2026: in the blind comparison above it sounded worse than
Qwen3-TTS, its Coqui Public Model License is non-commercial, and its
vocabulary has no stress marks, so Russian lines often came out with the wrong
stress (8 of 43 game lines were rated 0, mostly for that). It ran at ~0.9×
real time in ~2 GB of GPU memory.

## Not tested

Silero TTS (fast, good Russian voices, no cloning), Russian F5-TTS fine-tunes
and ESpeech, Fish Audio S2 Pro, Piper (CPU). These could be separate servers for
listening comparisons.
