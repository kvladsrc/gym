# SAM 3.1: a subject or a concept in video

`video-to-video`, port 9148, with Meta's
[SAM 3.1](https://huggingface.co/facebook/sam3.1) (Object Multiplex, March
2026). The result keeps the original pixels of what was chosen on white, at
the video's size, frame count and rate, without audio. Input: `video`, an MP4
of up to 10 seconds, 1080p and 60 fps. What stays is chosen in one of three
ways:

- `reference` only: as with [`sam2`](../sam2/README.md), the first frame with
  only the subject on white or transparent, in the original framing and pose.
  Its box and interior points prompt the tracker on the original first frame.
- prompt only: a short English noun phrase such as `person`, `wolf` or
  `yellow robot`. Every instance it names is found in every frame and kept.
  Prompts in other languages are refused: SAM 3.1 finds nothing with them.
- both: the phrase finds its instances on the first frame and the one that
  overlaps the reference subject most is kept.

`require_full_subject` (on by default, for mocap) refuses a reference subject
shorter than a quarter of the frame and a result that touches the frame edge.

```sh
just setup     # the environment (Python 3.12)
just download  # the checkpoint, ~3.5 GB; needs HF_TOKEN
just doctor
just serve     # http://127.0.0.1:9148
just test      # no GPU or weights needed
```

The checkpoint is in a gated repository: accept the licence on its Hugging
Face page and set `HF_TOKEN` (or `HUGGINGFACE_TOKEN`).

## How it runs

- Meta's `sam3` code at a pinned commit; there is no Transformers port of
  SAM 3.1. The package does not declare some of its imports (`einops`,
  `psutil`, `pycocotools`, and `pkg_resources` from setuptools below 81); the
  environment adds them.
- FlashAttention 3 runs on Hopper GPUs only, so the server builds the model
  with PyTorch's attention.
- Frames go to the model as a folder of JPEGs (quality 95): SAM 3 keeps those
  as float16, but a video file as float32, twice the memory. They stay in
  system memory, about 6 MB per frame. The result is composed from the
  decoded original frames, not the JPEGs.
- Text prompts run SAM 3.1's detector on every frame, so instances that
  enter later are kept too. With a reference only, the detector has no
  phrase and the tracker follows the one subject.
- Upstream's video predictor is adapted (`adapt` in the server):
  - its `start_session` passes an argument the SAM 3.1 model does not take
    ([sam3#544](https://github.com/facebookresearch/sam3/issues/544));
  - the detector and postprocessing take one frame at a time instead of 16;
  - point prompts only refine a text prompt's tracks upstream, so a subject
    given by points alone vanished after the first frame; frames without a
    detector pass now start empty;
  - the tracker kept every tracked frame's memory on the GPU, 12.7 MB a frame,
    and a 10-second clip ran out of memory. It reads back at most 16 frames, so
    frames more than 32 behind are dropped; the masks stay byte for byte the
    same. Upstream's own trimming fails on this model.
- After loading, the CPU copies of the checkpoint are returned to the system:
  the process holds about 1 GB instead of 7.4 GB.

## Measured

On an 8 GB RTX 3070 Laptop GPU:

- Loading: 38–48 s, 3.9 GB of GPU memory.
- About 1.7 frames a second with or without a prompt: 124 frames (1344×768)
  in 75 s, 144 frames (720p) in 85 s, 300 frames (720p, 10 s) in 178 s.
- At most 7.0 GB of GPU memory, level from about the 60th frame on; the
  process holds about 2.4 GB of system memory during a job.
- The same request gives the same file, byte for byte.
- Against [`sam2`](../sam2/README.md) (SAM 2.1 small) with the same reference
  frames:
  - a wolf running in side view, 124 frames: the masks agree at IoU 0.95 on
    average (0.91 at worst). On a contact sheet SAM 3.1 keeps the far legs as
    they come into view; SAM 2.1 leaves them out.
  - a toy robot picking up a shield, 144 frames: IoU 0.50. SAM 2.1 adds the
    shield to the robot; SAM 3.1 keeps only the robot's visible parts.
  - SAM 2.1 small takes 22–28 s and 0.9 GB of GPU memory for the same clips.
- The studio's contract check: 6 of 7 checks pass. Its generation fails as it
  should: the sample prompt is Russian, and the 32-pixel sample video shows
  nothing to name.

License: the SAM License (Meta): use, change and distribution are allowed,
without military, weapons or ITAR uses and subject to trade controls.
