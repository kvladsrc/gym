#!/usr/bin/env bash
set -euo pipefail

readonly base_url="https://storage.googleapis.com/cvdf-datasets/mnist"
readonly script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly data_dir="${script_dir}/data"

mkdir -p "${data_dir}"

files=(
    train-images-idx3-ubyte.gz
    train-labels-idx1-ubyte.gz
    t10k-images-idx3-ubyte.gz
    t10k-labels-idx1-ubyte.gz
)

for file in "${files[@]}"; do
    curl --fail --location --output "${data_dir}/${file}" "${base_url}/${file}"
    gzip --decompress --keep --force "${data_dir}/${file}"
done

echo "MNIST is ready in ${data_dir}"
