---
name: unity-workspace
description: Develop and verify the Mosslight Unity game in games/mosslight using the existing Home Manager Unity and Blender MCP workflow.
---

# Unity workspace

Read `games/mosslight/README.md` for commands and current versions. Run recipes
from the monorepo root with `nix develop -c just mosslight <recipe>`.

- The user launches Unity manually through Home Manager's Hub. Connect to the
  existing Editor for `games/mosslight/unity`; do not create a second Unity
  environment or launch an Editor automatically. Start with `status`.
- Unity MCP uses the official `unity mcp` CLI and the project's Pipeline
  package. Blender MCP uses the Home Manager wrapper with a pinned Python MCP
  SDK. Stable profile paths are configured by `mcp-config`; applying it changes
  the user's Codex config. Native tool discovery may require a client restart.
- If native tools are unavailable, `tools/mcp_call.py --server unity list`
  discovers the live schema; the same helper calls individual tools. Run it
  through the repository Nix environment. A missing instance inside a sandbox
  is not proof that the user closed Unity; retry with permitted host access.
- Use `build`, which queues the asynchronous MCP build and polls its build ID.
  Long synchronous builds inside `eval` can time out while continuing to run.
  Inspect build status before retrying; do not overlap builds or Play Mode.
- `assets-mcp` regenerates Blender art in the dedicated Mosslight scene.
  `assets` is the headless fallback. Both replace generated art. `prepare`
  replaces the demo Unity scene; preserve any manual scene edits separately.
- FBX and `.blend` stay local and ignored. Commit generators, text scenes,
  materials and `.meta`. Preserve existing GUIDs when regenerating assets.
  In a fresh checkout generate assets before opening Unity.
- `check` and the Zuul source job are lightweight checks, not C# compilation.
  Validate runtime changes with a local build and `smoke`; inspect the saved
  screenshot as well as the log. Smoke covers movement and one pickup, not the
  entire game. Do not add Unity or heavy art to CI without a new project need.
- Keep tools in Home Manager and project automation in the project. Add new
  infrastructure only when the game demonstrates a need for it.
