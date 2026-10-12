#!/bin/sh
# Runs the game's build on the laptop's NVIDIA GPU (PRIME render offload):
# without it the game opens on the integrated Radeon, at a few frames a second.
here=$(dirname "$(readlink -f "$0")")
exec env __NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia \
    __VK_LAYER_NV_optimus=NVIDIA_only "$here/unity/soulslike/Builds/Linux/Soulslike.x86_64"
