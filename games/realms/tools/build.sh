#!/usr/bin/env bash
# Build the Linux player (or `build.sh windows`: the Windows one). With the project open in the
# Editor: run Realms/Setup there, then Pipeline's asynchronous build. With the Editor closed: Unity
# headless (Setup + build in one go).
set -euo pipefail
project="$(cd "$(dirname "$0")/.." && pwd)/unity/realms"
case ${1:-linux} in
    linux) out="$project/Builds/Linux/Realms.x86_64" target=StandaloneLinux64 method=Linux ;;
    windows) out="$project/Builds/Windows/Realms.exe" target=StandaloneWindows64 method=Windows ;;
    *) echo "usage: build.sh [linux|windows]" >&2; exit 2 ;;
esac

if pgrep -f -- "-projectPath $project( |$)" >/dev/null; then
    cmd() { unity --no-banner command --project-path "$project" --json "$@"; }
    field() { python3 -c "import json, sys; r = json.load(sys.stdin)['data']['result']; print($1)"; }
    if [[ $(cmd script.eval --code 'return UnityEditor.EditorApplication.isPlaying;' | field "r['result']") == True ]]; then
        echo "the Editor is in Play Mode: stop it first" >&2
        exit 1
    fi
    status=$(cmd build.status | field "r.get('status')")
    if [[ $status == queued || $status == building ]]; then
        echo "an Editor build is already running: wait for it to finish" >&2
        exit 1
    fi
    cmd script.eval --code 'Realms.Editor.Setup.Run(); return "setup done";' >/dev/null
    request=$(cmd build.run --target "$target" --outputPath "$out" \
        --scenes '["Assets/Realms/Generated/Map.unity"]' --confirm true)
    if [[ $(field "r.get('status')" <<<"$request") != queued ]]; then
        echo "$request" >&2
        exit 1
    fi
    for _ in $(seq 1 360); do
        sleep 5
        # Reloads and busy Editors can fail the command or return a non-JSON reply.
        reply=$(cmd build.status) || continue
        status=$(field "r['status']" <<<"$reply" 2>/dev/null) || continue
        if [[ $status == completed ]]; then
            result=$(field "r['result']" <<<"$reply" 2>/dev/null) || continue
            echo "build: $result"
            if [[ $result != Succeeded ]]; then
                echo "build.status: $reply" >&2
                exit 1
            fi
            echo "$out"
            exit 0
        elif [[ $status == failed || $status == error || $status == cancelled || $status == canceled ]]; then
            echo "build.status: $reply" >&2
            exit 1
        fi
    done
    echo "build poll timed out; check build.status before retrying (the build may still be running)" >&2
    exit 1
fi

version=$(sed -n 's/^m_EditorVersion: //p' "$project/ProjectSettings/ProjectVersion.txt")
editor="$HOME/Unity/Hub/Editor/$version/Editor/Unity"
# Headless Unity runs inside the Hub's FHS environment; take the one the installed Hub uses.
hub=$(grep -o '/nix/store/[^ "]*/bin/unityhub' "$(command -v unityhub)" | head -n 1)
fhs=$(grep -o '/nix/store/[^ "]*/bin/unityhub-fhs-env' "$hub" | head -n 1)
if [[ ! -x $editor || ! -x $fhs ]]; then
    echo "Unity $version or the Hub's FHS environment is missing ($editor, $fhs)" >&2
    exit 1
fi
mkdir -p "$project/Logs"
log="$project/Logs/build.log"
echo "headless build (the first import takes a while); log: $log"
if ! nice -n 10 "$fhs" "$editor" -batchmode -nographics -quit -projectPath "$project" \
    -logFile "$log" -executeMethod "Realms.Editor.Build.$method"; then
    grep -E "error CS|Shader error|Exception|Realms: " "$log" | tail -n 40 >&2 || true
    echo "build failed; full log: $log" >&2
    exit 1
fi
# A shader that fails to compile does not fail the build: the player draws it magenta.
if grep -q "Shader error in 'Realms/" "$log"; then
    grep "Shader error in 'Realms/" "$log" | sort -u >&2
    echo "build has shader errors; full log: $log" >&2
    exit 1
fi
grep "Realms: " "$log" | tail -n 3
echo "$out"
