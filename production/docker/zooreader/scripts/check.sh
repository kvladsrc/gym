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
mapfile -t MJS_FILES < <(find "$APP_DIR" -name '*.mjs' | sort)
mapfile -t PNG_FILES < <(find "$APP_DIR/assets" -name '*.png' | sort)
mapfile -t SH_FILES < <(find "$APP_DIR/scripts" -name '*.sh' | sort)

FILES=(
    "${APP_DIR}/Dockerfile"
    "${APP_DIR}/ASSET_PIPELINE.md"
    "${APP_DIR}/PLAN.md"
    "${APP_DIR}/index.html"
    "${APP_DIR}/nginx.conf"
    "${APP_DIR}/style.css"
    "${JS_FILES[@]}"
    "${MJS_FILES[@]}"
    "${SH_FILES[@]}"
    "${PNG_FILES[@]}"
)

for dep in magick node pre-commit; do
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
for js_file in "${MJS_FILES[@]}"; do
    node --check "$js_file"
done

echo "-> Testing asset workflow"
node --test "${APP_DIR}/scripts/assets.test.mjs"

echo "-> Checking asset contract"
bash "${APP_DIR}/scripts/check-assets.sh"

echo "-> Running targeted pre-commit hooks"
pre-commit run --files "${FILES[@]}"

echo "Zooreader checks passed"
