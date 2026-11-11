#!/usr/bin/env bash
# Pack the built players for handing out: Builds/dist/hexcrown-<version>-linux.tar.gz and
# hexcrown-<version>-windows.zip, each one folder, without Unity's backup and debug folders.
# Build them first: tools/build.sh linux, tools/build.sh windows.
set -euo pipefail
project="$(cd "$(dirname "$0")/.." && pwd)/unity/realms"
version=$(sed -n 's/^  bundleVersion: //p' "$project/ProjectSettings/ProjectSettings.asset")
dist="$project/Builds/dist"
mkdir -p "$dist"
skip=(--exclude='*_BackUpThisFolder_ButDontShipItWithYourGame' --exclude='*_BurstDebugInformation_DoNotShip')
for platform in linux windows; do
    case $platform in
        linux) from="$project/Builds/Linux" player=Realms.x86_64 ;;
        windows) from="$project/Builds/Windows" player=Realms.exe ;;
    esac
    if [[ ! -f $from/$player ]]; then
        echo "no $platform player in $from: build it first" >&2
        continue
    fi
    name="hexcrown-$version-$platform"
    stage=$(mktemp -d)
    # The folder inside the archive carries the game's name; the files keep theirs.
    rsync -a "${skip[@]}" "$from/" "$stage/$name/"
    case $platform in
        linux) tar -C "$stage" -czf "$dist/$name.tar.gz" "$name" ;;
        windows) (cd "$stage" && rm -f "$dist/$name.zip" && zip -qr -9 "$dist/$name.zip" "$name") ;;
    esac
    rm -rf "$stage"
    ls -lh "$dist/$name".* | awk '{print $5, $9}'
done
