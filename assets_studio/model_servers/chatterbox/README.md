# Chatterbox: speech in 23 languages

`text-to-speech` with
[Chatterbox Multilingual V3](https://huggingface.co/ResembleAI/chatterbox)
by Resemble AI (MIT): a 0.5B Llama-based model writes speech tokens, S3Gen
turns them into sound. The prompt is the text to speak, the result a mono
16-bit WAV at 24 kHz. The studio lists it under Sound.

```sh
just setup     # the environment
just download  # weights 3.3 GB, stress dictionary 360 MB, Chinese 35 MB
just doctor    # PyTorch, the GPU, the weights and a stressed Russian example
just serve     # http://127.0.0.1:9147
just test      # no GPU or weights needed
```

## Parameters

- `language`: 23 languages, Russian by default.
- An optional `voice` input, a WAV of clean speech, clones a voice: upstream
  takes its first 10 s (6 s for the speech tokens). Samples of 1 s to 5 min
  are accepted; silent or unreadable ones are refused. Without a sample the
  checkpoint's built-in voice speaks.
- `exaggeration` (0.5): how expressive. Higher values also speed the speech up.
- `cfg_weight` (0.5): how closely the speech follows the text and the sample's
  pace. Resemble AI suggests about 0.3 for a fast sample or dramatic lines,
  and 0 when the sample is in another language, so its accent does not carry over.
- `temperature` (0.8): variability.

Texts are limited to 500 characters: upstream stops a take at 1000 speech
tokens, about 40 s, so a longer text would be cut off. Split longer texts.

Each candidate seeds the generator with its own seed, so one request with that
seed repeats it byte for byte.

## How it runs

- The `chatterbox-tts` package at the commit that loads the V3 checkpoint
  (PyPI's 0.1.7 cannot) and the checkpoint at a pinned revision, both from the
  Hugging Face cache; missing weights are downloaded on the first start.
- Upstream pins torch 2.6.0; the environment uses 2.8.0, as Resemble AI's own
  V3 demo does. Its gradio demo is left out.
- Russian is trained with stress marks. Upstream adds them with
  `russian-text-stresser` (GPL-3.0) but does not depend on it and silently
  skips the marks without it, so the environment installs it and the server
  refuses to start unless a test sentence comes back stressed. Its Wiktionary
  dictionary is downloaded from the stresser's release and checked against a
  SHA-256; the server keeps it in `~/.cache/assets-studio/chatterbox-russian-dictionary`.
  The stresser's LLM disambiguation is not used.
- Chinese needs a Cangjie table, which upstream keeps in the checkpoint
  directory, and the pkuseg segmenter, which goes to `~/.pkuseg`. `just download`
  fetches both, so the server does not reach for the network while it runs.
- Hebrew is spoken without vowel points: upstream adds them with `dicta_onnx`,
  which it does not install either.
- Upstream remembers the last cloned voice; a request without a sample gets
  the built-in voice back.
- Every file carries Resemble AI's inaudible Perth watermark, as upstream adds it.

## Measured

On an 8 GB RTX 3070 Laptop GPU, speaking Russian:

- Loading: 36 s on the first start.
- Speaking: about 1 s of computing per second of speech (75 s of speech for 12
  game lines in 75 s; a 403-character paragraph, 24.4 s, in 23.8 s).
- About 3.3 GB of GPU memory after loading, 4.2 GB after the long paragraph.
- The studio's contract check: 10 checks, no failures or warnings.

License: MIT for the model and the package; `russian-text-stresser` is GPL-3.0.
