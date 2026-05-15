# Cothic Plan

Cothic has moved past the initial prototype milestones. The repository
navigation loop, file reading, prompt tools, task queue, model dry run, draft
review, diff preview, and guarded apply flow all have playable first passes.

The current goal is to turn that working codebase interface into a readable
isometric game.

## Completed Foundation

The first eight milestones are considered complete enough to stop tracking as
separate active phases:

- Project baseline: Godot project, player scene, smoke check, and documented
  Nix workflow.
- Directory room: one repository directory generates a stable playable area.
- Directory navigation: portals enter subdirectories, parent traversal works,
  and repository access stays inside the configured root.
- Reading files: text files open in a read-only in-game reader with basic size,
  truncation, and binary-file handling.
- Map and fast travel: repository directories can be scanned incrementally and
  revisited without freezing the game loop.
- Prompt tools: Inspect, Refactor Hammer, and Investigate can mark files.
- Task queue: player intents are stored, displayed, run, deleted, and tracked
  through task history.
- Model review loop: OpenRouter dry-run requests create structured drafts that
  can be previewed, diffed, rejected, and guardedly applied.

These systems will still need polish, but they are no longer the main project
risk.

## Current Phase: Isometric Game Feel

The world has shifted from an abstract 3D prototype to a fixed-camera
isometric game built with Kenney Hexagon Kit assets.

Done:

- Hexagon Kit assets are imported under `assets/models/hexagon-kit`.
- The player uses the `Ranger.glb` character model.
- Directories render as portal models.
- Files render as `building-*` tiles.
- Pixel cursor and first-pass pixel panel/button styling are wired in.
- UI uses Alegreya Sans SC, while file reader, draft preview, and diff preview
  keep Mononoki for code-like text.
- Generated areas are connected grass islands with water around the edges.
- Sparse forest, hill, water-rock, and water-island dressing exists.
- Small beach clusters can appear near water.
- Rivers generate as readable coast-to-coast paths using calibrated side-mask
  matching against the river atlas.
- River generation and the calibrated river atlas are isolated in
  `scripts/world/river_generator.gd`.
- World generation is split into focused stage generators:
  `IslandGenerator`, `BeachGenerator`, `RiverGenerator`, `StoneGenerator`, and
  `EntryPlacementGenerator`.
- Large enough islands can reserve a single central rocky landmark area.
- File buildings are selected by relative file size within the current
  directory, with small/medium/large model pools and no large buildings on tiny
  islands.
- Generated layouts are stable while the game is running and reshuffled on the
  next launch.
- The last opened repository root is saved under `user://workspace.cfg` and can
  be reopened from the start menu.
- Buildings and portals use the same placement pipeline.
- Portals have physical collision.
- Primitive row-layout object builders have been removed from the main visual
  path.
- Existing file and directory interactions still work through
  `WorldInteractable`.
- Current scaling is acceptable.
- Water collision is acceptable.

Current world-generation constraints:

- River tiles are now stable enough to leave alone while building roads.
- Roads are still absent.

## Next Steps

Recommended next-session start:

1. Launch the game and inspect several generated directories.
2. Confirm that revisiting the same directory keeps the same layout during one
   run.
3. Confirm that the map opens quickly on a large repository and keeps adding
   directories incrementally.

4. Add animation.
   - Animate the player model during idle, movement, and tool use.
   - Add small interaction feedback for selected buildings, portals, and tool
     actions.
   - Keep animation readable from the fixed isometric camera.

5. Create roads.
   - Connect portals, file buildings, spawn, and important landmarks.
   - Pick road tiles from local connectivity instead of random decoration.
   - Decide how roads cross rivers before adding bridge tiles.
   - Keep the location readable when directories contain many entries.
   - Keep road generation separate from `HexLevelBuilder` once it grows beyond
     simple placement orchestration.

6. Add sound.
   - Footsteps and basic movement feedback.
   - Portal/file interaction sounds.
   - UI open/close/selection sounds.
   - Subtle ambient audio for generated locations.

7. Add menus, fullscreen, and settings.
   - Provide an in-game exit/settings menu.
   - Add a fullscreen toggle and persist display/audio preferences.
   - Move OpenRouter configuration into in-game settings instead of relying on
     `OPENROUTER_*` environment variables for normal use.
   - Make `ui_cancel` behavior predictable across gameplay and panels.
   - Keep UI scaling readable in both windowed and fullscreen modes.

8. Add controller support.
   - Add gamepad movement, interaction, panel navigation, and tool selection.
   - Keep mouse and keyboard as the primary tested path until late polish.
   - Validate controller behavior after menus/settings exist.

9. Finalize.
   - Sweep for broken interactions across navigation, map, tools, tasks, and
     draft apply.
   - Do a visual pass over several repository sizes.
   - Update README and plan to match the finished playable slice.

## Ongoing Cleanup

- Keep `HexLevelBuilder` as the layer orchestrator; add new generation stages as
  focused generator classes.
- Keep the river tile masks in `scripts/world/river_generator.gd` as the source
  of truth for river model matching. Avoid editing them unless recalibrating the
  models deliberately.
- Extract modal/UI blocking from `PlayerController` before adding many more
  panels.
- Improve batch-result review for multiple target files.
- Improve the plain repository tree map once the world-generation layers settle.
- Improve large-file reading, search, and non-UTF-8 presentation.
- Keep controller support late in the schedule so UI/menu behavior is stable
  before adding another input path.
- Decide whether raw GLB files should stay as direct source assets or be wrapped
  in dedicated Godot scenes with collision/import presets.

Keep each step runnable and visually inspectable before moving to the next.
