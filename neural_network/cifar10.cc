#include "cifar10.h"  // NOLINT(build/include_subdir)

#include <cstddef>
#include <cstdint>
#include <fstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace {

constexpr std::size_t kRecordSize = 1 + kCifar10ImageSize;

std::ifstream open_batch(const std::string& path) {
  std::ifstream input(path, std::ios::binary | std::ios::ate);
  if (!input) {
    throw std::runtime_error("cannot open " + path);
  }
  return input;
}

}  // namespace

Dataset load_cifar10(const std::vector<std::string>& batch_paths) {
  if (batch_paths.empty()) {
    throw std::invalid_argument("no CIFAR-10 batches provided");
  }

  Dataset dataset;
  dataset.rows = kCifar10Height;
  dataset.cols = kCifar10Width;
  dataset.channels = kCifar10Channels;
  dataset.class_count = 10;
  for (const std::string& path : batch_paths) {
    std::ifstream input = open_batch(path);
    const std::streamoff size = input.tellg();
    if (size <= 0 || size % static_cast<std::streamoff>(kRecordSize) != 0) {
      throw std::runtime_error("invalid CIFAR-10 batch size: " + path);
    }

    const std::size_t record_count =
        static_cast<std::size_t>(size) / kRecordSize;
    dataset.images.reserve(dataset.images.size() + record_count);
    dataset.labels.reserve(dataset.labels.size() + record_count);
    input.seekg(0);

    for (std::size_t record = 0; record < record_count; ++record) {
      std::uint8_t label = 0;
      Image image(kCifar10ImageSize);
      input.read(reinterpret_cast<char*>(&label), 1);
      input.read(reinterpret_cast<char*>(image.data()), image.size());
      if (!input) {
        throw std::runtime_error("unexpected end of " + path);
      }
      if (label >= 10) {
        throw std::runtime_error("invalid CIFAR-10 label in " + path);
      }

      dataset.labels.push_back(label);
      dataset.images.push_back(image);
    }
  }

  return dataset;
}
