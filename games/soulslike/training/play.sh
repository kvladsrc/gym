#!/usr/bin/env bash
# Play against the trained duellist: you are the near fighter, the run's
# newest model the far one, in a window, until it is closed (or Ctrl+C).
# The model is loaded once, at the start.
#
#   ./play.sh [run-id] [--deterministic]
#
# The mouse turns the camera, and you keep turning to face its way; W/S
# ahead and back, A/D aside. Held, a key gives full speed at once. Left button
# the three quickest blows of your weapon in turn (a combo; from the first
# again after a pause), right its heavy blow, F a quick kick, Shift a quick
# move (the way WASD points), Space the roll. Your body is the model's (the
# same blows, energy, adrenaline), only its speeds come at once.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
# The players read the balance from here, again as each fight begins when
# it has changed (Balance.cs).
export SOULSLIKE_BALANCE="$here/balance.json"
project="$here/../unity/soulslike"
args=()
run=duel
while [[ $# -gt 0 ]]; do
  case "$1" in
    --deterministic) args+=(--deterministic) ;;
    -*) echo "unknown option $1" >&2; exit 2 ;;
    *) run="$1" ;;
  esac
  shift
done
if [[ ! -x "$project/Builds/ArenaView/Arena.x86_64" ]]; then
  echo "no arena viewer: build it (Soulslike/Build Arena Viewer, or ./record.sh --build)" >&2
  exit 1
fi
cd "$here"
exec "$here/.venv/bin/python" "$here/play.py" "$run" "${args[@]}"
