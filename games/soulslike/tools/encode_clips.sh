#!/bin/sh
# The clip gallery's frames (Soulslike/Editor/ClipGallery.cs) as videos:
# <out>/frames/<pack>/<clip>/NNNN.png → <out>/clips/<pack>/<clip>.webm, then
# the frames go, and the Mixamo package's page of its clips is written anew.
#   encode_clips.sh ../../animations/mixamo/Gallery~
set -eu
out=$1
find "$out/frames" -mindepth 2 -maxdepth 2 -type d | while read -r dir; do
    clip=$(basename "$dir")
    pack=$(basename "$(dirname "$dir")")
    mkdir -p "$out/clips/$pack"
    ffmpeg -nostdin -hide_banner -loglevel error -y -framerate 15 -i "$dir/%04d.png" \
        -c:v libvpx-vp9 -b:v 0 -crf 38 -pix_fmt yuv420p "$out/clips/$pack/$clip.webm"
done
rm -rf "$out/frames"
python3 "$(dirname "$0")/../../../animations/mixamo/Tools~/gallery.py"
