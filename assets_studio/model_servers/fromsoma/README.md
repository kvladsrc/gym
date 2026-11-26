# fromsoma model server

The `image-to-3d` task makes a person on SOMA's mesh and skeleton from the
character's Anny parameters and two pictures of the bald head: the front
(`image`) and the left profile (`profile`). The parameters make the body: sex,
age in years, muscle, weight, height, proportions, origin mix, cup size and
firmness. The pictures give the face and its colours, in four steps:

1. MediaPipe's Face Landmarker finds the eye line, and the plain backdrop shows
   the crown. Each picture is framed to its own head, so the head's size and
   position in the picture do not matter. A profile that looks right is
   mirrored.
2. A reader trained on rendered Anny people reads the face: DINOv2 ViT-S/14
   looks at both pictures, a small CNN at the eye strip, and the given
   parameters are its extra input, as in its training. It reads the face's
   variation in MHR's head space (20 coefficients) and 14 pigment sliders. The
   sliders cover skin melanin, its eumelanin share and blood, the cheeks, nose
   and under-eye factors, lips, brows, irises and stubble.
3. SOMA-X carries Anny, with the given parameters and the face's variation,
   onto SOMA's mesh and 77-joint skeleton. The pigments are painted per vertex
   and baked into one texture.
4. With `texture=pictures`, the default, the head's UV tile takes the pictures'
   own colour. A camera is fitted to each picture against renders of the
   person's own head: the front to MediaPipe's landmarks on the picture and on
   the render, the profile to the visible eye, nose and lips and to the head's
   outline against the backdrop. Thin-plate splines carry the head's landmarks
   onto the pictures'. Each picture is divided by a blurred estimate of its
   lighting and projected onto the head; the profile is mirrored onto the right
   side as well. The views blend by their angle to the surface, the front
   preferred. The head's own eyes and mouth stay, and its ears are kept at the
   pictures' tone. Towards the back of the head and the neck the colour fades
   into the pigment texture over 35 mm, and the face's skin tone is pulled
   halfway to the pigments'. `texture=pigments` keeps the pigment texture
   alone.

```sh
just setup     # Python 3.12 + CPU PyTorch environment
just download  # SOMA-X's data, Face Landmarker, frommhr's reader
just doctor
just serve     # http://127.0.0.1:9142
just test      # no weights or data needed
```

- `fromsoma.safetensors` is not downloaded. It is the reader's training output,
  packed with the data it was trained with: MHR's head fields, the colour tables
  and Anny's feature masks. It goes in `FROMSOMA_ROOT`
  (`~/.cache/assets-studio/fromsoma`) beside `soma-x/` and
  `face_landmarker.task`. A package is one safetensors file: the tensors, and
  the rest as JSON in its metadata (`package.py`), so loading it runs no code.
- The pictures need a bald head on an even, plain backdrop, with some backdrop
  above the crown. The shoulders may reach the lower edges. In training the
  head was turned up to ±6° sideways, ±5° up or down and ±3° tilted, and the
  picture scaled by ±7 % and shifted by ±5 %. Three-quarter views and strong
  tilts lie outside that. The training pictures were orthographic, so a long
  portrait lens suits the reader; a wide-angle close-up does not. A picture
  without a face, or with the crown cut off, is refused.
- The pictures' colour needs colour pictures: the head's skin is found by its
  colour against the backdrop, so a grey picture is refused unless
  `texture=pigments`. Even, soft light carries over best; the broad light
  gradients are divided out, the finer shading stays in the colour. The
  pictures are padded to squares and scaled down to at most 1600 px.
- The parameters are Anny's, 0 to 1, with Anny's default of 0.5. The three
  origin shares are normalised to sum to 1. The age in years goes through
  Anny's own age mapping.
- The result is in SOMA's bind pose: metres, Y up, the face toward +Z. The
  skeleton is a root plus SOMA's 77 public joints, with SOMA's names. Each
  vertex keeps four weights: the four largest of SOMA's up to ten,
  renormalised. The eyeballs are separate meshes, `eye_left` and `eye_right`,
  bound to the eye joints and sized to the lid openings. One 4096 PNG texture
  holds SOMA's three UV tiles in three quarters and the eyes in the fourth.
  The materials set roughness only, with no metal.
- Everything runs on the CPU: about 10 s to load and 15 s per person with the
  pigments alone. The pictures' colour adds about 70 s, and the process peaks
  at about 6 GB of RAM. The output's metadata holds the phenotype, the MHR
  coefficients, the pigments, how each picture was framed and, under
  `texture`, the cameras' fits, the warps, the coverage and the timing.

Licences: SOMA-X, Anny, MHR, DINOv2 and MediaPipe's Face Landmarker are under
Apache-2.0, and the MPFB2 assets Anny uses are CC0. The reader was trained only
on renders of Anny and MHR people, starting from DINOv2's weights.

## frommhr: MHR's face space

`just serve-mhr` (<http://127.0.0.1:9143>) serves frommhr from the same Python package,
environment and root directory. It takes the same pictures and parameters and
returns the same kind of person, with the same body. The head differs:

- The reader was trained on renders of random MHR people. It has no extra
  inputs: it reads the head's 20 MHR coefficients and the 14 pigment sliders
  from the pictures alone, at 518 px. Stubble is kept only for men from 18
  years on.
- The body is Anny's, as fromsoma makes it; only the head is MHR's. Anny's
  body of the given parameters is put into SOMA's bind pose. MHR's 20 body
  coefficients and its bone scales, which hold the stature, are fitted to it by
  least squares, leaving out the head, hands, mouth and eye bags. The reader's
  head coefficients complete the rest shape, and SOMA-X rigs it as for
  fromsoma. SOMA-X's bind pose places a body by its own skeleton, so the legs'
  scales lift or lower the whole person; the server moves it to Anny's height
  and rigs it again.
- MHR's body space cannot reach Anny's bodies: the fit leaves 1-2 cm rms, with
  shoulders, upper arms and neck thicker and the shoulder joints 0.5-1 cm
  wider. So below the head the server takes Anny's body. The head's vertices
  (skinned mostly to Head and its children) stay MHR's, and Anny's body comes in
  over 6 cm below them, smoothly, on one mesh with nothing stitched. That shape
  is rigged once more, so the skeleton fits the body it moves. The rig moves
  the head as a whole by a few millimetres; its shape stays within about
  0.6 mm. The body stays within 2-4 mm rms of Anny's, the shoulder joints within
  2 mm.
- MHR's identity space was built from adult scans only. A child gets Anny's
  child body, but the head comes from the adult space: the closest adult head
  the reader finds, about adult size.

- `frommhr.safetensors` is published on Hugging Face,
  [zjgkkn/frommhr](https://huggingface.co/zjgkkn/frommhr), and `just download`
  fetches it. It holds the reader's weights with MHR's identity on SOMA's mesh
  (the mean and the body, head and scale directions, also in SOMA's bind pose
  for the fit), the colour tables and the feature masks, so the server needs no
  MHR files of its own. Its training code, from the data to the package, is
  `frommhr/` at the repository's root.
- About 25 s per person on the CPU, plus about 70 s for the pictures' colour.
  The output's metadata holds Anny's phenotype, MHR's head, body and scale
  values, the body's report (MHR's fit against Anny's body, the person's body
  beyond the neck's fade against it, the head's shape kept), Anny's height, the
  pigments, the framing and the texture's report.

frommhr's reader started from fromsoma's and was trained only on renders of MHR
people.
