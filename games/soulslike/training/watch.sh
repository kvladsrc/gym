#!/usr/bin/env bash
# Watch the trained duellists fight in a window, in real time, until it is
# closed (or Ctrl+C): the run's newest model, taken up again each minute while
# the training goes on. Nothing is written.
#
#   ./watch.sh [run-id] [--deterministic]
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
exec "$here/record.sh" "$@" --watch
