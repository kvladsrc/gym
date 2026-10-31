#!/usr/bin/env bash
# Bakes the island in the running Unity Editor and waits for the result:
#   bake.sh [seed|random] [bake|preview]
# A seed (a number, or "random") is written to the scene's recipe first; without
# one the recipe's seed is kept. Progress comes from artifacts/bake/status.json.
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
project="$here/unity/procedural"
status="$here/artifacts/bake/status.json"
cmd() { unity --no-banner command --project-path "$project" --json "$@"; }
field() { python3 -c "import json, sys; print(((json.load(sys.stdin).get('data') or {}).get('result') or {}).get('result', ''))"; }
read_status() { python3 -c "import json; d = json.load(open('$status')); print(d.get('startedUtc', '-'), d.get('state', '-'))" 2>/dev/null || echo "- -"; }

seed="${1:-}"
mode=Bake
[[ ${2:-bake} == preview ]] && mode=Preview
# Right after a compile the Editor reloads its domain and answers nothing for a while.
playing=
for _ in $(seq 1 30); do
    playing=$(cmd script.eval --code 'return UnityEditor.EditorApplication.isPlaying;' 2>/dev/null | field || true)
    [[ -n $playing ]] && break
    sleep 2
done
if [[ -z $playing ]]; then
    echo "the Editor does not answer" >&2
    exit 1
elif [[ $playing == True ]]; then
    echo "the Editor is in Play Mode: stop it first" >&2
    exit 1
fi
cmd editor.invoke_menu --path "Procedural/Open Island Scene" >/dev/null
if [[ -n $seed ]]; then
    [[ $seed == random ]] && seed=$((RANDOM * 32768 + RANDOM + 1))
    if ! [[ $seed =~ ^[0-9]+$ ]]; then
        echo "seed must be a number or 'random'" >&2
        exit 1
    fi
    cmd script.eval --code "var g = UnityEngine.Object.FindAnyObjectByType<Procedural.World.IslandGenerator>(); g.recipe.recipe.seed = $seed; UnityEditor.EditorUtility.SetDirty(g.recipe); UnityEditor.AssetDatabase.SaveAssets(); return g.recipe.recipe.seed;" >/dev/null
fi

read -r before _ < <(read_status)
cmd editor.invoke_menu --path "Procedural/$mode Island" >/dev/null
for _ in $(seq 1 450); do
    sleep 2
    read -r started state < <(read_status)
    [[ $started != "$before" && $state != running && $state != - ]] && break
done
python3 - "$status" <<'PY'
import json, sys
d = json.load(open(sys.argv[1]))
print(f"{d['mode']} of seed {d['seed']}: {d['state']} in {d['elapsedSeconds']:.0f} s, invariants {'passed' if d['passed'] else 'FAILED'}")
for key in ("report", "map", "error"):
    if d.get(key):
        print(f"  {key}: {d[key]}")
PY
[[ $state == "done" ]]
