#!/bin/sh
set -eu

target_root=/target
flake=/tmp/build-node-store-flake
nix=/nix/var/nix/profiles/default/bin/nix

mkdir -p "${flake}"
cp /workspace/flake.nix /workspace/flake.lock "${flake}/"

profile_is_valid() {
    profile=$1
    case "${profile}" in
    /nix/store/*) ;;
    *) return 1 ;;
    esac

    nix_link="$(readlink "${target_root}${profile}/bin/nix")" || return 1
    case "${nix_link}" in
    /nix/store/*) ;;
    *) return 1 ;;
    esac

    test -x "${target_root}${nix_link}"
}

default_profile="${target_root}/nix/var/nix/profiles/default"
if [ -L "${default_profile}" ]; then
    store_profile="$(readlink "${default_profile}")"
    if profile_is_valid "${store_profile}"; then
        echo "The local Nix store is already initialized."
        exit 0
    fi
fi

echo "Building a fresh build-node store at ${target_root}/nix"
store_profile="$("${nix}" \
    --extra-experimental-features "nix-command flakes" \
    --store "${target_root}" \
    build "${flake}#buildNodeStore" \
    --no-link \
    --print-out-paths \
    --max-jobs auto \
    --print-build-logs)"

mkdir -p "${target_root}/nix/var/nix/profiles"
ln -sfn "${store_profile}" "${default_profile}"

profile_is_valid "${store_profile}"
echo "Local Nix store initialized with ${store_profile}"
