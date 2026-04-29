#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd -- "${script_dir}/.." && pwd)"
godot_log_file="${GODOT_LOG_FILE:-/tmp/cothic-godot.log}"

echo "Project: ${project_dir}"
echo "Godot: $(command -v godot)"
godot --version

mkdir -p "$(dirname -- "${godot_log_file}")"

echo "Importing project assets..."
godot --headless --path "${project_dir}" --log-file "${godot_log_file}" --import --quit

echo "Opening project headlessly..."
godot --headless --path "${project_dir}" --log-file "${godot_log_file}" --quit
