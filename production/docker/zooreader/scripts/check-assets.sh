#!/usr/bin/env bash
# Validates the static Zooreader image asset contract.

set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$APP_DIR"
node scripts/check-assets.mjs
