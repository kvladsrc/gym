#!/usr/bin/env bash
# Builds through the running Unity editor (`unity cmd`), waiting on its log:
#   build.sh arena                    the arena scene, its training player, its viewer
#   build.sh game [linux|windows|all] the Last Ember with the newest network, its players
# The arena's players are not rebuilt while a training runs (it uses them).
set -euo pipefail
project="$(cd "$(dirname "$0")/.." && pwd)/unity/soulslike"
log="$project/Logs/Editor.log"
cd "$project"

wait_log() { # <start line> <pattern>
    for _ in $(seq 1 360); do
        tail -n +"$1" "$log" | grep -q "$2" && return 0
        sleep 5
    done
    echo "timed out waiting for: $2" >&2
    return 1
}

menu() { # <menu path> <log line it ends with>
    local start
    start=$(wc -l <"$log")
    # The editor answers within 30 s; a longer build goes on, and its end is
    # read from the log. Any other failure (no editor) stops here.
    local out
    if ! out=$(unity cmd --no-banner menu --path "Soulslike/$1" 2>&1) && ! grep -q "timed out" <<<"$out"; then
        echo "$out" >&2
        return 1
    fi
    wait_log "$start" "Soulslike: $2"
    tail -n +"$start" "$log" | grep -E "^Soulslike: |error CS" || true
}

player() { # <Unity BuildTarget> <path>
    local start
    start=$(wc -l <"$log")
    # The editor answers a command within 5 s; the build goes on and logs its end.
    timeout 20 unity cmd --no-banner eval "var r = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions { scenes = new[] { \"Assets/Soulslike/Scenes/LastEmber.unity\" }, locationPathName = \"$2\", target = UnityEditor.BuildTarget.$1 }); UnityEngine.Debug.Log(\"Soulslike: game player $1 \" + r.summary.result + \", \" + (r.summary.totalSize / 1048576) + \" MB\"); return \"\";" >/dev/null 2>&1 || true
    wait_log "$start" "Soulslike: game player $1"
    tail -n +"$start" "$log" | grep -E "^Soulslike: game player|UnityException|error CS" || true
    tail -n +"$start" "$log" | grep -q "Soulslike: game player $1 Succeeded"
}

case "${1:-}" in
arena)
    if pgrep -f 'training/learn.py' >/dev/null; then
        echo "a training runs on the arena's player: stop it first" >&2
        exit 1
    fi
    menu "Build Arena" "arena built"
    menu "Build Arena Player" "arena player"
    menu "Build Arena Viewer" "arena viewer"
    ;;
game)
    target="${2:-all}"
    menu "Build Last Ember" "last ember built"
    if [[ $target == linux || $target == all ]]; then player StandaloneLinux64 Builds/Linux/Soulslike.x86_64; fi
    if [[ $target == windows || $target == all ]]; then player StandaloneWindows64 Builds/Windows/Soulslike.exe; fi
    ;;
*)
    echo "usage: $0 arena | game [linux|windows|all]" >&2
    exit 2
    ;;
esac
