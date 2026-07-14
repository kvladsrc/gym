#ifndef NEURAL_NETWORK_MNIST_H_
#define NEURAL_NETWORK_MNIST_H_

#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

struct MnistDataset {
  std::vector<std::vector<double>> images;
  std::vector<std::uint8_t> labels;
  std::size_t rows = 0;
  std::size_t cols = 0;
};

// Reads a pair of unpacked MNIST IDX files. Pixels are returned in row-major
// order and normalized to the [0.0, 1.0] range.
MnistDataset load_mnist(const std::string& images_path,
                        const std::string& labels_path);

#endif  // NEURAL_NETWORK_MNIST_H_
