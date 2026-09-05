#!/usr/bin/env bash
# Syncs selected directories/files from this repo to the public gym repo,
# applying string replacements to strip private details.
#
# Usage:
#   ./scripts/sync_gym.sh [dest]
#
# Default dest: ~/repos/gym

set -euo pipefail

SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="${1:-"$HOME/repos/gym"}"

# Easy-to-edit lists
# Directories to sync as-is (src_relative_path:dest_relative_path)
DIRS=(
    ".agents:.agents"
    "cothic:cothic"
    "cpp/ayncibla/:cpp/ayncibla"
    "cpp/codeforces:cpp/codeforces"
    "cpp/warmup:cpp/warmup"
    "cv:cv"
    "home-manager:home-manager"
    "lisp/sicp:lisp/sicp"
    "neural_network:neural_network"
    "pet_project:pet_project"
    "pipellm:pipellm"
    "presentations:presentations"
    "production/docker:production/docker"
    "production/kubernetes/flux:production/kubernetes/flux"
    "production/kubernetes/helm-charts:production/kubernetes/helm-charts"
    "production/playbooks:production/playbooks"
    "production/terraform:production/terraform"
    "ripples_cli:ripples_cli"
    "static:static"
    "vibe_jakubovich_mvp:vibe_jakubovich_mvp"
    "zuul.d:zuul.d"
)

# Individual files to copy (relative to repo root)
FILES=(
    ".bazelignore"
    ".bazelrc"
    ".bazelversion"
    ".checkov.yaml"
    ".clang-format"
    ".clang-tidy"
    ".pre-commit-config.yaml"
    "BUILD.bazel"
    "LICENSE"
    "MODULE.bazel"
    "MODULE.bazel.lock"
    "README.md"
    "compile_commands.json"
    "flake.lock"
    "flake.nix"
    "go.mod"
    "go.sum"
    "justfile"
    "production/README.md"
    "renovate.json"
    "scripts/sync_gym.sh"
    "scripts/test_sync_gym.py"
)

# String replacements applied to every file (old|new)
REPLACEMENTS=(
    "https\://search.your.domain/raw/src/static|static"
    "myuser|myuser"
    "MYUSER|MYUSER"
    "review.your.domain|review.your.domain"
    "search.your.domain|search.your.domain"
    "your.domain|your.domain"
)
if [[ ! -d $DEST ]]; then
    echo "Destination $DEST does not exist" >&2
    exit 1
fi

echo "Syncing $SRC -> $DEST"

# Sync directories
for entry in "${DIRS[@]}"; do
    src_rel="${entry%%:*}"
    dest_rel="${entry##*:}"
    if [[ -d "$SRC/$src_rel" ]]; then
        mkdir -p "$DEST/$dest_rel"
        echo "  dir  $src_rel -> $dest_rel"
        rsync_args=(-a --delete)
        case "$src_rel" in
        vibe_jakubovich_mvp)
            # Keep source/assets, not Godot caches or local captures/builds.
            rsync_args+=(--filter=':- .gitignore')
            ;;
        esac
        rsync "${rsync_args[@]}" "$SRC/$src_rel/" "$DEST/$dest_rel/"
    else
        echo "  skip $src_rel (not found)"
    fi
done

# Sync files
for f in "${FILES[@]}"; do
    if [[ -f "$SRC/$f" ]]; then
        mkdir -p "$DEST/$(dirname "$f")"
        echo "  file $f"
        cp "$SRC/$f" "$DEST/$f"
    else
        echo "  skip $f (not found)"
    fi
done

# Apply replacements
echo "Applying replacements..."
sed_expr=""
for r in "${REPLACEMENTS[@]}"; do
    old="${r%%|*}"
    new="${r##*|}"
    sed_expr+="s@${old}@${new}@g; "
done
find "$DEST" -type f \
    -not -path '*/.git/*' \
    -not \( \
    -name '*.gif' \
    -o -name '*.jpeg' \
    -o -name '*.jpg' \
    -o -name '*.pdf' \
    -o -name '*.png' \
    -o -name '*.wav' \
    -o -name '*.ttf' \
    -o -name '*.otf' \
    -o -name '*.tgz' \
    -o -name '*.zip' \
    \) \
    -print0 |
    xargs -0 sed -i "$sed_expr"

echo "Sync complete"
