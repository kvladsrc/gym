# ViTPose++: the joints of a character in a video

Port 9134. One task, `video-to-pose-observations`: a `video` (MP4, up to 10 s,
1080p, 60 fps) → the 17 COCO joints of one character in every frame, as the
studio's 2D pose observations (`studio.pose-observations.v1`). The library
shows the result under Animations.

```sh
just setup
just download
just serve
just test
```

Model: `usyd-community/vitpose-plus-huge`, Apache-2.0, pinned revision
`9f36d7aec1800d23e97f10c2e74393aee92aa53f`; weights about 3.6 GB, about 3.5 GB
of VRAM, about 0.15 s per frame.

## Finding the joints

ViTPose++ is a top-down pose estimator: it looks at one subject inside a box and
answers with the 17 COCO joints (nose, eyes, ears, shoulders, elbows, wrists,
hips, knees, ankles). There is no person detector here, because detectors
trained on photographs miss drawn and small characters while the pose model
itself reads them well. The box comes from one of three places:

1. `subject_region`, "left,top,right,bottom" in shares of the frame (0–1), names
   the character in the first frame. Use it for gameplay recordings and any busy
   frame.
2. With no region, a character on a plain background is found by itself.
3. Otherwise the whole frame is the box, which suits a video already cut to the
   character.

`follow` (on by default) moves the box with the character's torso from frame to
frame; the box keeps its size, so it should hold the whole character through the
movement. Every frame is evaluated as the model was: the box widened to 3:4 with
a margin, the mirrored picture averaged in, heatmaps decoded to sub-pixel
positions.

The result's metadata reports how the subject was chosen, its box, how far the
box travelled, the mean score and the number of unsure joints (score under 0.3).

## The file

One line of JSON in `text/plain`, the format GVHMR's task of the same name
writes and PromptHMR's `observations` input reads:

- `fps`, `frames`, `size` ([width, height]) and `source_sha256` of the video;
- `kp2d`: per frame, [x, y, score] for each of the 17 joints in COCO order.
  Positions are in the video's pixels, x to the right and y down, with pixel
  centres at whole numbers;
- `bbx_xyxy`: the subject's box per frame, [left, top, right, bottom];
- `K_fullimg`: a 3×3 camera per frame. Nothing is known about the camera, so it
  is the one GVHMR assumes: the focal length is the picture's diagonal, the
  principal point its centre (`intrinsics_source` says so);
- `tracking`: how the subject was chosen, whether the box followed it and how
  far it travelled; `detector` and `revision` name the model.

The file has every field PromptHMR's `observations` input checks; PromptHMR
also wants the same video at 30 fps and at most 300 frames. The two have not
been run together.

## Limitations

One character per job. The box follows a moving character one frame late, so a
character that crosses its own height in a couple of frames needs a wider
region. Hands, feet and the face have no joints: ankles are the last joints of
the legs, and on a character in boots they sit at the boot tops. Animals and
other non-human bodies are outside this head of the model.

The model answers each frame on its own. In a side view it names the nearer leg
"left" whichever leg it is, so like limbs can change names from frame to frame,
and a limb that passes behind its pair is found on the visible one. The joints
tremble from frame to frame. A successful job does not prove the joints are
right.

The API follows ordinary contract v1. Why this model: see
[docs/models/video-to-pose-observations.md](../../docs/models/video-to-pose-observations.md).
