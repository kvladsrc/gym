# Qwen3-TTS: speech in ten languages

Three servers from one directory, each running one Qwen3-TTS 1.7B checkpoint
by Alibaba's Qwen team. All three do `text-to-speech`: the prompt is the
text to speak, the result a mono 16-bit WAV at 24 kHz. They speak Russian,
English, German, French, Spanish, Italian, Portuguese, Japanese, Korean and
Chinese. The studio lists them under Sound.

| `--model` | Port | Checkpoint | The voice |
| --- | --- | --- | --- |
| `design` (default) | 9139 | `Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign` `5ecdb67327fd` | described in words: sex, age, timbre, mood, pace, manner |
| `voice` | 9140 | `Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice` `0c0e3051f131` | one of nine built-in voices, with an optional instruction on how to speak |
| `clone` | 9141 | `Qwen/Qwen3-TTS-12Hz-1.7B-Base` `fd4b25438912` | the voice of a `voice` sample, 1 to 30 s of clean speech |

```sh
just setup                 # the environment
just download [design|voice|clone]  # weights, ~4.5 GB each
just doctor
just serve --model design  # http://127.0.0.1:9139
just test                  # no GPU or weights needed
```

## Parameters

- `design`: `voice_description`, in any of the languages; empty leaves the
  voice to the model.
- `voice`: `speaker` and `instruction` (emotion, pace, manner). The voices
  are native speakers of Chinese (`serena`, `vivian`, `uncle_fu`, and
  `dylan` and `eric` with Beijing and Sichuan speech), English (`ryan`,
  `aiden`), Japanese (`ono_anna`) and Korean (`sohee`). Each speaks every
  language, best its own.
- `clone`: `voice_text`, what the sample says, word for word. Without it the
  model takes only the speaker embedding of the sample, which Qwen says
  copies the voice less faithfully.
- All three: `language` (default Russian) and `temperature` (0.9 as the
  checkpoints ship it).

The other sampling settings are the checkpoints' own (top-k 50, repetition
penalty 1.05, the code predictor's temperature 0.9). Each candidate seeds the
generator with its own seed, so one request with that seed repeats it.

For one character across many lines, Qwen suggests making the voice once with
`design` and giving that clip and its text to `clone` for every line: a
description alone gives a somewhat different voice with every seed.

## How it runs

- bfloat16 weights on the GPU. Attention runs through PyTorch's
  scaled-dot-product kernels; Qwen recommends FlashAttention 2 to save
  memory, but it would have to be built from source.
- Weights come from the Hugging Face cache at the pinned revisions and are
  downloaded on the first start when they are missing.
- The calls are those of Qwen's `qwen-tts` package. It wants gradio for its
  web demo only, which the environment leaves out. At start it warns that
  SoX and flash-attn are missing: both warnings come from its 25 Hz
  tokenizer, which these 12 Hz checkpoints do not use.

## Measured

On an 8 GB RTX 3070 Laptop GPU, with `design` and `clone` (`voice` is not
measured yet) speaking Russian lines of 3.5 to 12 s:

- Loading a checkpoint from the cache: 8 to 10 s.
- Speaking: about 1.8 s of computing per second of speech.
- At most about 5.1 GB of GPU memory.
- The same seed gives the same file, byte for byte.

License: Apache-2.0, the checkpoints and the `qwen-tts` package.
