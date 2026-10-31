# Generator core outside Unity

`Core/Core.csproj` compiles the Unity-free generator in
`../../unity/procedural/Assets/Procedural/Generation/` (C# 9, .NET Standard
2.1, warnings as errors), so API use Unity's Mono lacks fails here too.
`Tests/CoreTests.csproj` runs the EditMode tests of
`…/Assets/Procedural/Tests/EditMode/` with NUnit, without the Editor.

```sh
export PATH=$HOME/Unity/Hub/Editor/6000.6.0f1/Editor/Data/DotNetSdk:$PATH
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1
cd games/procedural/tools/core/Tests
nice -n 10 dotnet test --no-restore
```

Sandboxed agents whose sandbox blocks VSTest's local socket can run the same
tests in process: `dotnet run --no-restore -p:InProcessTests=true`.

## Behaviour-preserving changes

`GoldenHashTests.PrintGoldenHashes` (explicit) fingerprints four whole runs:
heights, discharge, wetness, moisture, surface, lakes, rivers, plants, detail
maps and the report without timings. Record it before a refactor and compare
after; any difference is a behaviour change.

```sh
GOLDEN_OUT=/tmp/before.txt dotnet test --no-restore --filter "FullyQualifiedName~PrintGoldenHashes"
# … change the code …
GOLDEN_OUT=/tmp/after.txt dotnet test --no-restore --filter "FullyQualifiedName~PrintGoldenHashes"
diff /tmp/before.txt /tmp/after.txt
```

## Validation runs

`dotnet run -c Release --no-restore -p:InProcessTests=true -- <command>`:

| Command | What it does |
| --- | --- |
| `Sample Small 1` | One Bake; report, metrics and raw dumps in `artifacts/core/` |
| `PreviewSample Small 1` | The same in Preview mode |
| `QualitySweep` | Small seeds 1–5 and Medium 1–2: invariants, recipe targets, quality ranges |
| `SeedSweep` | 64 small fixture maps (Preview and Bake): invariants only |
| `Timing` | Small seed 7: Preview ≤ 3 s, Bake ≤ 20 s on CoreCLR |

Outputs go to `games/procedural/artifacts/core/` (ignored). Raw dumps are
large; delete them when done.

## Debug renders

`render_relief.py` and `render_moisture.py` turn the raw dumps of `Sample`
into PNG maps (hillshade, materials, water; moisture). They need only Python
and write next to the input:

```sh
nice -n 10 python games/procedural/tools/core/render_relief.py \
  games/procedural/artifacts/core/Small-1-StreamPower-6.raw --mode all \
  --output games/procedural/artifacts/core/small-1.png
```

Unity bakes write their own maps to `artifacts/maps/`.
