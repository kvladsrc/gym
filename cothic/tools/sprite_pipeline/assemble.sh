#!/usr/bin/env bash
# Assemble an 8-direction character sprite atlas from five generated strips.
#
# Rows 0-4 (south, south_west, west, north_west, north) are sliced from
# generated input strips. Rows 5-7 are horizontal mirrors of rows 1-3:
#   05_north_east <- 03_north_west
#   06_east       <- 02_west
#   07_south_east <- 01_south_west
# The row order is fixed by the game (player_controller.gd direction rows).
#
# Input per direction, under tmp/sprites/<name>/<animation>/strips/:
#   either <direction>.png  - one strip image, sliced into equal cells
#   or     <direction>/     - a directory with exactly 8 frame PNGs
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: assemble.sh <project_root> <animation> [options]

Options:
  --name NAME       Character name (default: knight)
  --size PX         Output cell size in pixels (default: 512)
  --layout LAYOUT   Strip layout: 8x1 or 4x2 (default: 8x1)
  --trim            Trim cell margins and re-anchor feet to the cell bottom
  --input DIR       Strips directory (default: tmp/sprites/<name>/<animation>/strips)
  --output FILE     Atlas path (default: tmp/sprites/<name>/<animation>.png)
EOF
}

project_root=${1:?project root is required}
animation=${2:?animation is required}
shift 2

name=knight
size=512
frame_count=8
layout=8x1
trim=0
input_root=""
output=""

while (($#)); do
    case "$1" in
    --name)
        name=$2
        shift 2
        ;;
    --size)
        size=$2
        shift 2
        ;;
    --layout)
        layout=$2
        shift 2
        ;;
    --trim)
        trim=1
        shift
        ;;
    --input)
        input_root=$2
        shift 2
        ;;
    --output)
        output=$2
        shift 2
        ;;
    -h | --help)
        usage
        exit 0
        ;;
    *)
        echo "Unknown argument: $1" >&2
        usage >&2
        exit 2
        ;;
    esac
done

case "${layout}" in
8x1 | 4x2) ;;
*)
    echo "Unsupported layout: ${layout} (expected 8x1 or 4x2)" >&2
    exit 2
    ;;
esac

generated=(south south_west west north_west north)
mirror_rows=(05_north_east 06_east 07_south_east)
declare -A mirror_of=(
    [05_north_east]=03_north_west
    [06_east]=02_west
    [07_south_east]=01_south_west
)

: "${input_root:=${project_root}/tmp/sprites/${name}/${animation}/strips}"
: "${output:=${project_root}/tmp/sprites/${name}/${animation}.png}"
frames_root="${project_root}/tmp/sprites/${name}/${animation}"

tmp_cells=$(mktemp -d)
trap 'rm -rf "${tmp_cells}"' EXIT

# Slice one direction strip (or copy pre-sliced frames) into a row directory.
slice_direction() {
    local direction=$1 row=$2
    local src_dir="${input_root}/${direction}"
    local src_strip="${input_root}/${direction}.png"
    local out_dir="${frames_root}/${row}"
    local cells_dir="${tmp_cells}/${row}"

    rm -rf "${out_dir}"
    mkdir -p "${out_dir}" "${cells_dir}"

    if [[ -d ${src_dir} ]]; then
        mapfile -t cells < <(find "${src_dir}" -maxdepth 1 -name '*.png' | sort)
        if ((${#cells[@]} != frame_count)); then
            echo "Expected ${frame_count} frames in ${src_dir}, found ${#cells[@]}" >&2
            exit 1
        fi
        for i in "${!cells[@]}"; do
            cp "${cells[$i]}" "$(printf '%s/frame_%02d.png' "${cells_dir}" "$i")"
        done
    elif [[ -f ${src_strip} ]]; then
        magick "${src_strip}" -crop "${layout}@" +repage +adjoin \
            "${cells_dir}/frame_%02d.png"
        local produced
        produced=$(find "${cells_dir}" -name 'frame_*.png' | wc -l)
        if ((produced != frame_count)); then
            echo "Slicing ${src_strip} as ${layout} produced ${produced} cells, expected ${frame_count}" >&2
            exit 1
        fi
    else
        echo "Missing input: ${src_strip} (or ${src_dir}/ with ${frame_count} PNGs)" >&2
        exit 1
    fi

    local gravity=center
    ((trim)) && gravity=south
    local cell
    for cell in "${cells_dir}"/frame_*.png; do
        local args=()
        ((trim)) && args+=(-trim +repage)
        magick "${cell}" "${args[@]}" \
            -resize "${size}x${size}" -background none \
            -gravity "${gravity}" -extent "${size}x${size}" \
            "${out_dir}/$(basename "${cell}")"
    done
}

for i in "${!generated[@]}"; do
    slice_direction "${generated[$i]}" "$(printf '%02d_%s' "$i" "${generated[$i]}")"
done

for row in "${mirror_rows[@]}"; do
    src_dir="${frames_root}/${mirror_of[$row]}"
    out_dir="${frames_root}/${row}"
    rm -rf "${out_dir}"
    mkdir -p "${out_dir}"
    for cell in "${src_dir}"/frame_*.png; do
        magick "${cell}" -flop "${out_dir}/$(basename "${cell}")"
    done
done

mapfile -t frames < <(find "${frames_root}" -mindepth 2 -maxdepth 2 -name 'frame_*.png' | sort)
expected_count=$((frame_count * 8))
if ((${#frames[@]} != expected_count)); then
    echo "Expected ${expected_count} frames, found ${#frames[@]}" >&2
    exit 1
fi

mkdir -p "$(dirname "${output}")"
magick montage "${frames[@]}" -background none \
    -tile "${frame_count}x8" \
    -geometry "${size}x${size}+0+0" "PNG32:${output}"
oxipng --opt 4 --strip safe --quiet "${output}"
echo "Wrote ${output}"
