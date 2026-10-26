# Guides: making assets with Studio

<!-- markdownlint-disable MD013 -->

What worked and what failed while making assets for an entire game: animated
characters, environments, levels, voices and music. Findings come from hundreds
of rated variants; measurements use a laptop RTX 3070 (8 GB). The rules are
not tied to a genre or project.

| | |
| --- | --- |
| [3d.md](3d.md) | Image → 3D pipeline, shape-friendly prompts and parameters |
| [characters.md](characters.md) | Rig-ready concepts, rigging, Mixamo animation and attacks |
| [engine.md](engine.md) | Engine import, collisions, Blender levels and performance |
| [audio.md](audio.md) | Voices (XTTS) and songs (YuE) |
| [process.md](process.md) | Finding good combinations: sweeps, ratings, analysis, queue and memory |

Key findings:

- **3D: Qwen-Image → Hunyuan3D-2mini → Hunyuan3D-Paint.** None of the tested
  alternatives (TripoSR, Stable Fast 3D, FLUX as image source) came close;
  even a weak image can produce a usable game model.
- **Write a paragraph describing the object's construction**, not a keyword
  list: simple style, broad color areas, matte lighting, one connected object.
- **Do not reduce meshes manually and weld them “later”:** Paint meshes are
  split at UV seams and tear when reduced without welding.
- **Characters: A-pose, empty hands, visible legs, no tails or capes.** Use
  Generic in the engine rather than Humanoid, with in-place clips.
- **Voice cloning requires clean speech;** XTTS does not accept stress marks.
