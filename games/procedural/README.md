# Procedural island

A Unity 6.6 (URP) level generator: one island from a seed and a recipe, with
eroded relief, rivers, lakes and (later) vegetation, baked on demand. Design,
algorithms and milestones: [`DESIGN.md`](DESIGN.md); the literature report it
started from: [`RESEARCH.md`](RESEARCH.md).

## Layout

- `unity/procedural/Assets/Procedural/Generation` — the generator core, plain
  C# without engine references.
- `…/World`, `…/Editor` — building Terrain and water, the bake runner, menus.
- `…/Recipes/Default.asset` — the level recipe (seed + parameters).
- `…/Generated` — the baked level; ignored by git, rebuilt from the recipe.
- `tools/core` — dotnet build and NUnit run of the core without Unity.
- `tools/island_proto.py` — the NumPy prototype behind DESIGN.md's numbers.
- `artifacts/` — ignored: `bake/status.json`, `reports/`, `maps/`,
  `bake/checkpoints/`.

## Baking

The user opens the project from Unity Hub. In the Editor: menu
**Procedural → Preview Island** (fast, half resolution) or **Bake Island**, or
the buttons on the *Island Generator* object of `Scenes/Island.unity`.

Through the Pipeline CLI or MCP (`unity-procedural` in `.mcp.json`):

```sh
P=games/procedural/unity/procedural
unity command --project-path "$P" editor.invoke_menu --path "Procedural/Bake Island"
cat games/procedural/artifacts/bake/status.json   # poll until state != running
```

From a terminal: `just procedural bake 42` (or `random`, or no seed to keep
the recipe's), `just procedural preview random`; they wait and print the
result.

A bake runs on a worker thread and returns at once; poll `status.json`
(compare `startedUtc` and `mode`: a menu call made during a domain reload is
lost). The report (`reports/<preset>-<seed>-<mode>.json`) lists recipe
targets, metrics and invariants; `maps/<…>.png` is a top-down overview.

With the Editor closed: `Unity -batchmode -projectPath "$P" -executeMethod
Procedural.Editor.Batch.Bake [-recipe Assets/…asset] [-mode Preview|Bake]`.

## Tests

```sh
export PATH=$HOME/Unity/Hub/Editor/6000.6.0f1/Editor/Data/DotNetSdk:$PATH
cd games/procedural/tools/core/Tests && dotnet test --no-restore
```

In the Editor: `unity command --project-path "$P" test.run --mode EditMode`;
PlayMode (walker and tree collisions) runs asynchronously:
`… test.run --mode PlayMode --async_tests true`, then poll `… test.status`.

## Playing on the GPU

The Editor runs on the integrated GPU; for a fair look at the level, build
the Linux player and run it on the NVIDIA GPU (PRIME offload):

```sh
just procedural build   # tools/build.sh: through the running Editor, ~5 min
just procedural play    # ./play.sh
```

Bake first: the player ships the level baked into `Scenes/Island.unity`,
with the walker if it is switched on (below).

## Walking

**Procedural → Toggle Walker** adds or switches a first-person walker: with
it, Play starts on foot at the island's centre (WASD, Shift, Space, right
mouse to look); without it the camera flies. Terrain tree colliders exist
only in Play Mode.

## Notes

- New Unity projects: the first Unity Search indexing grew
  `Library/Search` to 54 GB and filled the disk. The index is disabled here
  (`UserSettings/Search.index.disabled`, `indexOnEditorStartup = false`).
- Pipeline MCP tool names such as `status` or `run` exist in several groups
  and fail through MCP; use `unity command <group>.<name>` for those.
