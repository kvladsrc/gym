#!/usr/bin/env bash
# Serves the Zooreader static app locally.
#
# Usage:
#   just zooreader serve [port] [host]

set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PORT="${1:-8080}"
HOST="${2:-127.0.0.1}"

if ! command -v python3 >/dev/null 2>&1; then
    echo "Missing dependency: python3" >&2
    echo "Run through: nix develop -c just zooreader serve" >&2
    exit 1
fi

echo "Serving Zooreader from $APP_DIR"
echo "Open: http://$HOST:$PORT/"
cd "$APP_DIR"
exec python3 -m http.server "$PORT" --bind "$HOST"
