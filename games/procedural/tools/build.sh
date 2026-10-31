#!/usr/bin/env bash
# Builds the Linux player of the island scene through the running Unity Editor
# (Pipeline build.run, then build.status until it completes). Bake the level
# first: the player ships the baked level that is in the scene.
set -euo pipefail
project="$(cd "$(dirname "$0")/.." && pwd)/unity/procedural"
out="$project/Builds/Linux/Procedural.x86_64"
cmd() { unity --no-banner command --project-path "$project" --json "$@"; }
field() { python3 -c "import json, sys; r = json.load(sys.stdin)['data']['result']; print($1)"; }

if [[ $(cmd script.eval --code 'return UnityEditor.EditorApplication.isPlaying;' | field "r['result']") == True ]]; then
    echo "the Editor is in Play Mode: stop it first" >&2
    exit 1
fi
cmd build.run --target StandaloneLinux64 --outputPath "$out" \
    --scenes '["Assets/Procedural/Scenes/Island.unity"]' --confirm true >/dev/null
for _ in $(seq 1 360); do
    sleep 5
    status=$(cmd build.status | field "r.get('status')")
    [[ $status == completed ]] && break
done
result=$(cmd build.status | field "r.get('result')")
echo "build: $result"
[[ $result == Succeeded ]] && echo "$out"
