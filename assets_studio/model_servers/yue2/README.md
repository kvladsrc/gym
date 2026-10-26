# YuE2 model server

Vocal songs from lyrics and style (“Text → song”, `text-to-song`), using
contract v1, [YuE2-3B](https://huggingface.co/m-a-p/YuE2-3B) and the official
[yue2-infer package](https://github.com/multimodal-art-projection/YuE).

```sh
just setup    # Python 3.12 + PyTorch 2.10/CUDA 12.8 + yue2-infer environment
just doctor   # PyTorch, GPU, bf16 and downloaded weights
just serve    # http://127.0.0.1:9110; first start downloads ~7.8 GB
just test     # declarations and model half-splitting; no GPU or weights needed
```

Weights: CC BY-NC 4.0 with additional permission for personal use and content
creators, including monetization of generated music.

## Usage

- The prompt is **song lyrics**, with section markers: `[Verse]`, `[Chorus]`,
  `[Bridge]`.
- “Style” specifies language, genre, instruments, voice and tempo in English:
  `Russian, fantasy folk ballad, warm male voice, acoustic guitar, 76 BPM`.
- “Plan”: `full` first generates melody and chords (default); `melody` produces
  melody only; `off` skips planning.
- Output: 48 kHz stereo WAV. Metadata includes the plan's ABC score (`score`)
  and `truncated` if the model hits its token limit.
- Overlong lyrics are rejected immediately: lyrics, style and the score
  (4096 tokens) and song (9000) budgets must fit the model's 24,576-token context.

## Fitting in 8 GB

The authors target 24 GB; the entire model is about 7.1 GB in bf16. Each layer
has two copies of attention and MLP: autoregressive (AR: score and semantic
tokens) and streaming (NAR: acoustic latents). The halves run in turn:

- AR tokens: AR layers, embeddings and output head on GPU; NAR copies in RAM.
- Latents: before each chunk, the AR half moves to RAM and NAR to GPU
  (replacing `yue2.nar._offload_ar`). After the chunk, placement is reversed:
  the next chunk needs the AR prefix.
- VAE: the entire model moves to RAM, and the VAE decodes in 512-frame chunks.

Weights and computations match upstream; only weight placement changes.

- Each half remembers its original RAM tensors and returns to them without
  copying. With `module.to("cpu")`, every switch copied a half to new memory,
  growing the server to 17.5 GB RAM after two songs.

| | |
| --- | --- |
| 62-second song (verse and chorus) | ~62 s including initial model load |
| Two songs of 93 and 110 seconds | ~184 s |
| Peak VRAM | 5.7–6.1 GB |
| Server RAM | ~9 GB, stable across jobs |

Run alone: it does not fit alongside other heavy servers. Identical requests
with the same seed produce bit-identical songs.
