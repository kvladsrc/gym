#!/usr/bin/env bash
# Film the trained boss and hero: the run's newest models fight in the arena
# viewer (with graphics; a window opens for the length of it), and the video
# goes to results/<run-id>/videos/step-<steps>.mp4. The training may go on
# meanwhile.
#
#   ./record.sh [run-id] [--seconds 60] [--deterministic] [--build]
#
# --build first rebuilds the viewer through the running Unity editor (needed
# once, and after the arena or the agents change).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
# The players read the balance from here, again as each fight begins when
# it has changed (Balance.cs).
export SOULSLIKE_BALANCE="$here/balance.json"
project="$here/../unity/soulslike"
args=()
build=0
run=duel
while [[ $# -gt 0 ]]; do
    case "$1" in
    --build) build=1 ;;
    --seconds)
        args+=(--seconds "$2")
        shift
        ;;
    --deterministic) args+=(--deterministic) ;;
    --watch) args+=(--watch) ;;
    --seed)
        args+=(--seed "$2")
        shift
        ;;
    -*)
        echo "unknown option $1" >&2
        exit 2
        ;;
    *) run="$1" ;;
    esac
    shift
done
if [[ $build == 1 || ! -x "$project/Builds/ArenaView/Arena.x86_64" ]]; then
    start=$(wc -l <"$project/Logs/Editor.log")
    (cd "$project" && unity cmd --no-banner menu --path "Soulslike/Build Arena Viewer" >/dev/null)
    for _ in $(seq 1 360); do
        tail -n +"$start" "$project/Logs/Editor.log" | grep -q "Soulslike: arena viewer" && break
        sleep 5
    done
    tail -n +"$start" "$project/Logs/Editor.log" | grep "Soulslike: arena viewer"
fi
exec "$here/.venv/bin/python" "$here/record.py" "$run" "${args[@]}"
