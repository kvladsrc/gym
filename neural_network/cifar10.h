#ifndef NEURAL_NETWORK_CIFAR10_H_
#define NEURAL_NETWORK_CIFAR10_H_

#include <array>
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

constexpr std::size_t kCifar10Width = 32;
constexpr std::size_t kCifar10Height = 32;
constexpr std::size_t kCifar10Channels = 3;
constexpr std::size_t kCifar10ImageSize =
    kCifar10Width * kCifar10Height * kCifar10Channels;

using Cifar10Image = std::array<std::uint8_t, kCifar10ImageSize>;

struct Cifar10Dataset {
  std::vector<Cifar10Image> images;
  std::vector<std::uint8_t> labels;
};

// Reads CIFAR-10 binary batches. Pixels remain in the original channel-major
// byte representation and are normalized by the training binary.
Cifar10Dataset load_cifar10(const std::vector<std::string>& batch_paths);

#endif  // NEURAL_NETWORK_CIFAR10_H_
