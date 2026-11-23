#!/usr/bin/env python3
"""Copy the game's sounds from Assets Studio into Assets/Realms/Art/Audio (not in Git).

Reads sounds.json. Every entry is one name the game plays (an Sfx, an ambience layer or a kind of
music) with how it is played (group, volume, pitch and loudness spread, limit, cooldown, priority,
whether it is a sign) and its takes, each a Studio asset. How a take becomes clips depends on the
entry's kind:

  effect  the take holds one sound or a series of separate ones; each sound is cut at its attack
          (2 ms before it, faded in over 2 ms) and after its decay, made mono and set to a -3 dBFS
          peak. "events" picks which of a series to keep (from 0); "whole" keeps the take as one;
          "max" shortens a longer sound to that many seconds, fading over its last 0.2 s.
  sign    a short melody: the whole take from its attack, stereo, at a -3 dBFS peak. "semitones"
          moves the tune keeping its time, "lowpass" dulls it, "max" shortens it to that many
          seconds, fading over its last 0.35 s.
  loop    an ambience loop: untouched in time (the join stays seamless), set to -20 LUFS.
  music   a piece or a phrase: from its attack, set to the entry's "lufs" (default -16) with the
          true peak kept under -1 dBTP; a piece that does not end by itself fades over 3 s.

An entry with "from" is made from another entry's first clips by a script: "semitones" moves them,
"lowpass" dulls them, "pair" plays two of them 90 ms apart (the second "pair" semitones away).

Effects and signs are written as 16-bit WAV, loops and music as Ogg Vorbis. Art/Audio/library.json
lists every entry with its clips for the editor's Setup, which builds RealmsSounds.asset from it.
Needs ffmpeg and the Studio API (default http://127.0.0.1:9000, or $STUDIO_URL). Fetches the
takes that are new or changed since the last fetch (Art/Audio/manifest.json); --force fetches all.
"""

import argparse
import array
import json
import math
import os
import pathlib
import shutil
import subprocess
import urllib.request

here = pathlib.Path(__file__).resolve().parent
game = here.parent
audio = game / "unity/realms/Assets/Realms/Art/Audio"
RATE = 44100


def download(api, asset_id):
    with urllib.request.urlopen(
        f"{api}/api/assets/{asset_id}/file", timeout=120
    ) as reply:
        return reply.read()


def ffmpeg(*args, data=None):
    return subprocess.run(
        ["ffmpeg", "-v", "error", *args], input=data, capture_output=True, check=True
    ).stdout


def decode(path, channels):
    """The file as 16-bit samples at 44.1 kHz, interleaved."""
    raw = ffmpeg(
        "-i", str(path), "-ac", str(channels), "-ar", str(RATE), "-f", "s16le", "-"
    )
    return array.array("h", raw)


def envelope(mono, hop=220, window=441):
    """dB of the RMS of 10 ms windows every 5 ms (full scale is 0 dB)."""
    out = []
    for start in range(0, max(1, len(mono) - window), hop):
        chunk = mono[start : start + window]
        power = sum(v * v for v in chunk) / max(1, len(chunk))
        out.append(10 * math.log10(max(power, 1e-9) / (32768.0 * 32768.0)))
    return out


def events(level, depth=30.0, gap_frames=30):
    """(first, last) frames of the separate sounds: runs above the loudest minus depth dB, joined
    over gaps shorter than 150 ms."""
    top = max(level)
    runs, start = [], None
    for i, value in enumerate(level):
        if value > top - depth and start is None:
            start = i
        elif value <= top - depth and start is not None:
            runs.append([start, i])
            start = None
    if start is not None:
        runs.append([start, len(level)])
    joined = []
    for run in runs:
        if joined and run[0] - joined[-1][1] < gap_frames:
            joined[-1][1] = run[1]
        else:
            joined.append(run)
    return joined


def cut_points(level, first, last, limit, hop=220, floor=0):
    """Samples where a sound starts (its attack, less 2 ms) and ends (60 dB under its peak, or the
    limit frame, whichever comes first). A quiet take's background can stay within 50 dB of its
    peak, so the start never goes back past the floor frame (the end of the sound before it); when
    it would, the sound starts in the dip just before its attack."""
    peak = max(level[first:last])
    begin = first
    while begin > floor and level[begin - 1] > peak - 50:
        begin -= 1
    if begin == floor and floor > 0:
        begin = first
        while begin > floor and level[begin - 1] < level[begin]:
            begin -= 1
    end = last
    while end < limit and level[end] > peak - 60:
        end += 1
    return max(0, begin * hop - 88), min(limit, end + 4) * hop


def write_wav(path, samples, channels):
    raw = samples.tobytes()
    ffmpeg(
        "-f",
        "s16le",
        "-ar",
        str(RATE),
        "-ac",
        str(channels),
        "-i",
        "-",
        "-c:a",
        "pcm_s16le",
        "-y",
        str(path),
        data=raw,
    )


def shaped(samples, channels, fade_in_ms=2.0, fade_out_ms=15.0, peak_db=-3.0):
    """Faded at both ends and scaled to the peak."""
    out = array.array("h", samples)
    frames = len(out) // channels
    fade_in, fade_out = int(RATE * fade_in_ms / 1000), int(RATE * fade_out_ms / 1000)
    peak = max(1, max(abs(v) for v in out))
    gain = 32767 * 10 ** (peak_db / 20) / peak
    for f in range(frames):
        g = gain
        if f < fade_in:
            g *= f / fade_in
        if f >= frames - fade_out:
            g *= (frames - f) / fade_out
        for c in range(channels):
            i = f * channels + c
            out[i] = max(-32768, min(32767, round(out[i] * g)))
    return out


def loudness(path):
    """Integrated loudness (LUFS) and true peak (dBTP) by ffmpeg's EBU R128 meter."""
    report = subprocess.run(
        [
            "ffmpeg",
            "-nostats",
            "-v",
            "info",
            "-i",
            str(path),
            "-af",
            "ebur128=peak=true",
            "-f",
            "null",
            "-",
        ],
        capture_output=True,
        text=True,
        check=False,
    ).stderr
    lufs = peak = None
    section = ""
    for line in report.splitlines():
        line = line.strip()
        if line.startswith("Integrated loudness"):
            section = "I"
        elif line.startswith("True peak"):
            section = "TP"
        elif line.startswith("I:") and section == "I":
            lufs = float(line.split()[1])
        elif line.startswith("Peak:") and section == "TP":
            peak = float(line.split()[1])
    return lufs, peak


def to_ogg(source, out, gain_db, filters=""):
    chain = f"volume={gain_db:.2f}dB" + (("," + filters) if filters else "")
    ffmpeg(
        "-i",
        str(source),
        "-af",
        chain,
        "-ar",
        str(RATE),
        "-ac",
        "2",
        "-c:a",
        "libvorbis",
        "-q:a",
        "6",
        "-y",
        str(out),
    )


def make_effect(entry, name, source, folder, first_index):
    """Clips of one effect take: its separate sounds, or the whole of it."""
    mono = decode(source, 1)
    level = envelope(mono)
    found = events(level)
    if entry.get("whole") or entry.get("kind") == "sign":
        found = [[found[0][0], found[-1][1]]]
    keep = entry.get("events")
    clips = []
    for k, (first, last) in enumerate(found):
        if keep is not None and k not in keep:
            continue
        limit = found[k + 1][0] - 2 if k + 1 < len(found) else len(level)
        a, b = cut_points(
            level, first, last, limit, floor=found[k - 1][1] if k > 0 else 0
        )
        if b - a < int(0.04 * RATE):
            continue
        fade_out = 15.0
        if entry.get("max") and b - a > int(entry["max"] * RATE):
            b = a + int(entry["max"] * RATE)
            fade_out = 200.0
        out = folder / f"{name}-{first_index + len(clips) + 1}.wav"
        write_wav(out, shaped(mono[a:b], 1, fade_out_ms=fade_out), 1)
        clips.append(out)
    return clips


def make_sign(entry, name, source, folder, first_index):
    stereo = decode(source, 2)
    mono = decode(source, 1)
    level = envelope(mono)
    found = events(level, depth=45)
    a, b = cut_points(level, found[0][0], found[-1][1], len(level))
    piece, fade_out = stereo[2 * a : 2 * b], 40.0
    chain = sign_filters(entry)
    if chain:
        raw = ffmpeg(
            "-f",
            "s16le",
            "-ar",
            str(RATE),
            "-ac",
            "2",
            "-i",
            "-",
            "-af",
            chain,
            "-ar",
            str(RATE),
            "-ac",
            "2",
            "-f",
            "s16le",
            "-",
            data=piece.tobytes(),
        )
        piece = array.array("h", raw)
    if entry.get("max") and len(piece) > 2 * int(entry["max"] * RATE):
        piece, fade_out = piece[: 2 * int(entry["max"] * RATE)], 350.0
    out = folder / f"{name}-{first_index + 1}.wav"
    write_wav(out, shaped(piece, 2, 2.0, fade_out), 2)
    return [out]


def sign_filters(entry):
    """The ffmpeg filters of a sign's "semitones" (the pitch moved, the time kept) and "lowpass"."""
    parts = []
    if entry.get("semitones"):
        factor = 2 ** (entry["semitones"] / 12)
        parts += [
            f"asetrate={RATE * factor:.1f}",
            f"aresample={RATE}",
            f"atempo={1 / factor:.6f}",
        ]
    if entry.get("lowpass"):
        parts.append(f"lowpass=f={entry['lowpass']}")
    return ",".join(parts)


def make_loop(entry, name, source, folder, first_index):
    lufs, _ = loudness(source)
    out = folder / f"{name}-{first_index + 1}.ogg"
    to_ogg(
        source, out, entry.get("lufs", -20.0) - (lufs if lufs is not None else -20.0)
    )
    return [out]


def make_music(entry, name, source, folder, first_index):
    mono = decode(source, 1)
    level = envelope(mono)
    begin = next((i for i, v in enumerate(level) if v > max(level) - 45), 0)
    start_s = max(0.0, begin * 220 / RATE - 0.01)
    seconds = len(mono) / RATE - start_s
    trimmed = folder / f".{name}-{first_index + 1}.wav"
    filters = f"atrim=start={start_s:.3f},asetpts=PTS-STARTPTS"
    if not entry.get("ends", True):
        filters += f",afade=t=out:st={max(0.0, seconds - 3):.3f}:d=3"
    ffmpeg(
        "-i",
        str(source),
        "-af",
        filters,
        "-ar",
        str(RATE),
        "-ac",
        "2",
        "-c:a",
        "pcm_s16le",
        "-y",
        str(trimmed),
    )
    lufs, peak = loudness(trimmed)
    gain = entry.get("lufs", -16.0) - (lufs if lufs is not None else -16.0)
    if peak is not None and peak + gain > -1.0:
        gain = -1.0 - peak
    out = folder / f"{name}-{first_index + 1}.ogg"
    to_ogg(trimmed, out, gain)
    trimmed.unlink()
    return [out]


def derive(entry, name, made, folder):
    """Clips made by script from another entry's clips."""
    source = made[entry["from"]]
    clips = []
    semitones = entry.get("semitones", 0)
    lowpass = entry.get("lowpass")
    pair = entry.get("pair")
    for k, clip in enumerate(source[: entry.get("count", len(source))]):
        out = folder / f"{name}-{k + 1}.wav"
        rate = RATE * 2 ** (semitones / 12)
        chain = f"asetrate={rate:.1f},aresample={RATE}"
        if lowpass:
            chain += f",lowpass=f={lowpass}"
        if pair is not None:
            # The clip, 90 ms later the next clip moved by "pair" semitones.
            other = source[(k + 1) % len(source)]
            rate2 = RATE * 2 ** ((semitones + pair) / 12)
            ffmpeg(
                "-i",
                str(clip),
                "-i",
                str(other),
                "-filter_complex",
                f"[0]asetrate={rate:.1f},aresample={RATE}[a];"
                f"[1]asetrate={rate2:.1f},aresample={RATE},adelay=90[b];"
                "[a][b]amix=inputs=2:duration=longest:normalize=0",
                "-ac",
                "1",
                "-c:a",
                "pcm_s16le",
                "-y",
                str(out),
            )
        else:
            ffmpeg(
                "-i",
                str(clip),
                "-af",
                chain,
                "-ac",
                "1",
                "-c:a",
                "pcm_s16le",
                "-y",
                str(out),
            )
        clips.append(out)
    return clips


MAKERS = {
    "effect": make_effect,
    "sign": make_sign,
    "loop": make_loop,
    "music": make_music,
}


def main():
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "--force", action="store_true", help="fetch and cut every take again"
    )
    args = parser.parse_args()
    api = os.environ.get("STUDIO_URL", "http://127.0.0.1:9000").rstrip("/")
    spec = json.loads((game / "sounds.json").read_text())
    audio.mkdir(parents=True, exist_ok=True)
    cache = audio / ".takes"
    cache.mkdir(exist_ok=True)
    before = {}
    if (audio / "manifest.json").exists() and not args.force:
        before = json.loads((audio / "manifest.json").read_text()).get("sounds", {})
    manifest = {"source": api, "sounds": {}}
    library = {"entries": []}
    made = {}
    # Entries made from others come after them.
    order = sorted(spec["sounds"].items(), key=lambda item: "from" in item[1])
    for name, entry in order:
        folder = audio / name
        record = {k: v for k, v in entry.items() if k not in ("comment",)}
        unchanged = before.get(name) == record and folder.is_dir()
        if unchanged:
            clips = sorted(p for p in folder.iterdir() if p.suffix in (".wav", ".ogg"))
        else:
            if folder.exists():
                shutil.rmtree(folder)
            folder.mkdir()
            if "from" in entry:
                clips = derive(entry, name.lower(), made, folder)
            else:
                clips = []
                for take in entry["takes"]:
                    source = cache / f"{take['id']}"
                    if not source.exists():
                        source.write_bytes(download(api, take["id"]))
                    maker = MAKERS[entry.get("kind", "effect")]
                    clips += maker(
                        {**entry, **take}, name.lower(), source, folder, len(clips)
                    )
            print(f"{name}: {len(clips)} clips")
        made[name] = clips
        manifest["sounds"][name] = record
        if not clips:
            continue
        library["entries"].append(
            {
                "name": name,
                "group": entry.get("group", "World"),
                "volume": entry.get("volume", 1.0),
                "pitch": entry.get("pitch", 0.0),
                "spread": entry.get("spread", 0.0),
                "limit": entry.get("limit", 2),
                "cooldown": entry.get("cooldown", 0.05),
                "priority": entry.get("priority", 1),
                "symbolic": entry.get("kind") == "sign" or entry.get("symbolic", False),
                "kind": entry.get("kind", "effect")
                if "from" not in entry
                else "effect",
                "clips": [str(p.relative_to(audio)) for p in clips],
            }
        )
    (audio / "library.json").write_text(json.dumps(library, indent=1) + "\n")
    (audio / "manifest.json").write_text(json.dumps(manifest, indent=1) + "\n")
    print(f"{len(library['entries'])} sounds in {audio}")


if __name__ == "__main__":
    main()
