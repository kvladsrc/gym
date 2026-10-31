#!/bin/sh
# Runs the island's player build on the laptop's NVIDIA GPU (PRIME render
# offload); without it the game opens on the integrated Radeon. Build it with
# tools/build.sh (needs the running Editor) after baking the level.
#
# Under Wayland it runs natively (Vulkan, -force-wayland): through XWayland
# with OpenGL the NVIDIA driver hit Xid 13 "Const out of Bound" within a
# minute, and frames then waited 5 s each while memory grew to 13 GB.
here=$(dirname "$(readlink -f "$0")")
game="$here/unity/procedural/Builds/Linux/Procedural.x86_64"
if [ ! -x "$game" ]; then
    echo "no player build: run $here/tools/build.sh first" >&2
    exit 1
fi
if [ -n "${WAYLAND_DISPLAY:-}" ]; then
    set -- -force-wayland "$@"
fi
exec env __NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia \
    __VK_LAYER_NV_optimus=NVIDIA_only "$game" "$@"
