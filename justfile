set shell := ["bash", "-euo", "pipefail", "-c"]

mod aimam
mod cothic
mod cv
mod local_llm
mod neural_network
mod presentations
mod? vibe_jakubovich
mod vibe_jakubovich_mvp
mod talos "production/kubernetes/talos"
mod zooreader "production/docker/zooreader"

# Show all available commands
default:
    @just --list --list-submodules

# Run pre-commit on all files
lint:
    pre-commit run --all-files --show-diff-on-failure

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
