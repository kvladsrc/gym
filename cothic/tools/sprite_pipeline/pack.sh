#!/usr/bin/env bash
set -euo pipefail

project_root=${1:?project root is required}
animation=${2:?animation is required}
config="${project_root}/tools/sprite_pipeline/character.json"
character=$(jq -r '.name' "${config}")
size=$(jq -r '.output_size' "${config}")
frame_count=$(jq -r --arg animation "${animation}" '.frames[$animation]' "${config}")
direction_count=$(jq -r '.directions | length' "${config}")
input_root="${project_root}/tmp/sprites/${character}/${animation}"
output="${project_root}/tmp/sprites/${character}/${animation}.png"

mapfile -t frames < <(find "${input_root}" -mindepth 2 -maxdepth 2 -name 'frame_*.png' | sort)
expected_count=$((frame_count * direction_count))
if ((${#frames[@]} != expected_count)); then
    echo "Expected ${expected_count} frames, found ${#frames[@]}" >&2
    exit 1
fi

magick montage "${frames[@]}" -background none \
    -tile "${frame_count}x${direction_count}" \
    -geometry "${size}x${size}+0+0" "PNG32:${output}"
oxipng --opt 4 --strip safe --quiet "${output}"
echo "Wrote ${output}"
