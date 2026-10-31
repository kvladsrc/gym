set shell := ["bash", "-euo", "pipefail", "-c"]

mod assets_studio
mod cothic "games/cothic"
mod cv
mod? finish_the_game "games/finish_the_game"
mod neural_network
mod? pointandclick "games/pointandclick"
mod presentations
mod procedural "games/procedural"
mod? soulslike "games/soulslike"
mod talos "production/kubernetes/talos"
mod zooreader "games/zooreader"
mod? vibe_jakubovich "games/vibe_jakubovich"

# Show all available commands
default:
    @just --list --list-submodules

# Run pre-commit on all files
lint:
    pre-commit run --all-files --show-diff-on-failure

# Build and check the Emacs configuration without activating Home Manager
emacs-check:
    #!/usr/bin/env bash
    set -euo pipefail
    nix_bin=$(command -v nix || printf '%s' /nix/var/nix/profiles/default/bin/nix)
    emacs_package=$("$nix_bin" build --no-link --print-out-paths ./home-manager#homeConfigurations.myuser.config.programs.emacs.finalPackage)
    "$emacs_package/bin/emacs" --batch -q -l home-manager/tests/emacs-config.el

# Auto-fix formatting via pre-commit hooks
fmt:
    -pre-commit run clang-format --all-files
    -pre-commit run shfmt        --all-files
    -pre-commit run buildifier   --all-files
    -pre-commit run nixfmt       --all-files
    -pre-commit run prettier     --all-files
    -pre-commit run go-fmt       --all-files
    -pre-commit run go-imports   --all-files

# Update Nix flakes
flake-update:
    nix flake update
    cd home-manager && nix flake update

# Resolve Helm dependencies exactly as Flux does for Git-backed charts
helm-check-deps:
    bash scripts/check_helm_dependencies.sh

# Upgrade Flux and regenerate the install manifest
flux-upgrade:
    flux install --export --components-extra=image-reflector-controller,image-automation-controller > production/kubernetes/flux/infrastructure/flux-system/gotk-components.yaml

# Run Renovate now, as Zuul's daily renovate-bot job does (its credentials from
# zuul-config; Gerrit through the cluster's node port, outside Authentik).
# Pass Renovate flags, e.g. `just renovate --dry-run=lookup`.
renovate *args="":
    #!/usr/bin/env bash
    set -euo pipefail
    secrets=~/repos/zuul-config/zuul.d/jobs.yaml
    secret() { yq -r ".[] | select(.secret.name == \"gerrit_basic_auth\") | .secret.data.$1" "$secrets"; }
    port=$(kubectl -n gerrit-cluster get svc gerrit-service -o jsonpath='{.spec.ports[?(@.port==80)].nodePort}')
    node=$(kubectl get nodes -o jsonpath='{.items[0].status.addresses[?(@.type=="InternalIP")].address}')
    export LOG_LEVEL=info RENOVATE_CONFIG_FILE="$PWD/renovate.json"
    export RENOVATE_USERNAME RENOVATE_PASSWORD RENOVATE_GITHUB_COM_TOKEN
    RENOVATE_USERNAME=$(secret renovate_username)
    RENOVATE_PASSWORD=$(secret renovate_password)
    RENOVATE_GITHUB_COM_TOKEN=$(secret renovate_github_token)
    renovate --platform=gerrit --endpoint="http://$node:$port" \
        --git-author="Renovate Bot <renovate@bonfire.lit>" {{ args }} src

# Run all bazel tests
test *args="":
    bazelisk test //... --verbose_failures {{ args }}

# Check cross-file production infrastructure invariants
infra-doctor:
    bazelisk run //production/infra-doctor

# Sync selected directories/files to the public gym repo

gym_dir := env("HOME") / "repos" / "gym"

sync-gym dest=gym_dir:
    @just cv build
    @just presentations backprop
    bash scripts/sync_gym.sh {{ dest }}

# Exercise public sync in isolated fixtures, without changing ~/repos/gym
sync-gym-check:
    python3 scripts/test_sync_gym.py
