set shell := ["bash", "-euo", "pipefail", "-c"]

# Show all available commands
default:
    @just --list

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

# Run all checks needed while working on Cothic
cothic-check: cothic-fmt cothic-lint cothic-smoke

# Auto-fix Cothic formatting
cothic-fmt:
    find cothic/scripts -name '*.gd' -exec gdformat {} +

# Run Cothic linters
cothic-lint:
    gdlint cothic/scripts/

# Run Cothic Godot smoke checks
cothic-smoke:
    bash cothic/tools/check-godot.sh

# Run all bazel tests
test *args="":
    bazelisk test //... --verbose_failures {{ args }}

# Build resume HTML, Typst source, Markdown, and PDF
cv-build:
    node scripts/render_cv.mjs
    prettier --write cv/index.html cv/styles.css
    typst compile cv/cv.typ cv/cv.pdf

# Sync selected directories/files to the public gym repo

gym_dir := env("HOME") / "repos" / "gym"

sync-gym dest=gym_dir: cv-build
    bash scripts/sync_gym.sh {{ dest }}

# Renew Talos admin client certificates and update talosconfig
talos-renew-certs out_dir="production/kubernetes/talos/_out":
    bash scripts/renew_talos_certs.sh {{ out_dir }}
