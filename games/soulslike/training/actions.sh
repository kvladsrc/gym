#!/usr/bin/env bash
# Which actions the fighters take, by what they hold, and how often each blow
# lands, over the run's last stretch of training (actions.py).
#
#   ./actions.sh [run-id] [--steps 500000] [--weapon sword|hammer|hands] [--across] [--every SECONDS]
#
# --across: also before the balance's last change (by default the stretch
# starts there).
# --every: print it anew that often (as the training goes on), until Ctrl+C.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
args=()
every=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --steps) args+=(--steps "$2"); shift ;;
    --across) args+=(--across) ;;
    --weapon) args+=(--weapon "$2"); shift ;;
    --every) every="$2"; shift ;;
    -*) echo "unknown option $1" >&2; exit 2 ;;
    *) args+=("$1") ;;
  esac
  shift
done
show() { "$here/.venv/bin/python" "$here/actions.py" "${args[@]}"; }
if [[ $every == 0 ]]; then
  show
  exit
fi
while true; do
  clear
  show || true
  sleep "$every"
done
