#!/usr/bin/env bash
# Pack the game for handing out, each archive one folder, in Builds/dist:
#   hexcrown-<version>-linux.tar.gz, hexcrown-<version>-windows.zip: the built players, without
#     Unity's backup and debug folders (build them first: tools/build.sh linux, tools/build.sh windows);
#   hexcrown-<version>-source.zip: the game's sources as Git keeps them, with the Studio art that
#     .gitignore leaves to tools/fetch_art.py, so the Unity project opens without the Studio, and
#     the repository's licence.
# Usage: package.sh [linux] [windows] [source]   (all three without arguments)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
project="$root/unity/realms"
version=$(sed -n 's/^  bundleVersion: //p' "$project/ProjectSettings/ProjectSettings.asset")
dist="$project/Builds/dist"
mkdir -p "$dist"
skip=(--exclude='*_BackUpThisFolder_ButDontShipItWithYourGame' --exclude='*_BurstDebugInformation_DoNotShip')
wanted=("$@")
((${#wanted[@]})) || wanted=(linux windows source)
for platform in "${wanted[@]}"; do
    [[ $platform == source ]] && continue
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

if [[ " ${wanted[*]} " == *" source "* ]]; then
    name="hexcrown-$version-source"
    stage=$(mktemp -d)
    art=/unity/realms/Assets/Realms/Art
    # The Studio art goes in before .gitignore's rules, so that they do not leave it out.
    rsync -a \
        --include="$art.meta" --include="$art/" \
        --include="$art/Icons*" --include="$art/Icons*/***" \
        --include="$art/Models*" --include="$art/Models*/***" \
        --include="$art/Textures*" --include="$art/Textures*/***" \
        --include="$art/manifest.json*" \
        --filter=':- .gitignore' \
        "$root/" "$stage/$name/"
    if [[ -f $root/../../LICENSE ]]; then
        cp "$root/../../LICENSE" "$stage/$name/LICENSE"
    fi
    (cd "$stage" && rm -f "$dist/$name.zip" && zip -qr -9 "$dist/$name.zip" "$name")
    rm -rf "$stage"
    ls -lh "$dist/$name.zip" | awk '{print $5, $9}'
fi
