# Guides: making assets with Studio

<!-- markdownlint-disable MD013 -->

What worked and what failed while making assets for an entire game: animated
characters, environments, levels, voices and music. Findings come from hundreds
of rated variants; measurements use a laptop RTX 3070 (8 GB). The rules are
not tied to a genre or project.

| | |
| --- | --- |
| [3d.md](3d.md) | Image → 3D: the job graph, its settings and reference prompts |
| [characters.md](characters.md) | Rig-ready concepts, rigging, Mixamo animation and attacks |
| [quadrupeds.md](quadrupeds.md) | A rigged animal animated from AI video (AnyMove), hand-painted weights |
| [engine.md](engine.md) | Engine import, collisions, Blender levels and performance |
| [audio.md](audio.md) | Voices (XTTS) and songs (YuE) |
| [process.md](process.md) | Ratings and their analysis, the queue, memory and missing studio features |

Key findings:

- **3D: Qwen-Image → Hunyuan3D-2.1 → Hi3D → TRELLIS.2 Paint.** Every model
  rated 5 came from these four jobs: a transparent 1024×1024 reference,
  Hunyuan3D's defaults, Hi3D at 512 simplifying to 40K triangles, and
  TRELLIS.2 painting that mesh.
- **Describe one object by its construction:** parts, materials, counts and
  what it stands on. Ask for a three-quarter view from slightly above, the
  whole object in frame, even light, no background and no shadows.
- **Reduce in Hi3D, before painting:** its `max_faces` simplifies the full
  surface once. After painting, `3d-retopo` `decimate` keeps UVs and
  material; remeshing drops the paint.
- **Characters: A-pose, empty hands, visible legs, no tails or capes.** Use
  Generic in the engine rather than Humanoid, with in-place clips.
- **Quadrupeds: head in line with the spine, tail held clear of the legs.**
  Paint the body and leg regions by hand; automatic weights drag the flanks
  with the legs. Fast moves need lighter fit smoothing.
- **Voice cloning requires clean speech;** XTTS does not accept stress marks.
