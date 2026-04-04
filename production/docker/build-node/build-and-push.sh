#!/bin/bash
set -euo pipefail

### ── Конфигурация ─────────────────────────────────────────────────────────────

REGISTRY="registry.your.domain"
IMAGE_NAME="build"
NFS_NIX_STORE="/srv/nfs/nix"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

### ── Аргументы ────────────────────────────────────────────────────────────────

MODE="build"
TAG="latest"

if [[ ${1:-} == "--init" ]]; then
    MODE="init"
    TAG="init-temp"
elif [[ -n ${1:-} ]]; then
    TAG="$1"
fi

FULL_IMAGE="${REGISTRY}/${IMAGE_NAME}:${TAG}"

### ── Helpers ──────────────────────────────────────────────────────────────────

log() { echo -e "\033[1;32m[build-and-push]\033[0m $*"; }
warn() { echo -e "\033[1;33m[build-and-push] WARNING:\033[0m $*" >&2; }
die() {
    echo -e "\033[1;31m[build-and-push] ERROR:\033[0m $*" >&2
    exit 1
}

### ── Pre-flight ───────────────────────────────────────────────────────────────

command -v podman &>/dev/null || die "podman не найден"

if [[ ! -d ${NFS_NIX_STORE} ]]; then
    die "Создаю директорию ${NFS_NIX_STORE}..."
fi

### ── Логика работы ────────────────────────────────────────────────────────────

if [[ $MODE == "init" ]]; then
    log "РЕЖИМ ИНИЦИАЛИЗАЦИИ (Dockerfile.init)"

    [[ -f "${SCRIPT_DIR}/Dockerfile.init" ]] || die "Dockerfile.init не найден!"

    # Проверка на непустую директорию
    if [[ -f "${NFS_NIX_STORE}/receipt.json" ]]; then
        die "Nix уже инициализирован в ${NFS_NIX_STORE}."
    fi

    log "Запуск установки Nix на NFS..."
    podman build \
        --no-cache \
        --volume "${NFS_NIX_STORE}:/nix:z" \
        --file "${SCRIPT_DIR}/Dockerfile.init" \
        "${SCRIPT_DIR}"

    log "Инициализация завершена успешно."
    exit 0

else
    # Проверка, был ли сделан init
    if [[ ! -f "${NFS_NIX_STORE}/receipt.json" ]]; then
        die "Nix не найден в ${NFS_NIX_STORE}. Сначала запустите: $0 --init"
    fi

    log "Сборка рабочего образа ${FULL_IMAGE}..."

    # Основная сборка. Монтируем /nix целиком.
    # Команда 'nix profile install' внутри Dockerfile обновит софт на NFS.
    podman build \
        --tag "${FULL_IMAGE}" \
        --volume "${NFS_NIX_STORE}:/nix:z" \
        --file "${SCRIPT_DIR}/Dockerfile" \
        "${SCRIPT_DIR}"

    log "Сборка завершена. Пуш ${FULL_IMAGE}..."
    podman push --tls-verify=false "${FULL_IMAGE}"

    log "Готово!"
fi
