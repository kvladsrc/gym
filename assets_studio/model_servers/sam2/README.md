# SAM 2.1: a selected character in video

`video-to-video`, port 9125. Inputs: `video`, the original MP4 up to 10 seconds,
1080p/60 fps; `reference`, the first frame with only the target character on a
white or transparent background. Reference dimensions may differ, but framing
and pose must be preserved.

```sh
just setup
just serve
just test
```

Model: `facebook/sam2.1-hiera-small`, Apache-2.0, pinned revision
`ee5bba1d82bb8749febdf90f45e84b687142ba03`. Weights: about 176 MiB.
Readiness and metadata are available immediately; weights load on the first
valid job in the SDK worker thread. The environment is isolated from other servers.

## Mocap workflow

1. Cut a continuous segment with visible body, feet and hands.
2. First frame → Qwen image-to-image: keep the target person on white,
   preserving pose, scale and placement. Start with `size=1K, steps=10`.
3. Original segment + Qwen output → SAM 2 → one person on white.
4. Result → MoCapAnything (`background_removed=true`) and GVHMR. For a moving
   camera, pass the synchronized original into GVHMR's `camera_video`.

The editor runs once per segment. Its output supplies a body box and interior
points. SAM then re-segments the **original** and propagates the selection
across frames. Generated anatomy, clothing, sword and movement are not copied.
Output preserves original coordinates, dimensions, frame count and frame rate.
Before H.264 compression, colored pixels come from the source video. Audio is removed.

In Studio Flow this is an ordinary two-input node, with no SDK changes.
Add the server from `studio.example.toml`; the launcher knows the `sam2` recipe.

## Limitations

The first frame must identify the person unambiguously. If the editor scaled,
moved or substantially changed their pose, use another reference. Aspect-ratio
checks detect only some such errors. The tracker cannot reconstruct hidden
limbs, may lose a thin sword, or select the wrong object during close contact.
Review the cleaned video before judging mocap quality; a successful job does
not prove correct segmentation. An empty track fails with its frame number.

`require_full_subject=true` is enabled by default: the reference body must
occupy at least a quarter of the image height, and the track must not be clipped
at the frame edge. A thin sword tip does not count as a clipped body. Other
uses can disable this check; it does not replace review or catch every pose error.

The API follows ordinary contract v1. A real example is in
`workspace/sword_prof/flow.json` and `manifest.json`.

API sources: [Transformers SAM2 Video](https://huggingface.co/docs/transformers/model_doc/sam2_video)
and [Meta's original model](https://github.com/facebookresearch/sam2).
