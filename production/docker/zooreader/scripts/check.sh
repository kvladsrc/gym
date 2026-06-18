#!/usr/bin/env bash
# Runs the narrow local checks for the Zooreader static app.
#
# Usage:
#   just zooreader check

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
APP_DIR="production/docker/zooreader"

cd "$ROOT"
mapfile -t JS_FILES < <(find "$APP_DIR" -name '*.js' | sort)

FILES=(
    "${APP_DIR}/Dockerfile"
    "${APP_DIR}/PLAN.md"
    "${APP_DIR}/index.html"
    "${APP_DIR}/nginx.conf"
    "${APP_DIR}/style.css"
    "${JS_FILES[@]}"
)

for dep in node pre-commit; do
    if ! command -v "$dep" >/dev/null 2>&1; then
        echo "Missing dependency: $dep" >&2
        echo "Run through: nix develop -c just zooreader check" >&2
        exit 1
    fi
done

echo "-> Checking JavaScript syntax"
for js_file in "${JS_FILES[@]}"; do
    node --check "$js_file"
done

echo "-> Running targeted pre-commit hooks"
pre-commit run --files "${FILES[@]}"

echo "Zooreader checks passed"
