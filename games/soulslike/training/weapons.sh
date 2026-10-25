#!/usr/bin/env bash
# The weapons' table of a run: how often each weapon beats each other in the
# fights decided, and overall, over its last stretch of training (weapons.py).
#
#   ./weapons.sh [run-id] [--steps 500000] [--across] [--every SECONDS]
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
    --steps)
        args+=(--steps "$2")
        shift
        ;;
    --across) args+=(--across) ;;
    --every)
        every="$2"
        shift
        ;;
    -*)
        echo "unknown option $1" >&2
        exit 2
        ;;
    *) args+=("$1") ;;
    esac
    shift
done
show() { "$here/.venv/bin/python" "$here/weapons.py" "${args[@]}"; }
if [[ $every == 0 ]]; then
    show
    exit
fi
while true; do
    clear
    show || true
    sleep "$every"
done
