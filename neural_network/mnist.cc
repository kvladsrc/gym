#include "mnist.h"  // NOLINT(build/include_subdir)

#include <array>
#include <fstream>
#include <limits>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace {

std::uint32_t read_u32(std::istream& input) {
  std::array<unsigned char, 4> bytes{};
  input.read(reinterpret_cast<char*>(bytes.data()), bytes.size());
  if (!input) {
    throw std::runtime_error("unexpected end of IDX file");
  }

  return (static_cast<std::uint32_t>(bytes[0]) << 24) |
         (static_cast<std::uint32_t>(bytes[1]) << 16) |
         (static_cast<std::uint32_t>(bytes[2]) << 8) |
         static_cast<std::uint32_t>(bytes[3]);
}

std::ifstream open_file(const std::string& path) {
  std::ifstream input(path, std::ios::binary);
  if (!input) {
    throw std::runtime_error("cannot open " + path);
  }
  return input;
}

}  // namespace

Dataset load_mnist(const std::string& images_path,
                   const std::string& labels_path) {
  auto images = open_file(images_path);
  auto labels = open_file(labels_path);

  constexpr std::uint32_t kImagesMagic = 2051;
  constexpr std::uint32_t kLabelsMagic = 2049;

  if (read_u32(images) != kImagesMagic) {
    throw std::runtime_error(images_path + " is not an MNIST image file");
  }
  const std::uint32_t image_count = read_u32(images);
  const std::uint32_t rows = read_u32(images);
  const std::uint32_t cols = read_u32(images);

  if (read_u32(labels) != kLabelsMagic) {
    throw std::runtime_error(labels_path + " is not an MNIST label file");
  }
  const std::uint32_t label_count = read_u32(labels);

  if (image_count != label_count) {
    throw std::runtime_error("image and label counts do not match");
  }
  if (rows == 0 || cols == 0 ||
      rows > std::numeric_limits<std::size_t>::max() / cols) {
    throw std::runtime_error("invalid MNIST image dimensions");
  }

  const std::size_t image_size = static_cast<std::size_t>(rows) * cols;
  Dataset dataset;
  dataset.rows = rows;
  dataset.cols = cols;
  dataset.channels = 1;
  dataset.class_count = 10;
  dataset.images.reserve(image_count);
  dataset.labels.reserve(label_count);

  for (std::uint32_t i = 0; i < image_count; ++i) {
    Image image(image_size);
    images.read(reinterpret_cast<char*>(image.data()), image.size());
    if (!images) {
      throw std::runtime_error("unexpected end of " + images_path);
    }
    dataset.images.push_back(std::move(image));
  }

  for (std::uint32_t i = 0; i < label_count; ++i) {
    unsigned char label = 0;
    labels.read(reinterpret_cast<char*>(&label), 1);
    if (!labels) {
      throw std::runtime_error("unexpected end of " + labels_path);
    }
    dataset.labels.push_back(label);
  }

  return dataset;
}
