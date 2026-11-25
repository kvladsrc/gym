# Voices and music

<!-- markdownlint-disable MD013 -->

## Voice lines (Qwen3-TTS and Chatterbox)

The findings below come from XTTS-v2, the first `voice` server, removed in
November 2026 (see [text-to-speech](../models/text-to-speech.md)). Voices are
now cloned from a sample with `qwen3_tts_clone` or designed from a description
with `qwen3_tts_design`; `chatterbox` reads Russian with stress marks.

- **Keep lines short and situational:** spotting, attacking, taking damage,
  dying. Use 1–3 lines per situation, one at a time, with a pause after each
  (line duration + 2.5 seconds). Play attack lines every other attack, damage
  lines occasionally, and death lines always.
- **Voice samples must be clean speech only.** With music underneath, XTTS
  cloned the music too and added seconds of garbled continuation after short
  phrases: 8 seconds instead of 1.5. Lower `temperature` (0.3) made this
  worse, not better (28 seconds for “How rude!”).
- **Russian stress decides the rating:** XTTS had no stress marks, and 8 of
  43 Russian lines were rated 0, mostly for wrong stress. Generate extra
  takes, rate them, and admit only those rated ≥2 into the game.
- **Loudness:** XTTS was much quieter than game effects. Normalize voice lines
  (EBU R128, −16 LUFS) and play speech without random pitch (useful for steps
  and impacts, not speech).

## Songs (YuE, `song` server)

- The prompt is lyrics with `[Verse]`, `[Chorus]`, `[Bridge]` sections; style
  is specified in English: language, genre, instruments, voice and tempo.
- Output metadata contains the song plan (ABC notation), with `% intro`,
  `% verse`, `% chorus` and tempo. Use it to estimate chorus timing (for example,
  29-second intro, chorus at 50 seconds). If music accompanies an event such
  as a boss fight, start it early (intro during the approach) and make the
  event at least long enough to reach the chorus.
- Generate four takes across two styles, rate them and use the best in-game;
  match loudness to the rest of the music.
