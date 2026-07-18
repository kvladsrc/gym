#ifndef NEURAL_NETWORK_CIFAR10_H_
#define NEURAL_NETWORK_CIFAR10_H_

#include <cstddef>
#include <string>
#include <vector>

#include "dataset.h"  // NOLINT(build/include_subdir)

constexpr std::size_t kCifar10Width = 32;
constexpr std::size_t kCifar10Height = 32;
constexpr std::size_t kCifar10Channels = 3;
constexpr std::size_t kCifar10ImageSize =
    kCifar10Width * kCifar10Height * kCifar10Channels;

// Reads CIFAR-10 binary batches in their channel-major byte representation.
Dataset load_cifar10(const std::vector<std::string>& batch_paths);

#endif  // NEURAL_NETWORK_CIFAR10_H_
