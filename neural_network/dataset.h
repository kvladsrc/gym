#ifndef NEURAL_NETWORK_DATASET_H_
#define NEURAL_NETWORK_DATASET_H_

#include <cstddef>
#include <cstdint>
#include <vector>

using Image = std::vector<std::uint8_t>;

// A collection of flattened byte images and their class labels.
struct Dataset {
  std::vector<Image> images;
  std::vector<std::uint8_t> labels;
  std::size_t rows = 0;
  std::size_t cols = 0;
  std::size_t channels = 0;
  std::size_t class_count = 0;
};

struct DatasetSplit {
  Dataset training;
  Dataset test;
};

#endif  // NEURAL_NETWORK_DATASET_H_
