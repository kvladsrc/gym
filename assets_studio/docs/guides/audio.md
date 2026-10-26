# Voices and music

<!-- markdownlint-disable MD013 -->

## Voice lines (XTTS, `voice` server)

- **Keep lines short and situational:** spotting, attacking, taking damage,
  dying. Use 1–3 lines per situation, one at a time, with a pause after each
  (line duration + 2.5 seconds). Play attack lines every other attack, damage
  lines occasionally, and death lines always.
- **Voice samples must be clean speech only**, 6–30 seconds (XTTS listens to
  the first 30 seconds). With music underneath, the model clones the music
  too and adds seconds of garbled continuation after short phrases: 8 seconds
  instead of 1.5. Lower `temperature` (0.3) made this worse, not better
  (28 seconds for “How rude!”).
- **XTTS cannot accept stress marks:** its vocabulary has neither the combining
  stress mark U+0301 nor “+”, only the Russian letter “ё”. Of 43 Russian lines,
  8 received a rating of 0, mostly for incorrect stress. Generate extra takes,
  rate them, and admit only those rated ≥2 into the game.
- **Loudness:** XTTS is much quieter than game effects. Normalize (EBU R128,
  −16 LUFS) and play speech without random pitch (useful for steps and impacts,
  not speech).

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
