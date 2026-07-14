#!/usr/bin/env bash
set -euo pipefail

readonly url="https://data.brainchip.com/dataset-mirror/cifar10/cifar-10-binary.tar.gz"
readonly checksum="c32a1d4ab5d03f1284b67883e8d87530" # pragma: allowlist secret
readonly script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly data_dir="${script_dir}/data"
readonly archive="${data_dir}/cifar-10-binary.tar.gz"
readonly batches_dir="${data_dir}/cifar-10-batches-bin"

mkdir -p "${data_dir}"

if [[ -f "${batches_dir}/test_batch.bin" ]]; then
    echo "CIFAR-10 is already available in ${batches_dir}"
    exit 0
fi

if [[ ! -f ${archive} ]] || ! echo "${checksum}  ${archive}" | md5sum --check --status -; then
    curl --continue-at - --fail --location --output "${archive}" "${url}"
fi
echo "${checksum}  ${archive}" | md5sum --check -
tar --extract --gzip --file "${archive}" --directory "${data_dir}"

echo "CIFAR-10 is ready in ${batches_dir}"
