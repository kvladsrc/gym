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
    "assets_studio:assets_studio"
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
    "static:static"
    "zuul.d:zuul.d"
)

# Games published from games/ (with games/README.md); the others stay private
GAMES=(
    "cothic"
    "realms"
    "yaqs"
    "zooreader"
)

# Files of the published games that rsync's reading of their .gitignore leaves
# out but the public copy carries (game:pattern, as in that .gitignore): Realms'
# Studio art, ignored here because tools/fetch_art.py fetches it from a local
# Assets Studio (Realms/Setup makes the rest of Art/ again), and a negation in
# that .gitignore, which rsync does not follow. The copy's .gitignore lets its
# Git take them.
GAME_EXTRAS=(
    "realms:/unity/realms/Assets/Realms/Art.meta"
    "realms:/unity/realms/Assets/Realms/Art/Icons*"
    "realms:/unity/realms/Assets/Realms/Art/Models*"
    "realms:/unity/realms/Assets/Realms/Art/Textures*"
    "realms:/unity/realms/Assets/Realms/Art/manifest.json*"
    "realms:/unity/realms/UserSettings/Search.settings"
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
    "/home/myuser\/Dropbox/src/|"
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
        assets_studio)
            # Only tracked files: no environments, models, builds or node_modules.
            rsync_args+=(--filter=':- .gitignore')
            ;;
        esac
        rsync "${rsync_args[@]}" "$SRC/$src_rel/" "$DEST/$dest_rel/"
    else
        echo "  skip $src_rel (not found)"
    fi
done

# Sync the listed games: only their tracked files. Anything else under games/
# in the destination, such as a game published before, is removed.
games_args=(-a --delete --delete-excluded)
# Before the .gitignore rules, so that they win.
for entry in "${GAME_EXTRAS[@]}"; do
    pattern="/${entry%%:*}${entry#*:}"
    games_args+=(--include="$pattern" --include="$pattern/***")
done
games_args+=(--filter=':- .gitignore' --include='/README.md')
for game in "${GAMES[@]}"; do
    games_args+=(--include="/$game/***")
done
games_args+=(--exclude='*')
mkdir -p "$DEST/games"
echo "  dir  games: ${GAMES[*]}"
rsync "${games_args[@]}" "$SRC/games/" "$DEST/games/"
for game in "${GAMES[@]}"; do
    negations=()
    for entry in "${GAME_EXTRAS[@]}"; do
        if [[ ${entry%%:*} == "$game" ]]; then
            negations+=("!${entry#*:}")
        fi
    done
    if ((${#negations[@]})); then
        {
            cat "$SRC/games/$game/.gitignore"
            printf '\n# The public copy carries these (scripts/sync_gym.sh).\n'
            printf '%s\n' "${negations[@]}"
        } >"$DEST/games/$game/.gitignore"
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
    -o -name '*.glb' \
    -o -name '*.jpeg' \
    -o -name '*.jpg' \
    -o -name '*.mp4' \
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
