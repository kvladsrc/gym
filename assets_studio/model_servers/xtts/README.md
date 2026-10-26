# XTTS model server

Contract v1 text-to-speech based on
[XTTS-v2](https://huggingface.co/coqui/XTTS-v2) (Coqui Public Model License,
non-commercial), using the maintained
[coqui-tts fork](https://github.com/idiap/coqui-ai-TTS).

```sh
just setup    # Python 3.11 + PyTorch 2.5.1/CUDA 12.4 + coqui-tts environment
just doctor   # PyTorch, GPU, downloaded weights and built-in voice consistency
just serve    # http://127.0.0.1:9103; weights (~2 GB) download on first start
just test     # declarations, languages, text splitting, voice samples; no GPU
              # (downloads only the tokenizer vocabulary, ~0.4 MB)
```

- 14 languages, Russian by default; 58 built-in voices. Chinese, Japanese and
  Hindi are not advertised: their text processing requires missing packages,
  and coqui does not pronounce Hindi digits.
- Optional “Voice sample” input (WAV, 3–30 seconds of clean speech) clones a
  voice; otherwise the selected built-in voice is used.
- “Variability” is sampling temperature; “Speed” controls speech rate.
- Long text (from about 180 Russian characters) is split into sentences with
  spaCy; maximum length is 5000 characters.
- Voice samples may be 1 second to 5 minutes long and must not be silent.
  The first 30 seconds define the voice (cloning settings come from model config).
- Each variant has its own seed; a single request with that seed reproduces
  the variant bit for bit.

The model uses about 2 GB VRAM; synthesis runs roughly in real time (~0.9×).
It can stay loaded alongside an image or 3D model.

`transformers` is pinned to 4.57.6: XTTS uses internal functions removed in 5.x.
