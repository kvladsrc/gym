#!/usr/bin/env bash
# Train the duellists against each other (ML-Agents symmetric self-play) in
# the arena player.
#
#   ./train.sh [run-id] [--build] [--resume | --force] [--envs N] [--hours H]
#              [--from RUN]
#
# --from: a new run starting from another run's weights
# (results/RUN/Fighter/checkpoint.pt; put an older checkpoint there to start
# from it), its steps and curriculum from nothing.
# --hours: stop (and save) after this long, 4 by default; 0 for no limit.
# --force overwrites a run of that id (a failed start leaves its folder).
# --envs: arena players run side by side (4 arenas each), 4 by default.
# --build first rebuilds the arena scene and its player through the running
# Unity editor (`unity cmd`). Results (checkpoints, the .onnx models,
# TensorBoard logs) go to results/<run-id>; watch with
#   uv run tensorboard --logdir results
# The trainer is stopped if free memory drops below 1.5 GB.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
# The players read the balance from here, again as each fight begins when
# it has changed (Balance.cs).
export SOULSLIKE_BALANCE="$here/balance.json"
project="$here/../unity/soulslike"
run=duel
build=0
envs=4
hours=4
extra=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --build) build=1 ;;
    --resume) extra+=(--resume) ;;
    --force) extra+=(--force) ;;
    --envs) envs="$2"; shift ;;
    --hours) hours="$2"; shift ;;
    --from) extra+=(--initialize-from "$2"); shift ;;
    -*) echo "unknown option $1" >&2; exit 2 ;;
    *) run="$1" ;;
  esac
  shift
done

wait_log() { # wait_log <start line> <pattern>
  for _ in $(seq 1 360); do
    tail -n +"$1" "$project/Logs/Editor.log" | grep -q "$2" && return 0
    sleep 5
  done
  echo "timed out waiting for: $2" >&2
  return 1
}

if [[ $build == 1 ]]; then
  start=$(wc -l < "$project/Logs/Editor.log")
  (cd "$project" && unity cmd --no-banner menu --path "Soulslike/Build Arena" > /dev/null)
  wait_log "$start" "Soulslike: arena built"
  (cd "$project" && unity cmd --no-banner menu --path "Soulslike/Build Arena Player" > /dev/null)
  wait_log "$start" "Soulslike: arena player"
  tail -n +"$start" "$project/Logs/Editor.log" | grep "Soulslike: arena"
fi

cd "$here"
# The guard, in the background: when memory runs low or the time is up, it
# interrupts the trainer (this script's child), which saves and stops.
script=$$
deadline=$(( $(date +%s) + $(awk -v h="$hours" 'BEGIN {print int(h * 3600)}') ))
(
  while kill -0 "$script" 2> /dev/null; do
    free=$(awk '/MemAvailable/ {print int($2 / 1024)}' /proc/meminfo)
    if (( free < 1500 )); then
      echo "memory low ($free MB): stopping the trainer" >&2
      pkill -INT -P "$script" -f learn.py
      sleep 60
    elif [[ $hours != 0 ]] && (( $(date +%s) >= deadline )); then
      echo "$hours h are up: stopping the trainer" >&2
      pkill -INT -P "$script" -f learn.py
      sleep 60
    fi
    sleep 2
  done
) &
guard=$!
# The trainer in the foreground: a command run with & in a script ignores
# SIGINT, so Ctrl+C never reached it and it never saved. Run straight from
# the environment (not through uv), so the signal goes to it; learn.py is
# mlagents-learn with the threaded update kept on the GPU.
status=0
"$here/.venv/bin/python" "$here/learn.py" duel.yaml --run-id "$run" --env "$project/Builds/Arena/Arena.x86_64" \
  --num-envs "$envs" --no-graphics "${extra[@]}" || status=$?
kill "$guard" 2> /dev/null || true
exit "$status"
