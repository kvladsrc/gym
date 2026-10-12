#!/usr/bin/env bash
# The balance's panel (balance.py): balance.json edited in the browser,
# checked before it is saved; a running training takes it in as its next
# fights begin. Opens http://127.0.0.1:8765/ ; Ctrl+C stops it.
#
#   ./balance.sh [run-id] [--port 8765] [--no-browser]
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
exec "$here/.venv/bin/python" "$here/balance.py" "$@"
