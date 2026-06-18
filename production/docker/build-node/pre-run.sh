#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
nix_store="${NIX_STORE_PATH:-/srv/nfs/nix}"
image="${BUILD_NODE_IMAGE:-registry.your.domain/build:current}"
gc_root="/nix/var/nix/gcroots/build-node-tools-prewarm"
container="build-node-prewarm-$$"

if ! buildah="$(command -v buildah)"; then
    printf 'buildah is required to prewarm the Nix store.\n' >&2
    exit 1
fi

cleanup() {
    sudo "${buildah}" rm "${container}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

if [[ ! -d ${nix_store}/store || ! -d ${nix_store}/var/nix ]]; then
    printf 'Nix store not found at %s\n' "${nix_store}" >&2
    printf 'Set NIX_STORE_PATH to the directory mounted as /nix in CI.\n' >&2
    exit 1
fi

fs_type="$(stat --file-system --format=%T "${nix_store}")"
if [[ ${fs_type} == nfs* ]]; then
    printf '%s is an NFS client mount.\n' "${nix_store}" >&2
    printf 'Run this on the NFS server against its local export path.\n' >&2
    exit 1
fi

printf 'Prewarming %s with %s\n' "${nix_store}" "${image}"

sudo "${buildah}" from \
    --name "${container}" \
    --pull newer \
    "${image}" >/dev/null

sudo "${buildah}" run \
    --cap-add all \
    --network host \
    --volume "${nix_store}:/nix" \
    --volume "${script_dir}:/workspace" \
    --workdir /workspace \
    "${container}" \
    /nix/var/nix/profiles/default/bin/nix \
    build .#buildNodeTools \
    --out-link "${gc_root}" \
    --max-jobs auto \
    --print-build-logs

printf 'Prewarm complete: %s\n' "${nix_store}${gc_root#/nix}"
