#!/usr/bin/env bash
set -euo pipefail
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$root"
mode=${1:?Expected cli or blender-mcp}
shift
if [[ $mode == blender-mcp ]]; then
    exec "$HOME/.nix-profile/bin/blender-mcp" "$@"
fi
export UNITY_NO_UPDATE_CHECK=1 UNITY_NO_CONSENT_PROMPT=1 UNITY_NO_CRASH_REPORT=1
export UNITY_NO_PAGER=1
if [[ $mode == cli ]]; then
    exec "$HOME/.nix-profile/bin/unity" "$@"
fi
echo "Unknown mode: $mode. Open the Editor manually through Unity Hub." >&2
exit 2
