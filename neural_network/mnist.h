#ifndef NEURAL_NETWORK_MNIST_H_
#define NEURAL_NETWORK_MNIST_H_

#include <string>

#include "dataset.h"  // NOLINT(build/include_subdir)

// Reads a pair of unpacked MNIST IDX files.
Dataset load_mnist(const std::string& images_path,
                   const std::string& labels_path);

#endif  // NEURAL_NETWORK_MNIST_H_
