# ADR-003. Contract 1.1: text, video and primary parameters

<!-- markdownlint-disable MD013 -->

Status: accepted (M7; review: docs/reviews/M7-adr-0003.md).

## Context

The user added modalities: LLM text, first-frame video and text-to-3D.
ADR-001 knows three file kinds (image, audio, 3D) and hides every parameter
under “Advanced”, even essential ones such as duration and edit strength.
Studio also stores audio only as WAV, preventing FLAC, OGG or MP3 uploads.

## Decision

This is **backward-compatible**: `/v1/info` retains `contract: 1`; old servers
and Studio work unchanged. Additions are extra values and an optional marker.
“1.1” is a document revision, not a wire-protocol value.

### New tasks

Add known `task` values:

| Task | Input | Prompt | Output |
| --- | --- | --- | --- |
| `text-to-text` | Optional `text` to edit | Required | `text/plain` |
| `image-to-video` | `image` (first frame) | Server-defined | `video/mp4` |
| `text-to-3d` | — | Required | `model/gltf-binary` |

`text-to-text` with a `text` input is X-to-X, supporting evolution just like
image-to-image.

### New file kinds

| Kind | Canonical format | Requirements |
| --- | --- | --- |
| Text | `text/plain` | UTF-8 without BOM; `\n` line endings |
| Video | `video/mp4` | H.264 (profile at most High), `yuv420p`, tagged BT.709, `+faststart`; AAC or no audio |

Canonical video targets browser playback without transcoding. Color must be
BT.709 (matrix, primaries, transfer function, limited range), **tagged** in
file: browsers treat untagged HD as BT.709, but ffmpeg defaults to BT.601 for
RGB → YUV, which would shift reds and greens. Encoding options are shared by
SDK and Studio (`model_server_sdk.video.canonical_h264_options`). As before,
inputs accepting a kind **must accept its canonical format**, and video-output
servers must encode it themselves.

Signature checks (`media.looks_like` in SDK):

- `text/plain`: nonempty UTF-8 without C0, DEL or C1 controls, except tabs
  and line breaks.
- `video/mp4`: `ftyp` box with a primary video brand (`isom`, `iso2`–`iso6`,
  `mp41`, `mp42`, `avc1`, `M4V`, `dash`…). Audio (`M4A`, `M4B`), images
  (`heic`, `avif`), 3GP and QuickTime share the structure but are not `video/mp4`.
- `video/webm`: EBML with DocType `webm`, not Matroska `.mkv`.

Audio signatures are strict too (ID3 header, first Ogg page, FLAC STREAMINFO):
text starting with “ID3” or “OggS” remains text. Studio validates server outputs
against the **declared** type rather than the first matching type.

Video servers encode reproducibly (`-fflags +bitexact`, no `creation_time`),
since determinism checks compare bytes. `model_server_sdk.check` verifies
video layout (`moov` before `mdat`, `avc1` track) and text without BOM or `\r`.

### Primary parameters

Parameter schema properties may carry `"x-primary": true`. Studio displays
these directly below the prompt and others under “Advanced”. The marker
changes neither validation nor server behavior. Old servers work as before
(all under “Advanced”). SDK: `Field(..., json_schema_extra=PRIMARY)`.

Schema keys starting with `x-` are allowed without affecting ADR-001's flat
scalar rules. Studio does not yet read other `x-` keys.

### Import into Studio

The library stores only formats browsers can display:

- **Audio:** as uploaded (WAV, FLAC, OGG, MP3); converted to WAV on submission
  (16-bit PCM, original rate and channel count).
- **Video:** converted at import to canonical MP4 (H.264 High, CRF 18, `yuv420p`,
  BT.709, dimensions padded to even) unless already compliant: WebM, HEVC,
  10-bit or trailing `moov` require conversion. Compliance uses box layout
  and `ffprobe` (codec, profile, pixel format). Files without video tracks
  are rejected; QuickTime `.mov` is not accepted. Convert at import rather
  than dispatch so slow conversion does not block the queue. Untagged video
  is treated as BT.709 above 576 lines, otherwise BT.601, matching players.
  HDR (BT.2020, PQ/HLG) is not tone-mapped and appears washed out afterward.
- **Text:** strip BOM and normalize line endings to `\n`. Detect text last:
  WAV headers with null bytes are also valid UTF-8.

Generation file pickers offer every stored format of a kind, not just the
server's declared canonical type.

Audio/video use system `ffmpeg`/`ffprobe` (Studio has no numpy or codecs).
Explicitly set container format (`-f`) and allow only local-file reads
(`-protocol_whitelist file`), preventing content-selected demuxers such as
playlists from reading unrelated files. Without `ffmpeg`, video import and
noncanonical audio submission fail clearly; everything else works.

### Fake server and contract check

The fake server declares new tasks and returns deterministic text and tiny
MP4s (pre-encoded clips with seed in a player-ignored `free` box).
`model_server_sdk.check` gains text/video sample inputs.

## Consequences

- UI: copyable text preview, video player/thumbnail, primary parameters above
  “Advanced”.
- M0 servers mark key parameters `x-primary`: SDXL/FLUX size and strength,
  Stable Audio duration and strength, XTTS language and voice. TripoSR has
  only technical parameters, so none are marked.
- Storage adds `text` and `video` asset kinds. SQLite cannot modify the `assets`
  kind CHECK, so migration 0003 recreates the table. The migration runner
  temporarily disables foreign keys (otherwise dropping the table would
  cascade-delete tags), then runs `foreign_key_check` before commit.

## Alternatives

- **Contract version 2:** nothing breaks, so no new version is needed.
- **Parameter order instead of a marker** (first N are primary): implicit and
  fragile when parameters are added.
- **Canonical WebM:** less widely supported outside browsers (engines, editors);
  H.264 MP4 plays everywhere.
