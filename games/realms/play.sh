#!/bin/sh
# Run the Linux player on the NVIDIA GPU, using native Wayland when available.
here=$(dirname "$(readlink -f "$0")")
game="$here/unity/realms/Builds/Linux/Realms.x86_64"
if [ ! -x "$game" ]; then
    echo "no player build: run $here/tools/build.sh first" >&2
    exit 1
fi
if [ -n "${WAYLAND_DISPLAY:-}" ]; then
    set -- -force-wayland "$@"
fi
exec env __NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia \
    __VK_LAYER_NV_optimus=NVIDIA_only "$game" "$@"
