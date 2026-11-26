---
license: apache-2.0
library_name: pytorch
pipeline_tag: image-to-3d
tags:
- 3d
- head
- face
- avatar
- dinov2
- mhr
- soma
- synthetic-data
---

# frommhr

A reader for two pictures of a bald head, the front and the left profile, on
a plain backdrop. It reads the head's shape as the 20 head coefficients of
Meta's [Momentum Human Rig](https://github.com/facebookresearch/MHR) (MHR),
and its colour as 14 pigment sliders: skin, cheeks, nose, under the eyes,
lips, brows, irises and stubble. It was made for building 3D game characters
from concept pictures, and was trained only on synthetic renders of random
MHR people on NVIDIA's [SOMA](https://huggingface.co/nvidia/soma-x) mesh.

![Generated front pictures, the people built from them, and their mesh](example.jpg)

*Left, a generated front picture (not a photo of a real person); the reader
also got the head's left profile. Middle, the person the Assets Studio
server builds from the reader's output, the head's colour projected from the
pictures. Right, the same mesh without colour: SOMA's triangles, the head
shaped by the reader's 20 coefficients. The children (the three bottom rows)
get the closest adult head: see Limitations.*

Code for the data, training, evaluation and export:
[kvladsrc/gym/frommhr](https://github.com/kvladsrc/gym/tree/main/frommhr) on
GitHub. The [Assets Studio](https://github.com/kvladsrc/gym/tree/main/assets_studio)
server `frommhr` puts the head on a rigged, textured SOMA person.

## Files

| File | Holds |
| --- | --- |
| `frommhr.safetensors` | the reader's weights and what they need: MHR's identity on SOMA's mesh, SOMA's triangles, the colour tables, the feature masks |
| `predict.py` | reads two pictures and prints the coefficients and pigments as JSON |
| `example.jpg` | the picture above |

The package is one safetensors file: the tensors under dotted keys and
everything else as JSON in its metadata, where each tensor stands as
`{"$tensor": key}`. Loading it runs no code; `predict.py` shows how.

## Use

```sh
pip install torch timm safetensors mediapipe numpy pillow huggingface_hub
python predict.py front.png profile.png --obj head.obj
```

```json
{
 "mhr_head": [1.9799, 0.4323, -0.9529, ...],
 "pigments": {"melanin": 0.10335, "eu": 0.16995, "blood": 0.02432, ...},
 "crops_px": {"front": {...}, "left": {...}}
}
```

- `mhr_head` are MHR's identity coefficients 20–39, the head. The body's
  coefficients 0–19 and the hands' 40–44 are not read.
- `pigments`: the skin's melanin (volume fraction in the epidermis), its
  eumelanin share (`eu`) and the dermis's blood; factors on them for the
  cheeks, the nose and under the eyes; the lips' melanin and blood; the
  brows' and the irises' eumelanin and pheomelanin; and `stubble`, the share
  of the beard zone covered by hair of the brows' colour.
- `--obj` writes the head on MHR's mean body as SOMA's mesh: metres, Y up,
  the face towards +Z, no eyeballs.

MediaPipe's Face Landmarker (downloaded once into `~/.cache/frommhr`) finds
the eye line, and the backdrop the crown. Each picture is framed to its own
head as the training renders were, read under nine slightly shifted and
scaled crops, and the outputs are averaged. A profile that looks to the
picture's right is mirrored. About ten seconds on a CPU. Tested with Python
3.12, PyTorch 2.15, timm 1.0.30 and MediaPipe 1.2.3.

The pictures need:

- a bald head, no hair, hat or glasses, the whole head in the picture with
  some backdrop above the crown;
- a plain, even backdrop;
- a neutral face with open eyes;
- the front looking straight at the camera, the profile at 90°;
- little perspective: the training renders are orthographic, and a close
  phone camera's perspective is read as shape.

## Evaluation

On 256 random MHR people from a seed the training never used, rendered as in
training. The head skin's error is the mean distance of its vertices to the
truth, rebuilt with the true body, both heads centred and scaled to an
adult's head size.

| Head skin's error, mm | Mean | Median | 90th percentile |
| --- | --- | --- | --- |
| frommhr | 0.266 | 0.254 | 0.358 |
| MHR's mean head on the true body | 4.528 | 4.344 | 6.226 |
| One average head for everybody | 4.518 | 4.290 | 6.098 |

R² of the 20 head coefficients: 0.97–1.00.

| Colour difference, CIE76 | Skin | Cheeks | Nose | Under the eyes | Lips | Brows | Irises |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Mean | 1.39 | 2.31 | 1.87 | 2.09 | 1.56 | 2.27 | 1.39 |
| 90th percentile | 2.51 | 4.01 | 3.21 | 3.84 | 2.84 | 5.13 | 2.72 |

The median error of melanin is 6.5% (log 0.063), of blood 14.3% (log 0.134);
the mean error of the stubble share is 0.026. These are `evaluate.py`'s
numbers on the CPU; the random people depend on the device, and 256 others
drawn on a GPU gave a mean head error of 0.257 mm.

These numbers measure the reader on its own kind of picture. Real or
generated pictures have no ground truth here, and the reader will do worse
on them.

## Training

Each step draws random people: body and head coefficients from N(0, 1)
clipped at 2.5, the sliders from a prior of plausible skin and features,
random light and a grey backdrop. It renders them orthographically from the
front and the left, turned a little, with random shifts, scales, blur,
gamma and noise.

The network is DINOv2 ViT-S/14 (timm's `vit_small_patch14_dinov2.lvd142m`)
at 518 px on both pictures, a small CNN on the front picture's eye strip,
and an MLP to the 20 coefficients and 14 slider logits, 25 M parameters. The
loss rebuilds the head's skin from the predicted coefficients and measures
it in mm, plus the coefficients' and sliders' squared errors and the CIE76
difference of the colours.

It trained for 6.8 hours on an RTX 3070 Laptop GPU: 6 502 steps of 32
people, learning rate 2e-5 with a cosine schedule, EMA weights. It started
from an earlier reader of ours, the same network trained on renders of
another body model (Anny), which is not published. The published code starts
from DINOv2's weights.

## Limitations

- **Adults only.** MHR's identity space was built from adult scans, so a
  child's picture gets the closest adult head.
- **Synthetic training data.** The reader has seen only one renderer, with
  its light, skin model and features. Photos and generated pictures differ in
  ways it never saw.
- **Bald heads on plain backdrops.** Hair, hats, beards longer than stubble,
  glasses, make-up, expressions and busy backgrounds are outside what it
  learnt.
- **A simple colour model.** Fourteen pigment sliders; no freckles, scars,
  tattoos or wrinkles, and the colour of the skin under a light it guessed.
- **Not for identifying people.** It reads a head for a 3D character; it was
  not made or tested to identify or verify anyone.

## Licences and attribution

The weights and the package are Apache-2.0. They are built on:

- [SOMA-X](https://huggingface.co/nvidia/soma-x) v0.3.3 (NVIDIA,
  Apache-2.0): SOMA's mesh, its rig and its MHR and Anny correspondences;
- [MHR](https://github.com/facebookresearch/MHR) (Meta, Apache-2.0): the
  identity space, carried onto SOMA's mesh;
- [DINOv2](https://github.com/facebookresearch/dinov2) (Meta, Apache-2.0),
  through [timm](https://github.com/huggingface/pytorch-image-models): the
  backbone's starting weights;
- [Anny](https://github.com/naver/anny) (NAVER LABS Europe, Apache-2.0),
  with MPFB2's lip, eyelid and ear masks (CC0): the feature masks;
- Scott Prahl's tabulation of haemoglobin's molar extinction
  ([omlc.org](https://omlc.org/spectra/hemoglobin/summary.html)), from W. B.
  Gratzer and N. Kollias: the blood's colour.

`predict.py` uses MediaPipe's
[Face Landmarker](https://ai.google.dev/edge/mediapipe/solutions/vision/face_landmarker)
(Google, Apache-2.0).
