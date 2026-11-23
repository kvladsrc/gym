# Audio2Face-3D: a face that speaks

Port 9138. One task, `audio-to-motion`: an `audio` (WAV, speech in any
language, 0.25 s to 5 minutes) → a glTF (GLB) of the head of one of NVIDIA's
sample actors, animated by the speech: the skin through the 52 ARKit
blendshapes, the tongue through 16 more, the lower jaw and the eyes. The
library lists the result under Animations.

```sh
just setup     # pinned SDK source, the environment, the SDK and its runner (CUDA)
just download  # networks and heads (~1.8 GB)
just doctor
just serve     # http://127.0.0.1:9138
just test      # no GPU or weights needed
```

## Models

`model` picks the network and the actor:

| `model` | Network | Hugging Face revision |
| --- | --- | --- |
| `v3.0-claire` (default), `v3.0-james`, `v3.0-mark` | v3.0, diffusion, one network for the three actors | `nvidia/Audio2Face-3D-v3.0` `b74132732fd9` |
| `v2.3.1-claire` | v2.3.1, regression, this actor only | `nvidia/Audio2Face-3D-v2.3.1-Claire` `a46eafb067ad` |
| `v2.3.1-james` | v2.3.1, regression, this actor only | `nvidia/Audio2Face-3D-v2.3.1-James` `327d000d9f76` |
| `v2.3-mark` | v2.3, regression, this actor only | `nvidia/Audio2Face-3D-v2.3-Mark` `5451728e0737` |

v3.0 reads one-second windows and carries its state from window to window;
the regression networks look at about half a second around each frame. The
networks read sound, not text, so the language does not matter to them, but
they were trained on English speech of these actors.

Ten emotions, each 0 to 1 (amazement, anger, cheekiness, disgust, fear,
grief, joy, out of breath, pain, sadness), hold for the whole clip; all at 0
is neutral speech. NVIDIA's Audio2Emotion, which guesses the emotion from the
voice, is not used: its licence allows it only inside NVIDIA's Audio2Face
project and forbids emotion recognition.

## The file

- glTF 2.0, binary. Metres, Y up, the face looks along +Z.
- Nodes: `head` → `skin`, `upper_teeth`, `jaw` (→ `lower_teeth`, `tongue`),
  `right_eye`, `left_eye`. The eye nodes sit at the eyes' pivots and turn
  the eyeballs.
- The skin's morph targets carry the ARKit names, the tongue's its own 16, in
  each mesh's `extras.targetNames`; targets are sparse.
- One animation, `speech`, linear, one key per frame: the weights of the skin
  and the tongue, the jaw's translation and rotation, the eyes' rotations.
- The meshes are NVIDIA's (the Maya-ACE sample project): the skin and the
  tongue in the neutral pose of the blendshapes the networks were trained
  with, the teeth as the scenes have them. The eyeballs get sclera, iris and
  pupil as vertex colours; the lenses and corneas are left out. The colours
  are plain materials, not the actors' textures.
- The result's metadata names the model, the frames, the frame rate, the
  duration, the emotions used and the blendshape names.

## How it runs

- Each job runs NVIDIA's Audio2Face-3D SDK, built from a pinned revision with
  a small runner (`native/a2f_runner.cpp`), in its own process: the network,
  then upstream's post-processing (smoothing, strengths, eye saccades) and
  the blendshape solve, set up from the model's own `model.json` as upstream's
  sample does. The server's own process never touches CUDA.
- The networks are published as ONNX. The first time a network is used, a
  child process builds a TensorRT engine for this GPU in float32, as
  upstream's scripts build it, and stamps it with the TensorRT version, the
  GPU, the revision and the batch sizes; later loads reuse it.
- The sound is mixed to mono and resampled to 16 kHz (soxr, very high
  quality).
- The diffusion noise comes from a fixed seed, so a job has one result.

## Measured

On an 8 GB RTX 3070 Laptop GPU, with spoken lines of 5 to 7 s:

- The first use of a network builds its engine: 32 s for v3.0, 52 s for
  v2.3.1.
- A line takes about 4 s with v3.0 and 4.5 to 5.5 s with v2.3.1, at most
  about 1.3 GB of GPU memory.
- The same request gives the same file, byte for byte.
- At 60 fps a line of 5 to 7 s makes a file of 20 to 36 MB.

## Limitations

- The head is NVIDIA's actor, not your character. The blendshape weights
  carry over to any head with the 52 ARKit shapes, the jaw and the eyes to a
  rig with those joints; nothing here does that transfer.
- The face only: no head, neck or shoulder motion, no blinking beyond what
  the networks give.
- One emotion mix per clip.
- The library shows the file as a download, without a preview.

License: the networks are under the NVIDIA Open Model License (their cards
say they are ready for commercial and non-commercial use); the SDK and the
Maya-ACE heads are MIT. The SDK downloads its own dependencies when it is
built. Check which terms apply to you.
